# M2 Validation Report — Identity Service Completion

## Milestone Summary

- **Goal**: Complete the Identity Service's authentication surface — refresh token issuance/rotation with reuse detection, real server-side logout (revocation), forgot/reset/change password, the `MustChangePassword` gate, and Gateway-level Redis-backed rate limiting on `/auth/login` and `/auth/register` — per [ImplementationRoadmap.md](../../ImplementationRoadmap.md) §M2.
- **Status**: **Completed**
- **Duration**: One continuous session (spanning M1 approval → M2 implementation → this report).
- **Projects created**: none (M2 extends the M1 project set — no new projects).
- **Projects/files modified**: extensive changes across `SeeSight.Identity.Domain`, `SeeSight.Identity.Application`, `SeeSight.Identity.Infrastructure`, `SeeSight.Identity.Api`, `SeeSight.Gateway`, `SeeSight.SharedKernel`, `SeeSight.Shared.Observability`, plus all four test projects — see §Git Summary for the full file list.

## Features Implemented

- **Refresh tokens**: `RefreshToken` domain entity (opaque-token hash, expiry, revocation with `ReplacedByTokenId` chain linking), `POST /auth/refresh` — rotates on every use, detects reuse of an already-rotated-away token as a compromise signal and revokes every other active token for that user (per [Authentication.md](../../Authentication.md) §2). Refresh token lifetime is configurable (`Identity:Jwt:RefreshTokenLifetimeDays`, default 30 days).
- **Logout**: `POST /auth/logout` — real server-side revocation of the presented refresh token (not just clearing a cookie), deliberately idempotent/forgiving for missing or already-revoked tokens.
- **Forgot/reset password**: `POST /auth/forgot-password` (always a generic response, never reveals whether the email exists; in Development only, returns a debug token/URL since there's no email provider yet), `POST /auth/reset-password` (single-use `PasswordResetToken`, 1-hour expiry, one transaction updates both the password hash and marks the token used).
- **Change password**: `POST /auth/change-password` — requires the caller's current password, rejects a same-as-current new password, authenticated via the Gateway-forwarded identity headers (no separate JWT re-validation).
- **`MustChangePassword` gate**: `SeeSight.Gateway.Authentication.MustChangePasswordMiddleware` blocks every route except an allowlist (`/auth/change-password`, `/auth/me`, `/auth/logout`, `/auth/refresh`) for a token carrying `mustChangePassword=true`.
- **Rate limiting**: `SeeSight.Gateway.RateLimiting.RedisRateLimitMiddleware` — fixed-window (INCR+EXPIRE) per-client-IP limiting on `POST /auth/login` and `POST /auth/register`, backed directly by `StackExchange.Redis` (ADR 0008), fails open with a logged warning and an observable metric on any Redis error (ADR 0007).
- **Cookie hardening**: the Gateway now sets two cookies from register/login/refresh responses — the access-token cookie (`Path=/`) and a refresh-token cookie scoped to `Path=/auth` (real exposure-surface reduction, not previously scoped this way), and clears both on logout.
- **Security hardening**: opaque refresh/reset tokens are SHA-256-hashed before persistence (`Sha256TokenHasher`) and never stored in plaintext; tokens are generated via `RandomNumberGenerator` (`SecureOpaqueTokenGenerator`), base64url-encoded, 256 bits of entropy.
- **Docker Compose**: Redis added to the running stack (`docker/docker-compose.yml`), Gateway now depends on Redis being healthy.

## Validation

All raw artifacts are under [`docs/validation/M2/`](.):

- `build.log` — full solution Release build output
- `test.log`, `TestResults/{UnitTests,IntegrationTests,ArchitectureTests,GatewayTests}/*.trx` — per-project TRX files
- `TestResults/**/coverage.cobertura.xml` — per-project coverage reports
- `coverage-summary.txt` — condensed coverage table
- `format-lint.log` — static analysis / formatting check output
- `docker-validation.log` — full manual end-to-end verification transcript against the live containerized stack
- This file — the narrative summary

| Required validation | Status |
|---|---|
| `dotnet build` | ✅ 0 warnings, 0 errors (Release) — [build.log](build.log) |
| `dotnet test` | ✅ 148/148 passed — see §Test Results |
| Code coverage | ✅ collected per project — see §Test Results |
| Static analysis / formatting | ✅ `dotnet format --verify-no-changes` exit 0 — [format-lint.log](format-lint.log) |
| Docker Compose validation | ✅ full 9-container stack (`docker/docker-compose.yml`, now including Redis) built and verified running/healthy |
| Health checks | ✅ `/health/live`, `/health/ready` on Identity; aggregated `/health` on the Gateway — all verified against the live containerized stack |
| API endpoint verification | ✅ real HTTP requests through the Gateway against the containerized stack: register → cookie set → `/auth/me` via cookie → refresh rotation → reuse-detection 401 → logout revokes + clears cookies → forgot/reset password → change password via Bearer → rate limiting → Redis fail-open — see §Manual End-to-End Verification |
| Integration tests | ✅ 22/22, against a real ephemeral Testcontainers Postgres |
| Performance/benchmark tests | N/A this milestone — no perf-sensitive code path was added (fixed-window Redis INCR is O(1); rotation/reuse-detection queries are all single-row lookups by unique hash index). Deferred to a milestone where there's something meaningful to benchmark. |

### Manual End-to-End Verification (Containerized Stack)

Beyond the automated test suite, the full M2 flow was exercised against the actual running Docker Compose stack (not just `WebApplicationFactory`) — full transcript in [docker-validation.log](docker-validation.log):

1. `docker compose -f docker/docker-compose.yml up -d` — all 9 containers reached healthy (after fixing a leftover-container network issue, see §Architecture Verification).
2. Applied the new `AddRefreshAndPasswordResetTokens` EF Core migration against the containerized Postgres (explicit step, same convention as M1) — `refresh_tokens` and `password_reset_tokens` tables confirmed present via `psql`.
3. `POST /auth/register` through the Gateway (`localhost:8080`) → `201 Created`, both `seesight_access_token` (`Path=/`) and `seesight_refresh_token` (`Path=/auth`) cookies set, httpOnly.
4. `GET /auth/me` using only the cookie → `200 OK`, correct user.
5. `POST /auth/refresh` using only the cookie → `200 OK`, new access+refresh token pair, `refreshToken` value changed (rotation confirmed).
6. Re-presenting the **original** (now rotated-away) refresh token → `401 Unauthorized` (reuse detection).
7. `POST /auth/logout` using the rotated cookie → `204 No Content`, both cookies cleared from the jar (expired); a subsequent `/auth/refresh` with the logged-out token → `401`.
8. `POST /auth/forgot-password` (Development env) → `200 OK` with a debug token/URL; `POST /auth/reset-password` with that token → `204 No Content`; login with the old password → `401`; login with the new password → `200 OK`.
9. `POST /auth/change-password` via `Authorization: Bearer` through the Gateway → `204 No Content`; login with the final password → `200 OK`.
10. Rate limiting: 12 rapid `POST /auth/login` requests — the window count carried the 4 prior `/auth/login` calls from this same verification run, so the 7th request in the dedicated loop (the 11th `/auth/login` call in that 60-second window) correctly received `429 Too Many Requests` with a `Retry-After` header, matching the configured `RequestsPerWindow=10` under fixed-window semantics.
11. Fail-open: stopped the `redis` container, then `POST /auth/register` → still `201 Created` (request allowed through after the Redis connection timeout), with `Rate limiter could not reach Redis — failing open (request allowed).` logged and the ADR 0007/0008 fail-open metric incremented. Restarted Redis — the Gateway's aggregated `/health` returned `Healthy` again immediately, no service restart required.

## Test Results

| Project | Total | Passed | Failed | Skipped | Line Coverage | Duration |
|---|---|---|---|---|---|---|
| SeeSight.Identity.UnitTests | 92 | 92 | 0 | 0 | 87.6% (346/395) | 357 ms |
| SeeSight.Identity.IntegrationTests | 22 | 22 | 0 | 0 | 70.2% (828/1179) | ~3 s |
| SeeSight.Identity.ArchitectureTests | 15 | 15 | 0 | 0 | 0.0%* | 77 ms |
| SeeSight.Gateway.Tests | 19 | 19 | 0 | 0 | 18.3%* | 101 ms |
| **Total** | **148** | **148** | **0** | **0** | — | — |

\* Architecture tests use reflection (NetArchTest) to inspect assembly metadata — they never execute the business-logic bodies they check, so 0% line coverage is expected, not a gap. Gateway.Tests' line-rate is computed against the whole Gateway assembly (YARP transforms, `Program.cs` startup wiring, health checks), most of which is only exercised end-to-end via the containerized stack, not this in-process unit-test project — the two new M2 middlewares it specifically targets (`MustChangePasswordMiddleware`, `RedisRateLimitMiddleware`) are fully covered by its 19 tests. Each figure is from an independent single-project coverage run — same convention as M1 (see §Technical Debt).

New tests added this milestone (86 net-new across the four projects): `RefreshTokenTests`, `PasswordResetTokenTests` (domain); `RefreshTokenCommandHandlerTests`, `LogoutCommandHandlerTests`, `ForgotPasswordCommandHandlerTests`, `ResetPasswordCommandHandlerTests`, `ChangePasswordCommandHandlerTests`, and one validator test class per new command (application, including rotation, reuse-detection, idempotent logout, expired/used-token, wrong-password, same-password cases); `RefreshAndPasswordFlowsTests` (integration, 15 tests against a real Postgres); `MustChangePasswordMiddlewareTests`, `RedisRateLimitMiddlewareTests` (Gateway, including a live `MeterListener`-based assertion that the fail-open metric actually increments). The two pre-existing M1 handler test files (`RegisterUserCommandHandlerTests`, `LoginCommandHandlerTests`) were updated to compile against the new constructor signatures and `AuthResult` shape and continue to pass.

## Architecture Verification

- **No ADR violations**: refresh rotation + reuse detection, SHA-256 opaque-token hashing, `Path=/auth` cookie scoping, the Application-layer `IOpaqueTokenGenerator`/`ITokenHasher` abstractions (Infrastructure-implemented, no crypto leaking into Application), the hand-rolled `StackExchange.Redis` rate limiter (ADR 0008) with fail-open (ADR 0007), and the Application-agnostic-of-hosting-environment `ForgotPasswordCommandHandler` (the `IWebHostEnvironment.IsDevelopment()` check lives in the Api-layer controller, not Application) all match the documented decisions.
- **No circular dependencies / no unexpected coupling**: re-verified by the same structural test suites as M1 — `SeeSight.Identity.ArchitectureTests` (15 tests, unchanged assertions, now also covering the new Domain entities/Application handlers/Infrastructure security classes since they check whole-assembly references) and `SeeSight.Gateway.Tests` (19 tests, including the new middleware behavior tests) — all passing with the M2 additions in place.
- **No architectural drift** from the approved M2 scope, with two implementation-level bugs surfaced by writing tests and running the code end-to-end (not deviations from the architecture — bugs the architecture didn't anticipate at this level of detail):
  1. **`WriteAsJsonAsync` silently discarding the `application/problem+json` content type** — `MustChangePasswordMiddleware`, `RedisRateLimitMiddleware`, and the pre-existing (M1) `ExceptionHandlingMiddleware.WriteProblemAsync` all set `context.Response.ContentType = "application/problem+json"` and then called the parameterless `WriteAsJsonAsync(value)` overload, which unconditionally overwrites `ContentType` to `application/json; charset=utf-8` regardless of what was already set. Every 401/403/409/429/500 problem-details response was actually being served with the wrong content type, contradicting the documented RFC 7807 contract. Caught by a Gateway unit test asserting the exact content type. Fixed in all three call sites by passing `contentType: "application/problem+json"` explicitly to `WriteAsJsonAsync`. A regression test (`MustChangePassword_users_are_blocked_from_other_paths_with_403`) now guards this for the Gateway; the containerized manual verification log also shows the corrected header on every error response.
  2. **Leftover-container network attachment** — a Docker container-creation race (a prior `up` attempt partially failed on a host port conflict from an unrelated standalone infra stack, leaving `seesight-postgres-1` created but not attached to `seesight-network`) caused `identity` to be unable to resolve the `postgres` hostname. This was an environmental Docker state issue, not a compose-file or application bug — fixed by removing and letting Compose recreate the affected container; documented here since it's a plausible operator gotcha, not because anything in the milestone's code was wrong.

Neither of these required a design change or a new ADR — the first is a straightforward corrected use of an ASP.NET Core API (the fix is three call sites, not a redesign); the second is an operational Docker artifact unrelated to the M2 code itself.

## Technical Debt

**Known issues**:
- Same as M1: unit/integration/architecture/gateway coverage percentages are reported per-project, not merged into one solution-wide figure (`ReportGenerator` remains a reasonable future addition, not a blocker).
- The `MustChangePassword` gate (`MustChangePasswordMiddleware`) has no way to be exercised end-to-end yet — nothing in the system currently sets `MustChangePassword = true` on a user (that will come with an admin-invite/create-user flow in a later milestone). It's fully unit-tested at the middleware level (claim present/absent, allowlist/non-allowlist paths) but the manual Docker verification in this milestone could not demonstrate the 403 path live for that reason — this is a scope gap in what M2 could exercise, not a defect.

**Temporary workarounds**:
- Same .NET 9-via-Homebrew note as M1/M0 — the host's SDK-on-PATH is .NET 10; running (not building) net9.0 executables/tools (`dotnet run`, `dotnet test`, `dotnet ef`) still requires the explicit Homebrew-installed .NET 9 path. Unaffected by Docker (images pin `sdk:9.0`/`aspnet:9.0` explicitly).
- Forgot-password's debug token/URL exposure is gated on `IWebHostEnvironment.IsDevelopment()` only — there is still no real email provider; this is the documented, intentional interim state for M2 (see [Authentication.md](../../Authentication.md) §4), not an oversight.

**Risks**:
- The `WriteAsJsonAsync` content-type bug (see §Architecture Verification) affected every problem-details response since M1 and would not have been caught without writing an assertion on the literal header value — a reminder that integration/unit tests should assert on exact response contracts (status *and* headers), not just status codes, especially for anything documented as a fixed API contract.

**Future improvements**:
- Merged coverage reporting (carried over from M1).
- Once an admin/invite user-creation flow exists, add a live Docker verification of the `MustChangePassword` 403 gate to close the gap noted above.
- Revisit the Gateway JWKS refresh interval once Identity Service supports key rotation (still not needed — no rotation feature exists yet).

## Git Summary

- **Files created**: `docs/adr/0008-redis-rate-limiter-implementation.md`; 2 Domain entities; 2 Application abstractions, 5 exceptions, 15 command/handler/validator files across refresh/logout/forgot/reset/change-password; 2 Infrastructure security classes, 2 EF configuration classes, 1 migration (+ Designer); 7 Api contract records; 1 Gateway middleware (`MustChangePasswordMiddleware`) + the `RateLimiting/` folder (`RateLimitOptions`, `RateLimitMetrics`, `RedisRateLimitMiddleware`); 2 SharedKernel `Http` constant classes; 13 new test files across all four test projects (86 net-new tests); `docs/validation/M2/**`.
- **Files modified**: `Directory.Packages.props` (StackExchange.Redis, Testcontainers.Redis pinned); `docker/docker-compose.yml` (Redis added to the running stack); Gateway (`AuthCookieOptions`, `Program.cs`, both YARP transform providers, `yarp.config.json`, both `appsettings*.json`, the `.csproj`); Identity.Api (`AuthController` rewritten, `ExceptionHandlingMiddleware`, `appsettings.json`) — `Contracts/LoginResponse.cs` deleted, replaced by `AuthResponse.cs`; Identity.Application (`IIdentityDbContext`, `IJwtIssuer`, `AuthResult`, `LoginCommand`/`Handler`, `RegisterUserCommand`/`Handler`); Identity.Domain (`User.SetPasswordHash`); Identity.Infrastructure (`InfrastructureServiceCollectionExtensions`, `IdentityDbContext`, the migration snapshot, `JwtOptions`, `RsaJwtIssuer`); `Shared.Observability/ObservabilityExtensions` (additional-meter-names parameter); the two pre-existing M1 unit test files (constructor/shape updates) and the Gateway test project file (NSubstitute/StackExchange.Redis package references).
- **Branch**: `development` (unchanged — M2 continues on the same branch M1 was approved onto).
- **Commit hash / message**: recorded after this report is committed — see the commit immediately following this file in `git log development`.
- **Git tag**: none created for M2 (tags reserved for `main` releases per the agreed workflow — M2 lives on `development` pending your review).

## Readiness Checklist

- [x] Solution builds successfully (Release, 0 warnings/errors)
- [x] All tests pass (148/148)
- [x] Docker Compose starts correctly (9/9 containers, full stack verified end-to-end, Redis included)
- [x] Documentation is updated (this report, ADR 0008; architecture docs required no other changes — see §Architecture Verification)
- [x] Validation artifacts are saved (`docs/validation/M2/`)
- [x] Ready for review
- [x] Ready for the next milestone (not started per your instruction — awaiting your explicit approval and scope for M3)
