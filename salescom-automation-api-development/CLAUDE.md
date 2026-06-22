# SalesCom — Claude Project Reference

A sales-commission microservice. One node in a polyglot fleet (peer services exist in other languages/frameworks), so wire-level conventions — auth tokens, trace propagation, response envelopes — are intentionally portable.

This document is the source of truth for **how the codebase is shaped, what's already built, and the guardrails to preserve when extending it**. Read this before making changes.

---

## 1. Stack at a glance

| Layer | Tech |
|---|---|
| Runtime | .NET 10 |
| Web | ASP.NET Core 10 (controllers, not minimal APIs) |
| Application DB | PostgreSQL via `Npgsql.EntityFrameworkCore.PostgreSQL` |
| ORM | EF Core 10 only — **no Dapper**, no other ORMs |
| Identity / 2FA | External **Central Login** HTTP API (credentials + OTP). We provision a local user + RBAC and issue our **own** access (30-min) + refresh (2-day) tokens; central tokens are stored server-side, never returned |
| Validation | FluentValidation |
| Auth tokens | HS256-signed JWT, JWE-wrapped via `jose-jwt` (A128KW + A128CBC_HS256) |
| Logging | Serilog → rolling daily files (`logs/`) + Grafana Loki (config-driven), plus console in Development. Per-request correlation IDs. |
| API docs | Swashbuckle.AspNetCore (Swagger UI, Development only) |
| Tests | xUnit + NSubstitute + FluentAssertions + Testcontainers (Postgres) |
| Package management | Centralized via `Directory.Packages.props` |

---

## 2. Quick start

```pwsh
# Build everything
dotnet build SalesCom.slnx

# Run unit tests (no Docker required)
dotnet test tests\SalesCom.Domain.UnitTests
dotnet test tests\SalesCom.Application.UnitTests

# EF migrations
dotnet ef migrations add <Name> `
  --project src\SalesCom.Infrastructure `
  --startup-project src\SalesCom.Api `
  --output-dir Migrations
dotnet ef database update `
  --project src\SalesCom.Infrastructure `
  --startup-project src\SalesCom.Api

# Run the API (Development uses appsettings.Development.json)
dotnet run --project src\SalesCom.Api
# → Swagger at http://localhost:<port>/swagger

# Local PostgreSQL (logs are written to the API's logs/ folder — no log stack)
docker compose -f docker\docker-compose.yml up -d
```

`DatabaseInitializer` applies pending migrations on startup, then idempotently seeds the permission catalog and a `SUPERUSER` role. Users are provisioned from Central Login on first sign-in (no user seeding).

---

## 3. Architecture — Clean Architecture, four layers, strict dependency direction

```
SalesCom.Api             ASP.NET controllers, Program.cs, ApiResponse envelope
        │
        ▼
SalesCom.Infrastructure  EF Core (Postgres), JWT/JWE auth, Central Login HTTP client,
                         Serilog → files + Loki, generic repository / unit of work
        │
        ▼
SalesCom.Application     CQRS contracts, dispatchers, decorators, use-case
                         handlers, validators, interfaces for cross-cutting
        │
        ▼
SalesCom.Domain          Entities (plain classes), value objects, enums,
                         Result/ErrorBase, IClock — no external dependencies
```

### Dependency rules — non-negotiable

- **Domain** has no external dependencies.
- **Application** depends on Domain only.
- **Infrastructure** depends on Application + Domain.
- **Api** depends on Application + Infrastructure (Api is the composition root).

### Solution + folder layout

Every project organizes files **by technical type**, and **namespaces match the folder path** (the
standard .NET convention) — e.g. `SalesCom.Domain.Entities.Identity`,
`SalesCom.Application.Commands.Account.Login`. Each file declares **explicit `using` directives** for the
namespaces it consumes (see §12); only BCL global usings (`System`, `System.Linq`, …) come from
`Directory.Build.props`.

```
SalesCom/
├── Directory.Build.props          shared MSBuild props (TFM, NoWarn, nullable)
├── Directory.Packages.props       central package versions (CPM)
├── SalesCom.slnx                  .NET 10 SLN XML format
├── CLAUDE.md                      this file
│
├── src/
│   ├── SalesCom.Domain/           root namespace: SalesCom.Domain
│   │   ├── Common/                EntityBase<TId>, Result, ErrorBase, ErrorType, IStatus
│   │   ├── Entities/              grouped by area (one file per entity):
│   │   │     Identity/            User, Role, Permission, UserRole, RolePermission, RefreshToken, CentralAuthToken
│   │   │     Approvals/           ApprovalType, ApprovalFlow, ApprovalFlowLevel, ApprovalFlowLevelUser, ApprovalRequest, ApprovalDecision
│   │   │     DataSources/         DataSource
│   │   │     Auditing/            AuditLog, ApplicationAccessLog
│   │   │     Reporting/           ReportSetup
│   │   ├── Enums/                 UserStatus, ApprovalStatus, ApprovalRequestStatus, ApprovalDecisionType, AuditAction
│   │   ├── Errors/                DataSourceErrors, UserErrors, AuthErrors, RoleErrors, UserAdminErrors
│   │   └── Interfaces/            IGenericRepository<T>, ISqlRepository, IUnitOfWork, IApplicationAccessLogRepository, IClock
│   │
│   ├── SalesCom.Application/       root namespace: SalesCom.Application
│   │   ├── Commands/              <Feature>/<UseCase>/ holds Command + Handler + Validator (vertical slice)
│   │   ├── Queries/               <Feature>/<UseCase>/ holds Query + Handler
│   │   ├── Responses/             read models / DTOs
│   │   ├── Messaging/             ICommand, IQuery, handler contracts, dispatchers
│   │   ├── Behaviours/            decorator chain (Logging, Validation)
│   │   ├── Mappings/              ToResponse extensions
│   │   ├── Interfaces/            ICentralLoginClient, IAuthSessionService, IJwtTokenGenerator, ICurrentUser, IUserPermissionQuery, IDatabaseCatalog, IApplicationAccessLogger
│   │   ├── Authorization/         Permissions catalog (int ids) + reflection-based seed enumerator
│   │   ├── Common/                PagedResult, CentralUserId, ApiResponse / ApiResponse<T> (the unified envelope)
│   │   └── DependencyInjection.cs Auto-registers handlers/validators + decorator chain
│   │
│   ├── SalesCom.Infrastructure/    namespace: SalesCom.Infrastructure
│   │   ├── Data/                  SalesComDbContext, JSON converter, audit interceptor
│   │   │   └── Seed/              DatabaseInitializer (migration apply + catalog/role seed on startup)
│   │   ├── Gateways/              CentralLoginClient (typed HttpClient to the Central Login API)
│   │   ├── Repositories/          GenericRepository<T> + SqlRepository + UnitOfWork, ApplicationAccessLogRepository, PostgresDatabaseCatalog
│   │   ├── Authorization/         HasPermissionAttribute, PermissionRequirement, PermissionPolicyProvider, PermissionAuthorizationHandler, UserPermissionQuery
│   │   ├── EntityConfigurations/  EF IEntityTypeConfiguration classes (one per entity)
│   │   ├── Migrations/            EF migrations + model snapshot
│   │   ├── Configurations/        Strongly-typed config records (Database, Jwt, CentralLogin, Auth, Observability)
│   │   ├── Services/              AuthSessionService, JwtTokenGenerator, CurrentUserService, ApplicationAccessLogger, SystemClock
│   │   ├── Registrations/         Per-area DI extension methods
│   │   └── DependencyInjection.cs Composition entry (calls each registration)
│   │
│   └── SalesCom.Api/              root namespace: SalesCom.Api
│       ├── Controllers/           Account, DataSources, Roles, Permissions, Users (bind commands/queries directly)
│       ├── Extensions/            Registration extensions + ResultExtensions (Result → ApiResponse → IActionResult)
│       ├── GlobalHandlers/        GlobalExceptionHandler
│       ├── Middleware/            CorrelationIdMiddleware
│       ├── Program.cs             ~30-line composition root
│       ├── appsettings.json       production defaults (secrets are real here — see §10)
│       └── appsettings.Development.json
│
├── tests/
│   ├── SalesCom.Domain.UnitTests/
│   ├── SalesCom.Application.UnitTests/
│   ├── SalesCom.Api.UnitTests/
│   └── SalesCom.Api.IntegrationTests/   needs Docker; Testcontainers Postgres; stubs ICentralLoginClient
│
└── docker/
    └── docker-compose.yml         postgres (local development database)
```

---

## 4. CQRS — hand-rolled, no MediatR, no Scrutor

Manual decorator pattern wired in [`Application/DependencyInjection.cs`](src/SalesCom.Application/DependencyInjection.cs). Decorator order, outermost → innermost:

```
Logging → Validation → Handler
```

The Metrics decorator was removed — observability is logs-only (see §6).

### Adding a new command

Each use case is a **vertical slice** — one folder under `Commands/<Feature>/<UseCase>/` holding the
Command, its Handler, and its Validator together (queries: `Queries/<Feature>/<UseCase>/` with the
Query + Handler). Type names keep their plain form (`CreateDataSourceHandler`, not `...CommandHandler`).
Read models / DTOs live in `Responses/`; cross-cutting models (e.g. `PagedResult`) live in `Common/`.

1. Add `Command : ICommand<Result<T>>` (record) to `Commands/<Feature>/<UseCase>/`.
2. Add `Handler : ICommandHandler<Command, Result<T>>` (internal sealed; constructor injection) in the
   same folder. The handler resolves persistence through the unit of work — `unitOfWork.Repository<X>()`
   for each aggregate it touches — stages inserts/updates/deletes, then calls a single
   `SaveChangesAsync` (one transaction, all-or-none). DDD slices also run the injected `IValidator<X>`
   and return `ErrorBase.Validation` on failure.
3. If a new entity is involved, add its `AbstractValidator<X>` (entity- or command-level) in the same slice folder.
4. Any new read model goes in `Responses/`; a query follows the same vertical-slice pattern under `Queries/<Feature>/<UseCase>/`.
5. DI is automatic — the reflection scan in [`AddApplication`](src/SalesCom.Application/DependencyInjection.cs) is folder-agnostic: it picks up handlers and validators anywhere in the assembly and wraps the handler in the decorator chain.

Controllers **bind the command/query directly** (`[FromBody] CreateXCommand command` — the command is the
request contract; there are no separate Api request DTOs) and call `commandDispatcher.DispatchAsync(command, ct)`;
a route id is merged with `command with { Id = id }`. The dispatcher resolves the closed-generic handler from
DI; reflection invokes `HandleAsync`. The dispatcher itself is intentionally trivial — the decorator chain does the work.

---

## 5. Authentication — Central Login + a custom local session layer

Credential verification **and** the whole 2FA/OTP flow are owned by an external **Central Login** HTTP
API ("API Integration Documentation for Central Login"). On top of it this app keeps a **local
session layer**: it provisions a local `User`, resolves the user's **roles + permissions** (local
RBAC, role-rights), and issues **its own** short access token + refresh token — Central Login's own
tokens are stored server-side and **never returned to the client**. The OTP UX (2-min validity, max 2
resends, 3 wrong → 30-min lockout) lives entirely in the central service's OTP page.

### The two login flows

The central service decides per user whether OTP is required (external vs internal). Both are supported.

`POST /api/account/login` → `LoginCommand` → [`LoginHandler`](src/SalesCom.Application/Commands/Account/Login/LoginHandler.cs)
calls [`ICentralLoginClient.LoginAsync`](src/SalesCom.Application/Interfaces/ICentralLoginClient.cs)
(`POST {CentralLogin:BaseUrl}/account/v1/login` with `{applicationName, applicationKey, userName, password, rememberMe}`):

- **External user (`authType: "Normal"`)** — central returns `userInfo` + its `accessToken`/`refreshToken`
  directly, no OTP. Gates: `isLocked == "Y"` → 403 `User.Locked`; `userStatus != "Y"` → 403 `User.NotActive`.
  Otherwise issue a session (below) and return `{ authType: "Normal", session }` (log `LOGIN SUCCESS`).
- **Internal user (`authType: "SSO"`)** — central returns a `redirectUrl` to its OTP page; we return
  `{ authType: "SSO", redirectUrl }` (log `OTP CHALLENGE ISSUED`); the **frontend** redirects there. After
  OTP success the central service redirects back to the frontend with an `authToken` query param; the
  frontend calls `POST /api/account/verify-auth-token { authToken }` →
  [`VerifyAuthTokenHandler`](src/SalesCom.Application/Commands/Account/VerifyAuthToken/VerifyAuthTokenHandler.cs)
  validates it via `POST account/v1/verify-auth-token`, applies the same account gates, and issues a session
  (returns the `AuthSession` directly; log `OTP LOGIN SUCCESS`).
- **Rejected** → 401 `User.InvalidCredentials` / `User.AuthTokenInvalid` (log `LOGIN FAILED` / `OTP VERIFICATION FAILED`).
- **Central unreachable** → 500 `CentralLogin.Unavailable` (log `CENTRAL LOGIN UNAVAILABLE`) —
  [`CentralLoginClient`](src/SalesCom.Infrastructure/Gateways/CentralLoginClient.cs) never throws for a down dependency.

Every outcome writes one `application_access_logs` row; the session entities + access-log row commit in one `SaveChangesAsync`.

### Issuing a session — `IAuthSessionService`

[`AuthSessionService`](src/SalesCom.Infrastructure/Services/AuthSessionService.cs) (`IssueAsync`),
shared by login + verify-auth-token, given the central `userInfo` + tokens:

1. **Upserts the local `User`** by packed Guid id ([`CentralUserId`](src/SalesCom.Application/Common/CentralUserId.cs),
   deterministic int→Guid). First login auto-provisions: a configurable `Auth:DefaultRoleId`, plus the
   seeded `SUPERUSER` role if the login is in `Auth:SuperUserLogins` (bootstraps the first admins).
2. **Upserts `central_auth_tokens`** — one row per user, the central access + refresh tokens (store-only).
3. Resolves role names + effective permission ids from the local RBAC.
4. **Issues our access token** ([`JwtTokenGenerator`](src/SalesCom.Infrastructure/Services/JwtTokenGenerator.cs)):
   HS256 JWT, JWE-wrapped (`A128KW`+`A128CBC_HS256`), **30-min** default, claims `UserId`/`UserName`/`Login`/
   `Email`/`IsInternal`/`CenterId` plus one `Role` claim per role and one `Permission` claim per id.
5. **Issues our refresh token** — opaque 256-bit, stored **hashed** (SHA-256) in `refresh_tokens`,
   one active row per user, **2-day** default.
6. Returns `AuthSession { accessToken, refreshToken, accessTokenExpiresAtUtc, roles[], permissions[] }`.

All lifetimes are config-driven (`Jwt:AccessTokenLifetimeMinutes`, `Auth:RefreshTokenLifetimeMinutes`).

### Refresh + logout

- `POST /api/account/refresh { refreshToken }` (`[AllowAnonymous]`) → [`RefreshTokenHandler`](src/SalesCom.Application/Commands/Account/RefreshToken/RefreshTokenHandler.cs)
  → `AuthSessionService.RefreshAsync`: SHA-256-look-up the row, reject if expired (`Auth.RefreshTokenExpired`)
  / revoked / unknown (`Auth.RefreshTokenInvalid`), re-resolve roles/permissions (fresh from DB), **rotate**
  the row (new hash + expiry) and return a new `AuthSession`.
- `POST /api/account/logout` (`[Authorize]`) revokes the caller's refresh row.

### Token validation + authorization (every request)

[`AuthenticationRegistration`](src/SalesCom.Infrastructure/Registrations/AuthenticationRegistration.cs)
`JwtBearerEvents`: **`OnMessageReceived`** JWE-decrypts the `Authorization: Bearer` header (jose-jwt);
**`OnChallenge`/`OnForbidden`** emit the unified `ApiResponse` JSON. Standard JWT bearer validation then
verifies HS256 + lifetime + issuer/audience.

Authorization is **DB-backed**, not from the token: `[HasPermission(Permissions.X.Y)]`
([`HasPermissionAttribute`](src/SalesCom.Infrastructure/Authorization/HasPermissionAttribute.cs)) → the
dynamic [`PermissionPolicyProvider`](src/SalesCom.Infrastructure/Authorization/PermissionPolicyProvider.cs) →
[`PermissionAuthorizationHandler`](src/SalesCom.Infrastructure/Authorization/PermissionAuthorizationHandler.cs)
reads the `UserId` claim and calls [`IUserPermissionQuery`](src/SalesCom.Application/Interfaces/IUserPermissionQuery.cs)
(EF join `user_roles → role_permissions`) **per request** — so permission changes take effect immediately.
The permission ids in the token are for the **frontend's** convenience only. The integer permission
catalog is [`Permissions`](src/SalesCom.Application/Authorization/Permissions.cs) (seeded into `permissions`).

### Admin (role/permission/user-role management)

`[HasPermission(Permissions.UserManagement.*)]`-gated CQRS slices manage local RBAC:
`RolesController` (list/get/create/update/delete + `PUT {id}/permissions` to set role-rights),
`PermissionsController` (the catalog), `UsersController` (list users, get/set a user's roles).

---

## 6. Observability — structured logging (files + Grafana Loki) with correlation IDs

Logging is the whole observability story (no metrics, no traces). Serilog writes to **rolling daily
files** and, when enabled, ships the same structured events to **Grafana Loki** for fleet-wide
querying; Development additionally writes to the **console**.

### Sinks

| Sink | When | Detail |
|---|---|---|
| Rolling file | Always | `logs/salescom-<date>.log` — daily roll, 50 MB size cap, retained 31 days |
| Grafana Loki | When `Observability:LokiEnabled` (default true) | Pushes to `Observability:LokiUrl` (local default `http://localhost:3100`). Batched/non-blocking — an unreachable Loki never blocks or crashes the app (failures surface only in Serilog `SelfLog`). Low-cardinality labels `app` (= `ApplicationName`) and `environment`; `CorrelationId`/`UserId`/`Login` travel as structured properties, not labels. |
| Console | Development only | Same text template — keeps the `dotnet run` terminal readable |

Both use one output template:

```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] [{CorrelationId}] {SourceContext} {Message:lj}{NewLine}{Exception}
```

Configured in [`SerilogRegistration`](src/SalesCom.Infrastructure/Registrations/SerilogRegistration.cs),
hooked into the host via `builder.UseSalesComSerilog()` in [`Program.cs`](src/SalesCom.Api/Program.cs).
Log directory and retention come from [`ObservabilityConfiguration`](src/SalesCom.Infrastructure/Configurations/ObservabilityConfiguration.cs)
(the `Observability` config section).

### Correlation ID — the spine of debugging

[`CorrelationIdMiddleware`](src/SalesCom.Api/Middleware/CorrelationIdMiddleware.cs) runs first in
the pipeline. For every request it:

1. Reuses the inbound `X-Correlation-ID` header if the caller sent one, otherwise generates a fresh ID.
2. Pushes it into Serilog's `LogContext` — so **every log line emitted while handling that request
   carries `[CorrelationId]`**, including the request-log line and any exception entry.
3. Sets `HttpContext.TraceIdentifier` and echoes the ID back in the `X-Correlation-ID` response header.

To trace one request end-to-end: take its `X-Correlation-ID` (from the response, or the user's
report) and filter the log file on that value.

### Enrichers attached to every log entry

| Enricher | Property |
|---|---|
| `Enrich.FromLogContext` | `CorrelationId` (and any other `LogContext.PushProperty` scope) |
| `Enrich.WithEnvironmentName` | `EnvironmentName` |
| `Enrich.WithMachineName` | `MachineName` |
| `Enrich.WithProcessId` / `Enrich.WithThreadId` | PID / thread for narrow debugging |
| `Enrich.WithExceptionDetails` | Full exception object tree on errors |

### Levels

- `MinimumLevel.Information` by default; `Debug` in Development.
- The `Serilog` section in `appsettings.json` sets per-namespace overrides (`Microsoft`,
  `Microsoft.AspNetCore`, `Microsoft.EntityFrameworkCore`, `System` → `Warning`), read via
  `ReadFrom.Configuration` — changeable without a code change.
- `UseSerilogRequestLogging` emits one line per HTTP request (method, path, status, duration),
  escalated to `Error` on a 5xx or unhandled exception, and enriched (`EnrichDiagnosticContext`)
  with the authenticated caller's `UserId`/`Login` for per-user tracing across the fleet.

### What's *not* there (intentionally)

- **No OpenTelemetry** — no traces, no metrics, no OTLP. Loki is the only central aggregation.

---

## 7. API surface — unified envelope

### `ApiResponse` shape

Every response (success or failure) uses [`ApiResponse`](src/SalesCom.Application/Common/ApiResponse.cs) / [`ApiResponse<T>`](src/SalesCom.Application/Common/ApiResponse.cs):

```json
{
  "success": true|false,
  "message": "Human-readable summary or full error detail",
  "data": { ... },            // omitted when null (success without payload, or failure)
  "errorCode": "User.NotFound" // omitted on success
}
```

**Removed by request (do not re-add without checking):** `traceId`, `errors[]`. Multi-field validation errors are joined into `message` with `; ` separators.

HTTP status codes still follow REST semantics (200/201/400/401/403/404/409/500). Only the body shape is uniform.

### `Result<T>` → `ApiResponse<T>`

Controllers stay one-liners: they bind the command/query directly (no separate request DTOs — see §4) and
`return result.ToApiResponse(this, "Loaded.");`. Mapping lives in [`ResultExtensions`](src/SalesCom.Api/Extensions/ResultExtensions.cs).

`ErrorBase.Type` → HTTP status:

| ErrorType | HTTP |
|---|---|
| Validation | 400 |
| NotFound | 404 |
| Conflict | 409 |
| Unauthorized | 401 |
| Forbidden | 403 |
| Unexpected | 500 |

### Endpoints currently live

- `POST  /api/account/login` — `[AllowAnonymous]`. External user → `{ authType:"Normal", session }`; internal user → `{ authType:"SSO", redirectUrl }`.
- `POST  /api/account/verify-auth-token` — `[AllowAnonymous]`, exchanges the post-OTP `authToken` for an `AuthSession`.
- `POST  /api/account/refresh` — `[AllowAnonymous]`, rotates a refresh token → a new `AuthSession`.
- `POST  /api/account/logout` — `[Authorize]`, revokes the caller's refresh token.
- `GET   /api/account/me` — `[Authorize]`, the caller's profile + roles + permissions from token claims.
- `GET   /api/data-sources*` (View) / `POST|PUT /api/data-sources*` (Manage) — `[HasPermission(Permissions.DataSources.*)]`.
- `GET/POST/PUT/DELETE /api/roles`, `PUT /api/roles/{id}/permissions` — role + role-rights admin (`UserManagement.*`).
- `GET   /api/permissions` — the permission catalog (`UserManagement.ViewPermissions`).
- `GET   /api/users`, `GET|PUT /api/users/{id}/roles` — user list + role assignment (`UserManagement.ViewUsers`/`.AssignUserRoles`).
- `GET   /health/live`, `GET /health/ready` — health probes.

`AuthSession` = `{ accessToken, refreshToken, accessTokenExpiresAtUtc, roles[], permissions[] }`. Protected
endpoints use `[HasPermission(id)]` (DB-backed, see §5).

### Failure paths the framework owns

- **Model-binding failures** (malformed JSON, missing required field) → caught by `InvalidModelStateResponseFactory` in [`PresentationRegistration`](src/SalesCom.Api/Extensions/PresentationRegistration.cs), wrapped in `ApiResponse.Fail`.
- **Unhandled exceptions** → caught by [`GlobalExceptionHandler`](src/SalesCom.Api/GlobalHandlers/GlobalExceptionHandler.cs) (`IExceptionHandler`), wrapped in `ApiResponse.Fail`. Note: `services.AddProblemDetails()` is registered because .NET 10's `ExceptionHandlerMiddleware` requires `IProblemDetailsService` at construction — but it's never actually invoked because our handler always returns `true`.
- **401 / 403 from JWT bearer** → custom `OnChallenge` / `OnForbidden` events emit the same envelope.

---

## 8. Database — single Postgres backend

### Postgres (`salescom` schema)

The application's own data. EF Core 10 + Npgsql.

| Table | Purpose |
|---|---|
| `application_access_logs` | Audit trail of login/OTP attempts |
| `audit_logs` | Change-audit trail — one row per entity insert/update/delete (see §8.1) |
| `users` | Local user, provisioned from Central Login (`central_user_id` packed into the Guid `id`); no credentials |
| `roles`, `permissions`, `user_roles`, `role_permissions` | Local RBAC (role-rights); `permissions` seeded from the code catalog |
| `central_auth_tokens` | The central service's access/refresh tokens, one row per user (store-only, never returned; not audited) |
| `refresh_tokens` | Our own refresh token, hashed, one active row per user (rotated on refresh; not audited) |
| `approval_types`, `approval_flows`, `approval_flow_levels`, `approval_flow_level_users`, `approval_requests`, `approval_decisions` | Multi-level approval flow — **schema only**, no CQRS slice yet (see §11) |
| `data_sources` | Registered Postgres source tables (`_COM`-postfix tables onboarded for commission use); stores the source-table identity, description and active flag only. |
| `report_setups` | Commission report build (JSON-heavy sections in `jsonb`). Entity exists; no CQRS slice yet. |
| `__ef_migrations_history` | EF migration tracking. |

Migrations live in [`Infrastructure/Migrations/`](src/SalesCom.Infrastructure/Migrations); `DatabaseInitializer` applies pending migrations at host startup, then idempotently seeds the permission catalog (from [`Permissions`](src/SalesCom.Application/Authorization/Permissions.cs)) and a `SUPERUSER` role granted every permission. The latest migration, `ReintroduceRbacAndApprovalSchema`, re-creates the RBAC tables and adds the session-token + approval-flow tables.

### 8.1 Change-audit trail

[`AuditSaveChangesInterceptor`](src/SalesCom.Infrastructure/Data/AuditSaveChangesInterceptor.cs) — an
EF Core `SaveChangesInterceptor` — records **who changed what, when, and the full before/after
state** for every entity insert, update and delete:

- One [`AuditLog`](src/SalesCom.Domain/Entities/Auditing/AuditLog.cs) row per changed entity: `ApplicationName`
  (the configured `Observability:ApplicationName` — lets fleet components share one audit store),
  `EntityName`, `EntityId`, `Action` (`Created`/`Updated`/`Deleted`), `ChangedBy` (login, or `system`),
  `ChangedByUserId`, `ChangedOnUtc`, `ChangedColumns` (updates only), and `OldValues` / `NewValues`
  as `jsonb` — the complete previous and new property set.
- Audit rows are added to the **same `SaveChanges`**, so they commit in the **same transaction** as
  the change that produced them — no trail without the change, no change without the trail.
- The two log tables (`audit_logs`, `application_access_logs`) are never themselves audited.
- Capture-only by request — there is no read endpoint; query `audit_logs` directly.

### 8.2 Persistence — generic repository + unit of work

App-data writes and reads go through a **generic repository reached from the unit of work**, not
per-entity repositories:

- [`IUnitOfWork`](src/SalesCom.Domain/Interfaces/IUnitOfWork.cs) exposes `Repository<T>()`
  (the typed [`IGenericRepository<T>`](src/SalesCom.Domain/Interfaces/IGenericRepository.cs)),
  `Sql` (the dynamic raw-SQL [`ISqlRepository`](src/SalesCom.Domain/Interfaces/ISqlRepository.cs)),
  and `SaveChangesAsync`.
- A command handler resolves `Repository<T>()` for each aggregate it touches, stages as many
  inserts/updates/deletes as needed, then calls **one** `SaveChangesAsync` — EF commits them in a
  single transaction, so the handler is **all-or-none**. No explicit Begin/Commit/Rollback.
- [`UnitOfWork`](src/SalesCom.Infrastructure/Repositories/UnitOfWork.cs) is registered **transient**
  but wraps the **scoped** `SalesComDbContext`, so within a request the generic repos and the kept
  specialized gateways (`IApplicationAccessLogRepository`) share one change tracker — a handler's
  entity changes and its access-log row commit together.
- The Application layer never references EF Core: `IGenericRepository<T>` takes BCL
  `Expression<Func<T,bool>>` predicates and `Func<IQueryable<T>,IOrderedQueryable<T>>` ordering
  lambdas; materialization (`ToListAsync`, `AsNoTracking`, …) lives in Infrastructure.
- **Kept as dedicated gateways** (deliberately *not* folded into the generic repo): the
  `information_schema` catalog ([`IDatabaseCatalog`](src/SalesCom.Application/Interfaces/IDatabaseCatalog.cs))
  and the access-log repository.

### JSON-heavy storage

Fixed metadata lives in typed columns; dynamic logic and joins live in `jsonb`. A
[`JsonDocumentConverter`](src/SalesCom.Infrastructure/Data/JsonDocumentConverter.cs) maps
`JsonDocument` ⇄ `jsonb`. Current `jsonb` columns:

- `report_setups.supporting_uploads` / `acheivements` / `incentives` — the report build sections.
- `audit_logs.old_values` / `new_values` — the before/after entity snapshots.

When adding new aggregates, default to `jsonb` columns for any field whose schema is expected to evolve without migrations.

---

## 9. Configuration — strongly-typed options only

**Never** call `IConfiguration["..."]` or `IConfiguration.GetSection("...").Value` in application code. Pattern:

1. Add a `record` in [`Infrastructure/Configurations/`](src/SalesCom.Infrastructure/Configurations) with `[Required]`/`[Range]`/`[MinLength]` data annotations.
2. Bind once in the relevant per-area registration extension:
   ```csharp
   services.AddOptions<MyOptions>()
       .Bind(configuration.GetSection(MyOptions.SectionName))
       .ValidateDataAnnotations()
       .ValidateOnStart();
   ```
3. Inject `IOptions<MyOptions>` wherever needed.

`ValidateOnStart()` makes config errors fail at boot (clear message), not at first request.

### Sections used

| Section | Bound to | Purpose |
|---|---|---|
| `Database` | `DatabaseConfiguration` | Postgres connection + retry |
| `Jwt` | `JwtConfiguration` | Signing/encryption keys, issuer, audience, claim names, **30-min** access TTL |
| `CentralLogin` | `CentralLoginConfiguration` | Central Login base URL, application name + key, timeout |
| `Auth` | `AuthConfiguration` | Refresh-token TTL (**2 days**), `DefaultRoleId`, `SuperUserLogins` bootstrap list |
| `Observability` | `ObservabilityConfiguration` | App/component name, log-file prefix + directory + retention, Loki enable + URL + credentials |
| `Serilog` | (Serilog's own) | Per-namespace minimum-level overrides; honored via `ReadFrom.Configuration` |

### The composition-root exception

[`SerilogRegistration`](src/SalesCom.Infrastructure/Registrations/SerilogRegistration.cs) resolves `IOptions<ObservabilityConfiguration>` inside the `UseSerilog` callback (the only place logger config can read from DI), and reads the `Serilog` section via `ReadFrom.Configuration` (Serilog's native contract for level overrides).

---

## 10. Domain model conventions

This is **not** DDD. Entities are plain data; behaviour, derivation, and mapping live outside them.

### Plain entities

Entities are **plain classes** — only fields/properties. No factory methods, no `Result`-returning
constructors, no behavior methods, no computed properties, no domain events. An instance is built
with an object initializer:

```csharp
var dataSource = new DataSource { SourceTableName = name, AliasTableName = alias, CreatedOnUtc = clock.UtcNow };
```

- Any derived value or mapping is an **extension method**, never a member of the entity —
  e.g. `dataSource.ToResponse(columns)` ([`Mappings/DataSourceMappingExtensions.cs`](src/SalesCom.Application/Mappings/DataSourceMappingExtensions.cs)).
- The `Guid`-keyed entities (`DataSource`, `ReportSetup`)
  inherit the generic [`EntityBase<TId>`](src/SalesCom.Domain/Common/EntityBase.cs) closed as
  `EntityBase<Guid>` — `TId Id`, `CreatedOnUtc`, nullable `UpdatedAtUtc`, `Version` (the Postgres
  `xmin` concurrency token). The `Guid` key is `ValueGeneratedOnAdd` — Npgsql generates it
  client-side at `Add()`, so handlers never set `Id`. `User` `Ignore`s the inherited `UpdatedAtUtc`
  (the `users` table predates that column).
- `ApplicationAccessLog` and `AuditLog` are standalone classes keyed by `long` (DB identity).
- IDs are **raw primitives** — `Guid` / `int` / `long`. No strongly-typed ID wrappers. The central
  login integer user id is packed into a `Guid` via `CentralUserId` (§5).

### Approval status — `IStatus`

[`IStatus`](src/SalesCom.Domain/Common/IStatus.cs) is a one-property contract exposing an
[`ApprovalStatus`](src/SalesCom.Domain/Enums/ApprovalStatus.cs) (`Draft → Saved → Approved`).
[`ReportSetup`](src/SalesCom.Domain/Entities/Reporting/ReportSetup.cs) implements it today; any new entity that
needs the draft → save → approve workflow can opt in by declaring `: IStatus`. New entities default
to `ApprovalStatus.Draft`.

### Validation — FluentValidation

Validators are **command-level** (`AbstractValidator<TCommand>`, e.g.
[`CreateDataSourceValidator`](src/SalesCom.Application/Commands/DataSources/CreateDataSource/CreateDataSourceValidator.cs)),
live **alongside their command** in the vertical-slice folder, and are `internal sealed`. The
[`ValidationCommandHandlerDecorator`](src/SalesCom.Application/Behaviours/ValidationCommandHandlerDecorator.cs)
runs them before the handler and returns an `ErrorBase.Validation` (`;`-joined messages) on failure —
no entity-level validators, no exceptions in the happy path; validation surfaces through the explicit
`Result` channel.

### `Result<T>` / `ErrorBase` pattern

Handlers — not entities — return [`Result<T>`](src/SalesCom.Domain/Common/Result.cs). Implicit
conversions from `T` and `ErrorBase` keep handlers terse:

```csharp
return DataSourceErrors.NotFound;        // implicit conversion to Result<X>
return dataSource.ToResponse(columns);   // mapping extension → implicit conversion to Result<X>
```

`*Errors` static classes (e.g. [`DataSourceErrors`](src/SalesCom.Domain/Errors/DataSourceErrors.cs))
hold only **outcome** errors — `NotFound`, `AlreadyRegistered`, etc. Field-level messages come from
the validators. Exceptions remain reserved for infrastructure failures.

### Records vs classes

- **Records**: commands, queries, DTOs, responses, `ErrorBase`.
- **Classes**: entities (EF change tracking), `EntityBase<TId>`, `Result` / `Result<T>`.

---

## 11. Known gaps / TODO

### Current shape of the domain

The original commission scaffold (Kpi / DataFeed / Datasource / LibraryItem / Report) was removed
to start clean. The live feature is:

- **Data sources** — a [`DataSource`](src/SalesCom.Domain/Entities/DataSources/DataSource.cs) entity
  (source-table identity + description + active flag), a `DataSourceErrors`
  outcome class, command-level FluentValidation, the generic repository + unit of work (§8.2) for
  persistence, an [`IDatabaseCatalog`](src/SalesCom.Application/Interfaces/IDatabaseCatalog.cs) for
  introspecting `information_schema` (also powers the read-only column preview on the available-tables
  endpoint), and CQRS slices for discovery, registration, get/list, and update.

- **Identity & RBAC** — local `User` (provisioned from Central Login), `Role`/`Permission`/`UserRole`/
  `RolePermission` (role-rights), the dual-token session layer (§5), and admin CQRS slices under
  `Commands|Queries/UserManagement/`.

[`ReportSetup`](src/SalesCom.Domain/Entities/Reporting/ReportSetup.cs) and the six **approval-flow** entities
(the `Approval*` entities in `Domain/Entities/`) exist as entities + EF mappings + tables but have **no CQRS slices yet**
— schema seeds for the next feature. The multi-level approval engine (submit → decide → advance,
wired to `ApprovalStatus`) is the planned follow-up; columns are inferred from `docs/RRD.png` and may
be refined before endpoints exist.

### Integration tests

[`SalesCom.Api.IntegrationTests`](tests/SalesCom.Api.IntegrationTests) (needs Docker) stubs
`ICentralLoginClient` with a rejecting fake in `SalesComFactory` — no test ever calls the real
central service. The unit tests fully cover handler logic with mocks.

### Central Login environment values

`CentralLogin:BaseUrl` ships as a local placeholder (`http://localhost:8080`) and
`CentralLogin:ApplicationKey` as `REPLACE_WITH_APPLICATION_KEY` — the real values come from the
central login team. Until they're set, every login returns the graceful
`CentralLogin.Unavailable` error.

### Refresh tokens

Implemented (§5): our own opaque refresh token, hashed in `refresh_tokens`, **one active row per user**
(single rotating session), 2-day TTL, rotated on `POST /api/account/refresh`. Multi-device sessions
(per-device rows + family revocation) would be the extension if needed.

### Eventing

There is no domain-event mechanism — entities are simple data classes. If asynchronous messaging
is ever needed, an outbox table written in the same transaction as the entity is the place to add it.

---

## 12. Conventions / style guardrails

Things the user has been explicit about — preserve unless asked to change:

- **Every project organizes by technical type; namespaces match the folder path; explicit `using` directives per file**: Domain has `Common/ Entities/<Area>/ Enums/ Errors/ Interfaces/` (entities grouped by area — `Identity/ Approvals/ DataSources/ Auditing/ Reporting/`, one file per entity). Application has `Commands/ Queries/ Responses/ Messaging/ Behaviours/ Mappings/ Interfaces/ Authorization/ Common/` — each use case is a vertical slice under `Commands/<Feature>/<UseCase>/` (Command + Handler + Validator) or `Queries/<Feature>/<UseCase>/` (Query + Handler); the `<Feature>` level (`Account/ DataSources/ UserManagement/`) groups slices within the `Commands`/`Queries` type folders. Infrastructure has `Data/ Gateways/ Repositories/ Authorization/ EntityConfigurations/ Configurations/ Services/ Registrations/ Migrations/`; Api has `Controllers/ Extensions/ GlobalHandlers/ Middleware/`. Type names keep their plain form (`CreateDataSourceHandler`, not `...CommandHandler`). **Namespaces follow the folder path** (`SalesCom.Domain.Entities.Identity`, `SalesCom.Application.Commands.Account.Login`, …). Each file uses **explicit `using` directives** for the namespaces it consumes (the file-scoped `namespace X;` is the first line, then the `using` block, grouped `System.*` → `Microsoft.*` → third-party → `SalesCom.*`). Only BCL global usings (`System`, `System.Linq`, …) come from `Directory.Build.props`; test projects declare framework usings (`Xunit`, `FluentAssertions`, `NSubstitute`) via `<Using>` in their `.csproj`.
- **Not DDD; plain entities**: entities hold only fields/properties. Derived values and mapping are **extension methods** (in `Application/Mappings/`, e.g. `dataSource.ToResponse(cols)`) — never members of the entity. Responses are plain records (no static `From(...)` factories).
- **Validation is command-level FluentValidation** run by the decorator — no entity-level validators, no manual checks in handlers beyond input normalization.
- **Controllers bind the command/query directly** — no separate Api request DTOs; the command is the request contract, and a route id is merged via `command with { Id = id }`.
- **Minimal responses**: drop fields the client can derive. `LoginResponse` carries `authType` plus exactly one of `session` (Normal) / `redirectUrl` (SSO) — the other is omitted from the JSON. `PagedResult` has no `TotalPages` (derivable).
- **No `traceId` in response bodies**: removed by request. Correlation instead travels as the `X-Correlation-ID` response header and the `CorrelationId` stamped on every log line.
- **No `errors[]` array on `ApiResponse`**: multi-error cases get `;`-joined into `Message`.
- **Decorator chain is `Logging → Validation → Handler`**: no Metrics decorator.
- **EF Core is the only ORM**: no Dapper, no other data-access stacks.
- **`IOptions<T>` everywhere, never raw `IConfiguration`**: except at the composition-root exception point (§9).
- **No commentary that goes stale**: don't write "used by X" or "added for Y flow" — file history covers that.
- **Don't add features beyond what's requested**.
- **All async; no `.Result` / `.GetAwaiter().GetResult()`**. App code does **not** use `.ConfigureAwait(false)` — there's no `SynchronizationContext` under ASP.NET Core, so it's noise.

---

## 13. Reference documents

The authentication integration implements two documents (ask the user for them if auth-contract
questions arise):

- **"API Integration Documentation for Central Login"** — the wire contract this app implements:
  `POST account/v1/login` (`authType` `Normal`/`SSO`, the `userInfo` payload) and
  `POST account/v1/verify-auth-token`. The central base URL/application key are provisioned by the
  central login team (placeholders in appsettings until then).
- **"2FA User manual DMS"** — the OTP page UX (2-minute OTP validity, max 2 resends, 3 wrong
  attempts → 30-minute lockout). All implemented by the central service; this app only surfaces the
  central error messages it returns.

The legacy Banglalink reference solution (`D:\Reference\sales-commission-system`) shaped the
original JWT/JWE wire format (kept for fleet compatibility) but its local-identity flow has been
fully replaced by the Central Login integration.
