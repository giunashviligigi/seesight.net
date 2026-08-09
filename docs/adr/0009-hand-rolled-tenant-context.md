# 0009. Tenant isolation: hand-rolled `ITenantContext`/`TenantId` + native EF Core query filters, not Finbuckle.MultiTenant

Status: Accepted
Date: 2026-08-09

## Context

[TenantArchitecture.md](../TenantArchitecture.md) §2 flags an open implementation choice for Tenant Service (this milestone): "evaluate **Finbuckle.MultiTenant** at Phase 6 — it's a mature, purpose-built .NET library for exactly this problem (per-request tenant resolution + EF Core query filter integration) and may reduce custom code here further than what's described below... record the choice either way as an ADR." This is that ADR.

Finbuckle.MultiTenant solves two problems together: (1) **tenant resolution** — figuring out which tenant a request belongs to, typically from a subdomain, route segment, header, or claim, often supporting several strategies at once and letting the caller plug in custom `IMultiTenantStrategy`/`IMultiTenantStore` implementations; and (2) **EF Core integration** — an `IMultiTenantDbContext` base that applies a query filter automatically once a `ITenantInfo` has been resolved into the current `IMultiTenantContextAccessor`.

Our system's tenant-resolution problem is already fully solved, and solved differently from what Finbuckle assumes: the Gateway validates the JWT exactly once and forwards the `companyId` claim as a trusted `X-User-Company-Id` header (§ [Authorization.md](../Authorization.md) §2); each service already materializes that into a scoped `ICurrentUserContext` (`SeeSight.SharedKernel`, built in M1). There is exactly one resolution strategy, it is already implemented, and it is shared by every service that needs it. Finbuckle's multi-strategy resolution pipeline (header/claim/route/subdomain, configurable per app) has no problem left to solve here — adopting it would mean either bypassing most of its own resolution machinery to feed it our already-resolved `companyId`, or duplicating resolution logic Finbuckle doesn't need to run.

## Decision

Hand-roll tenant isolation instead of adopting Finbuckle.MultiTenant:

- **`TenantId`** — a small, stable value object (`readonly record struct TenantId(Guid Value)`) in `SeeSight.SharedKernel`, per [CodingStandards.md](../CodingStandards.md) §2's explicit allowance for small stable value objects in that library.
- **`ITenantContext`** — a new `SharedKernel` interface (`TenantId? CompanyId { get; }`, `bool IsSuperAdmin { get; }`), populated from the already-existing `ICurrentUserContext` (its `CompanyId`/`Role` are the only two pieces of data needed). This is pure data mapping, not a tenant-scoping *decision* — same non-business-logic classification `ICurrentUserContext` itself already has.
- **EF Core query filters** — each tenant-scoped `DbSet` (`Company`, `Department`, `Employee` in Tenant Service) gets a native `HasQueryFilter(e => e.CompanyId == tenant.CompanyId.Value || tenant.IsSuperAdmin)` in the owning service's `Infrastructure` layer — not a shared abstraction, since *which* entities are tenant-scoped and *what* the bypass condition is stays a per-service `DbContext` concern, consistent with [TenantArchitecture.md](../TenantArchitecture.md) §3's "the filter is on the `DbContext` model itself."
- **`ITenantResolver`** (the super-admin-must-pass-explicit-`companyId`-on-list/create rule from [TenantArchitecture.md](../TenantArchitecture.md) §4) stays exactly where that document already puts it: a small Application-layer helper inside Tenant Service, not shared — it's request validation, not query scoping, and it encodes a real business rule ("no default tenant for a super admin"), which per [CodingStandards.md](../CodingStandards.md) §2 does not belong in a shared library regardless of how convenient sharing it would be.

### Why Finbuckle.MultiTenant is unnecessary here

- **No multi-strategy resolution problem to solve.** Finbuckle's core value is letting one app resolve tenants from several possible signals; we have exactly one signal (a Gateway-forwarded, JWT-derived header), already resolved by existing shared code before Finbuckle would ever run.
- **No independent tenant store.** Finbuckle's `IMultiTenantStore` models tenants as directly-addressable configuration Finbuckle itself loads (by subdomain/identifier lookup, often cached). Our tenant *is* the aggregate root row Tenant Service owns and queries — there is no separate tenant directory to look up before a request can proceed.
- **Bridging cost, not reduction.** Adopting it would mean writing an `ITenantInfo`/`IMultiTenantStore` adapter around `ICurrentUserContext` just to satisfy Finbuckle's own contract, then still writing the super-admin-bypass and explicit-`companyId`-required rules ourselves in Application code (Finbuckle has no concept of either) — net more code and one more package to understand, not less.
- **Consistent with the project's standing rule.** [CodingStandards.md](../CodingStandards.md) §6/§2 asks "does this encode a business rule, or is it truly generic plumbing, and is the abstraction earning its keep?" The actual problem here — one column, one predicate, one bypass condition — is smaller than the library built to solve a broader class of problem. This is the same reasoning [ADR 0003](0003-adopt-masstransit-for-messaging.md) used to reach the *opposite* conclusion for messaging (there, hand-rolling outbox/retry/dead-letter plumbing was reinventing a genuinely hard, already-solved problem; here, hand-rolling is reinventing nothing — the resolution problem was already solved in M1, and the filter itself is a single native EF Core feature).

### Why the hand-rolled approach is sufficient

- `HasQueryFilter` is a first-class, well-understood EF Core feature — not custom infrastructure, just using the ORM's own tenant-isolation primitive directly.
- The three enforcement layers this system relies on (§ [TenantArchitecture.md](../TenantArchitecture.md) §3 — signed JWT claim, EF Core query filter, cross-service validation calls) are each independently sufficient defense-in-depth; none of them requires Finbuckle's resolution pipeline to hold.
- `ITenantContext` is trivially unit-testable (pure mapping from `ICurrentUserContext`, no static/ambient state), and the query filter itself is exercised directly by integration tests against a real database (cross-tenant negative tests, super-admin-bypass tests — see `docs/validation/M3/`), which is the same testing approach the query filter would need regardless of which abstraction sits above it.

## Security / isolation implications

- Isolation strength is unchanged either way — a `WHERE CompanyId = @tenant` filter is a `WHERE CompanyId = @tenant` filter whether EF Core applies it because of a hand-rolled `HasQueryFilter` call or because a third-party library configured the same call on our behalf. Finbuckle would not have made the isolation boundary stronger.
- The one thing a query filter can never protect against — a query that opts out of it (`.IgnoreQueryFilters()`) — is identical under either approach; the mitigation is the same regardless: `IgnoreQueryFilters()` is reserved for the one legitimate super-admin-bypass code path and reviewed accordingly, not something a library choice changes.
- Because resolution is unchanged (still the Gateway-validated JWT → forwarded header → `ICurrentUserContext`), this decision has zero effect on [ADR 0006](0006-internal-service-to-service-authentication.md)'s internal-call authentication or on any claim-forgery consideration already covered by [Authentication.md](../Authentication.md)/[Authorization.md](../Authorization.md) — it only changes *which code* wires an already-trusted `CompanyId` into an EF Core filter, not *how much that value can be trusted*.

## Consequences

- One fewer third-party dependency; the tenant-isolation code path is fully owned, readable, and testable in this codebase.
- If a future service ever needs genuinely multi-strategy tenant resolution (unlikely given the Gateway-centralized-claim-forwarding model this whole system is built on), revisit Finbuckle at that point — not a concern for Tenant Service or any service built the same way.
- `TenantId`/`ITenantContext` are additive to `SharedKernel`; no existing `ICurrentUserContext` consumer needs to change.
