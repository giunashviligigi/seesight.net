# API Contracts

Every endpoint below is reached through the **API Gateway** — no backend service is directly reachable from the frontend or the internet (see [Deployment.md](Deployment.md) §3). Paths are preserved close to the original system's contract wherever no architectural reason required a change, so the redesigned Next.js frontend's adaptation work is a matter of base-URL and response-shape verification, not a route rewrite.

Roles follow [Authorization.md](Authorization.md); "all" means `SUPER_ADMIN`, `COMPANY_ADMIN`, and `EMPLOYEE` may all call it (subject to the tenant/self-scoping rules noted per endpoint). List endpoints return the standard envelope: `{ items, total, page, pageSize }`.

## Identity Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/auth/register` | Public | Creates a `COMPANY_ADMIN` with `companyId = null`. |
| POST | `/auth/login` | Public | Returns access+refresh token pair; Gateway sets the httpOnly cookie. |
| POST | `/auth/refresh` | Public (requires valid refresh token) | **New** — rotates the refresh token, issues a new access token. |
| POST | `/auth/logout` | Authenticated | **Now revokes the refresh token server-side** (was cookie-clear-only). |
| POST | `/auth/forgot-password` | Public | Always returns a generic success response. |
| POST | `/auth/reset-password` | Public | Consumes the reset token. |
| POST | `/auth/change-password` | Authenticated | Clears `mustChangePassword`. |
| GET | `/auth/me` | Authenticated | Current user profile. |
| GET | `/users` | `SUPER_ADMIN` | Lists `COMPANY_ADMIN` users with `companyId = null` by default (`?unassignedOnly=false` widens it) — feeds the "assign admin to company" workflow. |
| GET | `/.well-known/jwks.json` | Public (internal — not used by the frontend) | Consumed by the Gateway only. |

## Tenant Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/companies` | `SUPER_ADMIN`, `COMPANY_ADMIN` | A `COMPANY_ADMIN` may self-create only while `companyId == null`. |
| GET | `/companies` | `SUPER_ADMIN` | Paginated, searchable. |
| GET | `/companies/me` | all | Current user's company. |
| GET | `/companies/{id}` | all | Tenant-scoped. |
| PATCH | `/companies/{id}` | `SUPER_ADMIN`, `COMPANY_ADMIN` | |
| POST | `/companies/{id}/deactivate` | `SUPER_ADMIN` | `status = INACTIVE`. |
| POST | `/companies/{id}/activate` | `SUPER_ADMIN` | Clears `deletedAt`. |
| DELETE | `/companies/{id}` | `SUPER_ADMIN` | Soft delete. |
| POST | `/companies/{id}/assign-admin` | `SUPER_ADMIN` | `replaceExisting` flag unassigns prior admins. |
| POST | `/departments` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Unique name per company. |
| GET | `/departments` | all | Tenant-scoped list. |
| PATCH | `/departments/{id}` | `SUPER_ADMIN`, `COMPANY_ADMIN` | |
| DELETE | `/departments/{id}` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Members' `departmentId` cleared, not cascade-deleted. |
| POST | `/employees` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `createLogin: true` provisions a linked Identity Service `User` via REST + returns a one-time temp password, synchronously, in this response. See [TenantArchitecture.md](TenantArchitecture.md) §6 for the compensating-action consistency handling if the local save fails after the remote `User` was already created. |
| GET | `/employees` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Search/filter/sort/paginate. |
| GET | `/employees/me` | all | Current user's own employee profile. |
| GET | `/employees/{id}` | all | `EMPLOYEE` restricted to own record. |
| PATCH | `/employees/{id}` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Syncs linked `User.firstName/lastName` via REST. |
| POST | `/employees/{id}/deactivate` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Deactivates linked `User` too (REST call), keeps trip history. |
| POST | `/employees/{id}/activate` | `SUPER_ADMIN`, `COMPANY_ADMIN` | |
| DELETE | `/employees/{id}` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Tombstones email, unlinks `userId`. |
| POST | `/internal/employees/validate` | *(internal only — not Gateway-routed)* | Called by Trip Service to validate traveler ids belong to a company and are active. |

## Trip Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/trips` | all | `EMPLOYEE` must include themselves as a traveler. |
| GET | `/trips` | all | Filters: status, date range, department. `EMPLOYEE` scoped to self as creator/traveler. |
| GET | `/trips/{id}` | all | Tenant + traveler/creator scoped for `EMPLOYEE`. |
| PATCH | `/trips/{id}` | all | Only while `DRAFT`/`REJECTED`. |
| POST | `/trips/{id}/submit` | all | `DRAFT`/`REJECTED` → `PENDING_APPROVAL`. |
| POST | `/trips/{id}/cancel` | all | → `CANCELLED`. |
| DELETE | `/trips/{id}` | all | Soft delete, any status. |
| POST | `/trips/{id}/approve` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `PENDING_APPROVAL` → `APPROVED`; immediately promotes to `IN_PROGRESS` if `startDate <= today`. |
| POST | `/trips/{id}/reject` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `PENDING_APPROVAL` → `REJECTED`. |
| POST | `/trips/{id}/complete` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `IN_PROGRESS` → `COMPLETED`. |
| POST | `/trips/{id}/reopen` | all | `REJECTED` → `DRAFT`. |
| POST | `/trips/{id}/offers/flight` | all | Only while `DRAFT`/`REJECTED`; deselects prior selection. |
| POST | `/trips/{id}/offers/hotel` | all | Same. |
| GET | `/trips/{id}/invoice` | all | Only once status ∈ `{APPROVED, IN_PROGRESS, COMPLETED}`; generates the persisted `Invoice` on first call, re-renders the PDF from the stored snapshot on subsequent calls. |
| GET | `/trips/{id}/approval-history` | all | Full `ApprovalAction` audit trail. |
| GET | `/trips/pending-approvals` | `SUPER_ADMIN`, `COMPANY_ADMIN` | Company's `PENDING` approval queue. |

**Removed vs. the original system**: the deprecated manual `POST /trips/{id}/start` (superseded entirely by auto-promotion — kept as dead code in the original, dropped here per "never leave placeholder/deprecated implementations"), and the duplicate `/approvals/:tripId/approve|reject` routes (Approval is not a separate service — see [Microservices.md](Microservices.md) §1 — so there is exactly one approve/reject route now, not two that do the same thing).

## Search Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/travel/flights` | all | SerpAPI Google Flights, normalized, Redis-cached (~60s TTL), rate-limited (30/min/user). |
| GET | `/travel/hotels` | all | SerpAPI Google Hotels, normalized, same cache/rate-limit policy. |

## AI Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| POST | `/ai/parse-travel-intent` | all | Structured NL parsing; supports `clarificationAnswer`/`clarificationFocus`/`draft` for continuation rounds (never re-invokes Groq — see [AIArchitecture.md](AIArchitecture.md) §4). |
| POST | `/ai/recommend-itinerary` | all | Ranks a shortlist **supplied inline in the request body** by the caller (the frontend already has this data from the trip it's displaying); Groq with rule-based fallback. AI Service never fetches it itself — see [ADR 0002](adr/0002-ai-service-no-callback-to-trip-service.md). Rate-limited (10/min/user). |
| GET | `/ai/trips/{tripId}/recommendations` | all | Persisted `AiRecommendation` history for a trip. |

## Notification Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/notifications` | all | `?unreadOnly&page&pageSize`; always self-scoped, no admin override. |
| POST | `/notifications/read-all` | all | Marks all of the caller's unread notifications read. |
| DELETE | `/notifications/clear-all` | all | Hard delete, caller's notifications only. |
| PATCH | `/notifications/{id}/read` | all | Ownership-checked; idempotent. |

## Reporting Service

| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/dashboard/summary` | all | Role-aware: `EMPLOYEE` scoped to own trips, admins see company-wide. **Moved here from Trip Service** — reads event-driven projections (upcoming trips, pending-approval count, active-employee count, period spend), so it never needs a synchronous call to Trip or Tenant Service — see [ADR 0001](adr/0001-no-gateway-aggregation-dashboard-in-reporting.md). |
| GET | `/reports/summary` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `?from&to`, max 24-month range. Reads directly from projection tables (no cache-TTL staleness window — see [DatabaseDesign.md](DatabaseDesign.md) §8). |
| GET | `/reports/export` | `SUPER_ADMIN`, `COMPANY_ADMIN` | `?format=csv\|json&dataset=summary\|monthly\|departments\|destinations`. CSV escaping ported verbatim from the original system. |

## Health (every service)

| Method | Path | Roles | Notes |
|---|---|---|---|
| GET | `/health/live` | Public | Process liveness. |
| GET | `/health/ready` | Public | DB + RabbitMQ (where applicable) reachability. |

The Gateway exposes an aggregated `GET /health` (fans out to each service's `/health/ready`) purely as a convenience — it performs no business logic, only aggregation, consistent with [Microservices.md](Microservices.md) §1's Gateway boundary.

## Response Envelope & Error Conventions

- List endpoints: `{ items: T[], total: number, page: number, pageSize: number }` — matches the original system's envelope exactly; no new endpoint introduces a different shape.
- Errors: `400` invalid input/invalid state transition, `401` unauthenticated, `403` wrong tenant/role/ownership, `404` not found, `409` duplicate/conflict, `429` rate limit exceeded — same status-code conventions as the original system.
- Every mutating/listing endpoint requires the caller's identity (forwarded by the Gateway per [Authorization.md](Authorization.md) §2) and enforces role + tenant scope server-side; there is no endpoint that trusts a client-supplied `companyId` without validating it against the caller's own tenant context.
