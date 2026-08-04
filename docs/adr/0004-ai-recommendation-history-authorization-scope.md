# 0004. AI Service recommendation-history reads are company/requester-scoped, not trip-access-scoped

Status: Accepted
Date: 2026-08-05

## Context

[ADR 0002](0002-ai-service-no-callback-to-trip-service.md) established that AI Service has zero outbound service dependencies — only Groq. On closer inspection while preparing the Service Dependency Matrix, `GET /ai/trips/{tripId}/recommendations` (recommendation history for a trip) needs an authorization check that, in the original monolith, was "is this user the trip's creator, a listed traveler, or an admin" — the same rule Trip Service uses for trip access. AI Service cannot evaluate that rule without either storing trip-access data itself (which it doesn't own) or calling Trip Service (which ADR 0002 rules out). Left unresolved, this is a real gap: either the endpoint enforces no meaningful access control, or ADR 0002 gets silently violated by adding a lookup call.

## Decision

`AiRecommendation` is extended with two denormalized fields captured at write time from the JWT claims of whoever requested the recommendation: `CompanyId` and `RequestedByUserId`. `GET /ai/trips/{tripId}/recommendations` is authorized using only data AI Service already owns:

- `SUPER_ADMIN` — any recommendation.
- `COMPANY_ADMIN` — any recommendation where `CompanyId` matches their own.
- `EMPLOYEE` — only recommendations where `RequestedByUserId` matches their own user id.

This is **narrower** than the original system's rule for `EMPLOYEE` (creator-or-traveler on the trip, not "only recommendations I personally requested") — an `EMPLOYEE` traveling on a trip they didn't personally request a recommendation for will no longer see a co-traveler's AI recommendation history for that trip.

## Consequences

- ADR 0002 holds with no exception — AI Service still has zero service-to-service dependencies.
- A minor, explicitly-documented behavior narrowing for `EMPLOYEE` users: recommendation history is personal, not shared across trip travelers, whereas the original system shared it with all trip travelers. `AiRecommendation` data is a travel suggestion, not sensitive personal data, so this is judged an acceptable trade-off — but it is a real, deliberate behavior change from the original spec, not a null-risk refactor, and should be called out if a user notices a co-traveler's recommendation is no longer visible to them.
- `AiRecommendation`'s schema in [DatabaseDesign.md](../DatabaseDesign.md) §6 gains `CompanyId` and `RequestedByUserId` columns and the same tenant-scoped EF Core query filter every other tenant-scoped entity uses (§ [TenantArchitecture.md](../TenantArchitecture.md)).
- If a product requirement later demands the fuller creator-or-traveler visibility, the fix is to add a narrowly-scoped Trip Service call for this one read endpoint specifically (an explicit, deliberate exception to ADR 0002 for a measured need) rather than reopening AI Service's dependency graph generally.
