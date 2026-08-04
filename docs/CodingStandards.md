# Coding Standards

## 1. Solution Structure

The complete project-by-project layout lives in **[SolutionStructure.md](SolutionStructure.md)**; the full reference-rule breakdown (allowed/forbidden per project, with reasons, and the argument for why no circular reference is possible) lives in **[ProjectReferenceDiagram.md](ProjectReferenceDiagram.md)**. In short: project references only ever point "inward" (`Api` → `Application` → `Domain`; `Infrastructure` → `Application`/`Domain`) — never the reverse, and never sideways to another service's project. Enforced by `NetArchTest` rules in each service's `ArchitectureTests` project, run in CI.

## 2. Shared Libraries — Infrastructure Only

Per the approved architecture, shared projects contain **cross-cutting infrastructure only** — never business logic. This is the single most important rule for avoiding a distributed monolith, so it's restated here as an enforceable checklist:

| Library | Allowed | Not allowed |
|---|---|---|
| `SeeSight.SharedKernel` | Small stable value objects (`TenantId`, `Money`), marker interfaces (`ISoftDelete`, `IHasTenant`), the `ICurrentUserContext` interface **and** its one shared implementation/middleware (`CurrentUserContextMiddleware`, populated by deserializing the Gateway-forwarded `X-User-*` headers into the DTO — pure header-to-object mapping, no authorization *decision* is made here, so sharing the plumbing doesn't violate the business-logic rule below); the internal-service-token validation middleware for internal-only endpoints (§ [ADR 0006](adr/0006-internal-service-to-service-authentication.md) — again pure header validation, no business decision) | Any tenant-scoping *decision* (EF Core query filter predicates, the super-admin-bypass check), any state-machine/workflow type |
| `SeeSight.Shared.Contracts` | Versioned RabbitMQ integration-event contracts (MassTransit message DTOs — `TripApprovedIntegrationEvent`, etc.), explicit per-event schema versioning (a new event type name, e.g. suffix `V2`, rather than mutating a shipped event's shape — consumers use a tolerant-reader pattern, ignoring unknown fields, so a producer-side additive change never breaks an older consumer) | Command/query types (those stay internal to each service) |
| `SeeSight.Shared.Messaging` | MassTransit bus configuration conventions (`AddSeeSightMessaging()`), consumer retry/redelivery policy defaults, the `ProcessedEvent` idempotency-table pattern — see [ADR 0003](adr/0003-adopt-masstransit-for-messaging.md) | Event *handler* logic, and no hand-rolled RabbitMQ.Client abstraction — MassTransit already owns that layer |
| `SeeSight.Shared.Observability` | `AddSeeSightObservability()`, correlation-id middleware, health-check extensions, log-scrubbing enricher | — |
| `SeeSight.Shared.Common` | `MoneyRounding`, `CsvEscaper`, the `PagedResult<T>` envelope | Anything encoding *when* money is rounded or *what* counts as committed spend — that's Trip/Reporting Service business logic |

**Before adding anything to a shared library, ask: "does this encode a business rule/decision, or is it truly generic plumbing?"** Header deserialization, bus configuration, and CSV escaping are plumbing — sharing them is fine and reduces duplication. Deciding *which* company a query is scoped to, *whether* a user is authorized for an action, or *what counts as* committed spend are decisions — those belong inside the one service that owns that rule, even if it looks convenient to share. A shared library is allowed to grow slowly; a shared library that starts encoding domain decisions is how a microservices system quietly becomes a distributed monolith.

## 3. Naming & Style

- Standard .NET/C# conventions (PascalCase for types/methods/public members, camelCase for locals/parameters, `_camelCase` for private fields), enforced via `.editorconfig` + `dotnet format` in CI.
- MediatR commands/queries named `<Verb><Noun>Command`/`<Verb><Noun>Query` (e.g. `SubmitTripCommand`, `GetTripByIdQuery`), handlers `<Name>Handler`, validators `<Name>Validator` (FluentValidation), one file each, co-located by feature.
- Domain entities expose behavior, not public setters — state changes happen through named methods (`trip.Submit()`, not `trip.Status = TripStatus.PendingApproval`), so invariants can't be bypassed by assigning a property directly.
- DTOs at the `Api` layer are distinct types from `Application`-layer command/query results — no leaking EF Core entities or domain objects across the HTTP boundary.

## 4. Validation

- FluentValidation for every command/query with input, wired as a MediatR pipeline behavior (`ValidationBehavior<TRequest, TResponse>`) so validation runs uniformly before a handler executes, rather than being hand-called per handler.
- Request DTOs at the `Api` layer use strict deserialization (a custom `JsonSerializerOptions` contract resolver, or `[JsonExtensionData]` + explicit rejection, since `System.Text.Json` doesn't reject unknown properties by default) — the direct successor to the original system's `ValidationPipe({ whitelist: true, forbidNonWhitelisted: true })`. This is called out explicitly because it has no framework default in ASP.NET Core and is easy to silently skip.

## 5. Testing

- **Unit tests** (xUnit): Domain layer tested with zero infrastructure. `FluentAssertions` for readable assertions.
- **Integration tests**: Testcontainers spin up real ephemeral Postgres/RabbitMQ per test run; `WebApplicationFactory<TEntryPoint>` for in-process API testing.
- **Architecture tests**: `NetArchTest`, one test class per service, asserting the layer-reference rules in §1 and the "no shared-library business logic" boundary (e.g. a rule that fails the build if any `Domain` project references a `Shared.*` project other than `SharedKernel`).
- **External providers always faked/mocked in tests, never called live** — `IAIService`/SerpAPI client interfaces are designed to be trivially fakeable (`FakeAIService`, `FakeSerpApiClient` in a shared test-utilities project, not `Shared.*` production code).
- A milestone is not "done" (per [DevelopmentRoadmap.md](DevelopmentRoadmap.md)) until its service compiles and its full test suite (unit + integration + architecture) passes.

## 6. SOLID — Applied Where It Earns Its Keep

- **Single Responsibility**: each MediatR handler does one thing; each service owns one bounded context (§ [Microservices.md](Microservices.md) §1).
- **Open/Closed**: domain entities expose behavior methods that can be extended by adding new methods, not by branching on type codes scattered across callers.
- **Liskov/Interface Segregation**: kept minimal and pragmatic — `IAIService` is intentionally a 2-method interface (§ [AIArchitecture.md](AIArchitecture.md)), not a large abstraction. No interface is introduced "for SOLID's sake" without a real substitution need (testing, or a genuine multiple-implementation case) — an interface with exactly one production implementation and no test-double need is a needless abstraction, not good design.
- **Dependency Inversion**: `Application` layers depend on interfaces they define, implemented in `Infrastructure` — but this is *not* applied to force a repository interface in front of every `DbContext` (see [Microservices.md](Microservices.md) §8's explicit rejection of blanket Repository pattern usage).

The operating rule: **SOLID guides the design of code that has more than one reason to change or more than one real implementation. It's not a checklist to satisfy on every class.**

## 7. ADR Process

Any time an implementation milestone introduces a **new pattern or technology not already named in this documentation set** (e.g. choosing a specific outbox-relay implementation strategy, adding a library not listed here, deviating from an agreed design after discussion per your explicit instruction), a short Architecture Decision Record is added to `docs/adr/NNNN-title.md`:

```markdown
# NNNN. <Short title>

Status: Accepted
Date: YYYY-MM-DD

## Context
What problem or question prompted this decision.

## Decision
What was decided.

## Consequences
What this makes easier or harder going forward; any trade-off accepted.
```

ADRs are numbered sequentially and never edited after acceptance — a later decision that changes course gets a **new** ADR that supersedes the old one (linked both ways), the same append-only philosophy already used for `ApprovalAction`/offer snapshots in the domain model itself.

Seven ADRs already exist from the two architecture review passes (`docs/adr/0001`–`0007`): the Gateway/Dashboard boundary correction, the AI/Trip circular-dependency removal, the MassTransit adoption, the AI recommendation-history authorization narrowing, Reporting Service's projection idempotency/department-lookup fix, internal service-to-service authentication, and the Redis fail-open policy — read these as the worked examples of the format. Two implementation-time evaluations flagged during review are **not yet ADRs** because no decision has been made: whether to adopt **Finbuckle.MultiTenant** for the tenant-context/query-filter plumbing in [TenantArchitecture.md](TenantArchitecture.md) (vs. the hand-rolled version currently documented), and which specific package backs the Redis-based distributed rate limiter in [Authentication.md](Authentication.md) §6 (ASP.NET Core's built-in limiter has no first-party distributed store). Whichever way each is decided at implementation time, it gets its own ADR then.

## 8. No Placeholders

No `TODO`, `NotImplementedException`, stub method, or commented-out block is committed as if a feature were done. If a milestone's scope must be trimmed, the roadmap ([DevelopmentRoadmap.md](DevelopmentRoadmap.md)) is updated to reflect the real scope — the code never silently pretends to implement something it doesn't.
