# Development Roadmap

## Milestone Reporting Convention

Per your explicit instruction, every completed milestone below is reported back in this fixed format before moving to the next one:

- **What was implemented**
- **What changed** (relative to the plan or prior milestones, if anything)
- **How to test it**
- **What remains**
- **Any risks or technical debt**

No milestone is marked done with failing tests, a partial implementation, or a `TODO`/placeholder in place of real logic. If a milestone reveals that a previously agreed design decision should change, implementation stops and the reasoning is presented before any code changes course — per your explicit instruction, the agreed design is not silently deviated from.

## Phases

Each phase is independently testable: its own test suite passes, its health endpoints respond, and — from Phase 5 onward — it's reachable end-to-end through the Gateway.

**This phase list is the high-level view.** [ImplementationRoadmap.md](ImplementationRoadmap.md) breaks Phases 3–13 into 16 smaller milestones (M0–M15), each independently shippable and ordered so a real working system exists as early as possible (a "walking skeleton" — Gateway + Identity, minimally functional — before any other feature is built). Use that document for the actual execution order; the phases below are the coarser reference for how the milestones group.

### Phase 1 — Architecture ✅
This documentation set (`docs/*.md`), approved.

### Phase 2 — Documentation ✅
All `docs/*.md` files (including [Frontend.md](Frontend.md)) and the `docs/adr/*` decision records, with every required diagram, generated and reviewed (this phase).

### Phase 3 — Solution & Shared Libraries
- `.sln` structure per [Architecture.md](Architecture.md) §5.
- `SeeSight.SharedKernel`, `SeeSight.Shared.Contracts`, `SeeSight.Shared.Messaging`, `SeeSight.Shared.Observability`, `SeeSight.Shared.Common` — infrastructure only, per [CodingStandards.md](CodingStandards.md) §Shared Libraries.
- `docker/docker-compose.infra.yml` (Postgres, RabbitMQ, Redis, OTel Collector, Jaeger, Prometheus, Grafana).
- CI workflow skeleton (path-filtered, per [Deployment.md](Deployment.md) §5).
- **Verify**: `docker compose -f docker/docker-compose.infra.yml up` brings up all infra healthy; empty solution builds; shared library unit tests (if any pure logic exists yet, e.g. `MoneyRounding`, `CsvEscaper`) pass.

### Phase 4 — Identity Service
Register/login/logout/refresh/forgot-reset-change-password, JWKS endpoint, rate limiting on login/register.
- **Verify**: full auth flow exercised via the service's own Swagger UI; refresh rotation confirmed (old token rejected after use); repeated bad logins trigger `429`; startup fails intentionally when the signing key is unset outside `Development` (a deliberate negative test).

### Phase 5 — API Gateway
YARP routing to Identity Service, JWT validation against the JWKS endpoint, cookie+bearer dual support, correlation-id middleware, CORS, Redis-backed rate limiting wired (even if only Identity Service is behind it yet).
- **Verify**: login through the Gateway sets the cookie; a subsequent authenticated call succeeds; Gateway request logs contain routing/auth/rate-limit concerns only — no business-domain terms, confirming the "thin gateway" boundary held.

### Phase 6 — Tenant Service
Company/Department/Employee CRUD + lifecycle, `POST /internal/employees/validate` REST endpoint for Trip Service, outbox publishing `EmployeeCreated`/`EmployeeActivated`/`EmployeeDeactivated`/`EmployeeLoginProvisioned`/`CompanyDeactivated` events (needed by Reporting Service from Phase 11 — see [ADR 0001](adr/0001-no-gateway-aggregation-dashboard-in-reporting.md)). The `createLogin: true` employee-creation flow implements the compensating-action pattern from [Microservices.md](Microservices.md)/[TenantArchitecture.md](TenantArchitecture.md): if the local Employee save fails after Identity Service already created the linked User, Tenant Service calls Identity Service to delete the orphaned account before returning an error.
- **Verify**: company self-signup → create → employee roster CRUD, all through the Gateway; `createLogin: true` on an employee provisions a real Identity Service user via REST and the temp password flow works end-to-end; a simulated failure of the local Employee save after User creation is confirmed to clean up the orphaned Identity Service user rather than leaving it dangling.

### Phase 7 — Trip Service
Trip/TripTraveler/Approval/ApprovalAction as domain methods enforcing the state machine, offer-snapshot attach/select, auto-promotion scheduler, MassTransit EF Core outbox, and invoice generation (`Invoice`/`InvoiceLineItem`, folded in per [Microservices.md](Microservices.md) §1). Trip Service has **no** AI Service client — recommendations are requested by the frontend directly via the Gateway, per [ADR 0002](adr/0002-ai-service-no-callback-to-trip-service.md).
- **Verify**: full `DRAFT → ... → COMPLETED` lifecycle including a `reject → reopen → resubmit` cycle; offer attach/select deselects the prior offer correctly; outbox events land in RabbitMQ (visible in the management UI); invoice regeneration returns identical data across repeated calls even after the underlying trip changes (immutability check); a dependency-graph check confirms Trip Service has no AI Service client package/reference.

### Phase 8 — Search Service
SerpAPI integration, Redis cache + rate limit.
- **Verify**: flight/hotel search returns normalized results matching the DTO shapes in [APIContracts.md](APIContracts.md); repeated identical queries hit the Redis cache (confirmed via a cache-hit metric or log line, not just "it's fast").

### Phase 9 — AI Service
`GroqAIService`, rule-based fallback, `TravelIntentHeuristicEngine` with regression fixtures, offer-id/date/IATA validation.
- **Verify**: a deliberately broken Groq key still returns a rule-based recommendation; a multi-turn NL clarification confirmed (via logs/tracing) to never trigger a second Groq call; a dependency-graph check (or simple `dotnet list package`) confirms AI Service has **no SerpAPI client and no Trip Service client** package/dependency reference at all — its only outbound dependency is Groq (per [ADR 0002](adr/0002-ai-service-no-callback-to-trip-service.md)).

### Phase 10 — Notification Service
RabbitMQ consumers wired to Trip Service's outbox events, read/unread/clear endpoints, `ProcessedEvent` idempotency table exercised.
- **Verify**: submitting a trip produces an in-app notification for company admins within a few seconds, asynchronously; redelivering the same event (simulated) does not create a duplicate notification.

### Phase 11 — Reporting Service
Event consumers building `TripSpendFact` and the four projection tables (including `ActiveEmployeeCountProjection`, fed by Tenant Service's employee-lifecycle events), **dashboard summary (moved here from Trip Service — [ADR 0001](adr/0001-no-gateway-aggregation-dashboard-in-reporting.md))** + report CSV/JSON export.
- **Verify**: dashboard/report numbers match a hand-computed expectation from a known seeded dataset, matching the original system's `docs/DASHBOARD.md`/`docs/REPORTS.md` spend definitions exactly (committed-status-only spend, primary-traveler department attribution, majority-currency picking, 24-month range cap); confirm `GET /dashboard/summary` never triggers a synchronous call to Tenant Service (its active-employee count comes from the projection, not a live query).

### Phase 12 — Frontend Redesign
New folder structure, API layer, cookie-based `AuthContext`, TanStack Query adoption, component reorganization, error/loading-state overhaul — all targeting the new Gateway. Same UI/functionality, modernized implementation (see the approved architecture's frontend section).
- **Verify**: every existing page/flow works end-to-end in a browser against the new backend; no `localStorage` token reads remain in the codebase (grep-verified); a manual walkthrough of the golden path (register → create company → add employees → plan a trip → search → attach offers → submit → approve → view dashboard/report → export invoice) succeeds without errors.

### Phase 13 — Deployment & Cutover
Railway services for all 8 services + Gateway, managed Postgres/RabbitMQ/Redis, one-time data migration from the original Prisma/Postgres database into the new per-service databases (per [Deployment.md](Deployment.md) §6), cutover plan execution.
- **Verify**: production health checks green for every service; a smoke-test pass of the golden path above against the production URL; the original system's data present and correctly attributed post-migration (spot-checked row counts and a handful of specific known records per table).

## Cross-Cutting, Threaded Through Every Phase (Not a Separate Phase)

- **Observability**: every service's "done" definition from Phase 4 onward includes working health checks and basic tracing/logging — not bolted on retroactively at Phase 13.
- **Tests**: every phase's service ships with unit tests (domain logic) and integration tests (Testcontainers-backed) before being considered complete — "compiles and passes tests" is the exit criterion for every milestone, not just a final QA pass.
- **ADRs**: any deviation from this roadmap, or introduction of a new pattern/library not already named in this documentation set, gets a short ADR (see [CodingStandards.md](CodingStandards.md) §ADR Process) before it's implemented.

## Explicit Non-Goals (Not on This Roadmap)

- Kubernetes deployment (documented as ready-for-later in [Deployment.md](Deployment.md) §4, not built).
- A standalone File Storage Service (no current feature needs it — see [Microservices.md](Microservices.md) §1).
- Email/push notification delivery (the original system is in-app-only; adding real delivery channels is net-new scope, not part of this port, unless separately requested).
- gRPC anywhere (removed from the default communication model per the approved revision — see [Microservices.md](Microservices.md) §2).
