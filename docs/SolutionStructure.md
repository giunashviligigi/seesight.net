# Solution Structure

The complete .NET solution layout, exactly as it should exist before any implementation begins. This supersedes the abbreviated trees in [Architecture.md](Architecture.md) §5 and [CodingStandards.md](CodingStandards.md) §1 — those stay as short pointers here.

## 1. Full Directory Tree

```
seesight.net/
├── SeeSight.sln
├── .editorconfig
├── Directory.Build.props                 # shared MSBuild settings (nullable enable, LangVersion, analyzers)
├── Directory.Packages.props               # central package version management (one version per NuGet package, no per-project drift)
│
├── src/
│   ├── Gateway/
│   │   └── SeeSight.Gateway/               # YARP host — routing, JWT validation, rate limiting, CORS, correlation IDs
│   │       ├── Program.cs
│   │       ├── appsettings.json
│   │       ├── appsettings.Development.json
│   │       └── yarp.config.json            # route/cluster definitions
│   │
│   ├── Services/
│   │   ├── Identity/
│   │   │   ├── SeeSight.Identity.Domain/           # User, RefreshToken, PasswordResetToken entities + behavior
│   │   │   ├── SeeSight.Identity.Application/      # MediatR commands/queries, FluentValidation, IJwtIssuer/IPasswordHasher interfaces
│   │   │   ├── SeeSight.Identity.Infrastructure/    # EF Core (IdentityDbContext), BCrypt, RS256 signing, JWKS
│   │   │   └── SeeSight.Identity.Api/               # Controllers, Program.cs, appsettings.*
│   │   │
│   │   ├── Tenant/
│   │   │   ├── SeeSight.Tenant.Domain/              # Company, Department, Employee entities + behavior
│   │   │   ├── SeeSight.Tenant.Application/         # Commands/queries, validators, IIdentityServiceClient interface
│   │   │   ├── SeeSight.Tenant.Infrastructure/       # EF Core (TenantDbContext), Identity Service HttpClient, outbox
│   │   │   └── SeeSight.Tenant.Api/                  # Controllers + internal validation endpoint, Program.cs
│   │   │
│   │   ├── Trip/
│   │   │   ├── SeeSight.Trip.Domain/                 # Trip, TripTraveler, Approval, ApprovalAction, offers, Invoice — the state machine lives here
│   │   │   ├── SeeSight.Trip.Application/            # Commands/queries, FluentValidation, ITenantServiceClient interface
│   │   │   ├── SeeSight.Trip.Infrastructure/          # EF Core (TripDbContext), Tenant Service HttpClient, MassTransit outbox, QuestPDF renderer, promotion scheduler
│   │   │   └── SeeSight.Trip.Api/                     # Controllers, Program.cs
│   │   │
│   │   ├── Search/
│   │   │   └── SeeSight.Search/                       # Vertical Slice — single project: SearchFlights/, SearchHotels/ feature folders, SerpApiClient, Redis cache
│   │   │
│   │   ├── AI/
│   │   │   ├── SeeSight.AI.Application/               # Commands/queries, TravelIntentHeuristicEngine, validation logic, IAIService interface
│   │   │   ├── SeeSight.AI.Infrastructure/             # EF Core (AiDbContext), GroqAIService, RuleBasedFallback, AirportResolver
│   │   │   └── SeeSight.AI.Api/                        # Controllers, Program.cs
│   │   │
│   │   ├── Notification/
│   │   │   └── SeeSight.Notification/                  # Vertical Slice — single project: CreateNotification/, ListNotifications/, event consumers
│   │   │
│   │   └── Reporting/
│   │       ├── SeeSight.Reporting.Application/         # Query handlers only (no commands) — GetDashboardSummary, GetReportSummary, ExportReport
│   │       ├── SeeSight.Reporting.Infrastructure/       # EF Core (ReportingDbContext), event consumers building projections
│   │       └── SeeSight.Reporting.Api/                  # Controllers, Program.cs
│   │
│   └── Shared/
│       ├── SeeSight.SharedKernel/            # TenantId, Money, ISoftDelete, IHasTenant, ICurrentUserContext (+ its middleware), internal-auth-token middleware (ADR 0006)
│       ├── SeeSight.Shared.Contracts/         # Versioned MassTransit integration-event DTOs (TripApprovedIntegrationEvent, etc.), each carrying Version/UpdatedAtUtc (ADR 0005)
│       ├── SeeSight.Shared.Messaging/         # AddSeeSightMessaging() MassTransit/RabbitMQ conventions, ProcessedEvent idempotency helper (ADR 0003)
│       ├── SeeSight.Shared.Observability/     # AddSeeSightObservability() — OTel, Serilog, health-check extensions, log-scrubbing enricher
│       └── SeeSight.Shared.Common/            # MoneyRounding, CsvEscaper, PagedResult<T>
│
├── tests/
│   ├── Gateway/
│   │   └── SeeSight.Gateway.Tests/            # routing/auth/rate-limit middleware tests (WebApplicationFactory-style)
│   ├── Identity/
│   │   ├── SeeSight.Identity.UnitTests/        # Domain + Application, no infrastructure
│   │   ├── SeeSight.Identity.IntegrationTests/  # Testcontainers Postgres, WebApplicationFactory
│   │   └── SeeSight.Identity.ArchitectureTests/ # NetArchTest layer rules
│   ├── Tenant/            # same 3-project pattern
│   ├── Trip/               # same 3-project pattern
│   ├── AI/                  # same 3-project pattern (no Domain project to unit test separately — Application tests cover validation/heuristic logic)
│   ├── Search/
│   │   └── SeeSight.Search.Tests/             # single test project (Vertical Slice — unit + integration together, no separate architecture-test project needed for a 1-project service)
│   ├── Notification/
│   │   └── SeeSight.Notification.Tests/        # same rationale as Search
│   ├── Reporting/          # same 3-project pattern (no Domain project)
│   └── Shared/
│       └── SeeSight.Shared.Common.UnitTests/    # MoneyRounding, CsvEscaper — pure logic worth testing directly
│
├── docker/
│   ├── docker-compose.yml                  # full local stack: all services + infra
│   ├── docker-compose.infra.yml            # infra-only: Postgres, RabbitMQ, Redis, OTel Collector, Jaeger, Prometheus, Grafana
│   ├── init-db.sql                          # creates the 6 per-service databases on the one local Postgres server
│   ├── otel-collector-config.yaml
│   ├── prometheus.yml
│   └── Dockerfile                           # one shared multi-stage Dockerfile, parameterized by build arg for which service's .csproj to publish
│
├── .github/
│   └── workflows/
│       └── ci.yml                           # path-filtered build/test/image-push, per Deployment.md §5
│
├── frontend/                                # Next.js app — see Frontend.md (kept as a top-level sibling, not under src/, since it's a separate deployable with its own package.json/tooling)
│
└── docs/
    ├── *.md                                  # this documentation set
    └── adr/
        └── NNNN-*.md
```

## 2. Why Each Project Exists

| Project | Why it exists | What it's allowed to reference |
|---|---|---|
| `SeeSight.Gateway` | The only public entry point; owns routing/auth/rate-limit/CORS — no business logic, so it needs no Domain/Application layer of its own. | `SeeSight.SharedKernel`, `SeeSight.Shared.Observability` only. **Never** references any service project — routing targets are configured URLs, not compile-time references. |
| `<Service>.Domain` | Entities with real behavior (state machines, invariants) — the one place business rules live, deliberately isolated from EF Core/HTTP/MassTransit so it's testable with zero infrastructure. | `SeeSight.SharedKernel` only. |
| `<Service>.Application` | MediatR commands/queries, FluentValidation, and the *interfaces* Infrastructure implements (e.g. `ITenantServiceClient`) — this is where Dependency Inversion happens. | `<Service>.Domain` (if it exists), `SeeSight.SharedKernel`, `SeeSight.Shared.Contracts`, `SeeSight.Shared.Common`. |
| `<Service>.Infrastructure` | EF Core `DbContext`, typed `HttpClient`s to other services, MassTransit outbox/consumers, third-party SDK wrappers (Groq, SerpAPI, QuestPDF). | `<Service>.Application`, `<Service>.Domain`, all `SeeSight.Shared.*`, plus NuGet packages (EF Core, Npgsql, MassTransit, etc.). **Never** another service's project. |
| `<Service>.Api` | Thin controllers + `Program.cs` composition root. | `<Service>.Application` (dispatch commands/queries), `<Service>.Infrastructure` (composition root only — wiring `AddInfrastructure()` in `Program.cs`, not runtime business calls), `SeeSight.Shared.Observability`. |
| `SeeSight.Search`, `SeeSight.Notification` (single-project services) | Vertical Slice — few enough endpoints that a 4-project split would be pure ceremony (§ [Microservices.md](Microservices.md) §8). | `SeeSight.SharedKernel`, `SeeSight.Shared.Contracts`, `SeeSight.Shared.Messaging` (Notification only — it's the consumer), `SeeSight.Shared.Observability`, `SeeSight.Shared.Common`. |
| `SeeSight.SharedKernel` | The one shared library every project may reference — value objects and interfaces with zero business decisions baked in (§ [CodingStandards.md](CodingStandards.md) §2). | Nothing but the .NET BCL. A pure leaf. |
| `SeeSight.Shared.Contracts` | Versioned event DTOs, so producers and consumers agree on shape without referencing each other's code. | `SeeSight.SharedKernel` only. |
| `SeeSight.Shared.Messaging` | MassTransit configuration conventions + outbox helper (§ [ADR 0003](adr/0003-adopt-masstransit-for-messaging.md)). | `SeeSight.Shared.Contracts`, `SeeSight.SharedKernel`, MassTransit packages. |
| `SeeSight.Shared.Observability` | One-line OTel/Serilog/health-check setup reused by every service. | `SeeSight.SharedKernel`, OpenTelemetry/Serilog packages. |
| `SeeSight.Shared.Common` | Money rounding, CSV escaping, pagination envelope. | Nothing — a pure leaf, like `SharedKernel`. |
| `<Service>.UnitTests` | Domain + Application logic, zero infrastructure, fast. | The service's `Domain`/`Application` projects, xUnit, FluentAssertions. |
| `<Service>.IntegrationTests` | Real EF Core against a Testcontainers Postgres, `WebApplicationFactory`-driven HTTP tests. | The service's `Api` project (transitively pulls in everything), Testcontainers. |
| `<Service>.ArchitectureTests` | Enforces the reference rules in this document at build time via `NetArchTest` — the automated backstop for "no circular references, no forbidden references." | All of that service's own projects (needs their assemblies to inspect), NetArchTest. |

## 3. Configuration Conventions

- Each `*.Api` project has its own `appsettings.json` (safe defaults + placeholders) and `appsettings.Development.json` (local dev values, gitignored where it contains anything sensitive) — no shared, cross-service config file, since each service's configuration surface (connection string, JWT validation params, feature-specific keys) is genuinely different.
- Required secrets (JWT signing key, `SERPAPI_API_KEY`, `GROQ_API_KEY`, the internal-service token from [ADR 0006](adr/0006-internal-service-to-service-authentication.md)) are `IOptions<T>`-bound with `ValidateOnStart()` — never hardcoded, never defaulted in non-Development environments (§ [Authentication.md](Authentication.md) §5).
- `Directory.Build.props` at the repo root enforces `<Nullable>enable</Nullable>` and a common `LangVersion`/analyzer set across every project — a missing-null-check class of bug is caught at compile time uniformly, not per-project-opt-in.
- `Directory.Packages.props` centralizes NuGet package versions — no two projects can silently drift onto different versions of, say, MassTransit or EF Core.

## 4. Why `frontend/` Sits Outside `src/`

The frontend is a genuinely separate deployable with its own toolchain (`npm`, Next.js build, its own `package.json`) — nesting it under `src/` (a .NET-solution-centric folder) would imply it's part of the `.sln`, which it isn't. Keeping it as a top-level sibling matches how the original monorepo separated `client/` and `server/`.
