# M1 Validation Report — Walking Skeleton: Gateway + Identity Core

## Milestone Summary

- **Goal**: The thinnest possible real end-to-end slice — prove Gateway↔Identity↔Postgres↔JWT all work together before building anything else out (per [ImplementationRoadmap.md](../../ImplementationRoadmap.md) §M1).
- **Status**: **Completed**
- **Duration**: One continuous session (spanning M0 approval → M1 implementation → this report), including a mid-milestone pause/recovery (see §Technical Debt for what that surfaced) and a branch-strategy setup performed alongside it.
- **Projects created**: `SeeSight.Gateway`, `SeeSight.Identity.Domain`, `SeeSight.Identity.Application`, `SeeSight.Identity.Infrastructure`, `SeeSight.Identity.Api`, `SeeSight.Identity.UnitTests`, `SeeSight.Identity.IntegrationTests`, `SeeSight.Identity.ArchitectureTests`, `SeeSight.Gateway.Tests` (9 new projects).
- **Projects/files modified**: `Directory.Packages.props` (all M1 package versions pinned), `Directory.Build.props` (CA1716/CA1848 handling), `.editorconfig` (naming-rule fixes for static/const fields and interface members, migration-folder BOM handling), `SeeSight.sln`, both `Shared.SharedKernel`/`Shared.Observability` csproj files (new `Http`/root-level members), `.github/workflows/ci.yml` (path filter widened from Shared-only to all backend paths), `.gitignore` (explicit carve-out so `docs/validation/**` is never accidentally excluded by the `TestResults/`/`*.trx` rules).

## Features Implemented

- **Identity Service**: `POST /auth/register` (self-signup, logs the user in immediately — issues a token, matching the original system's behavior), `POST /auth/login`, `GET /auth/me` (trusts Gateway-forwarded identity headers, not its own JWT validation), `GET /.well-known/jwks.json`. RS256 signing via an `RsaSigningKeyProvider` (PEM-configured, fails startup outside Development if unset; generates an ephemeral key with a warning in Development). BCrypt password hashing (12 rounds). EF Core + Npgsql persistence, one migration (`InitialCreate`, the `users` table). Strict JSON deserialization (unknown properties rejected). RFC 7807 problem-details error responses for validation/conflict/auth failures.
- **API Gateway**: YARP reverse proxy routing `/auth/register`, `/auth/login`, `/auth/me` (requires the `Authenticated` policy), `/.well-known/jwks.json` to Identity Service. JWT Bearer validation with a custom `IssuerSigningKeyResolver` backed by a periodically-refreshed JWKS cache. Cookie-first, Bearer-header-fallback token extraction. A response transform sets the httpOnly session cookie from Identity Service's register/login response body (cookie set/read exclusively at the Gateway, per Authentication.md §3). A request transform strips-then-sets the `X-User-Id`/`X-User-Role`/`X-User-Company-Id` forwarded-identity headers on every proxied request (defense-in-depth against a client trying to spoof them). Correlation-ID propagation, CORS, health checks, an aggregated `GET /health`.
- **Shared libraries**: `SeeSight.SharedKernel.Http` (`ICurrentUserContext`, the header-name constants, and the DI registration for reading them), `SeeSight.Shared.Observability` (`AddSeeSightObservability()` — OpenTelemetry tracing/metrics with OTLP export, Serilog structured logging, correlation-ID middleware).
- **Docker**: one parameterized multi-stage `Dockerfile` for every service, `docker/docker-compose.yml` (full stack — infra + Gateway + Identity, via `include:` reusing `docker-compose.infra.yml`).
- **Git workflow**: `development` branch created from the approved M0 baseline on `main`, pushed, tracking configured; CI path filter widened to cover the new backend code.

## Validation

All raw artifacts are under [`docs/validation/M1/`](.):

- `build.log` — full solution Release build output
- `test.log`, `TestResults/{UnitTests,IntegrationTests,ArchitectureTests,GatewayTests}/*.trx` — per-project TRX files
- `TestResults/**/coverage.cobertura.xml` — per-project coverage reports
- `coverage-summary.txt` — condensed coverage table
- `format-lint.log` — static analysis / formatting check output
- This file — the narrative summary

| Required validation | Status |
|---|---|
| `dotnet build` | ✅ 0 warnings, 0 errors (Release) — [build.log](build.log) |
| `dotnet test` | ✅ 62/62 passed — see §Test Results |
| Code coverage | ✅ collected per project — see §Test Results |
| Static analysis / formatting | ✅ `dotnet format --verify-no-changes` exit 0 — [format-lint.log](format-lint.log) |
| Docker Compose validation | ✅ full 9-container stack (`docker/docker-compose.yml`) built and verified running/healthy |
| Health checks | ✅ `/health/live`, `/health/ready` on both services; aggregated `/health` on the Gateway — all verified against the live containerized stack |
| API endpoint verification | ✅ real HTTP requests against the containerized stack: register → login → `/auth/me` via cookie, negative cases (no token, garbage token, duplicate email, wrong password, malformed JSON) — see §Manual End-to-End Verification |
| Integration tests | ✅ 9/9, against a real ephemeral Testcontainers Postgres |
| Performance/benchmark tests | N/A this milestone — no perf-sensitive code path exists yet (a single register/login call). Deferred to a milestone where there's something meaningful to benchmark, not skipped arbitrarily. |

### Manual End-to-End Verification (Containerized Stack)

Beyond the automated test suite, the full flow was exercised against the actual running Docker Compose stack (not just `WebApplicationFactory`):

1. `docker compose -f docker/docker-compose.yml up -d --build` — all 9 containers reached healthy.
2. Applied the EF Core migration against the containerized Postgres (`dotnet ef database update`) — deliberately a separate explicit step, not run automatically at container startup, matching Deployment.md's "migrations never run implicitly" convention.
3. `POST /auth/register` through the Gateway (`localhost:8080`) → `201 Created`, `Set-Cookie` header present, httpOnly/SameSite=Lax.
4. `GET /auth/me` through the Gateway, using **only** the cookie (no Authorization header) → `200 OK`, correct user returned — proving cookie extraction, JWKS-based signature validation, and Gateway→Identity header forwarding all work together for real.
5. `GET /auth/me` with no token → `401`. With a garbage token → `401`.
6. `GET /health` (Gateway, aggregated) → `{"status":"Healthy","services":{"identity":"Healthy"}}`.
7. Verified directly in Postgres (`docker exec ... psql`) that the registered user's row exists with the expected `Role`/`Status`.

## Test Results

| Project | Total | Passed | Failed | Skipped | Line Coverage | Duration |
|---|---|---|---|---|---|---|
| SeeSight.Identity.UnitTests | 33 | 33 | 0 | 0 | 71.7% (114/159 lines) | 226 ms |
| SeeSight.Identity.IntegrationTests | 9 | 9 | 0 | 0 | 75.9% (428/564 lines) | ~1 s |
| SeeSight.Identity.ArchitectureTests | 15 | 15 | 0 | 0 | 0.0%* | 66 ms |
| SeeSight.Gateway.Tests | 5 | 5 | 0 | 0 | 0.0%* | 44 ms |
| **Total** | **62** | **62** | **0** | **0** | — | — |

\* Architecture tests use reflection (NetArchTest) to inspect assembly references — they never execute the business-logic method bodies they're checking, so 0% line coverage from these two projects is expected, not a gap. The two coverage numbers that matter (Unit/Integration) are not merged into one figure here — each is from an independent single-project run; a merged report (e.g. via ReportGenerator) is a reasonable tooling improvement for a later milestone, not a blocker now (see §Technical Debt).

## Architecture Verification

- **No ADR violations**: RS256 + refresh-token-free (deferred to M2 exactly as scoped), BCrypt 12 rounds, cookie+Bearer dual support, strict JSON deserialization, and the Gateway's thin boundary (routing/auth/CORS/correlation-ID only) all match the documented decisions. No shared library gained business logic.
- **No circular dependencies**: enforced automatically — `SeeSight.Identity.ArchitectureTests` (15 tests) verifies Domain→Application→Infrastructure→Api layering and that Domain has zero framework dependency; `SeeSight.Gateway.Tests` (5 tests) verifies the Gateway has zero reference to any service's business-logic assembly.
- **No unexpected coupling**: confirmed by the same architecture tests — Application depends on the EF Core *abstraction* only (no Npgsql, no BCrypt, no IdentityModel in that layer); Infrastructure depends on nothing above it.
- **No architectural drift** from the approved design, with three implementation-level corrections surfaced by actually running the code (not deviations from the architecture — bugs the architecture didn't anticipate at this level of detail):
  1. **JWKS private-key leak** — `JsonWebKeyConverter.ConvertFromRSASecurityKey` serializes whatever key material it's given; passing the signing `RSA` instance directly (which holds the private key) leaked `d`/`p`/`q`/`dp`/`dq`/`qi` to every caller of the public JWKS endpoint. Fixed by exporting public-only parameters into a separate `RSA` instance before conversion. A regression test (`Jwks_endpoint_returns_only_public_key_material`) now guards this permanently.
  2. **`JwksCache` DI lifetime bug** — `AddHttpClient<JwksCache>(...)` registers `JwksCache` itself as **transient** (a new instance, and a new empty cache, on every resolution), so the JWT validation pipeline, the health check, and the background refresher each held their own separate cache. Fixed by registering `JwksCache` as an explicit singleton backed by `IHttpClientFactory` instead of a typed client.
  3. **YARP proxy destination drift** — the YARP cluster's destination address (`yarp.config.json`, a static file) and `IdentityService:BaseUrl` (used by the JWKS cache and aggregated health check) were two independently-configured "where is Identity Service" values. Only the latter was environment-overridden for Docker, so JWKS fetch and health checks worked while the actual `/auth/register`/`/auth/login`/`/auth/me` proxy routes failed with a connection error inside the container. Fixed by deriving the YARP destination from `IdentityService:BaseUrl` programmatically at startup — one canonical setting now drives both.

None of these three required a design change or a new ADR — they were implementation bugs caught by testing against the real containerized stack rather than trusting that "it compiles and the architecture doc says this should work."

## Technical Debt

**Known issues**:
- `X-Request-Id` appears twice (with the same value) in some chunked responses proxied through the Gateway — cosmetic, not a correctness issue (the correlation ID value is consistent end-to-end), likely because the response has already started streaming by the time the correlation middleware's post-`next()` code runs. Worth a proper fix during M13 (Observability Hardening Pass), not blocking now.
- Unit and integration test coverage percentages are reported per-project, not merged into one solution-wide figure. A `ReportGenerator`-based merged report is a reasonable addition to `docs/validation/` tooling for a future milestone.

**Temporary workarounds**:
- This development machine has only the .NET 10 SDK installed system-wide; a .NET 9 SDK was installed via Homebrew specifically to run/test executables (`dotnet run`, `dotnet test`, `dotnet ef`) that need the real net9.0 runtime — building net9.0 class libraries works fine under the .NET 10 SDK (proven in M0), but *running* a net9.0 executable does not without the matching runtime present. This doesn't affect the actual deployment path (Docker images pin `mcr.microsoft.com/dotnet/sdk:9.0`/`aspnet:9.0` explicitly, independent of host tooling) — it's a local-development-machine note, flagged in M0's report and now resolved for this machine specifically.
- EF Core migrations are applied as an explicit step (`dotnet ef database update`), not automatically on container startup — this is the *intended* design (matching Deployment.md's convention, not a shortcut), but is worth restating here since it means a fresh `docker compose up` alone is not sufficient to get a working database; the migration step must run once per environment.

**Risks**:
- The three bugs found this milestone (JWKS leak especially) are a concrete argument for why "compiles + architecture review" is not sufficient — this milestone's actual end-to-end testing against a real running stack is what caught all three. Future milestones should keep budgeting time for this, not just for writing code.

**Future improvements**:
- Merged coverage reporting (see above).
- A dedicated fix for the double `X-Request-Id` header.
- Once M2 adds refresh tokens, revisit whether the Gateway's JWKS refresh interval (currently 5 minutes) needs to be tightened for faster reaction to Identity Service key rotation — not needed yet since there's no key-rotation feature at all.

## Git Summary

- **Files created**: 9 new projects (116 `.cs` files across them), `docker/Dockerfile`, `docker/docker-compose.yml`, `.dockerignore`, `.config/dotnet-tools.json`, `docs/validation/M1/**`.
- **Files modified**: `Directory.Packages.props`, `Directory.Build.props`, `.editorconfig`, `SeeSight.sln`, `SeeSight.SharedKernel.csproj`, `SeeSight.Shared.Observability.csproj`, `.github/workflows/ci.yml`, `.gitignore`.
- **Branch**: `development` (created from the approved M0 commit on `main`, pushed, tracking `origin/development`).
- **Commit hash / message**: recorded after this report is committed — see the commit immediately following this file in `git log development`.
- **Git tag**: none created for M1 (tags reserved for `main` releases per the agreed workflow — M1 lives on `development` pending your review).

## Readiness Checklist

- [x] Solution builds successfully (Release, 0 warnings/errors)
- [x] All tests pass (62/62)
- [x] Docker Compose starts correctly (9/9 containers, full stack verified end-to-end)
- [x] Documentation is updated (this report; architecture docs required no changes — see §Architecture Verification)
- [x] Validation artifacts are saved (`docs/validation/M1/`)
- [x] Ready for review
- [x] Ready for the next milestone (M2 — Identity Service completion: refresh tokens, logout revocation, forgot/reset/change password, rate limiting)
