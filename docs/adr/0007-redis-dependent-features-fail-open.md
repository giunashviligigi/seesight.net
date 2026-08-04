# 0007. Redis-dependent features (rate limiting, search cache) fail open, not closed

Status: Accepted
Date: 2026-08-05

## Context

Redis backs three things in this system: distributed rate limiting (auth, AI, Search endpoints — § [Authentication.md](../Authentication.md) §6, [AIArchitecture.md](../AIArchitecture.md) §6), and Search Service's SerpAPI result cache. None of the existing documentation specified what happens to these features if Redis itself becomes unreachable — an unstated failure mode is exactly the kind of gap a production readiness review should catch before, not after, an incident.

## Decision

Every Redis-dependent feature **fails open** on a Redis connectivity error, rather than failing the request:

- **Rate limiting**: if the Redis-backed limiter can't be reached, the request is **allowed through** (not blocked), and the failure is logged and counted as a metric (`rate_limiter_redis_unavailable_total`). Rationale: rate limiting is a defense-in-depth measure against abuse, not the sole control (bcrypt's own cost factor, for instance, already throttles brute-force login attempts independently) — briefly running without it during a Redis outage is a smaller risk than making a Redis outage take down login, AI, and search entirely.
- **Search cache**: if Redis can't be reached, Search Service **bypasses the cache** and calls SerpAPI directly for every request (slower, more SerpAPI quota consumed, but functionally correct) rather than failing the search.

Neither of these two behaviors is treated as a `/health/ready` failure for their respective services — Redis reachability is monitored (a metric/log signal, per [Observability.md](../Observability.md) §5) but does not gate readiness, since the service remains fully functional (just temporarily unprotected/uncached) without it.

## Consequences

- A Redis outage degrades security/performance posture briefly rather than causing a full outage of auth, AI, or search — judged the correct trade-off for this system's actual risk profile.
- This must be implemented deliberately (a try/catch around the Redis call with an explicit fallback path), not left to whatever an underlying library does by default — most Redis client libraries throw on connection failure, which without this decision would otherwise propagate into a failed request.
- Monitoring (§ [Observability.md](../Observability.md)) needs a specific alert on sustained Redis unavailability, since the system will otherwise keep running "normally" from a user's perspective while quietly unprotected — silence here is a monitoring gap waiting to happen if not deliberately dashboarded.
