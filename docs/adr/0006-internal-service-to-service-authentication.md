# 0006. Internal service-to-service REST calls carry a shared internal-auth header, not network-boundary trust alone

Status: Accepted
Date: 2026-08-05

## Context

The only synchronous service-to-service REST edge in the system (Trip Service → Tenant Service, for employee/company validation, plus the Tenant Service → Identity Service call for login provisioning) was documented as trusted purely because it happens on Railway's private internal network — no backend service has a public URL (§ [Deployment.md](../Deployment.md) §3). Relying solely on "it's on the private network" is a real security anti-pattern if that network-isolation assumption is ever wrong — a misconfigured Railway networking setting, a future migration to a platform with different defaults, or simply a mistake — would silently turn every internal endpoint into an unauthenticated, unauditable admin-level API with no defense-in-depth behind the network boundary.

## Decision

Every internal-only endpoint (marked "internal only — not Gateway-routed" in [APIContracts.md](../APIContracts.md), e.g. `POST /internal/employees/validate`) requires a shared internal-service credential in addition to network placement: a static, per-environment secret (`Internal:ServiceToken`, distinct from the JWT signing key, rotated independently) sent as `X-Internal-Service-Token` and validated with a constant-time comparison before the request is processed. This is deliberately simple — not mTLS, not a second JWT issuer — proportionate to the actual risk (defense-in-depth against a network-boundary assumption failing, not defense against a sophisticated internal adversary).

## Consequences

- A network misconfiguration that accidentally exposes an internal endpoint no longer means an unauthenticated write path — the request still fails without the shared token.
- One more secret to provision per environment (alongside the JWT signing key), managed the same way (§ [Authentication.md](../Authentication.md) §5 — required at startup, fails fast if missing outside `Development`).
- This does not replace the tenant/role checks already documented (§ [TenantArchitecture.md](../TenantArchitecture.md)) — it's an additional layer *before* those checks run, not a substitute for them.
- If the system later adds more internal-only endpoints, they follow the same convention automatically via a shared `SeeSight.Shared.Observability`-adjacent middleware (or a small dedicated extension) rather than each service reimplementing the header check.
