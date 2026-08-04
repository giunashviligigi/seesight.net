# Implementation Roadmap

Fine-grained milestones superseding [DevelopmentRoadmap.md](DevelopmentRoadmap.md)'s phase-level view — each milestone is small, independently shippable, and ordered so the application is runnable end-to-end (even if minimally) as early as possible, per the "walking skeleton" principle. [DevelopmentRoadmap.md](DevelopmentRoadmap.md)'s Phases 1–2 (Architecture, Documentation) are complete; this roadmap begins at what that document called Phase 3.

Every milestone follows the reporting convention already agreed: **What was implemented / What changed / How to test it / What remains / Any risks or technical debt** — reported back before moving to the next one. No milestone is "done" with failing tests or placeholder logic.

## M0 — Solution & Infrastructure Skeleton

- **Goal**: An empty, buildable solution and a working local infra stack — nothing functional yet, but every tool works.
- **Projects created**: `SeeSight.sln`, all five `Shared.*` projects (empty classes/interfaces only, populated as needed later), `Directory.Build.props`/`Directory.Packages.props`.
- **Features implemented**: None. `docker-compose.infra.yml` (Postgres, RabbitMQ, Redis, OTel Collector, Jaeger, Prometheus, Grafana), `init-db.sql` creating the 6 per-service databases, CI skeleton (`ci.yml`, builds the empty solution).
- **Dependencies**: None — this is the root milestone.
- **Expected deliverables**: `dotnet build` succeeds on the empty solution; `docker compose -f docker/docker-compose.infra.yml up` brings up all infra containers healthy; CI runs green on an empty solution.
- **Validation before moving on**: All infra containers pass their own healthchecks; a manual `psql` connection to each of the 6 databases succeeds; RabbitMQ management UI and Grafana are reachable in a browser.

## M1 — Walking Skeleton: Gateway + Identity Core

- **Goal**: The thinnest possible real end-to-end slice — prove Gateway↔Identity↔Postgres↔JWT all work together before building anything else out.
- **Projects created**: `SeeSight.Gateway`, `SeeSight.Identity.Domain`, `SeeSight.Identity.Application`, `SeeSight.Identity.Infrastructure`, `SeeSight.Identity.Api`, `SeeSight.Identity.UnitTests`, `SeeSight.Identity.IntegrationTests`, `SeeSight.Identity.ArchitectureTests`, `SeeSight.Gateway.Tests`.
- **Features implemented**: `POST /auth/register`, `POST /auth/login`, `GET /auth/me`, `GET /.well-known/jwks.json`, RS256 signing, Gateway JWT validation against the JWKS endpoint, cookie+bearer dual support, YARP routing for these three routes only. **Not yet**: refresh tokens, logout revocation, password reset, rate limiting.
- **Dependencies**: M0.
- **Expected deliverables**: A real user can register, log in, receive a cookie through the Gateway, and call `GET /auth/me` successfully; an invalid/expired token is rejected at the Gateway.
- **Validation before moving on**: Identity's unit/integration/architecture test suites pass; a manual `curl` walkthrough (register → login → `/auth/me`) succeeds through the Gateway, not just directly against Identity Service; Gateway's own test suite confirms it makes no business-logic decision (only routes + validates).

## M2 — Identity Service Completion

- **Goal**: Round out Identity Service to its full documented scope.
- **Projects created**: None new.
- **Features implemented**: Refresh token issuance + rotation, real server-side logout (revocation), forgot/reset/change password, `MustChangePassword` gate, Gateway-level Redis-backed rate limiting on `/auth/login` and `/auth/register` (with the fail-open policy from [ADR 0007](adr/0007-redis-dependent-features-fail-open.md)), the fail-fast startup check on a missing signing key.
- **Dependencies**: M1. Requires a decision on the specific distributed rate-limiter package (§ [CodingStandards.md](CodingStandards.md) §7) — record as its own ADR here if not already chosen.
- **Expected deliverables**: The full [Authentication.md](Authentication.md) flow works end-to-end, including refresh rotation and revoked-token rejection.
- **Validation before moving on**: A deliberate negative test — starting the service in a non-Development environment with no signing key configured — fails to boot as designed; repeated bad logins trigger `429`; a used-and-rotated refresh token is rejected on reuse; killing Redis mid-test still allows logins through (fail-open confirmed, not just assumed).

## M3 — Tenant Service

- **Goal**: Company/Department/Employee lifecycle, wired into the Gateway.
- **Projects created**: `SeeSight.Tenant.Domain/Application/Infrastructure/Api` + its 3 test projects.
- **Features implemented**: Full `/companies`, `/departments`, `/employees` CRUD + lifecycle per [APIContracts.md](APIContracts.md), tenant-scoped EF Core query filters, the `createLogin: true` → Identity Service REST call **with the compensating-action handling** from [TenantArchitecture.md](TenantArchitecture.md) §6, and the internal-service-token check from [ADR 0006](adr/0006-internal-service-to-service-authentication.md) applied to `/internal/employees/validate` (built here even though Trip Service doesn't call it until M5, so the contract is stable when M5 needs it).
- **Dependencies**: M2 (needs Identity Service's user-creation endpoint to exist for `createLogin`).
- **Expected deliverables**: Company self-signup → create company → add departments → add employees (with and without login) all work through the Gateway.
- **Validation before moving on**: A simulated failure of the local `Employee` save *after* the remote `User` was already created is confirmed to trigger the compensating delete, not leave an orphaned Identity Service account; tenant isolation is confirmed with a cross-tenant-access negative test (§ [TenantArchitecture.md](TenantArchitecture.md) §5).

## M4 — Trip Service Core (State Machine Only)

- **Goal**: Isolate the highest-risk domain logic — the trip status state machine — and get it fully correct and tested *before* adding cross-service calls or messaging on top of it.
- **Projects created**: `SeeSight.Trip.Domain/Application/Infrastructure/Api` + its 3 test projects.
- **Features implemented**: `Trip`/`TripTraveler`/`Approval`/`ApprovalAction` entities and the full transition graph as domain methods (`Trip.Submit()`, `.Approve()`, `.Reject()`, `.Cancel()`, `.Reopen()`, `.Complete()`), the auto-promotion scheduler, all `/trips/*` endpoints from [APIContracts.md](APIContracts.md) **except** offer attach/select and invoice. Traveler employee-id validation is **stubbed as "always valid"** at this stage — the real Tenant Service call is M5, deliberately deferred so the state machine can be built and tested in isolation first.
- **Dependencies**: M0 only (does not need Tenant Service yet, by design — the stub keeps this milestone independently testable).
- **Expected deliverables**: The full `DRAFT → ... → COMPLETED` lifecycle works, including `reject → reopen → resubmit`, entirely within Trip Service.
- **Validation before moving on**: Domain-layer unit tests cover every entry in the transition table, including every *invalid* transition (asserting it's rejected); integration tests exercise the full lifecycle through the API.

## M5 — Trip ↔ Tenant Integration

- **Goal**: Replace M4's stub with the real cross-service validation call — the one synchronous service-to-service edge in the system.
- **Projects created**: None new.
- **Features implemented**: Trip Service's `ITenantServiceClient` (defined in Application, implemented in Infrastructure as a typed `HttpClient`) calling Tenant Service's `/internal/employees/validate`, carrying the internal-service token; trip creation now genuinely rejects an employee id that doesn't belong to the claimed company.
- **Dependencies**: M3 (Tenant Service's validate endpoint), M4 (the stub being replaced).
- **Expected deliverables**: The cross-tenant attack scenario in [TenantArchitecture.md](TenantArchitecture.md) §5 is demonstrable end-to-end, through real services, not just described.
- **Validation before moving on**: The negative test from TenantArchitecture.md §5 passes against real running services; a Tenant Service outage is confirmed to fail trip *creation* specifically (hard dependency) while trip *reads* still work (per [ServiceDependencyMatrix.md](ServiceDependencyMatrix.md)).

## M6 — Messaging Infrastructure (Outbox + Publish Only)

- **Goal**: Wire MassTransit/RabbitMQ/outbox into Trip Service and Tenant Service, isolated from any new business logic — a pure infrastructure milestone.
- **Projects created**: Populate `SeeSight.Shared.Contracts` and `SeeSight.Shared.Messaging` for real (were skeletons since M0).
- **Features implemented**: `AddSeeSightMessaging()` extension, MassTransit EF Core outbox in Trip Service (publishing `TripSubmitted/Approved/Rejected/Cancelled/Completed/OfferAttached`) and Tenant Service (publishing `EmployeeCreated/Activated/Deactivated/LoginProvisioned`, `CompanyDeactivated`, `DepartmentCreated/Updated`), every event carrying `Version`/`UpdatedAtUtc` per [ADR 0005](adr/0005-reporting-projection-idempotency-and-department-lookup.md). **No consumers exist yet** — events publish into RabbitMQ and are visible in the management UI, but nothing reacts to them.
- **Dependencies**: M3, M5 (needs real trip/employee mutations to have something to publish about).
- **Expected deliverables**: Every documented event type is observably published to RabbitMQ when its triggering action occurs.
- **Validation before moving on**: A crash-injection test (kill the process between the DB commit and the outbox publish) confirms the event still gets published on restart — proving the outbox actually closes the dual-write gap, not just that it works in the happy path.

## M7 — Notification Service

- **Goal**: The first real event consumer — proves the entire event pipeline end-to-end for the first time.
- **Projects created**: `SeeSight.Notification` (single Vertical Slice project) + `SeeSight.Notification.Tests`.
- **Features implemented**: Consumers for `TripSubmitted/Approved/Rejected`, `EmployeeLoginProvisioned`; `ProcessedEvent` idempotency table; `/notifications/*` endpoints.
- **Dependencies**: M6.
- **Expected deliverables**: Submitting a trip through the full stack (Gateway → Trip Service → RabbitMQ → Notification Service) produces a real, queryable notification.
- **Validation before moving on**: A redelivered event (simulated) is confirmed not to create a duplicate notification, proving the idempotency table works, not just that happy-path delivery works.

## M8 — Search Service

- **Goal**: SerpAPI integration, fully independent of every other service (per [ADR 0002](adr/0002-ai-service-no-callback-to-trip-service.md)'s dependency-free philosophy applied here too).
- **Projects created**: `SeeSight.Search` + `SeeSight.Search.Tests`.
- **Features implemented**: `/travel/flights`, `/travel/hotels`, Redis caching + rate limiting with the fail-open policy from [ADR 0007](adr/0007-redis-dependent-features-fail-open.md).
- **Dependencies**: M0 only — this milestone could technically run in parallel with M1–M7 if development bandwidth allows, since it has no dependency on Identity/Tenant/Trip.
- **Expected deliverables**: Real flight/hotel searches return normalized results matching [APIContracts.md](APIContracts.md)'s DTO shapes.
- **Validation before moving on**: A Redis outage (simulated) is confirmed to bypass the cache rather than fail the search, per ADR 0007.

## M9 — Offer Attach/Select on Trip Service

- **Goal**: Connect Search Service's transient results to Trip Service's persisted offer snapshots.
- **Projects created**: None new.
- **Features implemented**: `POST /trips/{id}/offers/flight`, `POST /trips/{id}/offers/hotel` — deselect-then-insert-new-snapshot semantics, `EDITABLE_STATUSES` enforcement.
- **Dependencies**: M4 (Trip core), M8 (Search Service's DTO shape needs to be stable for the frontend/tests to pass a real offer through).
- **Expected deliverables**: A full search → attach → submit flow works.
- **Validation before moving on**: Attaching a second flight offer is confirmed to deselect the first (immutable history preserved, only one `Selected = true` row at a time).

## M10 — AI Service

- **Goal**: The fully dependency-free recommendation/parsing service.
- **Projects created**: `SeeSight.AI.Application/Infrastructure/Api` + its 3 test projects.
- **Features implemented**: `GroqAIService`, `RuleBasedFallback`, `TravelIntentHeuristicEngine` (ported regex/rule engine with regression fixtures), all validation rules (offer id/date/IATA/policy sanitization), `CompanyId`/`RequestedByUserId` denormalization and the narrowed authorization rule from [ADR 0004](adr/0004-ai-recommendation-history-authorization-scope.md).
- **Dependencies**: M0 only structurally (no service dependency at all, per ADR 0002) — but most useful for manual/integration testing once M9 exists, since real offer data makes recommendations meaningful to verify by eye.
- **Expected deliverables**: Recommendation and NL-parsing endpoints work with a real Groq key; a deliberately invalid key still returns a rule-based result.
- **Validation before moving on**: A dependency-graph check (`dotnet list package` / architecture test) confirms zero references to any other service's project or to a SerpAPI/Trip Service HTTP client; a multi-turn NL clarification is confirmed via logs to never trigger a second Groq call.

## M11 — Invoice Generation

- **Goal**: Fold invoicing into Trip Service, per the Invoice Service decision in [Microservices.md](Microservices.md) §1.
- **Projects created**: None new.
- **Features implemented**: `Invoice`/`InvoiceLineItem` entities, `GET /trips/{id}/invoice` (snapshot-once-then-reuse semantics), Tenant Service billing-name lookup call, QuestPDF rendering.
- **Dependencies**: M4, M5 (needs the Tenant Service client already built), M9 (needs real offer snapshots to build line items from).
- **Expected deliverables**: An eligible trip produces a real PDF invoice with a stable invoice number.
- **Validation before moving on**: Regenerating the invoice after changing the underlying trip's data is confirmed to return the *original* snapshot, not the updated data (immutability check).

## M12 — Reporting Service

- **Goal**: Event-driven dashboard + report projections — the most event-consumption-heavy service.
- **Projects created**: `SeeSight.Reporting.Application/Infrastructure/Api` + its 3 test projects.
- **Features implemented**: All consumers from [ServiceDependencyMatrix.md](ServiceDependencyMatrix.md)'s Reporting row, `TripSpendFact`, all four projections, `DepartmentLookup` + the idempotent/versioned upsert logic from [ADR 0005](adr/0005-reporting-projection-idempotency-and-department-lookup.md), tenant query filters on every table, `/dashboard/summary`, `/reports/summary`, `/reports/export`.
- **Dependencies**: M6 (event contracts + publishers must exist), M3 (Tenant events), M4/M11 (Trip events, including invoice-adjacent data if relevant).
- **Expected deliverables**: Dashboard/report numbers match a hand-computed expectation from a known seeded dataset.
- **Validation before moving on**: An out-of-order event redelivery (simulated — e.g. replay `TripApproved` after `TripCompleted` has already been processed) is confirmed to be a no-op, not a corruption, proving the version-based last-write-wins rule works; renaming a department is confirmed to immediately relabel historical reports (proving the `DepartmentLookup` join, not a frozen denormalization).

## M13 — Observability Hardening Pass

- **Goal**: While tracing/logging/health-checks are threaded into every milestone above from M1 onward, this milestone is a dedicated pass to fill gaps: Grafana dashboards (§ [Observability.md](Observability.md) §6), alert rules on the metrics identified as important (outbox backlog age, Redis unavailability, RabbitMQ queue depth), and a full end-to-end trace review (submit a trip, follow one trace through all involved services in Jaeger).
- **Dependencies**: M1–M12 (needs everything else to exist to have something meaningful to observe).
- **Expected deliverables**: A working Grafana dashboard set; at least one alert rule firing correctly against an injected failure (e.g. stop a consumer, confirm the queue-depth alert fires).
- **Validation before moving on**: A person unfamiliar with the code can diagnose an injected failure (e.g. Notification Service stopped) using only Grafana/Jaeger, without reading logs by hand first.

## M14 — Frontend Redesign

- **Goal**: The full [Frontend.md](Frontend.md) redesign against the now-complete backend.
- **Dependencies**: Can **start** as early as M2–M3 (auth + tenant are stable) and proceed feature-by-feature alongside backend milestones if development bandwidth allows parallelization — but full completion depends on every backend feature it surfaces (M1–M12).
- **Expected deliverables**: Every original page/flow works against the new Gateway; no `localStorage` token reads remain.
- **Validation before moving on**: A full manual walkthrough of the golden path (register → create company → add employees → plan a trip → search → attach offers → get an AI recommendation → submit → approve → view dashboard/report → export invoice) succeeds in a real browser.

## M15 — Deployment & Cutover

- **Goal**: Production rollout.
- **Dependencies**: M0–M14 complete and validated.
- **Expected deliverables**: All services live on Railway, one-time data migration executed and verified (§ [Deployment.md](Deployment.md) §6), production smoke test passed.
- **Validation before moving on**: This is the final milestone — validation here *is* production readiness: health checks green, golden-path smoke test green, migrated data spot-checked against the original system's known records.

## Ordering Rationale

M1 (walking skeleton) is deliberately first among *functional* milestones so a real, working, deployed (even if minimal) system exists almost immediately — every subsequent milestone adds to something already running rather than being validated in isolation for the first time at the end. M4 deliberately isolates the trip state machine from cross-service and messaging concerns (M5, M6) so the highest-complexity domain logic is proven correct before infrastructure complexity is layered on top of it — if something's wrong with the state machine, it's cheaper to find that in M4 than after M6's messaging is also in the mix. M8 (Search) and M10 (AI) are placed where they are for narrative/testing convenience (having real offers to search/recommend against) but are structurally independent enough to be pulled earlier or run in parallel if a team has more than one developer.
