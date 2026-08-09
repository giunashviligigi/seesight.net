# M3 Validation Report — Tenant Service (Companies, Departments, Employees)

## Milestone Summary

M3 adds the Tenant Service: a new microservice owning Companies, Departments,
and Employees, with hand-rolled multi-tenant data isolation enforced by EF
Core global query filters (ADR 0009). It also adds five internal-only
endpoints to the Identity Service so the Tenant Service can provision,
deactivate, activate, patch, and (as a compensating-rollback safety net)
delete Identity `User` records when an Employee is created with
`createLogin: true`. The Gateway is wired to route all public Tenant Service
traffic and to never expose either service's `/internal/*` endpoints.

All work was scoped strictly to the documented M3 boundary: no RabbitMQ/
outbox, no Trip Service (M5), no frontend, no unrelated Identity features.
`GET /users` (list Identity users by company) was deliberately deferred, as
approved, with one downstream consequence documented under Technical Debt.

## Features Implemented

**ADR 0009 — hand-rolled tenant context, not Finbuckle.MultiTenant**
([docs/adr/0009-hand-rolled-tenant-context.md](../../adr/0009-hand-rolled-tenant-context.md)).
Tenant resolution was already solved in M1 (Gateway-forwarded headers →
`ICurrentUserContext`); adopting Finbuckle would add a bridging adapter, not
remove code, for no gain in isolation strength (the same SQL
`WHERE "CompanyId" = @tenant` runs either way).

**SharedKernel additions** (`src/Shared/SeeSight.SharedKernel/`):
`TenantId`, `ITenantContext` + `CurrentUserTenantContext` (derives tenant
context from the existing `ICurrentUserContext`), `IHasTenant` /
`ISoftDelete` marker interfaces, `SeeSightRoles`, and the internal-service-
token building blocks (`InternalServiceTokenMiddleware`,
`InternalServiceTokenOptions[Validator]`, header constants) — reused
identically by both Identity's and Tenant's new internal endpoints.

**Identity Service internal endpoints** (`/internal/users/*`, all guarded by
`X-Internal-Service-Token`, never Gateway-routed): `POST` (provision an
employee login), `DELETE` (idempotent — the createLogin compensating-
rollback safety net, never general user deletion), `POST .../deactivate`,
`POST .../activate`, `PATCH` (profile + tri-state company assignment).

**Tenant Service** (`src/Services/Tenant/`, 7 projects — Domain,
Application, Infrastructure, Api, plus 3 test projects): full CRUD +
lifecycle for Companies, Departments, and Employees; `ITenantResolver` for
list/create tenant targeting (super admin must pass an explicit
`companyId`; non-super-admin either omits it or must match their own);
EF Core global query filters for tenant scoping (`IHasTenant`) and soft
delete (`ISoftDelete`); `createLogin: true` on Employee creation calls
Identity Service to provision a `User`, with a compensating `DELETE` rollback
to Identity if the local `Employee` save then fails; `POST
/internal/employees/validate` (service-to-service, `IgnoreQueryFilters` by
design — see Security section) for future consumers like Trip Service (M5).

**Gateway wiring**: 21 new routes for Companies/Departments/Employees
(all `AuthorizationPolicy: Authenticated`), the aggregated `/health`
endpoint extended to fan out to both Identity and Tenant, and a dedicated
architecture test (`InternalRoutesNotExposedTests`) asserting no route in
`yarp.config.json` starts with `/internal`.

**Docker Compose**: a `tenant` service block (Postgres as a hard dependency,
Identity as a soft one per `ServiceDependencyMatrix.md`), sharing the same
`Internal:ServiceToken` dev secret as Identity; Gateway now depends on both
Identity and Tenant being healthy before it starts.

## Files/Projects Changed

- 16 existing files modified (Gateway routing/config, Identity's
  `ExceptionHandlingMiddleware`/`Program.cs`/`User.cs`/`appsettings.json`,
  `Directory.Packages.props`, `SeeSight.sln`, `docker-compose.yml`,
  `.editorconfig`).
- New Identity internal-endpoint slice: 1 controller, 10
  commands/handlers/validators, 1 exception, 3 contracts, 11 new
  integration tests, 7 new unit test files.
- New Tenant Service: 7 projects, 111 source files (Domain/Application/
  Infrastructure/Api + `.csproj`s), 23 test files across
  Unit/Integration/Architecture test projects.
- New SharedKernel: `Tenancy/`, `InternalAuth/`, `Persistence/ISoftDelete.cs`,
  `Http/SeeSightRoles.cs`; `Shared.Common/PagedResult.cs`.
- New docs: `docs/adr/0009-hand-rolled-tenant-context.md`,
  `docs/validation/M3/` (this report and artifacts).
- New Gateway test: `InternalRoutesNotExposedTests.cs`.

## Validation

All commands run from the repository root
(`/Users/gigi/Desktop/Seesight .net`) using the pinned .NET 9 SDK
(`/opt/homebrew/Cellar/dotnet@9/9.0.119/libexec/dotnet` — the `dotnet` on
`PATH` resolves to .NET 10, which builds fine but cannot run net9.0 test/ef/
run workloads).

| Step | Command | Result | Artifact |
|---|---|---|---|
| Clean build | `dotnet build SeeSight.sln` | 0 Warnings, 0 Errors | [build.log](build.log) |
| Format check | `dotnet format SeeSight.sln --verify-no-changes` | Clean (no output = no violations) | [format-lint.log](format-lint.log) |
| Full test suite | `dotnet test SeeSight.sln --collect:"XPlat Code Coverage"` | 296/296 passed, 0 failed, 0 skipped | [test.log](test.log), [TestResults/](TestResults/) |
| Coverage | per-project cobertura summary | see table below | [coverage-summary.txt](coverage-summary.txt) |
| Docker Compose | `docker compose up -d --build` (10 containers) | all healthy | [docker-validation.log](docker-validation.log) |
| Live E2E through Gateway | real JWTs, real HTTP, real Postgres | see Security section | [docker-validation.log](docker-validation.log) |

### Manual End-to-End Verification (Containerized Stack)

`docker compose up -d --build` brought up all 10 containers (postgres,
redis, rabbitmq, jaeger, otel-collector, prometheus, grafana, identity,
tenant, gateway) healthy, including the new `tenant` service. EF Core
migrations for both Identity and Tenant were applied against the compose
Postgres (`dotnet ef database update`) — the databases are fresh Docker
volumes per run and migrations are deliberately not auto-applied at
application startup (`docs/DatabaseDesign.md` §"Migrations": "migrations
never run implicitly inside application startup code in production").

The aggregated Gateway health check confirms both downstream services:

```json
{"status":"Healthy","services":{"identity":"Healthy","tenant":"Healthy"}}
```

A full scripted walkthrough then exercised the real system over HTTP through
the Gateway on port 8080 (JWT bearer auth, no header injection) — see
[docker-validation.log](docker-validation.log) for the complete transcript
(JWTs, opaque refresh tokens, and the one temp password used to log in as a
provisioned employee are redacted as `<REDACTED_...>`, preserving every
request/response and status code): register two CompanyAdmins, self-signup
two companies, bootstrap a SuperAdmin (no self-signup path for SuperAdmin
exists by design — promoted via a direct SQL `UPDATE`, a standard first-admin
bootstrap technique), create a Department and an Employee with
`createLogin: true`, then run the tenant-isolation and internal-endpoint
proofs described below.

## Test Results

| Project | Passed | Failed | Skipped |
|---|---|---|---|
| SeeSight.Identity.UnitTests | 123 | 0 | 0 |
| SeeSight.Identity.IntegrationTests | 33 | 0 | 0 |
| SeeSight.Identity.ArchitectureTests | 15 | 0 | 0 |
| SeeSight.Tenant.UnitTests | 54 | 0 | 0 |
| SeeSight.Tenant.IntegrationTests | 33 | 0 | 0 |
| SeeSight.Tenant.ArchitectureTests | 14 | 0 | 0 |
| SeeSight.Gateway.Tests | 24 | 0 | 0 |
| **Total** | **296** | **0** | **0** |

Per-project line coverage is in [coverage-summary.txt](coverage-summary.txt)
(same "independent single-project coverage run, no merged solution report"
convention as M1/M2).

## Architecture Verification

- **`SeeSight.Tenant.ArchitectureTests`** (14 tests, NetArchTest): Domain has
  no dependency on Application/Infrastructure/Api; Application does not
  reference Infrastructure or Api; Infrastructure does not reference Api;
  no layer references `Microsoft.AspNetCore.Mvc` except Api; mirrors the
  layering rules already proven for Identity in M1.
- **`GatewayHasNoBusinessLogicTests`** (extended): asserts the Gateway
  assembly has zero dependency on `SeeSight.Tenant.{Domain,Application,
  Infrastructure,Api}`, alongside the existing Identity assertions — the
  Gateway only routes and validates JWTs, it never touches tenant logic.
- **`InternalRoutesNotExposedTests`** (new): loads the actual
  `yarp.config.json` from the Gateway's build output and asserts no
  configured route's `Path` starts with `/internal` — a direct, executable
  proof (not just a code review) that neither service's internal API can be
  reached through the Gateway.

## Security / Tenant-Isolation Verification

This is the most important guarantee in M3. It was proven twice, at two
different levels:

**1. In-process, `SeeSight.Tenant.IntegrationTests.TenantIsolationTests`**
(9 tests, real Postgres via Testcontainers, forwarded-identity headers set
directly — no Gateway in the loop, same technique `Identity.IntegrationTests`
already used in M1/M2): Company B's admin cannot read, list, update, or
delete Company A's departments or employees — including when explicitly
passing Company A's `companyId` on a list call; cannot read Company A's
company record; a SuperAdmin can read and list across both companies but
must still supply an explicit `companyId` on list (no implicit default
tenant, so a SuperAdmin can never accidentally see "their own" data because
they have none).

**2. Live, through the real Gateway with real JWTs** (see
[docker-validation.log](docker-validation.log), steps 9-14): the exact same
proofs re-run against the actual containerized stack — Gateway JWT
validation → forwarded identity headers → Tenant Service's EF Core query
filter, no shortcuts:

- `GET /companies/{companyAId}` as Company B's admin → **404** (cross-tenant
  read is indistinguishable from "doesn't exist" — no information leak).
- `GET /employees?companyId={companyAId}` as Company B's admin → **403**
  (an explicit cross-tenant list/create request is a known, named error,
  per `docs/TenantArchitecture.md` §4 — the caller already knows the id they
  guessed, so there's nothing to hide).
- `GET /employees/{companyAEmployeeId}` and
  `PATCH /departments/{companyADepartmentId}` as Company B's admin → **404**.
- `GET /companies/{companyAId}` and
  `GET /employees?companyId={companyAId}` as SuperAdmin → **200**, listing
  correctly includes Company A's data.
- `GET /employees` (no `companyId`) as SuperAdmin → **400**.

**Internal endpoints**: `POST /internal/employees/validate` through the
Gateway → **404** (no route configured — not merely unauthorized, genuinely
unreachable). The same call direct-to-service with the correct
`X-Internal-Service-Token` → **200**; with a missing token → **401**.
`ValidateEmployeesQuery` deliberately uses `.IgnoreQueryFilters()` — this is
correct, not a bypass, because the caller (a future service like Trip
Service) has no forwarded user identity to filter by; it passes an explicit
`companyId` argument instead, which the handler filters on directly.

**Cross-service lifecycle sync verified live**: creating an Employee with
`createLogin: true` genuinely provisions an Identity `User` (confirmed by
logging in as the new employee) and correctly triggers the pre-existing M2
`MustChangePasswordMiddleware` gate (`403` until the temp password is
changed) — a real interaction between M2 and M3 features, not a defect.
Deactivating the Employee syncs to Identity: a subsequent login attempt with
the same credentials returns `401`.

## Technical Debt / Known Issues

**Found and fixed during this milestone (reported transparently, as agreed
for M1/M2's equivalent findings):**

1. **Missing FK constraint from `Department.CompanyId` /
   `Employee.CompanyId` to `companies`.** `docs/DatabaseDesign.md`'s Tenant
   Service ERD documents both as `FK` (not "logical reference" — this is a
   same-database, same-service reference, so the repo's documented
   "no cross-service FK" rule does not apply here). The initial EF Core
   configuration omitted the real foreign key. Caught by a genuinely failing
   test (`TenantIsolationTests.SuperAdmin_can_read_and_list_across_both_
   companies` — the test's own company id had no backing `Company` row).
   Fixed: added `HasOne<Company>().WithMany().HasForeignKey(...)
   .OnDelete(DeleteBehavior.Restrict)` to both `DepartmentConfiguration` and
   `EmployeeConfiguration`, generated migration
   `20260809014419_AddCompanyForeignKeys`, and updated every integration
   test that previously used a bare random `Guid` as a `companyId` to create
   a real `Company` row first (`TestSupport/TenantSeedHelpers.cs`). `Restrict`,
   not `Cascade`, because `Company` is only ever soft-deleted by the
   application (`DeleteCompanyCommandHandler`) — a hard delete reaching this
   constraint would indicate a bug, not a normal lifecycle event.
2. **Pre-existing (since M1), latent `WebApplicationFactory` connection-
   string bug, discovered while building Tenant's integration tests.**
   `AddDbContext<T>(options => options.UseNpgsql(connectionString))` in both
   `Identity.Infrastructure` and `Tenant.Infrastructure` read the connection
   string eagerly from `IConfiguration` at service-registration time — before
   `WebApplicationFactory`'s deferred `ConfigureAppConfiguration` test
   override (which supplies the Testcontainers-managed connection string) is
   merged into the built configuration. This silently baked in
   `appsettings.Development.json`'s hardcoded `localhost:5432` for every
   integration test in both services. It was previously masked whenever a
   real Postgres happened to already be listening on host port 5432 from an
   earlier `docker compose up` in the same terminal session — tests "passed"
   against the wrong, shared database without anyone noticing. Zero
   production impact: Docker Compose always supplies the real connection
   string via environment variables at container start, so this only ever
   affected `WebApplicationFactory`-based tests. Fixed in both services by
   switching to the `Action<IServiceProvider, DbContextOptionsBuilder>`
   overload of `AddDbContext`, resolving `IConfiguration` lazily from the
   fully-built container. Re-verified: `Identity.IntegrationTests` and
   `Tenant.IntegrationTests` both pass 33/33 against a genuine
   Testcontainers-managed database after the fix.

**Known, deliberate limitations (approved as part of M3's scope):**

3. **`AssignCompanyAdminCommand`'s `ReplaceExisting: true` does not unassign
   the company's prior admin(s).** Implemented and documented, not silently
   dropped — unassigning requires listing Identity users by `companyId`,
   which needs the deferred `GET /users` endpoint. The primary assign
   operation (setting the new admin) works correctly; only the "and clear
   the old one" side effect is unimplemented, with a code comment pointing
   here.
4. **`GET /users`** (list Identity users by company) is deferred, as
   explicitly approved before implementation began. No M3 feature depends on
   it except item 3 above.
5. **Employee email uniqueness is per-company in Tenant Service but globally
   unique in Identity Service.** `IdentityServiceClient.
   ProvisionEmployeeUserAsync` maps Identity's `409 Conflict` (same email,
   different company) to `DuplicateEmployeeEmailException` so the API
   contract stays consistent, but the underlying constraint mismatch is a
   real, narrow limitation: the same email cannot have `createLogin: true`
   logins in two different companies simultaneously, even though Tenant
   Service's own uniqueness rule would allow it. Not a security issue —
   documented in code and here for a future milestone to reconcile if it
   becomes a real product requirement.
6. No RabbitMQ/outbox/event publishing — explicitly out of scope for M3,
   deferred to whichever milestone introduces cross-service eventing.

## Git Commit Hash

_Filled in after commit — see final report message._

## Remote Push Verification

_Filled in after push — see final report message._
