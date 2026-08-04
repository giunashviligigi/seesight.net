# 0003. Adopt MassTransit for RabbitMQ integration instead of a hand-rolled abstraction

Status: Accepted
Date: 2026-08-03

## Context

The first documentation pass described `SeeSight.Shared.Messaging` as a from-scratch abstraction: publisher/consumer interfaces, an outbox-pattern helper, and retry/dead-letter conventions, implemented directly against `RabbitMQ.Client`. On review, this is reinventing a well-solved problem — modern .NET has a mature, widely-used library (MassTransit) purpose-built for exactly this: RabbitMQ transport, an EF Core-integrated transactional outbox, retry/circuit-breaker policies, and consumer routing conventions, all production-tested rather than hand-maintained. Hand-rolling equivalent plumbing is meaningfully more implementation and maintenance effort for the same result, and is exactly the kind of "unnecessary abstraction" the project's own coding standards ask to avoid.

## Decision

`SeeSight.Shared.Messaging` becomes a thin configuration/conventions layer over **MassTransit** (RabbitMQ transport), not a hand-rolled publisher/consumer abstraction:

- MassTransit's EF Core outbox integration (`AddEntityFrameworkOutbox`) is used directly in Trip Service and Tenant Service, rather than a custom `OutboxMessage` polling relay implemented from scratch. The `OutboxMessage` table shape in [DatabaseDesign.md](../DatabaseDesign.md) §2 stays conceptually the same (MassTransit's outbox table is functionally equivalent) — the schema there should be treated as the *intent*, with the actual column shape following MassTransit's own outbox entity conventions at implementation time.
- `SeeSight.Shared.Contracts`'s integration-event DTOs (`TripApprovedIntegrationEvent`, etc.) become MassTransit message contracts (plain C# records/classes — MassTransit doesn't require a special base type).
- `SeeSight.Shared.Messaging` retains ownership of: the shared MassTransit bus configuration convention (`AddSeeSightMessaging()` extension, applied per service), consumer retry/redelivery policy defaults, and the `ProcessedEvent` idempotency-table pattern for consumers (MassTransit has its own idempotency filters available too — evaluated at implementation time against the simpler custom table already documented).

## Consequences

- Less custom code to build, test, and maintain for outbox/retry/dead-letter behavior — this is a direct win against the "avoid unnecessary abstraction" instruction.
- The project takes a dependency on a specific third-party library (MassTransit) for its messaging layer — an accepted trade-off, since it's a mature, widely-adopted choice in the .NET ecosystem specifically for this problem, not a niche or high-risk dependency.
- The exact outbox table schema in [DatabaseDesign.md](../DatabaseDesign.md) §2 is provisional pending MassTransit's own outbox entity shape — reconcile at Phase 3/7 implementation time rather than treating the documented columns as final.
