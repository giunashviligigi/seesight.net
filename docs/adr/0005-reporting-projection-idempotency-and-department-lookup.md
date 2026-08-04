# 0005. Reporting Service projections are idempotent, apply last-write-wins by event version, and resolve department names via a live lookup table

Status: Accepted
Date: 2026-08-05

## Context

Two related correctness gaps surfaced during the architecture validation pass:

1. **Event ordering isn't guaranteed.** RabbitMQ (even via MassTransit) doesn't guarantee strict per-aggregate delivery order under retries, redeliveries, or concurrent consumers. If `TripCompleted` were somehow delivered and processed before `TripApproved` for the same trip (a retry after a transient failure, for instance), a projection that blindly "applies events in arrival order" could end up in a wrong final state.
2. **Denormalizing `DepartmentName` onto `TripSpendFact` at event-processing time freezes it.** The original monolith computes department attribution via a live join at report-generation time — renaming a department immediately changes how *all* historical trips are labeled in every future report. A projection that copies the name once, when the event is processed, would keep showing the old name for historical facts after a rename — a subtle, silent regression versus the original system's actual behavior, not something anyone asked to change.

## Decision

- **Idempotency + last-write-wins by version**: every integration event `Shared.Contracts` carries the source aggregate's `Version` (or `UpdatedAtUtc`, whichever the aggregate already tracks) alongside its payload. Reporting Service's projection upserts compare the incoming event's version against the version already recorded on `TripSpendFact` for that `TripId` and only apply the update if the incoming version is newer — an out-of-order redelivery is a no-op, not a corruption. Combined with the existing `ProcessedEvent` idempotency table (§ [DatabaseDesign.md](../DatabaseDesign.md) §2), this makes projection updates safe under both duplicate delivery and out-of-order delivery.
- **`DepartmentLookup` table, not frozen names**: Reporting Service maintains a small `DepartmentLookup(DepartmentId, DepartmentName, UpdatedAtUtc)` table, kept current by consuming two new Tenant Service events — `DepartmentCreated` and `DepartmentUpdated` (added to the event set in [Microservices.md](../Microservices.md) §2). `TripSpendFact`/`DepartmentTripProjection` store only `DepartmentId`; the department *name* shown in any dashboard/report query is resolved via a join against `DepartmentLookup` at read time, not baked into the fact row. This restores the original system's "renaming a department immediately relabels historical reports" behavior while staying fully event-driven (no synchronous call to Tenant Service at read time).

## Consequences

- Reporting Service's read queries do one extra local join (`TripSpendFact` → `DepartmentLookup`) — trivial cost, no cross-service call.
- Every domain event in `Shared.Contracts` needs a `Version`/`UpdatedAtUtc` field from here on — a small, permanent addition to the event contract shape, worth stating as a project-wide convention rather than a one-off for this projection.
- `DestinationProjection` doesn't have an equivalent staleness risk (country/city names on a trip aren't renamed by an external process the way departments are), so no analogous lookup table is needed there.
- This pattern (idempotent, versioned, lookup-joined projections) is the template for any future Reporting Service projection that denormalizes a name/label from another service's data.
