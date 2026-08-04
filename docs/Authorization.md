# Authorization

Authentication (§ [Authentication.md](Authentication.md)) answers "who is this." Authorization answers "what can they do" — this document covers role-based access control, claim propagation from the Gateway, and the ownership/self-scoping rules layered on top of roles inside each service.

## 1. Roles

Unchanged from the original system — three roles, no more:

| Role | Scope |
|---|---|
| `SUPER_ADMIN` | Platform-wide. Manages tenant companies. Bypasses tenant isolation (see [TenantArchitecture.md](TenantArchitecture.md)). |
| `COMPANY_ADMIN` | Single tenant. Manages employees/departments, approves/rejects trips, views reports. |
| `EMPLOYEE` | Single tenant, self-scoped. Plans own trips, searches travel, views own data and trips they're traveling on. |

## 2. Claim Propagation (Gateway → Services)

The Gateway validates the JWT exactly once per request (§ [Authentication.md](Authentication.md) §8) and forwards the validated claims to the target service as trusted internal headers rather than making every downstream service re-parse and re-validate the token:

```
X-User-Id: <sub>
X-User-Role: <role>
X-User-Company-Id: <companyId | empty>
```

Services trust these headers **only** because they arrive over the private internal network from the Gateway — a service is never directly reachable from the public internet, so there's no path for a client to forge these headers without going through Gateway validation first. Each service's `ICurrentUserContext` implementation (interface shared via `SeeSight.SharedKernel`, populated by a small per-service middleware — see [CodingStandards.md](CodingStandards.md) §Shared Libraries) reads these headers into a scoped request object used by controllers, MediatR handlers, and EF Core's tenant query filter alike.

## 3. Role-Based Authorization

Standard ASP.NET Core `[Authorize(Roles = "...")]` (or policy-based equivalents where a check is reused, e.g. `[Authorize(Policy = "CanManageCompany")]` for the `SUPER_ADMIN`/`COMPANY_ADMIN` pair) on every controller action, mirroring the original system's `@Roles()` decorator — every endpoint that isn't explicitly public declares its allowed roles; there is no "authenticated but roleless" endpoint.

`AllowAnonymous`-equivalent endpoints (the original system's `@Public()`): `POST /auth/{register,login,forgot-password,reset-password}`, `GET /health/*`.

## 4. Tenant + Ownership Checks (Beyond Role)

A role check alone doesn't establish *which* company's data a request may touch, or whether an `EMPLOYEE` may see a specific record. Two layers, same split as the original system's `tenant.utils.ts` plus its per-service ownership checks:

- **Tenant scoping** — "does this `companyId` belong to the caller" — is common enough to be a shared *contract* (`ITenantContext` interface, `TenantId` value object in `SeeSight.SharedKernel`) implemented by each service via an EF Core global query filter, per [TenantArchitecture.md](TenantArchitecture.md). This replaces the original system's manually-called `assertCompanyAccess`/`resolveTenantCompanyId` with something structurally harder to forget, while keeping the exact same rules (super-admin bypass, "no company assigned → forbidden", "cross-tenant → 403").
- **Self-scoping for `EMPLOYEE`** — "can this specific employee see/mutate this specific record" — is genuinely business-specific per aggregate and stays inside the owning service, never generalized into shared code:
  - Tenant Service: an `EMPLOYEE` may only view their own `Employee` record (`Employee.UserId == currentUser.Id`).
  - Trip Service: an `EMPLOYEE` may only view/mutate a trip they created or are a listed traveler on (`Trip.CreatedByUserId == currentUser.Id OR EXISTS TripTraveler WHERE EmployeeId IN (employee ids linked to currentUser.Id)`), and trip-transition mutations further require `COMPANY_ADMIN`/`SUPER_ADMIN` for admin-only actions (approve/reject/complete).
  - Notification Service: a user may only read/mark-read/clear their own notifications (`Notification.UserId == currentUser.Id`) — no admin override exists here, matching the original system exactly.

## 5. Authorization Decision Table (representative endpoints)

| Endpoint | Roles | Additional check |
|---|---|---|
| `POST /companies` | `SUPER_ADMIN`, `COMPANY_ADMIN` | A `COMPANY_ADMIN` may only self-create when they currently have no company (`companyId == null`). |
| `GET /companies` | `SUPER_ADMIN` | — |
| `GET /companies/{id}` | all | `assertCompanyAccess`-equivalent (tenant filter + super-admin bypass). |
| `POST /employees` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Tenant-scoped create. |
| `GET /employees/{id}` | all | `EMPLOYEE` restricted to own record. |
| `POST /trips` | all | `EMPLOYEE` must include themselves as a traveler. |
| `POST /trips/{id}/approve` \| `/reject` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Tenant-scoped; trip must be `PENDING_APPROVAL` (state-machine check, not an authz check — see [Microservices.md](Microservices.md) §7). |
| `GET /notifications` | all | Always self-scoped, no override. |
| `GET /reports/summary` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `EMPLOYEE` uses `/dashboard/summary` instead (unchanged from original). |
| `GET /ai/trips/{tripId}/recommendations` | all | **Narrower than trip access**: `SUPER_ADMIN` any; `COMPANY_ADMIN` own company only; `EMPLOYEE` only recommendations they personally requested (`RequestedByUserId`), not every trip they're a traveler on — see [ADR 0004](adr/0004-ai-recommendation-history-authorization-scope.md) for why AI Service can't evaluate the fuller creator-or-traveler rule without reintroducing a Trip Service dependency. |

The full endpoint catalog with roles is in [APIContracts.md](APIContracts.md).

## 6. What Changed vs. the Original System

- Tenant/ownership enforcement moves from "a utility function every new query must remember to call" to "an EF Core global query filter every query automatically respects," while keeping the exact same super-admin-bypass and cross-tenant-403 semantics.
- Claim validation happens once, at the Gateway, instead of every service independently decoding and validating a JWT — services trust the network boundary, not a repeated cryptographic check.
- Everything else (role list, self-scoping rules, which endpoints require which roles) is a faithful port — no authorization *rule* changes, only *where* the rule is enforced.
