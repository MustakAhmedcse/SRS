# SalesCom — LLD: Architecture, Modules & Dev Setup

> **Low-Level Design — foundation sections.** This file holds §1 Introduction, §2 System Architecture, §13 Module & Service Architecture, and §14 Getting Started / Dev Setup. It is written so a **zero-context team** (Next.js front-end, .NET back-end, Python calc-engine) can stand the system up and start building in the right order.
>
> Companion LLD sections referenced from here:
> - **`Reconciled_Canonical_DDL.md` (§3 Database Schema)** — the 22-table canonical DDL. All table/column names used below are taken verbatim from it.
> - **`IR_Schema_and_MultiKPI_Join.md`** — the `report_setup.definition` IR contract (the no-code report config). Referenced, not duplicated.
> - **`Commission_Logic_Catalog.md`** — the real commission patterns that justify the engine capabilities.
> - **`Salescom_System_Design_Document.md`** — the deeper design rationale (architecture, security, ICDs, DR).

---

# §1. Introduction

## 1.1 Purpose

SalesCom is an internal **Sales Commission Automation Platform** for Banglalink. It replaces the old manual process — where an analyst hand-wrote SQL against sales data, exported spreadsheets, and chased email approvals (the "SRF" process) — with a **self-service, config-driven web application**.

A Business User builds a commission report through a **5-step, no-code wizard**. The wizard output is a single JSON document (the **IR** — Intermediate Representation — stored in `report_setup.definition`). At Final Save, a Python **SQL Generator** compiles that IR into SQL and stores it stage-by-stage in the `stages` table. When the report is triggered (on a schedule, as a demo, or run-now), a **SQL Executor** runs each stage in order, materialising temp tables, and writes a per-channel result into `final_commission`. That result flows through a **sequential maker-checker approval** and is finally paid out either automatically via **EV** (with a recipient SMS) or by handing a **CSV file to POS**.

This LLD is the **single build specification**. It is detailed and implementation-ready by design: an engineer who has never seen the project should be able to read it, set up the repos, and build each module in the prescribed order.

## 1.2 Scope

**In scope (what this LLD covers):**

- The full web application: authentication, the 5-step report wizard, data-source registration, supporting-file upload, the IR, the IR→SQL generator, the run executor, the approval workflow, EV/POS disbursement, dashboards, notifications, and the audit trail.
- The four runtime services: the **.NET Web API**, the **Python Calc-Engine** (SQL Generator + Executor), the **Hangfire** background workers, and the **Next.js** front-end.
- All 22 database tables (see the canonical DDL), the message contracts between services, and the seven external integration points.
- A **phased build**: the architecture supports every pattern now; features are delivered in three phases (single-KPI commissions → multi-KPI → complex patterns). The data model and IR shape are **final from Phase 1** — later phases only add operation types, never reshape the schema.

**The seven external integration points** (each has its own Interface Control Document — ICD — see the SDD):

| # | External system | Direction | Used for |
|---|---|---|---|
| 1 | **Central Login** (`blposapi.banglalink.net`) | SalesCom → Central | SSO sign-in + OTP; user directory sync |
| 2 | **EV API** (`10.13.2.7:9898`) | SalesCom → EV | Automatic commission disbursement |
| 3 | **SMS gateway** (`172.16.7.210:13082`) | SalesCom → SMS | Recipient + OTP + alert SMS |
| 4 | **Email / SMTP** | SalesCom → SMTP | Approval and run notifications |
| 5 | **Source systems via Airflow ETL** (DWH, In-house, vPeople, POSDMSDB) | Source → SalesCom DB | Prepared sales data loaded into source tables |
| 6 | **POS** | SalesCom → POS | CSV handoff for POS-channel payouts |
| 7 | **RSO App** | RSO App → SalesCom | Consumes the SalesCom public API (read commission status) |

> **Out of scope for this LLD body:** the ETL pipelines themselves (owned by the Airflow team — SalesCom only *reads* the loaded tables), and the internal mechanics of the external systems.

## 1.3 Intended audience

| Reader | What they need from this document |
|---|---|
| **Front-end engineers (Next.js / React / TS)** | The Next.js folder map (§13.2), the wizard flow, API endpoint contracts, the role-based UI gating. |
| **Back-end engineers (.NET / ASP.NET Core)** | The 4-layer module map (§13.1), per-feature controllers, DTOs, validation rules, the run/approval/disbursement state machines. |
| **Calc-engine engineers (Python)** | The IR contract (`IR_Schema_and_MultiKPI_Join.md`), the SQL Generator and Executor responsibilities (§2.2), the RabbitMQ message contracts (§14.4). |
| **DevOps / Platform** | The deployment topology, environment variables, and local-wiring instructions (§14). |
| **Reviewers / Lead** | The architecture decisions and conventions that everything else must obey (§2). |

## 1.4 Definitions and acronyms

| Term | Meaning |
|---|---|
| **Maker** | Business User role — builds reports, manages schedules, triggers runs, initiates approvals. Cannot approve their own run (BR5). |
| **Checker** | Approver role — approves or rejects at an assigned approval level. |
| **Administrator** | Super-role that can perform Maker and Checker actions plus catalog/flow administration. |
| **IR** | **Intermediate Representation** — the JSON document in `report_setup.definition` that fully describes a no-code report. The heart of the system. |
| **Block** | A sub-pipeline of the IR (an *achievement* block or an *incentive* block). Each block ends in a `summarize` that fixes its **grain**. |
| **Grain** | The row identity of a block's output (e.g. "one row per RSO_CODE"). Block-to-block joins must be on the grain key (1:1) — this prevents double-paying. |
| **Stage** | One compiled SQL step. The IR compiles to N stages stored in `stages`; at run time each is snapshotted into `run_stage`. |
| **Demo run** | A throwaway execution that shows per-stage row counts and output previews but never disburses. |
| **Final run** | A real execution whose `final_commission` is eligible for approval and disbursement. |
| **EV** | Electronic Voucher — the automatic payout rail (`ev_disburse`), paired with a recipient SMS. |
| **POS** | Point of Sale — the manual payout rail; SalesCom produces a CSV (`pos_disbursement`) handed to the POS team. |
| **Channel** | The commission recipient identity (Distributor / RSO / Retailer / BDO / etc.) — the `channel` table. |
| **SRF** | Sales Requisition Form — the legacy manual commission request this system replaces. |
| **SQLGlot** | The Python library used to build SQL as an **AST** (never string concatenation) and re-validate it before execution. |
| **SeaweedFS** | The S3-compatible object store (raw uploaded files, generated output files, POS CSVs). |
| **ICD** | Interface Control Document — the pinned contract for one external integration. |
| **BR1–BR9** | The nine business rules (see §1.5). |
| **TIMESTAMPTZ** | PostgreSQL timestamp-with-time-zone; all timestamps are stored in **UTC**, serialised as ISO-8601. |

## 1.5 Business rules (BR1–BR9) — enforced throughout

These rules are referenced by ID across every module. They are enforced by a combination of DB constraints (see the DDL) and application logic.

| ID | Rule | Primary enforcement |
|---|---|---|
| **BR1** | Access is by the user's assigned role/right. | JWT claims + per-endpoint role check (§13.1). |
| **BR2** | A data source is never deleted — only deactivated. | API forbids DELETE; `data_source.is_active = false`. |
| **BR3** | Report name is system-wide unique. | `uq_report_setup_name` UNIQUE constraint. |
| **BR4** | `start_date ≤ end_date`. | `ck_report_setup_dates` CHECK + API validation. |
| **BR5** | The same user cannot be Maker **and** Checker of the same run. | Approval service rejects a decision by the run's `triggered_by_user`. |
| **BR6** | Approvals are sequential, ascending by level. | `approval_request.current_level_order` walks 1→N; lower levels must be APPROVED first. |
| **BR7** | A reject (or changes-requested) requires a comment. | `ck_approval_decision_comment` CHECK + API validation. |
| **BR8** | Disburse only after **full** approval. | Disbursement service requires `approval_request.overall_status = 'APPROVED'`. |
| **BR9** | Only the Maker manages the schedule; all changes are audited. | Role check + `audit_log` write on every mutation. |

---

# §2. System Architecture

## 2.1 Layered architecture (in words)

SalesCom is a small set of cooperating services. Read top-to-bottom as the request flows.

```
┌───────────────────────────────────────────────────────────────────────────────┐
│  CLIENT TIER                                                                     │
│  Next.js / React / TypeScript SPA  (browser)                                     │
│   • 5-step wizard, dashboards, approval queue, admin screens                     │
│   • Holds the SalesCom JWT; calls only /api/v1/* over HTTPS                       │
└───────────────────────────────────────────────────────────────────────────────┘
                                   │  HTTPS (F5 / VIP load balancer, Cumilla active)
                                   ▼
┌───────────────────────────────────────────────────────────────────────────────┐
│  APPLICATION TIER  (App servers CMVLSALCOMAPP01/02, all in Docker)               │
│                                                                                   │
│  ┌─────────────────────────┐   ┌──────────────────────┐   ┌───────────────────┐ │
│  │ .NET Web API             │   │ Public API Gateway   │   │ Hangfire workers  │ │
│  │ (ASP.NET Core, 4-layer)  │   │ (RSO App, ICD #7)    │   │ (scheduled runs,  │ │
│  │  Api/Application/Domain/  │   │                      │   │  EV worker, sweeps,│ │
│  │  Infrastructure)         │   │                      │   │  user sync, notif) │ │
│  └─────────────────────────┘   └──────────────────────┘   └───────────────────┘ │
│        │  publishes/consumes RabbitMQ messages; reads/writes Postgres; S3         │
└───────────────────────────────────────────────────────────────────────────────┘
            │                         │                         │
            ▼ (AMQP)                  ▼ (SQL)                    ▼ (S3)
┌────────────────────────┐   ┌──────────────────┐   ┌──────────────────────────────┐
│  CALC-ENGINE TIER       │   │  DATA TIER        │   │  OBJECT STORE                │
│  (AI servers CMVLSALCOM- │   │ Percona PG 18     │   │ SeaweedFS (S3-compatible)    │
│   AI01)                  │   │ (bare-metal,      │   │  • raw uploads               │
│  Python:                 │   │  NOT Docker)      │   │  • generated output files    │
│   • SQL Generator        │   │  salescom schema  │   │  • POS CSV handoff           │
│     (SQLGlot AST)        │   │  uploads.* tables │   └──────────────────────────────┘
│   • SQL Executor         │   │  runtmp.* temp    │
│     (SQLAlchemy)         │   └──────────────────┘
└────────────────────────┘
        │
        ▼ (HTTP, on AI servers)
┌───────────────────────────────────────────────────────────────────────────────┐
│  INFRASTRUCTURE TIER  (AI02): RabbitMQ broker · Loki/Prometheus log+metrics       │
└───────────────────────────────────────────────────────────────────────────────┘

  EXTERNAL SYSTEMS (reached from App tier / workers):
   Central Login · EV API · SMS gateway · SMTP · POS handoff · Source systems (Airflow ETL)
```

**Key separation (architecture decision D2):** the **SQL Generator** and the **SQL Executor** are two distinct responsibilities in the Python tier. The Generator runs once at Final Save (IR → SQL → `stages`). The Executor runs at run time (frozen SQL from `run_stage` → temp tables → `final_commission`). They never run in the same call.

**Notifications** are sent directly by the Web API / its Hangfire workers (there is **no** separate Notification microservice). A row is written to `notification_log`, and a Hangfire job delivers it via SMS gateway or SMTP.

## 2.2 Component responsibilities

| Component | Runs on | Responsibility | Does NOT do |
|---|---|---|---|
| **Next.js SPA** | Browser | Render wizard/dashboards; hold JWT; call `/api/v1/*`; client-side validation for UX only. | Never builds SQL; never talks to Postgres or external systems directly. |
| **.NET Web API** | App tier | All HTTP endpoints; auth + JWT issuance; CRUD; orchestrates wizard save → triggers SQL Generator; enqueues runs; runs the approval state machine; writes `final_commission` via the **trusted path**; writes `audit_log` + `notification_log`. | Never executes generated SQL itself; never builds commission SQL (delegates to Python). |
| **Public API Gateway** | App tier | The narrow read-only public surface consumed by the RSO App (ICD #7). Separate auth (API key/JWT), rate-limited. | No write access to commission data. |
| **Hangfire workers** | App tier | Background jobs: schedule trigger, run-queue dispatch, EV disbursement worker, POS CSV generation, notification delivery, stale-run sweep, temp-table cleanup, hourly user sync. | No synchronous request handling. |
| **SQL Generator (Python)** | AI tier | At Final Save: validate IR against the JSON Schema; compile IR → SQL via **SQLGlot AST**; decompose into ordered stages; write `stages.sql_text` + `output_table_name`. | Does not run the SQL. |
| **SQL Executor (Python)** | AI tier | At run time: snapshot `stages` → `run_stage`; re-parse + allowlist-validate each stage; run stage-by-stage against `uploads.*`/source tables, materialising `runtmp.*` temp tables; record per-stage row counts; hand the final result rows to the Web API to write `final_commission`. | Never writes `final_commission` directly (trusted-path rule, D2). |
| **PostgreSQL (Percona 18)** | DB server (bare metal) | `salescom` core schema (22 tables), `uploads.*` (CSV-derived tables), `runtmp.*` (per-run temp). Async streaming replication to Gazipur standby. | — |
| **RabbitMQ** | AI02 | Async message bus between Web API ↔ Python engine and for worker fan-out. | — |
| **SeaweedFS** | AI02 | S3-compatible object store: raw uploads, generated output files, POS CSVs. | — |

### 2.2.1 The trusted-path rule for `final_commission` (D2 — read this)

Generated SQL is **never trusted to write money tables.** The Executor's last stage produces a result set of `(channel_code, commission_amount)` rows in a temp table. It does **not** `INSERT INTO final_commission`. Instead, the Executor returns those rows (or their temp-table handle) to the **Web API**, which:

1. Resolves each `channel_code` to a `channel.id` (validation error if unmapped — see IR §4).
2. Rounds `commission_amount` to the money precision.
3. Inserts into `final_commission` under the `uq_final_commission_run_channel` UNIQUE constraint (idempotent — re-running cannot double-insert).

This is why the Executor runs as a **least-privilege DB role** that can `SELECT`/`CREATE TEMP TABLE`/`DROP TEMP TABLE` on source and temp schemas but has **no INSERT/UPDATE** on `salescom.final_commission`, `ev_disburse`, or `pos_disbursement`.

## 2.3 Tech stack (with rationale)

| Layer | Technology | Why this choice |
|---|---|---|
| **Front-end** | Next.js + React + TypeScript | SPA with file-based routing; TS gives compile-time safety on the DTO contracts; SSR available for the dashboard if needed. |
| **Back-end** | C# / .NET 8 (ASP.NET Core) | Mature, strongly-typed, 4-layer clean architecture; first-class DI; the org standard. |
| **Back-end data access** | **Dapper + EF Core** | EF Core for migrations, change-tracking CRUD, and the relational backbone; **Dapper** for hot read paths (dashboards, queue lookups) where raw SQL is faster. |
| **Calc engine** | **Python** + **SQLGlot** + **SQLAlchemy** | SQLGlot builds SQL as an AST and re-parses it for safe-mode validation (no string concatenation → no injection); SQLAlchemy executes against Postgres. Python is the natural fit for the data-shaping logic. |
| **Database** | **Percona PostgreSQL 18** (bare metal) | Window functions (Phase-3 `rank`), `NUMERIC` money, JSONB for the IR, GIN indexing; bare-metal for predictable I/O on the join-heavy workload. |
| **Message bus** | **RabbitMQ** | Decouples Web API from the Python engine; durable queues survive a worker restart mid-run. |
| **Object store** | **SeaweedFS** (S3 API) | Self-hosted S3 for uploads/outputs without a cloud dependency. |
| **Background jobs** | **Hangfire** | Schedules, retries, and a dashboard; runs in-process on the App tier. |
| **ETL** | **Apache Airflow** (shared server) | Owned by the data team; lands prepared sales data into source tables SalesCom reads. |
| **Auth** | Central Login **SSO + OTP** → SalesCom **JWT** (1-day) | Reuse the org identity provider; SalesCom issues its own short-lived JWT for API calls. |
| **Observability** | **Serilog → Loki** (+ Prometheus) | Structured logs shipped to Loki; Grafana dashboards are a future add. |

## 2.4 Common conventions (every module obeys these)

| Convention | Rule |
|---|---|
| **API base** | All endpoints under `/api/v1`. The public surface is a separate gateway. |
| **IDs** | UUID primary keys (`gen_random_uuid()`); never expose sequential ints. |
| **Time** | `TIMESTAMPTZ` stored in **UTC**; serialised as ISO-8601 (`2026-06-16T09:30:00Z`). The client converts to local time for display. |
| **Money** | `NUMERIC` BDT. Internal precision `NUMERIC(18,4)` (`final_commission.commission_amount`); disbursed amount `NUMERIC(18,2)` (`ev_disburse.amount_disbursed`). Round **half-up** at the disbursement write. |
| **Soft-delete** | Catalog tables use `is_active`; rows are deactivated, never hard-deleted (BR2). |
| **Auth** | Every `/api/v1/*` request carries a Bearer JWT; the API validates signature + expiry + role on each request. The public gateway uses its own credential. |
| **Auditing** | Every create/update/delete/approve/reject/schedule/run/disburse writes a row to `audit_log` with a JSONB `diff` (BR9). |
| **Error envelope** | All non-2xx responses use one shape (below). |
| **Naming** | DB: `snake_case`. C# DTOs: `PascalCase` properties serialised to `camelCase` JSON. The reserved word `user` is always quoted as `"user"` in SQL. |
| **Pagination** | List endpoints accept `?page=&pageSize=` (default 25, max 100) and return `{ items, page, pageSize, total }`. |

**Canonical error envelope (every error response):**

```json
{
  "error": {
    "code": "REPORT_NAME_TAKEN",
    "message": "A report named 'RSO Apr26' already exists.",
    "details": [ { "field": "reportName", "issue": "must be unique" } ],
    "traceId": "0HMVABC123"
  }
}
```

**Standard status codes:** `200` OK, `201` Created (with `Location`), `202` Accepted (async job queued), `204` No Content, `400` validation, `401` no/invalid JWT, `403` role denied, `404` not found, `409` conflict (unique/idempotency/state), `422` business-rule violation, `500` server error.

---

# §13. Module & Service Architecture

This section maps the architecture onto concrete code: the .NET 4-layer back-end (one controller per feature), the Next.js folder layout, and the Hangfire workers.

## 13.1 .NET back-end — 4-layer clean architecture

The solution is four projects. Dependencies point **inward only**: `Api → Application → Domain ← Infrastructure`. The Domain has no outward dependencies.

```
SalesCom.sln
├── SalesCom.Api               (presentation: controllers, middleware, DI wiring, Hangfire host)
├── SalesCom.Application       (use-cases: services, DTOs, validators, RabbitMQ contracts, interfaces)
├── SalesCom.Domain            (entities matching the 22 tables, enums, business-rule logic, interfaces)
└── SalesCom.Infrastructure    (EF Core DbContext + migrations, Dapper queries, repos, external clients,
                                 RabbitMQ publisher, SeaweedFS client, Hangfire job implementations)
```

### Per-layer responsibility

| Layer | Contains | Depends on | Must NOT contain |
|---|---|---|---|
| **Api** | Controllers (one per feature), the JWT auth middleware, the global exception→error-envelope filter, the audit filter, Swagger, DI registration, the Hangfire server bootstrap. | Application | Business logic, SQL, EF entities directly. |
| **Application** | Feature **services** (the use-cases), **request/response DTOs**, **FluentValidation** validators, **RabbitMQ message contracts**, repository **interfaces**, the role/permission resolver. | Domain | EF Core, Dapper, HttpClient, RabbitMQ concrete types. |
| **Domain** | Entities (one per DDL table), enums (run status, approval status, disburse status…), value objects, pure business-rule helpers (e.g. "can this user decide this level?"). | nothing | Persistence or framework code. |
| **Infrastructure** | `SalesComDbContext` (EF Core, mapped to the `salescom` schema), EF **migrations**, Dapper read repos, external clients (Central Login, EV, SMS, SMTP, SeaweedFS), the RabbitMQ publisher/consumer, the Hangfire job classes. | Application, Domain | HTTP controllers. |

### One controller per feature (the controller map)

Each controller is thin: validate → call an Application service → map to the response DTO. The build order in §14.5 follows this list top-to-bottom.

| Controller | Route prefix | Backing service | Roles | Covers |
|---|---|---|---|---|
| `AuthController` | `/api/v1/auth` | `AuthService` | anonymous → all | SSO start, OTP verify, token refresh, `/me`, logout. |
| `UserController` | `/api/v1/users` | `UserService` | Admin | List/get users; assign rights; (sync is a worker). |
| `DataSourceController` | `/api/v1/data-sources` | `DataSourceService` | Admin (write), Maker (read) | Register / activate / deactivate source tables (BR2). |
| `SupportingUploadController` | `/api/v1/reports/{id}/uploads` | `SupportingUploadService` | Maker | Upload CSV → SeaweedFS + `uploads.*` table registration. |
| `ReportController` | `/api/v1/reports` | `ReportService` | Maker (write), all (read) | The wizard: draft, save-step, final-save (triggers SQL Generator), schedule on/off, list/get. |
| `RunController` | `/api/v1/reports/{id}/runs` | `RunService` | Maker (trigger), all (read) | Run-now / demo / cancel; run + per-stage status; output downloads. |
| `ApprovalController` | `/api/v1/approvals` | `ApprovalService` | Checker/Admin (decide), Maker (initiate/view) | The approval queue; approve / reject / changes-requested. |
| `ApprovalFlowController` | `/api/v1/approval-flows` | `ApprovalFlowService` | Admin | Define flows, levels, level-users, types. |
| `DisbursementController` | `/api/v1/runs/{runId}/disbursement` | `DisbursementService` | Maker/Admin (trigger), all (read) | Trigger EV / POS after approval (BR8); status; CSV download. |
| `DashboardController` | `/api/v1/dashboard` | `DashboardService` (Dapper) | all | Rollups: runs, commissions, login attempts, queue depth. |
| `NotificationController` | `/api/v1/notifications` | `NotificationService` | all (own), Admin (all) | Read notification log; resend (Admin). |
| `AuditController` | `/api/v1/audit` | `AuditService` (Dapper) | Admin | Filtered audit-log read. |

> **Role enforcement (BR1):** an `[Authorize(Roles="...")]` attribute (or a custom `RequirePermission` filter that reads `user_right`) guards every action; the resolver maps the user's `user_right` bits → {Maker, Checker, Administrator}. Administrator is a superset of Maker+Checker. Reads that should be owner-scoped (e.g. "my reports", "my approval queue") filter by the JWT's user id in the service.

### Cross-cutting Api middleware (in pipeline order)

1. **Request logging** (Serilog enrich: traceId, user, route).
2. **JWT authentication** — validate signature/expiry; reject `401` if missing/invalid.
3. **Authorization** — role/permission check; `403` on deny.
4. **Model validation** — FluentValidation; `400` with the error envelope on failure.
5. **Audit filter** — after a successful mutating action, write `audit_log` (action, entity_type, entity_id, diff).
6. **Exception filter** — map any unhandled exception/known domain exception to the canonical error envelope.

## 13.2 Front-end — Next.js folder map

App Router layout. The front-end never builds SQL and never holds DB credentials — it only calls `/api/v1/*` with the JWT.

```
salescom-web/
├── app/
│   ├── (auth)/
│   │   ├── login/page.tsx              # SSO start
│   │   └── otp/page.tsx                # OTP entry
│   ├── (app)/                          # authenticated shell (sidebar + topbar guard)
│   │   ├── layout.tsx                  # reads /auth/me, gates by role, renders nav
│   │   ├── dashboard/page.tsx
│   │   ├── reports/
│   │   │   ├── page.tsx                # report list
│   │   │   ├── new/                    # the 5-step wizard
│   │   │   │   ├── layout.tsx          # wizard shell + stepper + draft autosave
│   │   │   │   ├── step-1-basics/page.tsx        # name, channel, cycle, dates, recurrence
│   │   │   │   ├── step-2-sources/page.tsx       # pick data sources + upload CSVs
│   │   │   │   ├── step-3-blocks/page.tsx        # build achievement/incentive blocks (IR)
│   │   │   │   ├── step-4-mapping/page.tsx       # final_mapping + payout (EV/POS) + approval flow
│   │   │   │   └── step-5-review/page.tsx        # review → Final Save → demo run
│   │   │   └── [id]/
│   │   │       ├── page.tsx            # report detail
│   │   │       ├── runs/page.tsx       # run history + per-stage status
│   │   │       └── edit/...            # re-enter wizard
│   │   ├── approvals/page.tsx          # checker queue
│   │   ├── disbursement/page.tsx
│   │   └── admin/
│   │       ├── users/page.tsx
│   │       ├── data-sources/page.tsx
│   │       └── approval-flows/page.tsx
│   └── api/                            # (optional) thin BFF route handlers / proxy only
├── components/
│   ├── wizard/                         # StepBasics, SourcePicker, BlockBuilder, StageEditor, FinalMapping
│   ├── ir/                             # IR editor widgets: FilterRow, JoinRow, SummarizeRow, IfCaseEditor
│   ├── runs/                           # RunStatusTimeline, StageRowCounts (guardrail G3 display)
│   ├── approvals/                      # ApprovalCard, DecisionDialog
│   └── ui/                             # design-system primitives (table, dialog, toast, stepper)
├── lib/
│   ├── api/                            # generated/typed clients, one file per controller (authApi, reportApi…)
│   ├── auth/                           # JWT storage, refresh, role helpers (useRole, <RequireRole/>)
│   ├── ir/                             # TS types mirroring the IR schema + client-side IR validators
│   └── format/                         # money/date/UTC↔local formatters
├── types/                              # shared DTO TS types (mirror the .NET DTOs)
├── hooks/                              # useReportDraft (autosave), useRunPolling, useApprovalQueue
├── public/
├── next.config.js
├── tsconfig.json
└── package.json
```

**Front-end conventions:**

- **DTO parity:** `types/` mirror the .NET DTOs exactly (camelCase JSON). Keep them in sync — a mismatch is a bug.
- **Role gating:** `<RequireRole roles={['MAKER','ADMIN']}>` wraps screens; the real enforcement is server-side (the FE gate is UX only).
- **Wizard state:** held in a draft (autosaved per step via `PUT /reports/{id}` → see §13.1 ReportController). The IR is assembled client-side from the block builder and POSTed as `definition`.
- **Run polling:** `useRunPolling` polls `GET /reports/{id}/runs/{runId}` until terminal; shows per-stage row counts (guardrail **G3**).
- **All times** rendered via the `format` helpers (UTC → Asia/Dhaka for display).

## 13.3 Hangfire background workers

Hangfire runs in-process on the App tier (server bootstrapped in `SalesCom.Api`; job classes in `SalesCom.Infrastructure`). Jobs are **idempotent** — re-running a job must not double-pay or duplicate, which the DDL's UNIQUE constraints guarantee.

| Worker | Type | Trigger | Does |
|---|---|---|---|
| **ScheduleTriggerJob** | Recurring (cron) | Per `recurrent_type` (DAILY/WEEKLY/MONTHLY) on reports with `status='ON'` | Creates a `report_run` (`run_type=FINAL`, `triggered_by=SYSTEM`) and enqueues it on the run queue (priority **low**). |
| **RunDispatchJob** | Queue consumer | A `report_run` is queued (run-now / demo / schedule) | Honours **D1 single-run + priority queue** (RunNow=high, Demo=mid, Schedule=low): picks the highest-priority pending run **only when no run is RUNNING**, publishes the `run.execute` message to the Python Executor, flips status `QUEUED→RUNNING`. |
| **EvDisbursementWorker** | Queue consumer | Disbursement triggered after full approval (BR8) | For each `final_commission` row of the run: creates/updates `ev_disburse`, calls the EV API (ICD #2), records `provider_txn_id`, then sends the recipient SMS and updates `sms_status`. Retries failed rows (status `RETRYING`). |
| **PosCsvJob** | Fire-and-forget | POS disbursement triggered after approval | Builds the CSV from `final_commission`, uploads to SeaweedFS, writes `pos_disbursement` (`dump_status=GENERATED→HANDED_OFF`). |
| **NotificationDeliveryJob** | Recurring (short interval) | `notification_log` rows with `status='PENDING'` | Sends each via SMS gateway or SMTP; updates `status`/`attempt_count`/`error_message`. |
| **StaleRunSweepJob** | Recurring | Every few minutes | Finds `report_run.run_status='RUNNING'` past a max duration (or orphaned by a crash); marks `FAILED` with `error_message`, frees the single-run slot, triggers temp-table cleanup. |
| **TempCleanupJob** | Fire-and-forget | After a run ends (success or fail) | Drops the run's `runtmp.*` temp tables; sets `run_stage.cleanup_status='DONE'`. |
| **UserSyncJob** | Recurring (hourly) | Hourly | Pulls the user directory from Central Login; upserts `"user"`; deactivates (`is_active=false`) users no longer present. |

> **D1 reminder:** at the real scale (30–50 concurrent users, ~200 commission runs/month) the **single-run-at-a-time** model with a priority queue is correct. `RunDispatchJob` must never start a second run while one is `RUNNING`. Do **not** add parallel-run complexity.

---

# §14. Getting Started / Dev Setup

This section gets a new engineer from a clean machine to a running stack, then prescribes the **build order**.

## 14.1 Repository layout

Three repos (or one monorepo with these top-level folders):

```
salescom/
├── salescom-api/        # .NET solution (the 4 projects from §13.1)
├── salescom-engine/     # Python calc engine (SQL Generator + Executor)
│   ├── salescom_engine/
│   │   ├── generator/   # IR validation + SQLGlot AST builder → stages
│   │   ├── executor/    # snapshot → allowlist re-parse → run stages → result rows
│   │   ├── ir/          # IR dataclasses + JSON Schema validator
│   │   ├── safe_sql/    # allowlist re-parse, identifier whitelist, bound literals
│   │   ├── messaging/   # RabbitMQ consumer/publisher
│   │   └── db/          # SQLAlchemy engine, least-privilege connection
│   ├── tests/
│   ├── pyproject.toml
│   └── Dockerfile
├── salescom-web/        # Next.js front-end (§13.2)
├── deploy/
│   ├── docker-compose.dev.yml   # local: postgres, rabbitmq, seaweedfs, api, engine, web
│   ├── .env.example
│   └── sql/                     # the canonical DDL + seed scripts (run by EF or psql)
└── docs/                        # this LLD, the IR spec, the DDL, the catalog
```

## 14.2 Local infrastructure (docker-compose)

For local dev, everything except (optionally) Postgres runs in `docker-compose.dev.yml`:

| Service | Image / port | Notes |
|---|---|---|
| `postgres` | Percona PostgreSQL 18 → `5432` | The `salescom` schema; create `uploads` and `runtmp` schemas too. (Prod DB is bare-metal, not Docker.) |
| `rabbitmq` | rabbitmq:management → `5672` / `15672` | Management UI on 15672. |
| `seaweedfs` | seaweedfs (S3 mode) → `8333` (S3) | Buckets: `uploads`, `outputs`, `pos`. |
| `api` | built from `salescom-api` → `5080` | The .NET Web API + Hangfire server. |
| `engine` | built from `salescom-engine` | RabbitMQ consumer; least-privilege DB role. |
| `web` | built from `salescom-web` → `3000` | Next.js; `NEXT_PUBLIC_API_BASE` → `http://localhost:5080/api/v1`. |

Bring it up:

```bash
cd deploy
cp .env.example .env          # fill in the values from §14.3
docker compose -f docker-compose.dev.yml up -d postgres rabbitmq seaweedfs
# apply schema + seed (see §14.5 build order, steps 1–2), then:
docker compose -f docker-compose.dev.yml up -d api engine web
```

## 14.3 Environment variables

Single `.env` consumed by compose; each service reads only its own keys.

```ini
# ── Database ───────────────────────────────────────────────
PG_HOST=postgres
PG_PORT=5432
PG_DB=salescom
# App role: full DML on salescom.* (used by .NET)
PG_APP_USER=salescom_app
PG_APP_PASSWORD=change_me
# Executor role: SELECT + CREATE/DROP TEMP only; NO write on money tables (D2)
PG_ENGINE_USER=salescom_engine
PG_ENGINE_PASSWORD=change_me

# ── RabbitMQ ───────────────────────────────────────────────
RABBIT_HOST=rabbitmq
RABBIT_PORT=5672
RABBIT_USER=salescom
RABBIT_PASSWORD=change_me
RABBIT_VHOST=/salescom
# queues: run.execute  run.result  notify.send

# ── SeaweedFS (S3) ─────────────────────────────────────────
S3_ENDPOINT=http://seaweedfs:8333
S3_ACCESS_KEY=change_me
S3_SECRET_KEY=change_me
S3_BUCKET_UPLOADS=uploads
S3_BUCKET_OUTPUTS=outputs
S3_BUCKET_POS=pos

# ── Auth / JWT ─────────────────────────────────────────────
JWT_ISSUER=salescom
JWT_AUDIENCE=salescom-web
JWT_SIGNING_KEY=change_me_long_random
JWT_EXPIRY_HOURS=24
CENTRAL_LOGIN_BASE=https://blposapi.banglalink.net

# ── External systems (ICDs) ────────────────────────────────
EV_API_BASE=http://10.13.2.7:9898
SMS_GATEWAY_HOST=172.16.7.210
SMS_GATEWAY_PORT=13082
SMTP_HOST=...
SMTP_PORT=587
SMTP_USER=...
SMTP_PASSWORD=...

# ── Web ────────────────────────────────────────────────────
NEXT_PUBLIC_API_BASE=http://localhost:5080/api/v1

# ── Observability ──────────────────────────────────────────
LOKI_URL=http://loki:3100
SERILOG_MINIMUM_LEVEL=Information
```

> **Two DB roles, on purpose (D2):** `salescom_app` (the .NET role) has full DML on `salescom.*`. `salescom_engine` (the Python Executor role) has `SELECT` on source/`uploads.*` and `CREATE/DROP TEMP TABLE` on `runtmp`, but **no INSERT/UPDATE** on `final_commission`, `ev_disburse`, `pos_disbursement`. This is what makes the trusted-path rule enforceable at the database level.

## 14.4 Local wiring — how the services talk

The end-to-end wiring a new dev must understand:

```
[Next.js :3000]
   │  HTTPS, Bearer JWT
   ▼
[.NET Web API :5080] ──EF Core/Dapper──> [Postgres salescom.*]      (CRUD, state machines)
   │                  ──S3 SDK──────────> [SeaweedFS]                (uploads, outputs, POS CSV)
   │
   │  Final Save:  publish  ir.compile        ─┐
   │  Run trigger: publish  run.execute        │  (AMQP, RabbitMQ)
   ▼                                           ▼
[RabbitMQ :5672] <───────────────────> [Python Engine]
                                          ├─ Generator: consume ir.compile → write salescom.stages
                                          └─ Executor:  consume run.execute → snapshot run_stage,
                                                        run stages on runtmp.*, publish run.result
   ▲                                           │
   └─ run.result (channel_code, amount rows) ──┘
   │
[.NET Web API] consumes run.result → trusted-path INSERT into final_commission
```

**RabbitMQ message contracts (the three core messages):**

```jsonc
// 1) Web API → Engine (Generator) — at Final Save
// queue: ir.compile
{ "reportSetupId": "uuid", "irVersion": "1.0", "requestedBy": "user_name", "traceId": "..." }
// Generator reads report_setup.definition, validates, compiles, writes salescom.stages,
// replies on ir.compile.result: { "reportSetupId", "status": "OK|INVALID", "stageCount", "errors": [] }

// 2) Web API → Engine (Executor) — at run trigger
// queue: run.execute
{ "reportRunId": "uuid", "reportSetupId": "uuid", "runType": "DEMO|FINAL", "traceId": "..." }
// Executor snapshots stages→run_stage, runs each stage, records row_count + output files.

// 3) Engine (Executor) → Web API — when the run finishes
// queue: run.result
{ "reportRunId": "uuid", "status": "COMPLETED|FAILED", "errorMessage": null,
  "resultTempTable": "runtmp.run_<id>_final",   // (channel_code, commission_amount)
  "stageRowCounts": [ { "stageOrder": 1, "rowCount": 12000 }, ... ] }
// Web API then does the trusted-path write into final_commission and updates report_run.
```

(A fourth queue, `notify.send`, decouples notification delivery; the Web API writes `notification_log` and the worker drains it.)

## 14.5 EF Core migration command

The relational schema is owned by EF Core in `SalesCom.Infrastructure`. The migration **must reproduce the canonical DDL** (`Reconciled_Canonical_DDL.md`) — same table names, columns, constraints, and the deferred FK from §3.9.0.

```bash
# from salescom-api/
# 1) create the initial migration (entities are pre-written to match the DDL)
dotnet ef migrations add InitialSchema \
  --project src/SalesCom.Infrastructure \
  --startup-project src/SalesCom.Api \
  --output-dir Persistence/Migrations

# 2) apply it to the database
dotnet ef database update \
  --project src/SalesCom.Infrastructure \
  --startup-project src/SalesCom.Api
```

> **Important migration notes:**
> - Set `search_path`/default schema to `salescom`; create `uploads` and `runtmp` schemas (the engine creates temp tables there).
> - Enable `pgcrypto` and use `gen_random_uuid()` defaults (per the DDL §3.0).
> - The one **forward-reference FK** (`report_setup.approval_flow_id → approval_flow`) must be added **after** both tables exist — model it as a deferred `ALTER TABLE` so the migration ordering matches §3.9.0.
> - Quote the reserved word `"user"` (configure the entity's table name explicitly).
> - After `database update`, run the **seed script** (§14.5 step 2) for lookup data.
> - For a hand-run `.sql` install instead of EF, execute `deploy/sql/` in DDL order, then the deferred FK, then seeds.

## 14.6 Recommended BUILD ORDER

Build modules in this order. Each step is usable/testable before the next begins; later steps depend on earlier ones. This is also the Phase-1 delivery order.

| # | Module | Why this order / depends on | "Done when…" |
|---|---|---|---|
| **1** | **Schema + migrations** | Everything reads/writes the DB. Apply the canonical DDL via EF (§14.5). | All 22 tables + constraints + indexes exist; deferred FK applied. |
| **2** | **Auth** (`AuthController` + JWT + `/auth/me`) | Every other endpoint needs a JWT + role. Wire Central Login SSO+OTP → SalesCom JWT; the `UserSyncJob`. | A user can sign in, get a 1-day JWT, and `/auth/me` returns role. |
| **3** | **Seed / lookup data** | Reports/approvals/disbursement reference lookups. Seed `recurrent_type`, `channel`, `approval_type` (with `phase`), notification templates, and the `user_right`→role mapping. | Lookups queryable; reference data stable. |
| **4** | **Data source registration** (`DataSourceController`) | The wizard Step 2 picks from registered sources. Enforce BR2 (deactivate, never delete). | Admin can register/activate/deactivate source tables. |
| **5** | **Supporting upload** (`SupportingUploadController`) | Wizard Step 2 also uploads CSVs → SeaweedFS + `uploads.*` tables. Needs SeaweedFS wired. | A CSV upload lands in S3 and creates a typed `uploads.*` table + `report_supporting_upload` row. |
| **6** | **Wizard / IR** (`ReportController`) | Produces `report_setup` + the `definition` IR. Depends on sources (4) + uploads (5). Client-side IR build (§13.2) + server-side JSON-Schema validation. | Maker can complete all 5 steps and save a valid IR draft; BR3/BR4 enforced. |
| **7** | **SQL Generator** (Python) | At Final Save, compiles the IR → `stages`. Depends on the IR (6). Validate IR against the schema; SQLGlot AST build; stage decomposition; `output_table_name` naming. | Final Save produces correct `stages.sql_text` rows; invalid IR is rejected with clear errors. |
| **8** | **Run executor** (Python) + **RunController** | Runs the stages. Depends on `stages` (7). Snapshot → `run_stage`; allowlist re-parse; run on `runtmp.*`; record row counts (G3); publish `run.result`; trusted-path `final_commission` write. Honour **D1** single-run queue. | Demo and Final runs complete; per-stage row counts shown; `final_commission` populated. |
| **9** | **Approval** (`ApprovalController` + `ApprovalFlowController`) | Gates disbursement (BR8). Depends on runs (8). Sequential ascending walk (BR6), reject-needs-comment (BR7), maker≠checker (BR5). | A Final run can be routed through its flow to APPROVED/REJECTED with a full decision trail. |
| **10** | **Disbursement** (`DisbursementController` + EV/POS workers) | Only after full approval (BR8). Depends on approval (9). EV worker (API + SMS + `provider_txn_id`), POS CSV job; EV/POS mutual exclusion (`ck_report_setup_payout_xor`). | An approved run pays out via EV (with SMS) or produces a POS CSV; idempotent (no double-pay). |
| **11** | **Dashboard + Notifications** (`DashboardController`, `NotificationController`, delivery worker) | Read-side + comms; depends on all the above producing data. Dapper rollups; `notification_log` delivery. | Dashboards show run/commission/login rollups; notifications deliver and are logged. |

> **Phasing:** steps 1–11 deliver the **whole system + single & multi-KPI runs (Phase 1)**. Multi-KPI works in Phase 1 via the block-join rule and guardrails G1–G4 (IR spec §5). **Phase 2/3** add operation types to the Generator/Executor (weighted pools, VLR gate, `rank`/window functions, historical reads) — **no schema or IR-shape change**, so steps 1–6 and 9–11 are untouched; only steps 7–8 gain new op handlers.

---

## Appendix — pointers to companion sections

- **IR contract** (the `definition` JSONB, stages, multi-KPI join rule, guardrails G1–G4, JSON Schema, worked example): `IR_Schema_and_MultiKPI_Join.md`.
- **Database schema** (all 22 tables, constraints, indexes, fix log): `Reconciled_Canonical_DDL.md` §3.
- **Real commission patterns** (what the engine must express): `Commission_Logic_Catalog.md`.
- **Deeper design** (security, ICDs, DR, run orchestration rationale): `Salescom_System_Design_Document.md`.
