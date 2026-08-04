# Architecture Validation — Pre-Phase-3 Review

This is a critical pass over the entire documentation set, performed before any code is written, looking specifically for the failure modes that don't show up in a diagram but do show up in production. Four findings from this pass changed the architecture (recorded as [ADR 0004](adr/0004-ai-recommendation-history-authorization-scope.md)–[0007](adr/0007-redis-dependent-features-fail-open.md), already applied to the relevant docs); the rest are either confirmed non-issues or deliberate, named trade-offs.

## 1. Unnecessary Complexity

**Honest assessment, not a hidden problem**: this architecture — 8 services, RabbitMQ + MassTransit + an outbox pattern, Redis, a full OpenTelemetry/Jaeger/Prometheus/Grafana stack, database-per-service — is genuinely more infrastructure than the underlying business problem (trip planning and approval for a modest number of companies) would organically need. A well-organized modular monolith, or 2–3 services at most, would serve the same feature set with less operational surface. This complexity is justified by the **explicit, repeatedly-reaffirmed goal of this project**: demonstrating a production-grade, enterprise-style .NET microservices architecture, not solving the smallest possible version of the business problem. That's a legitimate reason to accept the complexity — but it should be named plainly rather than left implicit, since "is this over-engineered" is a fair question whose honest answer is "yes, relative to the business problem alone, and that's the point."

**No change recommended** — this is the one dimension where the finding is "confirmed and accepted," not "fixed."

## 2. Over-Engineering (Specific Instances)

- **Reporting Service's fact-table + four projections** — checked against actual query needs (monthly spend, department breakdown, destination breakdown, active employee count) in [DatabaseDesign.md](DatabaseDesign.md) §8: each projection maps to a real, distinct query, none are speculative. **Not over-engineered.**
- **AI Service's 3-project split** (Application/Infrastructure/Api, no Domain) for what's ultimately one external HTTP call plus validation logic — this is a closer call than Trip/Tenant Service's full 4-layer split. The heuristic NL-parsing engine and the id/date/IATA validation logic are substantial enough to justify keeping `Application` and `Infrastructure` separate (there's real logic worth isolating from the Groq HTTP client), but it's fair to flag this as **worth revisiting** once implemented — if `Application` ends up thin in practice, collapsing to a Vertical Slice single project (like Search/Notification) would be a legitimate simplification, not a regression. **Watch item, no action now.**
- **Nothing else in the current design reads as speculative** — no unused abstraction layers, no interfaces with only one implementation and no test-double need (§ [CodingStandards.md](CodingStandards.md) §6 already states this as an explicit rule, and the design was checked against it: `IAIService` has exactly one production implementation *and* a documented test-fake need, which is why it's kept as an interface at all).

## 3. Hidden Coupling

- **Found and fixed**: [ADR 0004](adr/0004-ai-recommendation-history-authorization-scope.md) — AI Service's recommendation-history endpoint implicitly needed Trip Service's access-control data, which would have either silently reintroduced the circular dependency [ADR 0002](adr/0002-ai-service-no-callback-to-trip-service.md) removed, or shipped with broken/no authorization on that one endpoint if nobody noticed. Resolved by denormalizing the two fields AI Service actually needs onto its own table.
- **Checked and clear**: `Shared.*` libraries were audited against [CodingStandards.md](CodingStandards.md) §2's business-logic exclusion list — none of the five shared libraries encode a business decision (confirmed in [ProjectReferenceDiagram.md](ProjectReferenceDiagram.md) §6's reference table, which would make a cross-service coupling-through-shared-code possible if violated).
- **Checked and clear**: no service reads another service's database (§ [DatabaseDesign.md](DatabaseDesign.md) §1) and no service project references another service's project (§ [ProjectReferenceDiagram.md](ProjectReferenceDiagram.md) §1) — the two most common forms of hidden coupling in a "microservices" system that's secretly a distributed monolith are both structurally ruled out here, not just documented as a convention.

## 4. Duplicated Responsibilities

- **Rate limiting** — three call sites (auth, AI, Search) now explicitly share one distributed-limiter mechanism and one fail-open policy ([ADR 0007](adr/0007-redis-dependent-features-fail-open.md)) rather than three independent implementations, closing a duplication risk flagged in the previous review round.
- **Money/date/CSV helpers** — already consolidated into `SeeSight.Shared.Common` (§ [DatabaseDesign.md](DatabaseDesign.md) §1), directly fixing the original system's three duplicate copies of the same functions.
- **Tenant/ownership checks** — role-based checks live once per service (not duplicated across a controller-level and service-level check the way the original sometimes did) — see [Authorization.md](Authorization.md) §4.
- **No new duplication found** in this pass.

## 5. Scalability Bottlenecks

- **Trip Service's hourly auto-promotion scheduler** does a bulk update across all companies' due trips in one query — fine at this project's realistic scale, would need sharding/batching at a scale several orders of magnitude larger. **Accepted, not a current concern**, matches the original system's own design.
- **MassTransit outbox relay polling** — a single relay instance polling one table is a bottleneck only at a trip-mutation volume this system won't see. **Accepted.**
- **Redis as a single shared cache/rate-limit store** — already addressed via [ADR 0007](adr/0007-redis-dependent-features-fail-open.md)'s fail-open policy, which means Redis being *slow* or *down* degrades specific features rather than bottlenecking the whole system.
- **No genuine scalability bottleneck identified** for this project's realistic load profile.

## 6. Security Concerns

- **Found and fixed**: [ADR 0006](adr/0006-internal-service-to-service-authentication.md) — internal-only endpoints (Tenant Service's employee-validation endpoint) relied solely on network-boundary trust ("it's not publicly routable"), with no defense-in-depth if that assumption ever turned out to be wrong. Fixed with a shared internal-service token.
- **Confirmed adequate**: RS256 + JWKS + short-lived access tokens + rotating refresh tokens (§ [Authentication.md](Authentication.md)) is a standard, sound pattern. The one accepted trade-off, stated explicitly rather than hidden: a deactivated user's still-valid access token remains usable for up to 15 minutes after deactivation (the JWT is stateless by design) — an accepted blast-radius limit, not an oversight, and already about as tight as JWT access-token lifetimes get in practice without reintroducing a per-request DB check that the whole RS256/JWKS design exists to avoid.
- **Confirmed adequate**: secrets fail-fast at startup (§ [Authentication.md](Authentication.md) §5), CSV escaping ported verbatim (§ [CodingStandards.md](CodingStandards.md)/[DatabaseDesign.md](DatabaseDesign.md)), strict deserialization called out as needing explicit implementation (§ [CodingStandards.md](CodingStandards.md) §4, since it has no ASP.NET Core default).

## 7. Multi-Tenant Risks

- **Found and fixed**: Reporting Service's and AI Service's databases hold `CompanyId`-scoped data but hadn't been explicitly confirmed to carry the same EF Core tenant query filter as the write-side services (Trip, Tenant) — an easy omission on "it's just a read model" services. Now explicit in [DatabaseDesign.md](DatabaseDesign.md) §6 and §8.
- **Confirmed adequate**: the three-layer defense-in-depth (JWT claim integrity, EF Core query filter, cross-service validation call) in [TenantArchitecture.md](TenantArchitecture.md) §3 covers every write path in the system; the cross-tenant attack scenario (§5 of that document) is now demonstrable against real services per [ImplementationRoadmap.md](ImplementationRoadmap.md) M5's validation step, not just describable on paper.

## 8. Event Consistency Issues

- **Found and fixed** (the most substantive finding of this pass): [ADR 0005](adr/0005-reporting-projection-idempotency-and-department-lookup.md) — two related gaps. First, RabbitMQ doesn't guarantee per-aggregate event ordering, and the original projection design had no defense against an out-of-order redelivery corrupting a projection; fixed with a version-based last-write-wins rule on every projection upsert. Second, denormalizing `DepartmentName` onto `TripSpendFact` at event-processing time would have silently changed the original system's behavior (which always shows the *current* department name for historical trips via a live join) into "shows whatever name was current when the event was processed" — a subtle regression nobody asked for. Fixed with a `DepartmentLookup` table joined at read time.
- **Confirmed adequate**: the transactional outbox pattern (§ [Microservices.md](Microservices.md) §2) correctly closes the dual-write gap between a domain-state commit and its corresponding event publish — this was already sound design, re-verified here rather than re-derived.

## 9. Deployment Risks

- **Confirmed adequate, already documented**: the one-time data migration (§ [Deployment.md](Deployment.md) §6) already specifies a dry-run/verification pass before cutover, and [ImplementationRoadmap.md](ImplementationRoadmap.md) M15 makes production-readiness validation an explicit milestone exit criterion rather than an afterthought.
- **Confirmed adequate**: per-service `healthcheckPath`/`preDeployCommand` (§ [Deployment.md](Deployment.md) §2) means a bad deploy to one service doesn't silently go live — Railway won't route traffic to a service that fails its own readiness check.
- **No new deployment risk identified** beyond what §11's cost note already covers.

## 10. Operational Complexity

This is the honest cost side of §1's honest complexity assessment: **9 always-on services plus RabbitMQ, Redis, and a 4-component observability stack is a real day-2 operational burden for a single maintainer** — more log streams to check when something breaks, more independent things to keep patched, more moving parts in any incident. The documentation set already mitigates this as much as reasonably possible for the architecture chosen: a unified Grafana view across all services (§ [Observability.md](Observability.md) §6) rather than 9 separate places to look, `docker-compose.yml` for a consistent one-command local environment, and path-filtered CI so a change to one service doesn't require rebuilding/redeploying the rest. But mitigated is not eliminated — this remains the single biggest ongoing cost of the architecture, and it is a direct, accepted consequence of the project's stated goal (§1), not something further doc changes can design away.

## Summary Table

| # | Dimension | Finding | Resolution |
|---|---|---|---|
| 1 | Unnecessary complexity | System is more infrastructure than the business problem alone needs | **Accepted** — explicit project goal, named plainly, not reduced |
| 2 | Over-engineering | AI Service's 3-project split is a closer call than Trip/Tenant's | **Watch item** — no action, revisit after implementation if it feels heavy |
| 3 | Hidden coupling | AI Service recommendation-history endpoint implicitly needed Trip Service data | **Fixed** — [ADR 0004](adr/0004-ai-recommendation-history-authorization-scope.md) |
| 4 | Duplicated responsibilities | None found this pass (prior round's findings already fixed) | **Confirmed clear** |
| 5 | Scalability bottlenecks | None at this project's realistic scale | **Confirmed clear** |
| 6 | Security concerns | Internal endpoints relied on network-boundary trust alone | **Fixed** — [ADR 0006](adr/0006-internal-service-to-service-authentication.md) |
| 7 | Multi-tenant risks | Reporting/AI Service databases weren't explicitly confirmed to carry the tenant query filter | **Fixed** — [DatabaseDesign.md](DatabaseDesign.md) §6, §8 |
| 8 | Event consistency issues | No ordering/idempotency guarantee on projections; department-name denormalization would go stale | **Fixed** — [ADR 0005](adr/0005-reporting-projection-idempotency-and-department-lookup.md) |
| 9 | Deployment risks | None new beyond what was already documented | **Confirmed clear** |
| 10 | Operational complexity | Real day-2 burden for a solo maintainer, already mitigated as much as the architecture allows | **Accepted** — inherent to the chosen architecture, not further reducible without reducing the architecture itself |

## Final Recommendation

**The architecture is ready for Phase 3 implementation.**

Every finding that represented a genuine defect (hidden coupling, a security gap, a multi-tenant omission, an event-consistency correctness bug) has been resolved and reflected in the documentation, not just noted. The two findings left open (unnecessary complexity, operational complexity) are named honestly rather than hidden, and are direct, accepted consequences of this project's explicit goal — not oversights, and not fixable without changing that goal, which is not being proposed. The one remaining "watch item" (AI Service's project split) is low-stakes and explicitly deferred to a natural revisit point after implementation, not a blocker.

No further architectural changes are required before Phase 3 begins. Waiting for your explicit approval to proceed.
