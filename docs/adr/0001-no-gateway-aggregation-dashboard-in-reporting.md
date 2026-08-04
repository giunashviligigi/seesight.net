# 0001. No business-data aggregation at the Gateway; Dashboard moves to Reporting Service

Status: Accepted
Date: 2026-08-03

## Context

The first documentation pass listed "response aggregation for the frontend (composing calls to multiple services into one response)" as a Gateway responsibility, with `GET /dashboard/summary` given as the example — the Gateway would call Trip Service, Tenant Service, and Reporting Service and merge the results. Separately, [APIContracts.md](../APIContracts.md) placed `GET /dashboard/summary` entirely inside Trip Service.

These two documents contradicted each other, and neither was actually correct on inspection. The original system's dashboard summary mixes data that, in the new service boundaries, is owned by two different services: upcoming trips / pending-approval counts (Trip Service) and active-employee count (Tenant Service). Composing that into one response is a **business decision about what the dashboard means**, not a structural pass-through — which fields come from where, how they're combined, and what "active" means are all domain concerns. Doing that composition in the Gateway would be exactly the kind of business logic the Gateway is explicitly supposed to never contain (per the approved architecture's "keep the API Gateway thin" requirement). Doing it synchronously inside Trip Service would give Trip Service a hard runtime dependency on Tenant Service for every dashboard load, and would make the dashboard unavailable whenever Tenant Service is briefly down — even though nothing about viewing a dashboard should depend on the roster service being up at that exact moment.

## Decision

- The API Gateway's responsibilities are narrowed to remove "response aggregation" entirely. It performs routing, auth, rate limiting, CORS, correlation IDs, and Swagger/OpenAPI *spec* aggregation only (listing multiple services' API docs in one UI is not business logic — merging their business data is).
- `GET /dashboard/summary` moves from Trip Service to **Reporting Service**, alongside `GET /reports/summary`. Reporting Service already exists specifically to compose cross-service data into read models via events (§ [Microservices.md](../Microservices.md) §1) — Dashboard is simply another projection, built the same way as the historical report projections, just refreshed on a tighter event set (trip status changes, offer attachment) for a "live-enough" feel.
- Tenant Service is extended to publish `EmployeeCreated` and `EmployeeActivated` events (alongside the already-planned `EmployeeDeactivated`) so Reporting Service can maintain an `ActiveEmployeeCount` projection without a synchronous call to Tenant Service on every dashboard load.

## Consequences

- The Gateway stays provably free of business logic — nothing about "what counts as an active employee" or "how spend is computed" lives there.
- Dashboard reads never have a synchronous cross-service dependency; Reporting Service being briefly behind on events means a dashboard is at most a few seconds stale, not unavailable.
- Trip Service loses a synchronous outbound call it previously would have needed to Tenant Service purely for employee counts — one less coupling point.
- Reporting Service's scope grows slightly (now "live-ish operational view" + "historical spend analysis," both event-driven projections) — this is judged a better fit than either alternative, not scope creep, since both are the same kind of work (cross-service read composition) Reporting Service already exists to do.
