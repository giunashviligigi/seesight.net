# 0008. Gateway rate limiting: StackExchange.Redis with a custom middleware, not a RateLimiter-abstraction package

Status: Accepted
Date: 2026-08-05

## Context

[Authentication.md](../Authentication.md) §6 and [ADR 0007](0007-redis-dependent-features-fail-open.md) already establish *that* `/auth/login` and `/auth/register` are Redis-backed, distributed, fail-open rate limited. Neither document picks the specific package — [CodingStandards.md](../CodingStandards.md) §7 explicitly deferred that choice to implementation time, to be recorded as its own ADR. This is that ADR, not a reopening of the earlier decision.

ASP.NET Core's built-in `Microsoft.AspNetCore.RateLimiting` middleware ships only in-memory partitioned limiters; a distributed store requires either a community package (e.g. `RedisRateLimiting`, which plugs a Redis-backed `PartitionedRateLimiter` into that same middleware) or a small hand-written implementation directly against `StackExchange.Redis`.

## Decision

Implement rate limiting as a small, explicit ASP.NET Core middleware talking directly to `StackExchange.Redis` (`INCR` + `EXPIRE`, a fixed-window counter keyed by client IP and route), rather than adopting a third-party package that integrates with the built-in `RateLimiter` abstraction.

Reasoning: the built-in `RateLimiter`/`RateLimitLease` abstraction exists to let many different limiter *algorithms* share one extensibility point across arbitrarily many endpoints — valuable when a codebase has many differently-configured limiters. This system has exactly one limiter policy (fixed-window, Redis-backed, fail-open) applied to two routes. A ~40-line middleware doing a direct `INCR`/`EXPIRE` check is easier to read, test, and reason about than correctly implementing `RateLimiter`'s lease/disposal contract for a third-party abstraction that buys nothing here — consistent with the project's standing rule to avoid unnecessary abstraction (docs/CodingStandards.md §6).

`StackExchange.Redis` itself (not a wrapper) is the dependency — the de facto standard .NET Redis client, already implied by every other Redis-touching decision in this project (Search Service's cache, per [Microservices.md](../Microservices.md) §1), not a new kind of dependency being introduced.

The `IConnectionMultiplexer` is configured with `AbortOnConnectFail = false` so a Redis outage at Gateway *startup* doesn't crash the process — consistent with [ADR 0007](0007-redis-dependent-features-fail-open.md)'s fail-open policy applying to connection failures generally, not just to requests made after a successful connection.

## Consequences

- Rate limiting logic is fully owned, readable, and testable in this codebase — no dependency on a third-party package's maintenance status or its own abstraction quirks.
- If a second, differently-shaped limiter policy is ever needed (e.g. a sliding window, or a per-user rather than per-IP partition), revisit whether the custom middleware still scales to that need or whether adopting the `RateLimiter` abstraction (or a package built on it) becomes worth its ceremony at that point. Not a concern for the two fixed-window, per-IP limiters this system has today.
- The fixed-window (not sliding-window) algorithm has the well-known boundary edge case (a burst just before and just after a window boundary can total ~2x the nominal limit in the worst case) — accepted as adequate for this system's actual risk profile (brute-force *deterrence*, not a hard security boundary — bcrypt's own cost factor is the real throttle on password-guessing throughput, per ADR 0007's reasoning).
