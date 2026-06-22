# SalesCom

A sales-commission microservice. One node in a polyglot fleet — peer services exist in other
languages and frameworks — so wire-level conventions (auth tokens, response envelopes, the
`X-Correlation-ID` header) are intentionally portable.

> The full engineering reference lives in [`CLAUDE.md`](CLAUDE.md). This README is the orientation
> and getting-started guide.

## Architecture at a glance

Clean Architecture, four projects, strict dependency direction (top to bottom):

```
SalesCom.Api             ASP.NET Core 10 controllers, ApiResponse envelope, Swagger (Dev)
        │
        ▼
SalesCom.Infrastructure  EF Core 10 — Postgres (Npgsql) + Oracle POS, JWT/JWE auth,
                         Serilog → rolling files, repositories
        │
        ▼
SalesCom.Application      CQRS (hand-rolled, no MediatR), dispatchers, decorators, handlers
        │
        ▼
SalesCom.Domain          Entities (plain classes), enums, Result/ErrorBase
```

### Key design choices

- **Centralized package management** via `Directory.Packages.props` — versions pinned in one place.
- **CQRS with a manual decorator pattern** — no third-party mediator. Decorator chain, outermost →
  innermost: `Logging → Validation → Handler`. See [`Application/DependencyInjection.cs`](src/SalesCom.Application/DependencyInjection.cs).
- **Simple entities** — plain classes (the generic `EntityBase<TId>` base, closed as
  `EntityBase<Guid>`, for the Guid-keyed ones); no factories, no behavior methods, no domain
  events. `record` is used for commands, queries, DTOs and responses.
- **FluentValidation** — used in two flavours: entity-level (handler validates the built entity)
  and command-level (the `ValidationCommandHandlerDecorator` validates the incoming command before
  the handler runs).
- **`jsonb` columns** in Postgres for evolving payloads — used by `audit_logs` (`old_values` /
  `new_values`) to capture full before/after snapshots without a per-entity schema change.
- **Strongly-typed options only** — never raw `IConfiguration`. Bound with
  `ValidateDataAnnotations().ValidateOnStart()`, so a missing secret fails at boot, not at first request.
- **`Result<T>` + `ErrorBase`** — handlers return `Result`; exceptions are reserved for
  infrastructure faults. Business-rule failures travel through the explicit `Result` channel.
- **JWT + JWE auth** — HS256-signed JWT, JWE-wrapped (`A128KW` + `A128CBC_HS256`). Permissions are
  **not** in the token; they are queried from the database on every request. Controllers declare
  `[HasPermission(Permissions.X.Y)]`; a dynamic policy provider materializes the policy on first use.
- **Pluggable identity** — `Pos:Enabled` toggles between the local Postgres user store (dev) and the
  Oracle POS database accessed via stored procedures (production).
- **File-based logging with correlation IDs** — Serilog writes rolling daily files under `logs/`
  (console too, in Development). `CorrelationIdMiddleware` stamps every request with an
  `X-Correlation-ID`, carried on every log line, so one request is trivial to trace. No log shipper.
- **Change-audit trail** — an EF Core `SaveChangesInterceptor` records who/when/before/after for
  every entity insert, update and delete into `audit_logs`, in the same transaction as the change.

## Layout

Every project organizes files **by technical type** (Entities, Interfaces, Handlers, …) and uses
a **single flat namespace** per project, so a file can move between folders without breaking a
reference. Cross-project references use an explicit `using` directive in each file.

```
SalesCom/
├── Directory.Build.props          shared MSBuild props (TFM, nullable, NoWarn)
├── Directory.Packages.props       central package versions
├── SalesCom.slnx                  .NET 10 solution (XML format)
│
├── src/
│   ├── SalesCom.Domain/           namespace: SalesCom.Domain
│   │   ├── Entities/              EntityBase<TId> + Dummy, SalesComDataSource(+Column), User+RBAC, AuditLog, …
│   │   ├── Enums/                 UserStatus, ApprovalStatus, AuditAction
│   │   ├── Errors/                Result, ErrorBase + outcome-error catalogs (SalesComDataSourceErrors, UserErrors)
│   │   └── Interfaces/            repository interfaces, IUnitOfWork, IClock, IStatus
│   │
│   ├── SalesCom.Application/       namespace: SalesCom.Application
│   │   ├── Commands/ Queries/     CQRS request records
│   │   ├── Handlers/ Validators/  handlers + FluentValidation validators
│   │   ├── Responses/             read models, PagedResult
│   │   ├── Messaging/ Behaviors/  dispatchers, handler contracts, decorators
│   │   ├── Interfaces/            cross-cutting contracts
│   │   ├── Authorization/         Permissions catalog (int IDs)
│   │   └── DependencyInjection.cs
│   │
│   ├── SalesCom.Infrastructure/    namespace: SalesCom.Infrastructure
│   │   ├── Data/                  DbContexts + EF plumbing + audit interceptor; Data/Seed/ = initializer + seeder
│   │   ├── Gateways/              POS stored-procedure access
│   │   ├── Repositories/          repository / permission-query implementations
│   │   ├── EntityConfigurations/  EF entity configurations
│   │   ├── Migrations/            EF migrations + model snapshot
│   │   ├── Configurations/        strongly-typed config records
│   │   ├── Services/              auth services, hasher, permission handler, SystemClock
│   │   ├── Registrations/         per-area DI extension methods
│   │   └── DependencyInjection.cs
│   │
│   └── SalesCom.Api/              namespace: SalesCom.Api
│       ├── Controllers/           Account, SalesComDataSources, Dummies
│       ├── Contracts/ Requests/   ApiResponse envelope, request DTOs
│       ├── Extensions/            registration extensions + ResultExtensions
│       ├── Handlers/              GlobalExceptionHandler
│       ├── Program.cs             composition root
│       └── appsettings*.json
│
├── tests/
│   ├── SalesCom.Domain.UnitTests/        xUnit + FluentAssertions
│   ├── SalesCom.Application.UnitTests/   xUnit + NSubstitute + FluentAssertions
│   └── SalesCom.Api.IntegrationTests/    WebApplicationFactory + Testcontainers Postgres
│
└── docker/
    └── docker-compose.yml         postgres (local development database)
```

## Getting started

Prerequisites: .NET 10 SDK. Docker is needed only for a local database and integration tests.

```pwsh
# 1. (optional) Local PostgreSQL — logs are written to the API's logs/ folder
docker compose -f docker/docker-compose.yml up -d

# 2. Build
dotnet build SalesCom.slnx

# 3. Unit tests (no Docker required)
dotnet test tests/SalesCom.Domain.UnitTests
dotnet test tests/SalesCom.Application.UnitTests

# 4. Integration tests (Docker required — Testcontainers spins its own Postgres)
dotnet test tests/SalesCom.Api.IntegrationTests

# 5. Run the API
dotnet run --project src/SalesCom.Api
```

In Development the API serves Swagger UI at `http://localhost:<port>/swagger`.

`DatabaseInitializer` applies pending EF migrations on startup and, when `Seed:Enabled` is `true`
and `Seed:AdminPassword` is at least 8 characters, seeds the admin user / role / permission rows.

## API surface

Every response uses the unified [`ApiResponse`](src/SalesCom.Api/Contracts/ApiResponse.cs) envelope —
`{ success, message, data?, errorCode? }`. HTTP status codes still follow REST semantics.

- `POST /api/account/login` · `GET /api/account/me`
- `GET /api/sales-com-data-sources/available` — Postgres tables ending in `_COM` via `information_schema`
- `GET|POST /api/sales-com-data-sources` · `GET /api/sales-com-data-sources/{id}`
- `PATCH /api/sales-com-data-sources/{id}/columns/{columnId}` · `PATCH /api/sales-com-data-sources/{id}/columns` (bulk)
- `GET|POST /api/dummies` · `GET /api/dummies/{id}` — the non-DDD CQRS demo
- `GET /health/live` · `GET /health/ready`

## Authentication

The service issues its own JWT on login (HS256-signed, JWE-wrapped) and validates it on every
request. Configure under the `Jwt` section in `appsettings.json` (signing key, encryption key,
issuer, audience, token lifetime). Identity source is toggled by `Pos:Enabled`:

| `Pos:Enabled` | User store | Permission source |
|---|---|---|
| `false` (dev) | Postgres `users` table | EF join on `user_roles → role_permissions` |
| `true` (prod) | Oracle POS stored procedures | Oracle `GET_ROLERIGHT` procedure |

Permissions are checked against the database per request — edits take effect immediately, no token
refresh needed. See [`CLAUDE.md` §5](CLAUDE.md) for the full login flow.

## Adding a new feature

Files go into each project's **type folders** — there are no per-feature folders. Two patterns
sit in the codebase as references:

- **DDD-style** (see `SalesComDataSource`) — entity in `Domain/Entities/`, repository interface in
  `Domain/Interfaces/`, errors in `Domain/Errors/`, entity validator in `Application/Validators/`,
  repository impl + EF config in Infrastructure, CQRS slice in Application, controller in Api.
- **Non-DDD CQRS** (see `Dummy`) — entity in `Domain/Entities/`, no repository, no errors class;
  the handler uses `IUnitOfWork.AddAsync` for writes and a thin reader interface for queries; the
  validator sits on the command (`AbstractValidator<TCommand>`) instead of the entity.

## Known gaps

- **Async eventing** — there is no domain-event/outbox mechanism. Add an outbox table if messaging is ever needed.
- **Integration-test auth** — a seeding helper that provisions a test user + role + permissions is
  needed before the permission-gated tests can verify authorization.
- **Refresh tokens** — not implemented; a single 2-day access token only.
- **CI pipeline** — `dotnet build` / `dotnet test` / container build & push.
