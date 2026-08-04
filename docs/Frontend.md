# Frontend Architecture

## 1. Scope

The frontend stays **Next.js, React, TypeScript** — no migration to Blazor or another framework. UI and functionality stay the same as the original system; this document covers the redesigned *implementation* that targets the new API Gateway ([APIContracts.md](APIContracts.md)) with modern patterns, since the backend rewrite is otherwise invisible to a user of the app.

## 2. Folder Structure

Feature-based, layered on top of Next.js App Router (routing stays conventional — `app/` owns routes only):

```
src/
  app/**              # routes only — thin pages composing feature components
  features/
    auth/{api,components,hooks,types}
    trips/{api,components,hooks,types}
    employees/{api,components,hooks,types}
    companies/{api,components,hooks,types}
    ai/{api,components,hooks,types}
    reports/{api,components,hooks,types}
  components/
    ui/                # shadcn primitives (kept as-is)
    layout/             # shells/header (kept as-is)
  lib/
    api-client.ts        # one central typed request wrapper (fetch + auth + error typing + correlation id)
    query-client.ts       # TanStack Query client setup
    generated/            # OpenAPI-generated TypeScript types (see §4)
```

This replaces the original system's flatter "one `lib/api/*.ts` per resource + loosely organized `components/`" layout: everything about Trips (API calls, hooks, components, types) lives together instead of being split across a global API-client folder and scattered components. It mirrors the backend's vertical-slice thinking applied to the frontend.

## 3. API Layer

One central `apiClient` (successor to the original `lib/api/client.ts`'s `apiRequest`) that:

- Targets the Gateway's single base URL — no service is ever addressed directly from the frontend.
- Relies on the httpOnly cookie for auth (§5) rather than attaching a bearer token itself.
- Auto-refreshes on a `401` using the new refresh-token flow ([Authentication.md](Authentication.md)) before retrying the original request once, then surfaces the error if the refresh itself fails.
- Stamps a correlation-id header on every request so a frontend error can be traced into backend logs/traces end-to-end ([Observability.md](Observability.md)).
- Keeps the original one-file-per-resource pattern for actual endpoint calls, now colocated per feature (e.g. `features/trips/api/trips.ts`).
- Surfaces a typed `ApiError` with `.status`, so the one rule that matters — "only clear auth/redirect on `401`/`403`, never on every failed request" — can be centralized in one place instead of being reimplemented inconsistently per page (a gap the original codebase had, per its own conventions doc).

## 4. Type Safety Across the Language Boundary

The original system's frontend and backend were both TypeScript, so hand-written API types drifting from the real Prisma-backed DTOs was a smaller risk (the same person/PR often touched both). With a .NET backend, that implicit safety net is gone — a frontend-side interface for a trip DTO is no longer "the same language as the source of truth," just a hand-maintained guess at it.

Two concrete practices close that gap, rather than relying on manual diligence to keep hand-written types in sync with [APIContracts.md](APIContracts.md):

- **Generate TypeScript types from the Gateway's aggregated OpenAPI spec** (via `openapi-typescript` or `orval`), checked into `src/lib/generated/` and regenerated as a build/CI step whenever the spec changes — the frontend's types are derived from the actual .NET DTOs, not hand-transcribed from documentation that can drift.
- **Runtime validation at the API-client boundary** (Zod schemas matching the generated types, parsed on response) — a "parse, don't just cast" step that turns a silent contract-drift bug (a field renamed on the backend, a nullable becoming required) into an immediate, visible parse error during development rather than a confusing downstream `undefined` deep in a component.

## 5. Authentication Handling

Since the backend now supports real refresh tokens ([Authentication.md](Authentication.md)), the frontend moves off "read a bearer token from `localStorage` on every request" — vulnerable to XSS token theft — to relying on the **httpOnly cookie** as the sole browser-side transport: the browser sends it automatically, and JavaScript never touches the token at all.

An `AuthContext`/`useAuth()` hook resolves session state by calling `GET /auth/me` on mount (the cookie is sent automatically) rather than reading anything out of `localStorage`. This is still a client-only check — no SSR data fetching is introduced (§7) — it just no longer depends on a token the client itself has to manage. Bearer-header auth remains supported by the backend for Swagger/non-browser clients; it's simply not how the browser app authenticates anymore.

## 6. State Management

**TanStack Query** for all server state — data fetching, caching, request de-duplication, loading/error states — replacing the original "every page is a client component doing its own `fetch` in `useEffect`" pattern with a standard, well-tested library purpose-built for exactly that. This requires **no global client-state store** (Redux/Zustand): almost everything the app renders *is* server data, and TanStack Query already handles caching/invalidation for it. `AuthContext` (plain React Context) is the one piece of genuinely global client state — current user/session — and stays minimal by design.

## 7. Explicit Trade-off: No SSR Data Fetching (Intentional, Not the Most Idiomatic Next.js Pattern)

Modern Next.js (App Router) best practice as of the current era generally favors **Server Components and server-side data fetching** for initial page loads — less client JS, no client-side fetch waterfall after hydration, better perceived performance. This redesign **does not adopt that** — it deliberately keeps the original system's "client-rendered only, no server-side data fetching" architecture.

This is called out explicitly rather than silently claimed as fully modern, because it isn't the most idiomatic *current* Next.js pattern — it's a **scope-preserving trade-off**: introducing Server Components/SSR data fetching would require forwarding the httpOnly auth cookie through server-side fetches, restructuring pages around Server/Client Component boundaries, and is a meaningfully larger frontend change than "same functionality, modernized implementation." It's flagged here as a legitimate future improvement, explicitly out of scope unless separately requested — not adopted as part of this redesign.

## 8. Reusable Components

Feature-specific components (e.g. the travel search widget, the airport combobox) move into their owning feature folder (`features/trips/components/`, `features/ai/components/`) rather than living in a shared `components/travel/` grab-bag. Only genuinely cross-feature primitives (buttons, inputs, dialogs — the shadcn layer) stay in `components/ui`.

## 9. Error Handling

- A top-level React error boundary for render-time failures.
- TanStack Query's per-query error state for data-fetching failures, surfaced via a consistent toast/inline pattern instead of ad hoc per-page handling.
- The "only log out on `401`/`403`" rule is centralized in the query client's global error handler (§3) — one place, not "already inconsistent across pages" as the original codebase's own conventions doc flagged it to be.

## 10. Loading States

TanStack Query's `isLoading`/`isFetching` states drive consistent skeleton components instead of ad hoc spinners scattered per page.

## 11. What Changed vs. the Original System

| Original | New | Why |
|---|---|---|
| Bearer token read from `localStorage` on every request | httpOnly cookie, JS never touches the token | Removes an XSS token-theft vector. |
| Ad hoc `fetch` in `useEffect` per page | TanStack Query | Standard caching/loading/error handling, less repeated code, without introducing a heavy global store. |
| Hand-written API types, same-language implicit safety net | OpenAPI-generated types + Zod runtime validation at the boundary | The implicit TS-to-TS safety net is gone now that the backend is .NET — this replaces it explicitly. |
| Inconsistent "log out on any failed request" handling across pages | Centralized in the query client's global error handler | One place, one rule, not per-page reimplementation. |
| Flat `lib/api/*.ts` + loosely organized components | Feature-based folders (`features/<domain>/{api,components,hooks,types}`) | Colocation — everything about one feature lives together. |
| Client-rendered only, no SSR data fetching | **Unchanged, intentionally** — see §7 | Scope-preserving; the more idiomatic Next.js Server Components pattern is a larger, separate change, not adopted here. |
