# SalesCom — Low-Level Design (LLD)

**Banglalink Sales Commission Automation Platform**

| | |
|---|---|
| **Document** | Low-Level Design (LLD) |
| **Version** | 2.0 — FINAL |
| **Date** | 17 June 2026 |
| **Status** | Build-ready |
| **Grounding** | This LLD is grounded in the **real implemented database schema** `salescomdbtst` (see §3). All table and column names, key types, and enum codes match that schema exactly. |
| **Audience** | Frontend (Next.js/React/TS), Backend (.NET / ASP.NET Core, Dapper + EF Core), Calc-engine (Python), DBA, QA. |

> **⚠️ §15 (Errata & Reconciliation) is authoritative.** A final consistency pass flagged a few cross-section gaps (phase-aware reject, `channel_code`→MSISDN for EV/SMS, and HTTP-code / enum / pagination drift). Where §1–§14 conflict with §15, **§15 wins** — apply §15 when writing the code.

---

## Table of Contents

- **§1 Introduction** — purpose, scope, audience, definitions
- **§2 System Architecture** — layered overview, components, tech stack, common conventions
- **§3 Database Schema** — authoritative `salescomdbtst` DDL (20 tables), relationships, compact reference
- **§4 Authentication & Session** — Central Login SSO + OTP → JWT, RBAC, hourly sync
- **§5 Data Source Management** — registry + supporting CSV → DB ingestion
- **§6 Report Management** — the 5-step wizard, save/publish state machine, run lifecycle
- **§7 Approval (Maker-Checker)** — flows, levels, sequential approval, reject→Maker
- **§8 Dashboard** — read-only role-scoped views
- **§9 Notification** — SMS + Email outbox
- **§10 Disbursement** — EV (auto + SMS) / POS (CSV), reconciliation
- **§11 Asynchronous Services & Events** — RabbitMQ topology, Python services, Hangfire workers
- **§12 Cross-cutting Concerns** — audit, error envelope, pagination, retention
- **§13 Module & Service Architecture** — .NET layering, workers, FE module map
- **§14 Getting Started / Dev Setup** — repo layout, env, wiring, build order
- **Appendix A** — IR reference (points to `IR_Schema_and_MultiKPI_Join.md`)
- **Appendix B** — Enum & State-Transition reference
- **Appendix C** — Business Rules (BR1–BR9) + enforcement points

---

# §1 Introduction

## 1.1 Purpose

This Low-Level Design (LLD) is the **single, build-ready specification** for the Banglalink **SalesCom — Sales Commission Automation Platform**. It is written for a development team starting with zero prior context — Frontend (Next.js/React/TypeScript), Backend (.NET / ASP.NET Core), and Python (SQL Gen Engine). For every feature it covers the screens, the exact database tables and columns (§3), the step-by-step process logic, the API contracts, and the validation rules. A developer should be able to build a module from its section alone.

SalesCom replaces the old manual SQL / SRF commission process. A Business User (Maker) builds a commission report through a **5-step no-code wizard**. At **Final Save**, the SQL Gen Engine compiles the configuration into SQL statements. A run trigger (schedule / demo / run-now) drives the SQL Executor, which runs the SQL step by step and produces the commission amount for each recipient. That output flows through a **sequential maker–checker approval**, then is paid via **EV** (automatic + SMS) or **POS** (CSV handoff).

## 1.2 Scope

This LLD covers the **whole platform**, end to end:

- **Authentication & session** — Central Login SSO + OTP → SalesCom JWT (3-hour inactivity logout), hourly user sync, login attempt logging.
- **Data source management** — registering the source tables a report may read. A data source is never deleted, only deactivated.
- **Report management** — the 5-step wizard, supporting-file uploads, and demo / run-now / scheduled runs.
- **SQL Gen Engine** — the Python service that compiles the report configuration into SQL statements, and the SQL Executor that runs those statements step by step to produce per-recipient commission amounts.
- **Approval** — configurable sequential maker–checker flows.
- **Disbursement** — EV (automatic + SMS) and POS (CSV handoff), mutually exclusive per report.
- **Dashboard** — run / approval / disbursement visibility.
- **Notification** — SMS for EV disbursement and ETL status; email and SMS for approval events.
- **Cross-cutting** — audit trail, RBAC, error handling, pagination.

**End-to-end flow:**

```
5-step wizard  →  report configuration saved
              →  Final Save: SQL Gen Engine compiles configuration → SQL statements stored
              →  run trigger (schedule / demo / run-now)
              →  SQL Executor runs step by step
              →  per-recipient commission amounts stored
              →  sequential maker–checker approval
              →  EV (auto + SMS)  OR  POS (CSV handoff)
```

The system is sized for **300–500 total users**, 30–50 peak concurrent, ~200 commission runs/month. This drives the **single-run** execution model (§2, §11) with a priority queue (RunNow=high, Demo=mid, Schedule=low).

**Phasing.** The architecture supports all capabilities from day one, but the build is phased: **Phase 1** = the whole system + single-KPI and multi-KPI commission runs; **Phase 2** = more multi-KPI operation types; **Phase 3** = complex patterns (ranking/quartile, history-read, multipliers). Later phases *add* operation types without redesigning the configuration format or the pipeline.

## 1.3 Definitions and Acronyms

| Term | Meaning |
|---|---|
| **LLD** | Low-Level Design — this document. |
| **SRS / HLD** | Software Requirements Specification / High-Level Design — the higher-level specs this LLD implements. |
| **IR** | Intermediate Representation — the JSON form of a report's configuration, stored in the database. The full IR contract is in `IR_Schema_and_MultiKPI_Join.md` (Appendix A). |
| **Block** | A pipeline of steps computing one performance number (**achievement block**) or one payout (**incentive block**). |
| **Stage** | One operation in a block: filter, combine, summarize, calculate, or modify (Phase 1); plus rank and others in later phases. Each stage is compiled into one SQL statement. |
| **Grain** | The row-identity a block resolves to — for example, "one row per RSO". Fixed by the block's final summarize step. |
| **Grain key** | The column(s) that define the grain (e.g. `RSO_CODE`). Block-to-block joins must be on the grain key (1:1 match). |
| **Maker / Checker** | Business User who builds and submits a report / Approver who reviews it. The same person cannot be both Maker and Checker for the same item. |
| **Demo Run / Final Run** | A preview run — never disbursed, skips pre-run checks — used to verify numbers before committing / The real run whose output is approved and paid. |
| **EV / POS** | Electronic Value (automatic payout + SMS) / Point-of-Sale (CSV handoff). Mutually exclusive per report. |
| **Channel type / channel code** | Channel type is a small lookup (Distributor / RSO / Retailer) — each report has one channel type. Channel code is the individual recipient identifier on each commission row. |
| **SQL Gen Engine** | Python service that compiles the report configuration (IR) into SQL statements. |
| **SQL Executor** | Python service that runs those SQL statements step by step and produces the commission amounts. |
| **SQLGlot** | Python SQL library used to build and validate SQL safely — no string concatenation. |
| **Guardrail (G1–G4)** | Multi-KPI safety checks: pre-join uniqueness (G1), post-join row-count check (G2), demo-run per-step row counts (G3), reconciliation (G4). |
| **OTP / SSO / JWT** | One-Time Password (2nd factor) / Single Sign-On (Central Login) / JSON Web Token (SalesCom session; 3-hour inactivity logout). |

---

# §2 System Architecture

## 2.1 Architecture Overview (layered)

SalesCom is a **layered, service-oriented** system. The request path is kept **thin**: the .NET API validates, persists state, and **publishes an event** — heavy or timed work (SQL generation, run execution, disbursement, user sync) happens off the request thread via RabbitMQ + Hangfire. This lets a wizard "Final Save" return instantly while the Python generator compiles SQL in the background, and lets the **single-run priority queue** (RunNow=high, Demo=mid, Schedule=low) serialize execution cleanly.

```
┌──────────────────────────────────────────────────────────────────────────┐
│  PRESENTATION — Next.js / React / TypeScript (App Router)                  │
│  Talks to the backend only over HTTPS /api/v1, carrying the SalesCom JWT.  │
│  No business logic: renders the wizard, lists, approval, dashboard.        │
└───────────────────────────────┬──────────────────────────────────────────┘
                                 │ HTTPS (JWT per request)
┌───────────────────────────────▼──────────────────────────────────────────┐
│  APPLICATION / API — .NET ASP.NET Core, 4 layers                          │
│  • Api          — controllers, DTOs, JWT + RBAC middleware, DI, Hangfire   │
│  • Application  — use-case handlers, validators, ports (interfaces)        │
│  • Domain       — entities, int enums, pure business rules                 │
│  • Infrastructure — Dapper + EF Core, SeaweedFS, RabbitMQ, Central Login,  │
│                     SMS/SMTP, Hangfire jobs                                │
│  Owns: auth, wizard save, the IR, trusted-path writes (final_commissions,  │
│         ev_disburse), approval state machine, notifications.              │
└───────┬───────────────────────────────┬─────────────────────┬────────────┘
        │ Dapper / EF Core              │ publish/consume       │ S3 API
┌───────▼──────────┐   ┌────────────────▼───────────┐   ┌──────▼───────────┐
│  PostgreSQL      │   │  RabbitMQ (event bus)       │   │  SeaweedFS (S3)  │
│  Percona PG 18   │   │  q.sql-generate             │   │  uploads,        │
│  schema          │   │  q.run.high/mid/low         │   │  stage outputs,  │
│  salescomdbtst   │   │  q.ev-disburse              │   │  stage outputs   │
│  (21 tables) +   │   └────────────┬───────┬────────┘   └──────────────────┘
│  per-run temp    │                │       │
└──────────────────┘   ┌────────────▼──┐ ┌──▼──────────────────────┐
                       │ SQL Generator │ │ SQL Executor            │  (Python:
                       │ IR → SQL →    │ │ run_stages → output →   │   SQLGlot +
                       │ section_wise_ │ │ final_commissions       │   SQLAlchemy)
                       │ report_sqls   │ └─────────────────────────┘
                       └───────────────┘

  Hangfire workers (in the .NET app): Scheduler, EV Disbursement, Notification.
  POS disbursement and user sync run via Airflow (daily / hourly schedule).

  EXTERNAL SYSTEMS (7 integration points, contracts-first):
  Central Login (SSO+OTP) · EV API (10.13.2.7:9898) · SMS (172.16.7.210:13082) ·
  Email/SMTP · Source systems via Airflow ETL (DWH/In-house/vPeople/POSDMSDB) ·
  POS (CSV handoff) · RSO App (consumes the public API).
```

Everything is deployed in **Docker** except the production database (bare-metal Percona PostgreSQL 18).

## 2.2 Component Responsibilities

| Component | Responsibility |
|---|---|
| **Web app (Next.js)** | Render UI; collect wizard input; call `/api/v1`; hold the JWT; show demo previews, run status, approval queues, dashboard. |
| **.NET API (4 layers)** | Auth & JWT issuance; CRUD for data sources, reports, flows; **own the IR** (validate shape before save into `report_setups.definition`); **trusted-path writes** of `final_commissions` and `ev_disburse`; approval state machine (`report_approvals.overall_status`); publish RabbitMQ events; host Hangfire workers; send SMS/email. |
| **SQL Generator (Python)** | Consume `report.saved`; compile the IR into per-stage SQL using **SQLGlot AST builders** (never string concat); write `section_wise_report_sqls` (one row per `stage_order`, frozen at Final Save). |
| **SQL Executor (Python)** | Consume `run.requested`; snapshot `section_wise_report_sqls` → `run_stages` (frozen `sql_text` per stage); **re-parse each stage's SQL through the allowlist** (only `SELECT/WITH/JOIN`, aggregates, `CASE`, and `CREATE/DROP TEMP TABLE`); execute stage-by-stage with a **least-privilege** DB role; create per-stage temp/output tables (`run_stages.output_table_name`), record row counts (guardrails G1–G4); hand the last stage's output to the .NET trusted path for `final_commissions`. |
| **RabbitMQ** | Decouple API from workers; carry `report.saved` (triggers SQL generation), `run.requested` (priority queues: RunNow/Demo/Schedule), `run.completed`, `ev.disburse` (triggers per-recipient EV payout). |
| **SeaweedFS (S3)** | Store raw uploads, per-stage outputs. |
| **Hangfire workers** | **Scheduler** (publish `run.requested` for due scheduled reports), **EV Disbursement** (on approval-complete, drive EV payout via `ev.disburse` queue), **Notification** (send SMS/email for approval events). |
| **Airflow (ETL + POS)** | Daily POS disbursement job (generate CSV, hand off to POS); hourly user sync from the **POS system** (direct DB connection → dump → upsert `users` + `user_rights`; Central Login handles auth only, no rights API); bring source data (DWH, In-house, vPeople, POSDMSDB) into prepared `data_sources` tables. |
| **PostgreSQL (Percona 18)** | Authoritative store: schema `salescomdbtst`, 21 tables + per-run temp tables; JSONB IR; hot (3-month) + archive split. |

## 2.3 Technology Stack

| Layer | Technology | Rationale |
|---|---|---|
| Frontend | **TypeScript · React · Next.js** (App Router) | Type-safe UI; server + client components. The wizard is stateful and form-heavy — React fits. |
| Backend API | **C# · .NET (ASP.NET Core)** | Strong typing, mature DI, first-class background-job and messaging ecosystem; 4-layer clean architecture isolates the IR / trusted-path logic from infrastructure. |
| Data access | **Dapper + EF Core** | Dapper for fast hand-tuned reads (lists, dashboard, big joins); **EF Core for the ORM + migrations** (schema as code, owns the `salescomdbtst` DDL). |
| Background jobs | **Hangfire** (PostgreSQL-backed) | Scheduling, retries, timed jobs (scheduler, disbursement, user sync) without a separate scheduler product. |
| Calc engine | **Python + SQLGlot + SQLAlchemy** | SQLGlot builds SQL as an **AST** (no string concatenation → no injection) and re-parses it to enforce a **read-only allowlist**. Python is the natural home for the dynamic IR→SQL compiler. |
| Event bus | **RabbitMQ** | Priority queues give the locked single-run model (RunNow > Demo > Schedule) for free; decouples the thin API from heavy Python work. |
| Database | **PostgreSQL (Percona 18, bare-metal)** | Window functions (`NTILE`, `PERCENT_RANK`) for Phase-3 ranking; JSONB for the IR; temp tables for stage outputs; `NUMERIC` for exact money. |
| Object storage | **SeaweedFS (S3-compatible)** | Self-hosted S3 for uploads, stage outputs, POS CSVs; no cloud dependency. |
| Auth | **Central Login SSO + OTP → SalesCom JWT** (3-hour inactivity logout) | Internal-only; reuses the company IdP; SalesCom issues its own JWT and never exposes the IdP token to the browser. |
| ETL | **Apache Airflow** (shared) | Brings source data (DWH, In-house, vPeople, POSDMSDB) into prepared `data_sources` tables; long cohorts pre-computed here. |
| Deployment | **Docker** (everything except the prod DB) | Each deployable (web, api, calc) ships as a container; local stack via `docker-compose`. |

---

# §3 Database Schema

This section is the authoritative, runnable physical schema for SalesCom. All objects live in the PostgreSQL schema `salescomdbtst`. The DDL below is the real implemented schema — copy it directly.

**Two columns use mixed-case names and must be quoted in SQL:**
- `report_setups."IsSetupComplete"` — use double quotes in every query.
- `final_commissions."Msisdn"` — use double quotes in every query.

**Conventions (apply to every table):**
- **PK** = `id int8 GENERATED BY DEFAULT AS IDENTITY` (bigint identity — NOT `BIGSERIAL`, NOT UUID).
- **FK** columns = `int8` (bigint).
- **Money** = `numeric(18,4)` (BDT).
- **Timestamps** = `timestamptz` (stored UTC). Exception: user-facing date/time inputs such as `report_setups.start_date`, `end_date`, `run_start_date`, `run_end_date`, and `ev_disbursement_time` are stored as `date` or `time` in the DB exactly as the user enters them — no timezone conversion applied.
- Audit columns where present: `created_at` / `updated_at` (timestamptz), `created_by` / `updated_by` (varchar user_name).

### 3.1 Module grouping (21 tables)

| Module | Tables |
|---|---|
| Identity & Access | `users`, `user_rights`, `login_log` |
| Catalog | `data_sources`, `channels` |
| Report | `report_setups`, `report_supporting_uploads`, `section_wise_report_sqls` |
| Run & Output | `report_runs`, `run_stages`, `final_commissions` |
| Approval | `approval_flows`, `approval_flow_levels`, `approval_flow_level_users`, `report_approvals`, `report_approval_details` |
| Disbursement | `ev_disburse`, `pos_disbursement` |
| Notification | `email_notifications`, `sms_notifications` |
| Cross-cutting | `audit_logs` |

> `recurrent_type` and `approval_type` are `int4` enum columns (not separate lookup tables). Email and SMS notifications are stored in separate tables. POS disbursement is triggered by Airflow daily.

---

### 3.2 Identity & Access

```sql
-- ===== users =====
CREATE TABLE salescomdbtst.users (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	user_name varchar(200) NOT NULL,
	user_id varchar(100) NOT NULL,
	full_name varchar(300) NOT NULL,
	mobile_no varchar(30) NOT NULL,
	email varchar(256) NOT NULL,
	department varchar(200) NOT NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NULL,
	created_by varchar(200) NOT NULL,
	updated_by varchar(200) NULL,
	CONSTRAINT "PK_users" PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ux_users_user_id ON salescomdbtst.users USING btree (user_id);

-- ===== user_rights =====
-- rights_code: 10=Maker, 20=Checker, 30=Admin
CREATE TABLE salescomdbtst.user_rights (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	user_id int8 NOT NULL,
	rights_code int4 NOT NULL,
	CONSTRAINT "PK_user_rights" PRIMARY KEY (id),
	CONSTRAINT "FK_user_rights_users_user_id" FOREIGN KEY (user_id) REFERENCES salescomdbtst.users(id)
);
CREATE UNIQUE INDEX ux_user_rights_user_right ON salescomdbtst.user_rights USING btree (user_id, rights_code);

-- ===== login_log =====
-- Every sign-in attempt. login_status: 1=Success, 2=Failed
CREATE TABLE salescomdbtst.login_log (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	user_name varchar(200) NOT NULL,
	full_name varchar(300) NOT NULL,
	login_time timestamptz NOT NULL,
	login_status int4 NOT NULL,
	remarks varchar(500) NOT NULL,
	CONSTRAINT "PK_login_log" PRIMARY KEY (id)
);
```

---

### 3.3 Catalog

```sql
-- ===== channels =====
-- Channel-TYPE lookup: Distributor / RSO / Retailer etc. Small seeded table.
CREATE TABLE salescomdbtst.channels (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	channel_name varchar(200) NOT NULL,
	CONSTRAINT "PK_channels" PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ux_channels_channel_name ON salescomdbtst.channels USING btree (channel_name);

-- ===== data_sources =====
-- Registered source tables the calc engine may read. Never deleted, only deactivated.
CREATE TABLE salescomdbtst.data_sources (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	source_table_name varchar(200) NOT NULL,
	table_description varchar(1000) NULL,
	is_active bool NOT NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NULL,
	created_by varchar(200) NOT NULL,
	updated_by varchar(200) NULL,
	CONSTRAINT "PK_data_sources" PRIMARY KEY (id)
);
CREATE UNIQUE INDEX ux_data_sources_source_table ON salescomdbtst.data_sources USING btree (source_table_name);
```

---

### 3.4 Report

```sql
-- ===== report_setups =====
-- The report definition. definition (JSONB) = the IR (the calc configuration).
-- IMPORTANT: "IsSetupComplete" is mixed-case -- always quote it in SQL queries.
-- is_report_stop: true = report is stopped (replaces the old ON/STOP status column).
-- run_start_date / run_end_date: schedule window for recurring reports.
-- sms_content: template for the EV payout SMS sent to the recipient.
CREATE TABLE salescomdbtst.report_setups (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_name varchar(200) NOT NULL,
	report_type varchar(100) NOT NULL,
	channel_type_id int8 NOT NULL,
	commission_cycle varchar(100) NOT NULL,
	start_date date NOT NULL,
	end_date date NOT NULL,
	"IsSetupComplete" bool NOT NULL,
	is_recurrent bool NOT NULL,
	recurrent_type int4 NOT NULL,
	is_ev_disbursement bool NOT NULL,
	ev_disbursement_time time NULL,
	is_pos_disbursement bool NOT NULL,
	definition jsonb NULL,
	run_start_date timestamptz NULL,
	run_end_date timestamptz NULL,
	is_report_stop bool NOT NULL,
	sms_content text NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NULL,
	created_by varchar(200) NOT NULL,
	updated_by varchar(200) NULL,
	approval_flow_id int8 NOT NULL,
	CONSTRAINT "PK_report_setups" PRIMARY KEY (id),
	CONSTRAINT "FK_report_setups_approval_flows_approval_flow_id" FOREIGN KEY (approval_flow_id) REFERENCES salescomdbtst.approval_flows(id),
	CONSTRAINT "FK_report_setups_channels_channel_type_id" FOREIGN KEY (channel_type_id) REFERENCES salescomdbtst.channels(id)
);
CREATE INDEX "IX_report_setups_approval_flow_id" ON salescomdbtst.report_setups USING btree (approval_flow_id);
CREATE INDEX "IX_report_setups_channel_type_id" ON salescomdbtst.report_setups USING btree (channel_type_id);

-- ===== report_supporting_uploads =====
-- Files uploaded to support a report (e.g. target sheets, config tables). Stored in SeaweedFS.
CREATE TABLE salescomdbtst.report_supporting_uploads (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_setup_id int8 NOT NULL,
	db_table_name varchar(200) NOT NULL,
	db_schema varchar(2000) NOT NULL,
	object_bucket varchar(200) NOT NULL,
	object_key varchar(500) NOT NULL,
	file_name varchar(300) NOT NULL,
	row_count int4 NULL,
	uploaded_at timestamptz NOT NULL,
	uploaded_by varchar(200) NOT NULL,
	CONSTRAINT "PK_report_supporting_uploads" PRIMARY KEY (id),
	CONSTRAINT "FK_report_supporting_uploads_report_setups_report_setup_id" FOREIGN KEY (report_setup_id) REFERENCES salescomdbtst.report_setups(id)
);
CREATE INDEX ix_report_supporting_uploads_report_setup ON salescomdbtst.report_supporting_uploads USING btree (report_setup_id);

-- ===== section_wise_report_sqls =====
-- Compiled SQL for each stage of the report, frozen at Final Save.
-- One row per (report_setup_id, stage_order). Snapshotted into run_stages at run time.
CREATE TABLE salescomdbtst.section_wise_report_sqls (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_setup_id int8 NOT NULL,
	stage_order int4 NOT NULL,
	sql_text text NULL,
	CONSTRAINT "PK_section_wise_report_sqls" PRIMARY KEY (id),
	CONSTRAINT "FK_section_wise_report_sqls_report_setups_report_setup_id" FOREIGN KEY (report_setup_id) REFERENCES salescomdbtst.report_setups(id)
);
CREATE UNIQUE INDEX ux_section_wise_report_sqls_setup_order ON salescomdbtst.section_wise_report_sqls USING btree (report_setup_id, stage_order);
```

---

### 3.5 Run & Output

```sql
-- ===== report_runs =====
-- One row per run attempt. run_type: 1=Demo, 2=Final.
-- run_status: 0=Pending, 1=Running, 2=Completed, 3=Failed
-- disburse_status (varchar): NONE / PENDING / IN_PROGRESS / DONE / FAILED
CREATE TABLE salescomdbtst.report_runs (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_setup_id int8 NOT NULL,
	run_date timestamptz NOT NULL,
	run_type int4 NOT NULL,
	triggered_by varchar(200) NULL,
	run_status int4 NOT NULL,
	disburse_status varchar(50) NOT NULL,
	started_at timestamptz NULL,
	ended_at timestamptz NULL,
	CONSTRAINT "PK_report_runs" PRIMARY KEY (id),
	CONSTRAINT "FK_report_runs_report_setups_report_setup_id" FOREIGN KEY (report_setup_id) REFERENCES salescomdbtst.report_setups(id)
);
CREATE INDEX ix_report_runs_report_setup ON salescomdbtst.report_runs USING btree (report_setup_id);

-- ===== run_stages =====
-- Snapshot of each SQL stage for a specific run (frozen from section_wise_report_sqls).
-- sort_order: execution sequence. cleanup_status: 0=Pending, 1=Done, 2=Skipped.
-- run_status: 0=Pending, 1=Running, 2=Completed, 3=Failed
CREATE TABLE salescomdbtst.run_stages (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	run_id int8 NOT NULL,
	sql_text text NOT NULL,
	sort_order int4 NOT NULL,
	run_status int4 NOT NULL,
	started_at timestamptz NULL,
	ended_at timestamptz NULL,
	document_type varchar(100) NULL,
	bucket varchar(200) NULL,
	object_url varchar(1000) NULL,
	file_name varchar(300) NULL,
	file_generated_at timestamptz NOT NULL,
	output_table_name varchar(200) NULL,
	cleanup_status int4 NOT NULL,
	CONSTRAINT "PK_run_stages" PRIMARY KEY (id),
	CONSTRAINT "FK_run_stages_report_runs_run_id" FOREIGN KEY (run_id) REFERENCES salescomdbtst.report_runs(id)
);
CREATE INDEX ix_run_stages_run ON salescomdbtst.run_stages USING btree (run_id);

-- ===== final_commissions =====
-- Per-recipient commission output. Idempotent: UNIQUE(report_run_id, channel_code).
-- IMPORTANT: "Msisdn" is mixed-case -- always quote it in SQL queries.
-- channel_id = channel TYPE (FK to channels). channel_code = individual recipient key.
CREATE TABLE salescomdbtst.final_commissions (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_run_id int8 NOT NULL,
	channel_id int8 NOT NULL,
	channel_code varchar(100) NOT NULL,
	"Msisdn" text NOT NULL,
	commission_amount numeric(18, 4) NOT NULL,
	CONSTRAINT "PK_final_commissions" PRIMARY KEY (id),
	CONSTRAINT "FK_final_commissions_channels_channel_id" FOREIGN KEY (channel_id) REFERENCES salescomdbtst.channels(id),
	CONSTRAINT "FK_final_commissions_report_runs_report_run_id" FOREIGN KEY (report_run_id) REFERENCES salescomdbtst.report_runs(id)
);
CREATE INDEX ix_final_commissions_channel ON salescomdbtst.final_commissions USING btree (channel_id);
CREATE UNIQUE INDEX ux_final_commissions_run_channel_code ON salescomdbtst.final_commissions USING btree (report_run_id, channel_code);
```

---

### 3.6 Approval

```sql
-- ===== approval_flows =====
CREATE TABLE salescomdbtst.approval_flows (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	flow_name varchar(200) NOT NULL,
	description varchar(1000) NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NULL,
	created_by varchar(200) NOT NULL,
	updated_by varchar(200) NULL,
	CONSTRAINT "PK_approval_flows" PRIMARY KEY (id)
);

-- ===== approval_flow_levels =====
-- approval_type: 1=PRE_RUN (setup approval), 2=POST_RUN (result approval)
CREATE TABLE salescomdbtst.approval_flow_levels (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	approval_flow_id int8 NOT NULL,
	approval_type int4 NOT NULL,
	level_order int4 NOT NULL,
	level_name varchar(200) NOT NULL,
	created_at timestamptz NOT NULL,
	updated_at timestamptz NULL,
	created_by varchar(200) NOT NULL,
	updated_by varchar(200) NULL,
	CONSTRAINT "PK_approval_flow_levels" PRIMARY KEY (id),
	CONSTRAINT "FK_approval_flow_levels_approval_flows_approval_flow_id" FOREIGN KEY (approval_flow_id) REFERENCES salescomdbtst.approval_flows(id)
);
CREATE UNIQUE INDEX ux_approval_flow_levels_flow_order ON salescomdbtst.approval_flow_levels USING btree (approval_flow_id, level_order);

-- ===== approval_flow_level_users =====
-- Checkers assigned to each level. user_id is int8 FK to users.id.
CREATE TABLE salescomdbtst.approval_flow_level_users (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	approval_flow_level_id int8 NOT NULL,
	user_id int8 NOT NULL,
	CONSTRAINT "PK_approval_flow_level_users" PRIMARY KEY (id),
	CONSTRAINT "FK_approval_flow_level_users_approval_flow_levels_approval_flo~" FOREIGN KEY (approval_flow_level_id) REFERENCES salescomdbtst.approval_flow_levels(id),
	CONSTRAINT "FK_approval_flow_level_users_users_user_id" FOREIGN KEY (user_id) REFERENCES salescomdbtst.users(id)
);
CREATE INDEX "IX_approval_flow_level_users_user_id" ON salescomdbtst.approval_flow_level_users USING btree (user_id);
CREATE UNIQUE INDEX ux_approval_flow_level_users_level_user ON salescomdbtst.approval_flow_level_users USING btree (approval_flow_level_id, user_id);

-- ===== report_approvals =====
-- One live approval instance per report_setup (reused across its lifetime).
-- overall_status: 0=Draft/Pending-Edit, 1=Pre-Approval-Pending, 2=Pre-Approved,
--                 3=Post-Approval-Pending, 4=Post-Approved
CREATE TABLE salescomdbtst.report_approvals (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_setup_id int8 NOT NULL,
	approval_flow_id int8 NOT NULL,
	current_level_order int4 NOT NULL,
	overall_status int4 NOT NULL,
	initiated_by varchar(200) NOT NULL,
	initiated_at timestamptz NOT NULL,
	CONSTRAINT "PK_report_approvals" PRIMARY KEY (id),
	CONSTRAINT "FK_report_approvals_approval_flows_approval_flow_id" FOREIGN KEY (approval_flow_id) REFERENCES salescomdbtst.approval_flows(id),
	CONSTRAINT "FK_report_approvals_report_setups_report_setup_id" FOREIGN KEY (report_setup_id) REFERENCES salescomdbtst.report_setups(id)
);
CREATE INDEX ix_report_approvals_flow ON salescomdbtst.report_approvals USING btree (approval_flow_id);
CREATE INDEX ix_report_approvals_report_setup ON salescomdbtst.report_approvals USING btree (report_setup_id);

-- ===== report_approval_details =====
-- One row per individual approve/reject decision.
-- approval_status: 1=Approved, 2=Rejected
CREATE TABLE salescomdbtst.report_approval_details (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	approval_request_id int8 NOT NULL,
	level_order int4 NOT NULL,
	approval_status int4 NOT NULL,
	remarks varchar(1000) NULL,
	approval_by varchar(200) NOT NULL,
	approval_at timestamptz NOT NULL,
	CONSTRAINT "PK_report_approval_details" PRIMARY KEY (id),
	CONSTRAINT "FK_report_approval_details_report_approvals_approval_request_id" FOREIGN KEY (approval_request_id) REFERENCES salescomdbtst.report_approvals(id)
);
CREATE INDEX ix_report_approval_details_approval ON salescomdbtst.report_approval_details USING btree (approval_request_id);
```

---

### 3.7 Disbursement

```sql
-- ===== ev_disburse =====
-- Per-recipient EV payout. Idempotent: UNIQUE(report_run_id, channel_code).
-- ev_msisdn: recipient phone number for the EV API call and the payout SMS.
-- disburse_status: 0=Pending, 1=Sent, 2=Success, 3=Failed, 4=Retry
CREATE TABLE salescomdbtst.ev_disburse (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_run_id int8 NOT NULL,
	channel_code varchar(100) NOT NULL,
	ev_msisdn varchar(15) NOT NULL,
	amount numeric(18, 4) NOT NULL,
	disburse_status int4 NOT NULL,
	disburse_at timestamptz NULL,
	CONSTRAINT "PK_ev_disburse" PRIMARY KEY (id),
	CONSTRAINT "FK_ev_disburse_report_runs_report_run_id" FOREIGN KEY (report_run_id) REFERENCES salescomdbtst.report_runs(id)
);
CREATE INDEX ix_ev_disburse_report_run ON salescomdbtst.ev_disburse USING btree (report_run_id);

-- ===== pos_disbursement =====
-- One row per POS batch. Triggered by Airflow daily job; CSV stored in SeaweedFS.
-- dump_status: 0=Pending, 1=Dumped, 2=Failed
CREATE TABLE salescomdbtst.pos_disbursement (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	report_run_id int8 NOT NULL,
	dump_status int4 NOT NULL,
	disburse_at timestamptz NULL,
	CONSTRAINT "PK_pos_disbursement" PRIMARY KEY (id),
	CONSTRAINT "FK_pos_disbursement_report_runs_report_run_id" FOREIGN KEY (report_run_id) REFERENCES salescomdbtst.report_runs(id)
);
CREATE INDEX ix_pos_disbursement_report_run ON salescomdbtst.pos_disbursement USING btree (report_run_id);
```

---

### 3.8 Notification

```sql
-- ===== email_notifications =====
-- Outbound email queue (approval events). status: 0=Pending, 1=Sent, 2=Failed
CREATE TABLE salescomdbtst.email_notifications (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	to_address varchar(256) NOT NULL,
	cc varchar(1000) NULL,
	bcc varchar(1000) NULL,
	subject varchar(500) NULL,
	body text NOT NULL,
	from_address varchar(256) NULL,
	status int4 NOT NULL,
	attempt_count int4 NOT NULL,
	error_message varchar(2000) NULL,
	sent_at timestamptz NULL,
	created_at timestamptz NOT NULL,
	CONSTRAINT "PK_email_notifications" PRIMARY KEY (id)
);

-- ===== sms_notifications =====
-- Outbound SMS queue (EV payout confirmation + approval events). status: 0=Pending, 1=Sent, 2=Failed
CREATE TABLE salescomdbtst.sms_notifications (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	phone_number varchar(30) NOT NULL,
	messages text NOT NULL,
	status int4 NOT NULL,
	attempt_count int4 NOT NULL,
	error_message varchar(2000) NULL,
	sent_at timestamptz NULL,
	created_at timestamptz NOT NULL,
	CONSTRAINT "PK_sms_notifications" PRIMARY KEY (id)
);
```

---

### 3.9 Cross-cutting

```sql
-- ===== audit_logs =====
-- Full audit trail for every config change and state transition.
-- changed_by_user_id is uuid (Central-Login id type); all other ids are int8.
CREATE TABLE salescomdbtst.audit_logs (
	id int8 GENERATED BY DEFAULT AS IDENTITY( INCREMENT BY 1 MINVALUE 1 MAXVALUE 9223372036854775807 START 1 CACHE 1 NO CYCLE) NOT NULL,
	application_name varchar(100) NOT NULL,
	entity_name varchar(200) NOT NULL,
	entity_id varchar(200) NOT NULL,
	action_type int4 NOT NULL,
	changed_by_user_id uuid NULL,
	changed_by varchar(200) NOT NULL,
	changed_at timestamptz NOT NULL,
	changed_columns text NULL,
	old_values jsonb NULL,
	new_values jsonb NULL,
	CONSTRAINT "PK_audit_logs" PRIMARY KEY (id)
);
CREATE INDEX ix_audit_logs_entity ON salescomdbtst.audit_logs USING btree (entity_name, entity_id);
```

---

### 3.10 Entity Relationship Diagram

For the full table-and-column visual reference, see the ERD file: **`commission_system_erd_5.drawio`** (Downloads folder). It covers all 21 tables, their columns, and FK relationships. The DDL in §3.2–§3.9 above is the authoritative source; the ERD is the visual companion.
---

# §4 Authentication & Session

## 4.1 Overview

SalesCom is an **internal-only** application. Every user signs in through Banglalink's **Central Login** — the company-wide Identity Provider at `blposapi.banglalink.net` — using a **username + password plus a One-Time Password (OTP)** second factor. SalesCom **never stores passwords** and **never renders its own OTP screen**: both the password check and the OTP challenge belong to Central Login. This is **integration point #1 of the 7 ICDs** (Central Login).

The flow in one line: the web app posts credentials to the **SalesCom backend** → the backend (holding the secret application name/key) calls Central Login → Central Login returns an SSO redirect → the user completes OTP on Central Login's own page → Central Login bounces the browser back to a SalesCom **callback** carrying a single-use `authToken` → the backend verifies that token server-to-server, loads the user's profile and rights from the locally-synced `users` / `user_rights` tables, and mints its own **SalesCom JWT**. The browser holds only the SalesCom JWT and sends it on every request. **No Central Login token is ever exposed to the browser.**

Two background mechanics keep the session honest:

- **3-hour inactivity logout** — the frontend drops the JWT after **3 hours** of no user activity; the user must sign in again from the start.
- **Hourly provisioning sync** — an Airflow DAG runs every hour and pulls SalesCom users and their rights directly from the **POS system database** (via DB connection string — no API), dumps the data on the Airflow server, then upserts `users` and `user_rights` in SalesCom. Only users who exist in SalesCom are pulled. Role/right changes take effect within an hour.

The exact `authToken` format, the verify-endpoint contract, and the redirect parameter set are pinned in the **Central Login ICD** (D4, contracts-first); this section specifies the SalesCom-side behavior that does not depend on those final details.

---

## 4.2 User Interface

| Screen | Owner | Contents |
|---|---|---|
| **Login** | SalesCom web app | Username field, Password field (with show/hide toggle), **Remember-me** checkbox, **Sign In** button. The form posts to the **SalesCom backend**, never straight to Central Login. While the backend calls Central Login, the button shows a spinner. |
| **OTP challenge** | Central Login | The browser is redirected to Central Login's own page, which renders the OTP input, the resend countdown, and the failed-attempt lockout. SalesCom has **no** OTP screen and cannot read the OTP. |
| **Callback (transient)** | SalesCom web app | A brief "Signing you in…" interstitial while the backend verifies the `authToken` and issues the JWT, then forwards to the Dashboard. |
| **Dashboard (post-login landing)** | SalesCom web app | Shows the user's **last successful sign-in** and **last refused sign-in** (read from the `login_log` table) plus the role-scoped KPI cards. |
| **Session-expiry / inactivity** | SalesCom web app | On 3-hour inactivity or JWT expiry, a "Session expired — please sign in again" modal appears and routes back to Login. |

**UI gate rule:** every authenticated page is gated by the presence of a valid JWT. If `GET /api/v1/auth/me` returns `401`, the app clears local state and routes to Login. The FE renders menus/controls from the **resolved role + permissions** returned by `/auth/me` (§4.6) — server-side enforcement is independent of that list.

---

## 4.3 Data Model

Authentication reads and writes **three tables** from the canonical DDL (§3.2 Identity & Access) — **no new tables** are introduced here.

| Table | Role in auth | Key columns used |
|---|---|---|
| `users` | Local cache of Central-Login identities, refreshed hourly. The login lookup and the JWT subject resolve against this table. | `id` (int8 PK — the JWT `sub`), `user_id` (varchar — the **external Central-Login id**, used to match the verified `authToken` profile), `user_name`, `full_name`, `mobile_no`, `email`, `department` |
| `user_rights` | The user's role/permission rights, refreshed hourly. Resolved into a single SalesCom role at login and on every `/auth/me`. | `user_id` (int8 FK → `users.id`), `rights_code` (int4 — role code; semantics in §4.6) |
| `login_log` | Audit row for **every** sign-in attempt, success or failure. Powers the Dashboard "Login Attempts" card. `user_name` is a snapshot (no FK) so attempts by unknown/disabled users are still recorded. | `user_name`, `full_name`, `login_time` (timestamptz), `status` (int4 — **1 = Success, 2 = Failed**), `remarks` |

**Schema notes (real schema, do not deviate):**

- The **external Central-Login id** lives in `users.user_id` (varchar, unique index `ux_users_user_id`). It is **not** the PK and carries **no FK**; the JWT `sub` is the numeric `users.id`.
- `user_rights.user_id` is **int8 → `users.id`** (a real FK), **not** the external text key. One row per `(user, right)`, enforced by `ux_user_rights_user_right (user_id, rights_code)`.
- `users` in this real schema has **no `is_active` column** and **no soft-delete flag**. "Deactivation" is therefore modeled by **removing the user's `user_rights` rows** during the hourly sync: a user with **no active right resolves to no role and is refused login** (§4.6). The Central-Login profile's own status flag (consumed at verify) is the upstream source of truth; if the profile is inactive the backend writes a `FAILED` login row and refuses.
- The `login_log` table in the real schema has **no FK and no audit columns** beyond those listed; it is a flat append-only attempt log.
- **No password, OTP, or Central Login token is ever persisted.** The single-use `authToken` is held only in memory for the duration of the callback verify call.
- The SalesCom JWT is **stateless** — not stored server-side. Logout and inactivity simply discard the token on the client (no revocation list in Phase 1).

---

## 4.4 Process Logic

### 4.4.1 Login & SSO redirect

1. The web app sends `{ username, password, rememberMe }` to **`POST /api/v1/auth/login`**.
2. The backend attaches the **secret application name + key** (from server-side config / Docker secret — never sent to the browser) and calls Central Login at **`POST /account/v1/login`**.
3. Because all SalesCom users are internal, Central Login responds with an **SSO redirect URL** (no token yet). The backend generates a `state` (anti-CSRF nonce) and tracks it for the callback.
4. The backend returns `{ redirectUrl, state }`. The web app sends the browser to that URL (Central Login's OTP page).

The raw password is relayed to Central Login and **never stored** by SalesCom. If Central Login rejects the credentials, the backend writes a **`status = 2` (Failed)** row to `login_log` and returns `401`.

### 4.4.2 OTP verification & callback

5. Central Login fully owns the OTP step (entry, countdown, resend, lockout). SalesCom is not involved.
6. On a correct OTP, Central Login redirects the browser to **`GET /api/v1/auth/callback?authToken=...&state=...`** with a **single-use `authToken`**.
7. The backend validates the echoed `state` (rejects on mismatch — CSRF/replay guard), then calls Central Login at **`POST /account/v1/verify-auth-token`** with the `authToken` to confirm the login and retrieve the **user profile** (`userId`, `userName`, status flags, etc.).
8. Any login token Central Login may return at this step is **ignored** — SalesCom consumes only the profile. The `authToken` is single-use; a second callback with the same token is rejected (`401`).

### 4.4.3 SalesCom JWT issuance

9. The backend looks up the user in `users` by the **external `user_id`** from the verified profile (kept current by the hourly sync, §4.4.6) and loads that user's `user_rights` rows. If the user row is missing, the upstream profile is inactive, or the user has **no `user_rights`** → login is refused (`403`, **`status = 2`** row written to `login_log`).
10. The backend **resolves the SalesCom role** (Maker / Checker / Admin) from `user_rights.rights_code` (§4.6) and mints a **SalesCom JWT**:
    - **Claims:** `sub` = `users.id` (int8), `userName`, `role`, `iat`, `exp` (from config — see §4.1 note), `iss = salescom`, `aud = salescom`, `jti`. Signed with the backend secret.
11. The JWT is returned to the web app (as an httpOnly + Secure cookie and/or response body per the FE transport choice). **No Central Login token reaches the browser.**
12. A **`status = 1` (Success)** row is written to `login_log` (`user_name`, `full_name`, `login_time = now()`, `status = 1`, `remarks`). The web app forwards to the Dashboard.

### 4.4.4 Per-request validation

13. The web app sends the JWT on **every** non-public request. On each request the backend:
    a. Verifies the JWT signature and `exp` / `iss` / `aud`. On failure → `401`.
    b. Loads the user by `sub` and confirms they still exist with at least one `user_rights` row. A user whose rights were removed since their last sync is rejected → `403`.
    c. For **sensitive actions** (approve, disburse, schedule, all config writes) re-checks the role **live** against `user_rights` in the DB — the JWT `role` claim alone is **not** trusted for money/approval paths (anti-stale-authorization). On role-check failure → `403`.
14. Read / own-scope endpoints (Dashboard, listing, `/auth/me`) accept the JWT claim directly; only sensitive actions pay the live-check cost.

### 4.4.5 3-hour inactivity logout

15. The frontend maintains an inactivity timer reset by user interaction. After **3 hours** of no activity, the frontend **discards the JWT** (clears the cookie/store) and routes to Login. This is a client-side session control; the JWT itself remains valid until its `exp`, but the browser no longer holds it.
16. **`POST /api/v1/auth/logout`** lets the user log out explicitly; it clears the SalesCom session/cookie on the client. (Stateless JWT → no server-side invalidation in Phase 1.)
17. On JWT expiry or logout, the user signs in again from step 1.

### 4.4.6 Hourly provisioning sync

18. An **Airflow DAG runs every hour** to sync SalesCom users and rights from the **POS system**. The POS system is the authoritative user store — Central Login (also part of POS) handles authentication only and has no rights API. The sync works as follows:
    - Airflow connects to the **POS database directly** (via a DB connection string configured on the Airflow server — no API call).
    - It queries only the users who are relevant to SalesCom (not all POS users), dumps the result on the Airflow server, then **upserts into `users` and `user_rights`**:
      - **`users`:** insert new users; update changed `full_name` / `mobile_no` / `email` / `department` on existing rows (matched by `users.user_id`, the external POS key).
      - **`user_rights`:** replace each user's rows to mirror the POS rights assignment. A user removed or deactivated in POS has their `user_rights` rows **deleted** — this is how deactivation is expressed (no `is_active` flag in `users`).
19. Effect: a role change or deactivation in the POS system takes effect in SalesCom **within one hour**, after which the per-request live check (§4.4.4c) immediately reflects it. The job is **idempotent** (upsert keyed on `users.user_id`) and writes an `audit_logs` row (`entity_name = 'users'`, `action_type` 1/2/3).

---

## 4.5 API Endpoints

Two endpoint sets: **Central Login's own service** (called only server-to-server by the backend, never by the browser) and **SalesCom's backend endpoints** (used by the web app). All SalesCom paths are under `/api/v1`.

### Central Login service (backend-only, external)

| Method | Path | Purpose |
|---|---|---|
| `POST` | `/account/v1/login` | Authenticate username/password (+ app name/key); for internal users returns the SSO redirect URL. |
| `POST` | `/account/v1/verify-auth-token` | Validate the single-use SSO `authToken` and return the user profile. |

These live at `blposapi.banglalink.net` and are governed by the Central Login ICD; their exact DTOs are pinned there.

### SalesCom backend endpoints

**`POST /api/v1/auth/login`** — start login; relay to Central Login, return the SSO redirect.
**Role:** Anonymous (public).

Request DTO:
```json
{ "username": "rso.ops01", "password": "********", "rememberMe": true }
```
Response `200 OK`:
```json
{ "redirectUrl": "https://blposapi.banglalink.net/sso/authorize?...&state=8f1c...", "state": "8f1c2a9e-..." }
```
Status codes: `200` redirect issued · `400` missing/blank username or password · `401` Central Login rejected credentials (`login.status = 2` written) · `502` Central Login unreachable/timeout.

---

**`GET /api/v1/auth/callback`** — receive + verify the `authToken`, issue the SalesCom JWT.
**Role:** Anonymous (entered via Central Login redirect).

Request (query string): `authToken` (single-use), `state` (must match the value issued at `/auth/login`).
Response `200 OK` (JWT also set as an httpOnly + Secure cookie):
```json
{
  "token": "eyJhbGciOiJIUzI1NiIs...",
  "expiresAt": "2026-06-17T09:32:00Z",
  "user": { "id": 412, "userName": "rso.ops01", "fullName": "Tarikul Islam",
            "email": "tarikul.islam@banglalink.net", "role": "MAKER" }
}
```
> `expiresAt` is computed from the configured JWT lifetime (§4.1 note); the FE treats it as opaque and relies on `/auth/me` returning `401` for the authoritative expiry signal.

Status codes: `200` JWT issued (`login.status = 1` written) · `400` missing `authToken`/`state` · `401` invalid / expired / already-used `authToken`, or `state` mismatch · `403` user not found in `users`, profile inactive upstream, or no `user_rights` (`login.status = 2` written) · `502` Central Login verify call failed.

---

**`POST /api/v1/auth/logout`** — clear the SalesCom session.
**Role:** Any authenticated user.
Request: empty body; JWT in cookie/header. Response `204 No Content`.
Status codes: `204` cleared · `401` no/invalid token.

---

**`GET /api/v1/auth/me`** — return the current user with the resolved role and effective permissions.
**Role:** Any authenticated user.
Response `200 OK`:
```json
{
  "id": 412,
  "userId": "BL-CL-90231",
  "userName": "rso.ops01",
  "fullName": "Tarikul Islam",
  "email": "tarikul.islam@banglalink.net",
  "mobileNo": "+8801XXXXXXXXX",
  "department": "Sales Commission",
  "role": "MAKER",
  "rightsCodes": [10],
  "permissions": [
    "report.create", "report.edit", "report.upload",
    "report.run_demo", "report.run_final", "report.submit_approval",
    "report.disburse", "schedule.manage"
  ],
  "lastLoginAt": "2026-06-16T09:32:00Z"
}
```
Field mapping: `id` = `users.id`, `userId` = `users.user_id` (external), `rightsCodes` = the raw `user_rights.rights_code` list, `role` = the **resolved** role (§4.6), `permissions` = the role→action expansion the FE uses to show/hide controls, `lastLoginAt` = most recent `login.login_time` with `status = 1` for this `user_name`.
Status codes: `200` profile returned · `401` no/invalid/expired token · `403` user has lost all `user_rights` since last sync (live check fails).
> **Server-side enforcement does not rely on `permissions`** — it is a UI hint only.

---

## 4.6 RBAC Model

### Roles

SalesCom has exactly **three roles**; each user has **exactly one** resolved role.

| Role | `rights_code` | Who | Core capability |
|---|---|---|---|
| **Maker** (Business User) | `10` | Builds commission reports | Create/edit reports & the IR wizard, upload supporting files, run Demo & Final, submit for approval, manage schedules (BR9), trigger disbursement after full approval (BR8). **Cannot approve.** |
| **Checker** (Approver) | `20` | Approves/rejects | Review and **approve/reject** at an assigned approval level (sequential, ascending — BR6), mandatory comment on reject (BR7). Read access to reports/runs. **Cannot create reports**, and **cannot be maker + checker of the same run (BR5)**. |
| **Administrator** | `30` | Super-user | Everything a Maker and Checker can do, **plus** data-source management (BR2), channel catalog, approval flow/level/user configuration, and user oversight. |

**`rights_code` semantics (`user_rights.rights_code`, int4):**

- `user_rights.user_id` is **int8 → `users.id`** (real FK), **not** the external text key.
- `rights_code` is an **int4 role code**: **`10 = MAKER`, `20 = CHECKER`, `30 = ADMIN`**. These are the seed values; the column is a plain int4 enum (app owns code→meaning, kept in sync with Appendix B), so additional codes can be added by migration. Values are **contiguous tens** to leave room for future fine-grained rights (e.g. `11`, `12`) without renumbering.
- **Role resolution:** a user may have one or more `user_rights` rows. The resolved role is the **highest** code present — `30` (Admin) wins over `20`, which wins over `10`. If a user has **no `user_rights` row at all**, they have **no role** and login is refused (`403`). (Recall §4.3: removing a user's rights rows is exactly how the hourly sync expresses deactivation, since `users` has no `is_active` flag.)

### Permission matrix (action → role)

Each API action is bound to a permission and enforced **server-side**. ✓ = allowed; ✗ = blocked; **L** = requires a **live DB role re-check** (not the JWT claim).

| Action (endpoint-bound) | Maker | Checker | Admin | Note |
|---|---|---|---|---|
| Login / OTP / Dashboard (own-scope) / `auth/me` | ✓ | ✓ | ✓ | JWT claim sufficient |
| Data Source create / edit / deactivate | ✗ | ✗ | ✓ **L** | BR2 (`data_sources.is_active`, never hard-delete) |
| Channel catalog manage | ✗ | ✗ | ✓ **L** | |
| Report create / edit IR / upload supporting file | ✓ | ✗ | ✓ | Maker = creator/last-editor |
| Run Demo | ✓ | ✗ | ✓ | Demo skips pre-run checks |
| Run Final / submit for approval | ✓ | ✗ | ✓ | |
| **Approve / Reject** (assigned level) | ✓\* **L** | ✓\* **L** | ✓\* **L** | BR5 + BR6 + assigned-level check |
| Disburse (after full approval) | ✓ **L** | ✗ | ✓ **L** | BR8 |
| Report Stop / schedule manage | ✓ (Maker only) | ✗ | ✓ **L** | BR9 |
| Approval flow / level / user config | ✗ | ✗ | ✓ **L** | |

\* Approve/Reject is allowed only for the user **assigned to that flow's level** (`approval_flow_level_users.user_id` = the actor's `users.user_id`), and **BR5** bars the run's Maker (report creator/last-editor, resolved via `COALESCE(report_setups.updated_by, report_setups.created_by)`) from approving **any** level of that report — with **no Admin override** on the same report.

### `/auth/me` resolved-role → permissions

The FE calls `GET /api/v1/auth/me` once after login (and on app reload) to obtain the resolved `role` + `permissions`, then renders the menu/controls for that role:

| Role | Permissions emitted |
|---|---|
| `MAKER` | `report.create`, `report.edit`, `report.upload`, `report.run_demo`, `report.run_final`, `report.submit_approval`, `report.disburse`, `schedule.manage` |
| `CHECKER` | `report.read`, `approval.act` |
| `ADMIN` | all Maker + all Checker permissions, plus `datasource.manage`, `catalog.manage`, `approvalflow.manage`, `user.manage` |

---

## 4.7 Validation & Business Rules

| # | Rule | Where enforced |
|---|---|---|
| V1 | `username` and `password` required and non-blank on `/auth/login`. | API (`400`) |
| V2 | Credential check, OTP entry/countdown/resend, and failed-attempt lockout + OTP expiry are owned entirely by **Central Login** — SalesCom does not implement them. | Central Login |
| V3 | `authToken` is **single-use**: a second callback with the same token is rejected; `state` must match the value issued at `/auth/login` (CSRF/replay guard). | API (`401`) |
| V4 | The Central Login token returned at verify is **ignored**; only the profile is consumed. No password/OTP/IdP token is ever persisted. | API |
| V5 | Login is refused if the user is absent from `users`, the upstream profile is inactive, or the user has **no `user_rights` row**. | API (`403`) |
| V6 | The SalesCom JWT carries `sub`, `userName`, `role`, `iat`, `exp`, `iss`, `aud`, `jti`; signed with the backend secret. **Expiry duration is config-driven and left unspecified in this spec** (Team LLD v2). | API / Config |
| V7 | **Every** non-public request carries the JWT; signature + `exp` / `iss` / `aud` validated; user re-loaded and still-has-rights confirmed. | API (`401`/`403`) |
| V8 | **Sensitive actions** (approve, disburse, schedule, all config writes) require a **live DB role re-check** — the JWT `role` claim is not trusted for these paths (BR1, anti-stale-authorization). | API (`403`) |
| V9 | **3-hour inactivity** → frontend discards the JWT and routes to Login (client-side session control). | FE |
| V10 | **Every** sign-in attempt — success or failure — is written to `login_log` (`user_name`, `full_name`, `login_time`, `status` **1 = Success / 2 = Failed**, `remarks`). | API |
| V11 | The hourly provisioning sync (Airflow DAG → POS DB direct connection → dump → upsert) is **idempotent** (upsert keyed on `users.user_id`); deactivations are expressed by deleting `user_rights` rows; changes take effect within one hour and are then honored by V8. | Airflow DAG |
| V12 | Resolved role = **highest** `rights_code` (`30` > `20` > `10`); used as the JWT `role` claim and the `/auth/me` `role`. No rights → no role → refused. | API |
| V13 | Secret application name/key, JWT signing secret + lifetime, and Central Login endpoints live in server-side config / Docker secrets — never sent to the browser. | Config |

**Reconciliation notes (deliberate divergences resolved):**
- **Session model:** earlier SDD drafts described a short access token (15–30 min) + rotating refresh + server-side revocation list. This LLD follows the **Team LLD v2** model — a **single stateless JWT (config-driven expiry) + 3-hour frontend inactivity logout, no Phase-1 refresh/revocation list** — matching the `login_log`-table session model and the 20-table schema (no revocation table). Short-token + refresh rotation can be added later via an auxiliary `jwt_revocation(jti, expires_at)` table without touching the core schema.
- **Inactivity window:** **3 hours** (Team LLD v2), superseding any earlier "4-hour" figure.
- **Deactivation:** the real `users` table has **no `is_active` column**; deactivation is modeled by the hourly sync **removing `user_rights` rows** (→ no resolved role → refused), not by flipping a flag.

---

# §5 Data Source Management

A **data source** is a registered, pre-loaded PostgreSQL table that a report's IR is allowed to read from. The ETL layer (Airflow) lands cleaned sales / recharge / lifting / vPeople data into physical source tables; an **Administrator** then **registers** the ones Business Users may build reports on. Registration is the gate between "a table exists in the database" and "the no-code wizard may select it as a `source`."

Two things consume a registered data source:

1. The **wizard** (Report Step-3 / Step-4) — a block's `source` / `combine.join_with` of `type: "data_source"` picks a registered source by its `source_table_name` (see Appendix A / `IR_Schema_and_MultiKPI_Join.md` §2–§3.2).
2. The **SQL Generator** — at Final Save it resolves every `data_source` ref against the registry and emits the physical table name into the stage SQL; the execute-time allowlist (D2) only permits reads against registered source tables and the `salescom_upload` schema.

This section also specifies the **Supporting CSV → DB table ingestion** mechanics (§5.7), because Report Step-2 turns user-uploaded CSVs into physical tables that the IR references with `source.type: "upload"`. Registered data sources and uploaded tables are the **only two physical-table inputs** an IR may read (the third input type, `block`, is just another block's in-run temp output).

> **Schema note.** This section uses the real implemented tables from §3: **`data_sources`** (catalog) and **`report_supporting_uploads`** (Report module). The real `data_sources` table has **no `column_aliases` column** — it stores only `source_table_name`, `table_description`, `is_active`, and audit columns. Per-column friendly labels needed by the wizard are therefore **derived by live introspection** of the physical table at pick-time (§5.4 / endpoint #4), **not persisted**. This keeps generated SQL stable (the IR always references the physical column name) and avoids alias drift.

---

## 5.1 Overview

| Aspect | Detail |
|---|---|
| Who manages it | **Administrator** only (register / edit / activate / deactivate). Business User and Approver have **read-only** access (they need the active list to build reports). |
| What it is | One row in **`data_sources`** pointing at a physical table the ETL layer maintains, plus a human `table_description` and an `is_active` flag. Column metadata (names, types, friendly labels) is read live from the database, not stored. |
| Lifecycle | Register → optionally edit `table_description` → activate / deactivate. **Never hard-deleted** (BR2) — only `is_active = false`. |
| Key guard | A source **referenced by any non-archived report's IR** cannot be deactivated (§5.6, R5). |
| Phase | Phase 1. The registry shape is final; later phases only register more tables. |
| ETL coupling | The Final-run pre-check (§3 decision 7) verifies that every registered system source used by a report has its **ETL finished up to the report End Date** before a Scheduled / Run-Now FINAL run executes. That check reads the source table's latest data date; registration is what makes a table eligible to be checked. |

---

## 5.2 User Interface

**Data Source list** — `/admin/data-sources` (Administrator nav):

- **Columns:** SL · **Source Table Name** · **Description** · **#Columns** (live count from introspection) · **Status** (Active / Inactive toggle) · Updated On · Action menu.
- **Toolbar:** search box (matches `source_table_name` + `table_description`), Status filter (All / Active / Inactive), **"Register Data Source"** button (top-right, Administrator only).
- **Row actions:** **View**, **Edit**, **Activate / Deactivate** (the toggle is disabled with tooltip *"In use by N report(s)"* when the source is referenced — R5).

**Register / Edit drawer:**

1. **Pick physical table** — dropdown populated by `GET /api/v1/data-sources/db-tables` (live introspection of candidate tables not yet registered). On **Edit** the table is fixed (read-only).
2. On selection the system **introspects the table** and lists every column for reference: physical column name + detected PG type + suggested logical type + suggested friendly label (Title-Cased). These are **display-only previews** (not persisted) so the Admin can confirm the table is the right one.
3. **Description** (table-level, → `table_description`) and **Status** (Active / Inactive, → `is_active`).
4. Save.

**Read-only consumer view (Business User):** the wizard's source-picker (Report Step-3 / Step-4) lists only `is_active = true` sources by `source_table_name` + `table_description`; expanding one shows the live column list with friendly labels. Business Users never reach the Register / Edit drawer.

---

## 5.3 Data Model

Primary table: **`data_sources`** (canonical DDL §3.3).

| Column | Type | Role |
|---|---|---|
| `id` | int8 identity | PK |
| `source_table_name` | varchar(200) | Physical table name (system-wide unique — `ux_data_sources_source_table`). The IR's `data_source` ref equals this value. |
| `table_description` | varchar(1000) NULL | Admin-entered human description, searchable. |
| `is_active` | bool | Active flag. `false` = deactivated (BR2 soft-delete). |
| `created_at` / `updated_at` | timestamptz | Audit timestamps (UTC). |
| `created_by` / `updated_by` | varchar(200) | Audit user_name. |

**Column metadata is not stored.** When the wizard or the Register drawer needs the column list / types / friendly labels for a source, the BE introspects PostgreSQL `information_schema.columns` live (endpoint #4). The mapping from PG type → wizard-facing **logical type** is the same table used for upload inference (§5.7.2), collapsed to: `text · integer · numeric · date · timestamp · boolean`.

**Related tables (referenced, not redefined here):**
- **`report_supporting_uploads`** (§3.4) — written by the Step-2 ingestion (§5.7).
- **`report_setups.definition`** JSONB (§3.4) — holds the IR whose `source` refs point at `data_sources.source_table_name` or an upload table; the "in use" check (R5) is logical, resolved by scanning the IR JSONB.

No new tables are introduced by this module.

---

## 5.4 Process Logic — register / edit / deactivate

**Register a data source (Administrator):**

1. Admin opens the Register drawer; FE calls `GET /api/v1/data-sources/db-tables` → list of physical tables in the allowed source schema(s) that are **not already registered**.
2. Admin selects a table. FE calls `GET /api/v1/data-sources/db-tables/{tableName}/columns` → BE introspects `information_schema.columns` and returns each column with its PG type, a **suggested logical type**, and a **suggested friendly label** (Title-Cased from the physical name). Display-only.
3. Admin fills the table `table_description` and sets status (`is_active`).
4. **`POST /api/v1/data-sources`** → BE validates (R1–R4), inserts one `data_sources` row (`source_table_name`, `table_description`, `is_active`, `created_at`, `created_by`), writes an `audit_logs` row (`action_type = 1` Create, `entity_name = 'data_sources'`, `entity_id = <id>`).

**Edit:** `PUT /api/v1/data-sources/{id}` — only `table_description` and (rarely) `is_active` may change; **`source_table_name` is immutable** once registered (changing the physical table = register a new source). Audited (`action_type = 2` Update, with `changed_columns` + `old_values`/`new_values` JSONB).

**Activate / deactivate:** `PATCH /api/v1/data-sources/{id}/status`.
- **Activate** → set `is_active = true`. Always allowed.
- **Deactivate** → **first run the in-use check (R5)**: scan every non-archived `report_setups.definition` for a `source` / `combine.join_with` of `type = 'data_source'` whose ref equals this `source_table_name`. If any match → reject `409 SOURCE_IN_USE` with the offending report names. Otherwise set `is_active = false`. Audited (`action_type = 2`, diff on `is_active`).
- **Never delete.** There is no `DELETE` endpoint (BR2).

---

## 5.5 API Endpoints

All under `/api/v1`. JWT required on every call. Standard error envelope:
```json
{ "error": { "code": "STRING_CODE", "message": "human readable", "details": { } } }
```

| # | Method / Path | Purpose | Role | Success |
|---|---|---|---|---|
| 1 | `GET /data-sources` | List (search + paginate) | All authenticated | 200 |
| 2 | `GET /data-sources/{id}` | Read one (+ live columns) | All authenticated | 200 |
| 3 | `GET /data-sources/db-tables` | Candidate physical tables not yet registered | Administrator | 200 |
| 4 | `GET /data-sources/db-tables/{tableName}/columns` | Introspect a physical table's columns | Administrator | 200 |
| 5 | `POST /data-sources` | Register | Administrator | 201 |
| 6 | `PUT /data-sources/{id}` | Edit description / status | Administrator | 200 |
| 7 | `PATCH /data-sources/{id}/status` | Activate / deactivate | Administrator | 200 |

**1) `GET /data-sources`** — Query: `search`, `status` (`active|inactive|all`, default `active`), `page` (default 1), `pageSize` (default 20, max 100).
Response `200`:
```json
{ "items": [
    { "id": 12, "sourceTableName": "ev_lifting_daily_com", "tableDescription": "Daily EV lifting",
      "columnCount": 14, "isActive": true, "updatedOn": "2026-06-10T09:12:00Z" } ],
  "page": 1, "pageSize": 20, "total": 37 }
```
> `columnCount` is computed via introspection at read time (or cached); it is not a stored column.

**2) `GET /data-sources/{id}`** → `200`:
```json
{ "id": 12, "sourceTableName": "ev_lifting_daily_com", "tableDescription": "Daily EV lifting", "isActive": true,
  "columns": [
    { "column": "rso_code",    "pgType": "text",    "logicalType": "text",    "label": "Rso Code" },
    { "column": "send_amount", "pgType": "numeric", "logicalType": "numeric", "label": "Send Amount" } ],
  "createdOn": "2026-05-01T06:00:00Z", "createdBy": "admin01",
  "updatedOn": "2026-06-10T09:12:00Z", "updatedBy": "admin01" }
```
`columns` is filled by live introspection. `404 NOT_FOUND` if the row is missing.

**3) `GET /data-sources/db-tables`** → `200`:
```json
{ "tables": [ { "tableName": "ev_lifting_daily_com", "schema": "salescom_src", "estimatedRowCount": 4200000 } ] }
```
Returns only allowlisted source schema(s); excludes already-registered tables and SalesCom's own operational tables (`salescomdbtst.*`, `salescom_upload.*`).

**4) `GET /data-sources/db-tables/{tableName}/columns`** → `200`:
```json
{ "tableName": "ev_lifting_daily_com", "schema": "salescom_src",
  "columns": [
    { "column": "rso_code",    "pgType": "text",                     "logicalType": "text",      "suggestedLabel": "Rso Code" },
    { "column": "send_amount", "pgType": "numeric",                  "logicalType": "numeric",   "suggestedLabel": "Send Amount" },
    { "column": "fcd",         "pgType": "timestamp with time zone", "logicalType": "timestamp", "suggestedLabel": "Fcd" } ] }
```
`404 NOT_FOUND` if the table doesn't exist in an allowed schema.

**5) `POST /data-sources`** — Request:
```json
{ "sourceTableName": "ev_lifting_daily_com", "tableDescription": "Daily EV lifting", "isActive": true }
```
Response `201`: full object (as endpoint #2). Errors: `400 VALIDATION_ERROR`, `409 DUPLICATE_SOURCE`, `422 TABLE_NOT_FOUND`.

**6) `PUT /data-sources/{id}`** — Request (`sourceTableName` ignored / immutable):
```json
{ "tableDescription": "Daily EV lifting (DWH)", "isActive": true }
```
Response `200` (full object). Errors: `400`, `404`.

**7) `PATCH /data-sources/{id}/status`** — Request `{ "isActive": false }`. Response `200`:
```json
{ "id": 12, "isActive": false }
```
Errors: `404`; **`409 SOURCE_IN_USE`** on deactivate while referenced:
```json
{ "error": { "code": "SOURCE_IN_USE",
  "message": "Data source is used by active reports and cannot be deactivated.",
  "details": { "reports": ["RSO Campaign GA Recharge LSO Apr26", "Deno Lifting May26"] } } }
```

**Role enforcement:** endpoints 3–7 require the **Administrator** right (`user_rights.rights_code` per Appendix B RBAC matrix); a Business User / Approver calling them gets `403 FORBIDDEN`. Endpoints 1–2 are open to any authenticated user.

---

## 5.6 Validation & Business Rules

| ID | Rule | Enforcement |
|---|---|---|
| R1 | `sourceTableName` required, non-empty, must match a real physical table in an allowed source schema. | App check → `422 TABLE_NOT_FOUND` |
| R2 | `sourceTableName` system-wide unique. | `ux_data_sources_source_table` + `409 DUPLICATE_SOURCE` |
| R3 | `tableDescription` ≤ 1000 chars; trimmed. | App `400 VALIDATION_ERROR` |
| R4 | Only **Administrator** may register / edit / activate / deactivate (BR1). Business User / Approver read-only. | JWT role check → `403` |
| R5 | A source **referenced by any non-archived report's IR** cannot be deactivated (BR2 spirit). | App scan of `report_setups.definition` → `409 SOURCE_IN_USE` |
| R6 | A source is **never deleted**; deactivate only. No DELETE route exists (BR2). | API surface |
| R7 | `source_table_name` is **immutable** after registration. | App ignores it on `PUT` |
| R8 | The wizard source-picker and the SQL Generator only see / resolve `is_active = true` sources. Deactivating a source does not break a saved IR's already-frozen `section_wise_report_sqls` (already compiled at Final Save) or a frozen `run_stages` snapshot, but it blocks **new** edits / Final Saves that re-select it. | Query filter + Final-Save resolution |
| R9 | A registered source used by a report is subject to the **Final-run ETL pre-check** (§3 decision 7): its latest data date must be ≥ the report End Date, else the Scheduled / Run-Now FINAL run is blocked (Demo skips). | Run-trigger pre-check (§6.10) |

---

## 5.7 Supporting CSV → DB table ingestion (Report Step-2)

Report Step-2 ("Supporting Uploads") lets the Maker attach CSV files — external B2C target / config files, slab tables, agent lists, exclusion lists, and prior-commission outputs for priority de-dup (see `Commission_Logic_Catalog.md`). Each CSV is loaded into a **physical table** so the IR can reference it with `source.type: "upload"` / `combine.join_with.type: "upload"`. This subsection is the contract for that pipeline; the **BE owns it** (not the Python engine), and it writes **one `report_supporting_uploads` row per file**.

### 5.7.1 Target schema, table & column naming

- **Schema:** all upload tables live in a dedicated schema **`salescom_upload`** (kept separate from registered source data and from SalesCom operational tables; the execute-time allowlist permits reads from `salescom_upload` + the registered-source schema only).
- **Table name:** `up_<reportId>_<slug>` where `<reportId>` = `report_setups.id`, `<slug>` = sanitised base file name (lowercased, non-alphanumerics → `_`, collapsed, trimmed to 40 chars), with a `_<n>` suffix on collision within the same report.
  Example: report `812`, file `RSO Agent Target (Apr).csv` → `up_812_rso_agent_target_apr`. The physical reference stored on the registry row is `db_schema = 'salescom_upload'`, `db_table_name = 'up_812_rso_agent_target_apr'`; the IR's `upload` ref is `salescom_upload.up_812_rso_agent_target_apr`.
- **Column names:** sanitised from the CSV header (§5.7.3). The IR / `combine.match_on` references the **sanitised** column name.

### 5.7.2 Detected-type → PostgreSQL type mapping

The ingester samples up to the first **10,000 rows** per column to infer a type, then creates the table with that type. Inference is **narrowest-that-fits**, in this priority order; any value that doesn't fit falls back to the next wider type, ultimately `TEXT`:

| Detected (in order) | Test | PG column type |
|---|---|---|
| boolean | every value ∈ {`true,false,t,f,0,1,yes,no,y,n`} (case-insensitive) | `BOOLEAN` |
| integer | every value matches `^-?\d{1,18}$` | `BIGINT` |
| numeric | every value matches a decimal / scientific number | `NUMERIC(38,10)` |
| date | every value parses as ISO date `YYYY-MM-DD` (or configured `DD-MMM-YYYY`) | `DATE` |
| timestamp | every value parses as ISO-8601 datetime | `TIMESTAMPTZ` |
| (fallback) | anything else / mixed / empty column | `TEXT` |

**Rules:** empty string / configured null-tokens (`""`, `NULL`, `N/A`) → SQL `NULL` and are ignored for inference. A fully-null column → `TEXT`. **Leading-zero strings** (MSISDN, codes) stay `TEXT` when any value has a significant leading zero, so join keys aren't silently turned into integers. Monetary columns infer as `NUMERIC(38,10)` (wide; the engine narrows at calc time). The wizard preview surfaces the inferred type; the Maker may override a column to `TEXT` (only widening to TEXT is allowed — never narrowing).

### 5.7.3 Column-name sanitisation

Applied to every CSV header cell, in order:

1. Trim, lowercase.
2. Replace any run of non-`[a-z0-9_]` (spaces, punctuation, BOM, etc.) with a single `_`; strip leading / trailing `_`.
3. If empty after step 2 → `col_<index>` (1-based).
4. If it starts with a digit → prefix `c_`.
5. **Reserved word** (PG keyword: `order`, `user`, `select`, `from`, `group`, `table`, …) → suffix `_col` (`order` → `order_col`).
6. **Duplicate** sanitised names → suffix `_2`, `_3`, … in header order.
7. Truncate to 59 chars (PG identifier limit is 63; leaves room for the dedupe suffix).

The original → sanitised header map is returned to the FE so the wizard can show *"Original CSV column → loaded column."*

### 5.7.4 Limits & pre-load validation

| Limit | Value | On breach |
|---|---|---|
| Max columns | **30** | reject `422 TOO_MANY_COLUMNS` |
| Max file size | **500 MB** | reject `413 FILE_TOO_LARGE` |
| Format | `.csv` (UTF-8 / UTF-8-BOM), comma-delimited, first row = header | reject `415 UNSUPPORTED_FORMAT` |
| Header | every header cell non-empty after sanitisation; ≥ 1 data row | reject `422 EMPTY_OR_BAD_HEADER` |
| Per-report file cap | configurable (default 10 files) | reject `422 TOO_MANY_FILES` |

Limits are checked **before** the COPY: the column count from the parsed header, the size from the multipart / SeaweedFS object metadata.

### 5.7.5 Load mechanics (COPY-based)

The ingester is transactional and idempotent per `(report_setup_id, db_schema, db_table_name)`:

1. **Stage the object** — the raw CSV is first stored in SeaweedFS (→ `object_bucket`, `object_key`) so it survives retries and is the audit copy.
2. **Parse header** → sanitise (§5.7.3); enforce limits (§5.7.4).
3. **Infer types** — stream-sample up to 10k rows (§5.7.2).
4. **DDL** — `CREATE TABLE salescom_upload.up_<reportId>_<slug> (...)` with the inferred columns (all `NULL`-able) plus an internal `_row_no BIGINT` (load order; helps fan-out / guardrail debugging). `DROP TABLE IF EXISTS` first (re-upload replaces).
5. **Bulk load** — server-side `COPY salescom_upload.up_... FROM STDIN WITH (FORMAT csv, HEADER true, NULL '', QUOTE '"', ESCAPE '"')` streamed from the staged object via the Npgsql binary `COPY` API. Null-tokens normalised to SQL `NULL`. The whole COPY is **one transaction** — on any malformed row the load **rolls back** and returns `422 LOAD_FAILED` with the offending line number (no half-loaded table).
6. **Index** — Step-2 indexes nothing by default (join keys aren't known until the IR is built); the **SQL Generator** issues `CREATE INDEX` on the upload table's `match_on` columns at Final Save (cheap — table is already physical). (Indexing strategy: index join keys, filter-early-then-join.)
7. **Write the registry row** — one `report_supporting_uploads` row:
   - `report_setup_id` = the report,
   - `db_schema = 'salescom_upload'`, `db_table_name = 'up_<reportId>_<slug>'`,
   - `object_bucket`, `object_key` (SeaweedFS location from step 1),
   - `file_name` = original upload name,
   - `row_count` = rows loaded,
   - `uploaded_at` = now (UTC), `uploaded_by` = caller user_name.
   On re-upload of the same file, the existing row for that `(report_setup_id, db_schema, db_table_name)` is **updated in place** (drop + reload + update `row_count`/`object_key`/`uploaded_at`), so no duplicate registry row is created.
   > The real schema (§3.4) has only `ix_report_supporting_uploads_report_setup`. To make the in-place upsert deterministic, add a **UNIQUE index `ux_report_supporting_uploads_setup_table ON report_supporting_uploads (report_setup_id, db_schema, db_table_name)`** (idempotency guard for re-upload). If not added at DB level, the BE must enforce the same uniqueness in the upload transaction.
8. **Audit** — `audit_logs` row (`action_type = 1` Create on first upload / `2` Update on re-upload, `entity_name = 'report_supporting_uploads'`, `entity_id = <id>`).

### 5.7.6 Upload API endpoints

| Method / Path | Purpose | Role | Success |
|---|---|---|---|
| `POST /api/v1/reports/{reportId}/uploads` | Upload + ingest one CSV (multipart) | Maker (report owner) / Administrator | 201 |
| `GET /api/v1/reports/{reportId}/uploads` | List a report's uploaded tables | Maker / Approver / Administrator | 200 |
| `DELETE /api/v1/reports/{reportId}/uploads/{uploadId}` | Remove an upload (drops the table + row) — only while the report setup is not yet approval-locked and not referenced by a frozen run | Maker / Administrator | 204 |

**`POST … /uploads`** request = `multipart/form-data` with field `file`. Response `201`:
```json
{ "id": 5501, "reportSetupId": 812,
  "dbSchema": "salescom_upload", "dbTableName": "up_812_rso_agent_target_apr",
  "fileName": "RSO Agent Target (Apr).csv", "rowCount": 1834,
  "columns": [
    { "original": "RSO Code",        "loaded": "rso_code",        "type": "text" },
    { "original": "Recharge Target", "loaded": "recharge_target", "type": "numeric" },
    { "original": "Order",           "loaded": "order_col",       "type": "integer" } ],
  "uploadedAt": "2026-06-16T11:20:00Z", "uploadedBy": "maker01" }
```
Errors: `413 FILE_TOO_LARGE`, `415 UNSUPPORTED_FORMAT`, `422 TOO_MANY_COLUMNS | EMPTY_OR_BAD_HEADER | LOAD_FAILED | TOO_MANY_FILES`, `403 FORBIDDEN`, `409 REPORT_LOCKED` (the report setup is already in / past approval — uploads frozen).

**`GET … /uploads`** → `200`:
```json
{ "items": [
    { "id": 5501, "dbSchema": "salescom_upload", "dbTableName": "up_812_rso_agent_target_apr",
      "fileName": "RSO Agent Target (Apr).csv", "rowCount": 1834,
      "uploadedAt": "2026-06-16T11:20:00Z", "uploadedBy": "maker01" } ] }
```

**`DELETE … /uploads/{uploadId}`** → `204`. Errors: `403 FORBIDDEN`, `404 NOT_FOUND`, `409 REPORT_LOCKED` (cannot delete once the report is approval-locked) / `409 UPLOAD_IN_USE` (referenced by the current IR — remove the IR ref first).

### 5.7.7 Ingestion validation & rules

| ID | Rule |
|---|---|
| U1 | Only the report's **Maker** (owner) or an Administrator may upload to / delete uploads for a report (BR1). Ownership resolved per the single-Maker rule (`COALESCE(updated_by, created_by)` on `report_setups`). |
| U2 | Uploads / deletes allowed only while the report setup is **not yet approval-locked** (i.e. its `report_approvals.overall_status` is still `0` Draft, or no approval instance exists / it was sent back for edit). Once the setup enters Pre-Approval and beyond, the upload set is **frozen** → `409 REPORT_LOCKED`. |
| U3 | ≤ 30 columns, ≤ 500 MB, `.csv` only (§5.7.4). |
| U4 | Column names always sanitised (§5.7.3); the IR references the **sanitised** name. |
| U5 | Type inference is narrowest-that-fits with `TEXT` fallback; leading-zero / code columns stay `TEXT` to protect join keys (§5.7.2). Maker override may only widen to `TEXT`. |
| U6 | Load is **all-or-nothing** (single-transaction COPY); a bad row rolls the whole file back (no half-loaded table). |
| U7 | Upload tables live in `salescom_upload`; only that schema + the registered-source schema are readable by generated SQL (D2 allowlist). The trusted `final_commissions` write path never reads from generated SQL. |
| U8 | Re-upload of the same generated table name **replaces** the table in place (drop + reload) and updates the same `report_supporting_uploads` row (idempotent via `ux_report_supporting_uploads_setup_table` — §5.7.5 step 7). |
| U9 | An upload table is **dropped** when its `report_supporting_uploads` row is deleted (only allowed pre-lock) and on report archival; never left orphaned in `salescom_upload`. |

---

# §6 Report Management (Core)

> **Scope.** This section specifies the heart of SalesCom: the **5-step no-code wizard**, the **report Save/Publish state machine**, and the **run execution lifecycle**. It covers the tables `report_setups`, `report_supporting_uploads`, `section_wise_report_sqls`, `report_runs`, `run_stages`, and `final_commissions` (all defined in §3). The **achievement / incentive configuration the wizard collects *is* the IR** (`report_setups.definition` JSONB); this section summarises the IR shape and the multi-KPI grain-join rule, then **references `IR_Schema_and_MultiKPI_Join.md`** (Appendix A) for the full contract — it does not duplicate it.

---

## 6.1 Overview

Report Management is where a **Business User (Maker)** turns commission logic into an automated, repeatable report — with no SQL. Through a **five-step wizard** the Maker defines the report's basics, uploads any supporting config files, builds the **achievement** blocks (performance figures) and **incentive** blocks (payout logic), and reviews the result. The wizard's entire output is one row in **`report_setups`**, whose **`definition` (JSONB)** column holds the **IR** (Intermediate Representation).

The lifecycle of a report:

1. **Draft Save** — the wizard persists partial work into `report_setups` (`is_setup_complete = false`); no SQL, no demo, no approval yet.
2. **Final Save** — the full IR is validated, then the Python **SQL Generator** compiles it into per-stage SQL stored in **`section_wise_report_sqls`** (`is_setup_complete = true`). The report is now Demo-runnable and approval-eligible.
3. **Approval** — the report walks its bound approval flow (§7). It stays **editable while approval is pending** (any edit re-validates and regenerates the SQL); once **fully approved it is LOCKED**.
4. **Run** — a trigger (Run-Now / scheduled / demo) creates a **`report_runs`** row, snapshots `section_wise_report_sqls` into **`run_stages`**, executes each stage into temp tables, writes per-recipient **`final_commissions`**, and (for an approved FINAL run) hands off to **EV** or **POS** disbursement.

**Roles in this module:**
- **Business User (Maker)** — creates, edits, demo-runs, submits for approval, runs-now (after approval), schedules, clones, and stops/starts reports they own.
- **Approver (Checker)** — does not build reports; acts only in the Approval module (§7) but may view report detail.
- **Administrator** — may do anything a Maker can, across all reports.

> **IR reference.** The block structure, the operation set (`filter` / `combine` / `summarize` / `calculate` / `modify` / `rank`), the multi-KPI grain-join rule, the guardrails (G1–G4), and the formal JSON Schema are specified in full in **`IR_Schema_and_MultiKPI_Join.md`** (Appendix A). §6.4 below states **which part of the IR each wizard step writes** and summarises the rules, but the authoritative IR contract is that annex.

---

## 6.2 User Interface

**Report List** (`/reports`) — a paginated table: serial no., **report name**, **channel** (the channel TYPE), **start / end date**, **recurrence**, **EV** flag, **POS** flag, a **derived status label**, and an **Action** menu. Filters: date range, status, channel; free-text search by name. A **Create a New Report** button (top-right) opens the wizard at Step 1.

The **status label is DERIVED** from approval progress, not a stored string (2026-06-16 decision). It is computed from `report_approvals.current_level_order` + `report_approval_details` rows (see §7). Examples shown in the list:
- `"Draft"` — `is_setup_complete = false` (wizard not finished).
- `"Approved by L1, now Pending at L2"`.
- `"Rejected by L2, now Pending at L1"`.
- `"Rejected by L1, now pending for edit & resend"`.
- `"Pre Approved"` / `"Post Approved"` — fully approved phase complete.

Per-row actions (shown conditionally by report state):

| Action | Visible when |
|---|---|
| **View** | always |
| **Edit** | not LOCKED (report not yet fully/post-approved) |
| **Clone** | always |
| **Demo Run** | IR compiled (`is_setup_complete = true`, i.e. `section_wise_report_sqls` rows exist) |
| **Approval History** | a `report_approvals` row exists for the report |
| **Run Now** | report fully approved AND `report_setups.status` is active |
| **Report Stop / Start** | approved + scheduled report (toggles `report_setups.status`) |

**Five-step wizard** (one route, five panels with a stepper):
`Step 1 Basic → Step 2 Supporting Uploads → Step 3 Achievements → Step 4 Incentives → Step 5 Review / Run / Schedule`.
A persistent footer shows **Save as Draft** (always) and **Next** (Steps 1–4) / **Final Save** (Step 5). The wizard is keyed by the **draft report id** returned from Step 1; every later step PUTs into that same `report_setups` row.

**Report Detail** (`/reports/{id}`) — read-only tabs: **Basic**, **Supporting Uploads**, **Achievements**, **Incentives**, **Run Log**, and a single **Disbursement** tab (EV *or* POS, shown only when one is enabled). The **Run Log** lists each `report_runs` row (Demo or Final), newest first, with per-stage file downloads and a **Download All**.

---

## 6.3 Data Model

Report Management owns six tables from the canonical schema (§3). Exact physical names and columns:

| Table | Role in this module |
|---|---|
| **`report_setups`** | The wizard output — one row per report. `definition` (JSONB) = the IR. Holds basics (`report_name`, `report_type`, `channel_type_id`, `commission_cycle`, `start_date`, `end_date`), recurrence (`is_recurrent`, `recurrent_type`), disbursement flags (`is_ev_disbursement`, `ev_disbursement_time`, `is_pos_disbursement`, `sms_content`), the run window (`run_start_date`, `run_end_date`), `status` (varchar, ON/STOP), the **`is_setup_complete`** flag (wizard fully completed), and the `approval_flow_id` binding. `ux_report_setups_report_name` enforces BR3. |
| **`report_supporting_uploads`** | One row per Step-2 CSV that was turned into a real DB table (`db_schema`.`db_table_name`), with its SeaweedFS location (`object_bucket`, `object_key`), `file_name`, and `row_count`. |
| **`section_wise_report_sqls`** *(was `stages`)* | The compiled per-section SQL, one row per stage, frozen at Final Save: `(report_setup_id, stage_order, sql_text)`. `ux_section_wise_report_sqls_setup_order` = UNIQUE(report_setup_id, stage_order). |
| **`report_runs`** | One execution. `run_type int4` (1=Demo, 2=Final), `triggered_by` (user_name; NULL when system/schedule-triggered), `run_status` (varchar: QUEUED/RUNNING/SUCCEEDED/FAILED), `disburse_status` (varchar: NONE/PENDING/DONE/FAILED), `started_at`/`ended_at`. |
| **`run_stages`** | Per-run snapshot of each `section_wise_report_sqls` row (`sql_text` copied at run start), plus execution result: `run_status int4` (0=Pending,1=Running,2=Succeeded,3=Failed), `output_table_name`, the exported-file fields (`document_type`, `bucket`, `object_url`, `file_name`, `file_generated_at`), and `cleanup_status int4` (0=Not Cleaned,1=Cleaned). `ux_run_stages_run_order` = UNIQUE(run_id, sort_order). |
| **`final_commissions`** | Per-recipient output of a run: `(report_run_id, channel_id, channel_code, commission_amount NUMERIC(18,4))`. **`channel_id`** = the report's channel TYPE (constant per report, FK→`channels.id`); **`channel_code`** = the individual recipient/payee code from the IR `final_mapping`. `ux_final_commissions_run_code` = UNIQUE(report_run_id, channel_code). Written by the **trusted backend path**, never by generated SQL. |

Supporting lookups read by the module: **`channels`** (`report_setups.channel_type_id` → `channels.id` — the report's channel TYPE), **`data_sources`** (referenced logically inside the IR), and **`approval_flows`** (`report_setups.approval_flow_id` → `approval_flows.id`).

**Channel model (important).** `channels` is a channel-TYPE lookup (Distributor / RSO / Retailer). A report has exactly one type via `report_setups.channel_type_id`. On `final_commissions` / `ev_disburse`, `channel_id` carries that constant type, while `channel_code` is the per-recipient key produced by the IR. There is **no `channel_code` column on `channels`** and **no special channel resolution** — the executor writes the report's `channel_id` literally and the recipient code from the final block.

**Key relationships (this module):**
`report_setups` (1) → `report_supporting_uploads` (N), `section_wise_report_sqls` (N), `report_runs` (N).
`report_runs` (1) → `run_stages` (N, snapshot of `section_wise_report_sqls`) → `final_commissions` (N, one per recipient) → then EV (`ev_disburse`, N) **or** POS (`pos_disbursement`, 1).
EV/POS are mutually exclusive at report level (app-enforced: `is_ev_disbursement` and `is_pos_disbursement` are never both true).

---

## 6.4 The Five-Step Wizard

Steps 1–2 set up the report shell and its data; **Steps 3–4 build the IR** (`report_setups.definition`); Step 5 reviews, compiles, and (optionally) schedules. Each step has its own save endpoint so the wizard can be left and resumed from a draft.

### 6.4.1 Step 1 — Basic Details

**Overview.** Creates the draft `report_setups` row and returns its `id`; that id keys the rest of the wizard.

**UI.** Fields: **Report Name** (unique, system-wide), **Report Type**, **Commission Cycle** (e.g. "Apr 2026"), **Channel** (dropdown of `channels` rows — the channel TYPE), **Start Date**, **End Date**. Two switches:
- **Recurrent** — when ON, choose a frequency → sets `is_recurrent = true` and `recurrent_type int4` (1=Daily, 2=Weekly, 3=Monthly, 4=Quarterly, 5=Yearly).
- **Disbursement** — choose **EV** or **POS** (radio, mutually exclusive). When **EV**: pick **EV Disbursement Time** (`ev_disbursement_time`) and enter **SMS Content** (`sms_content`). When **POS**: `is_pos_disbursement = true`.

The Maker also picks the **Approval Flow** (`approval_flow_id`, dropdown of `approval_flows`).

**Data model.** Writes `report_setups`: `report_name`, `report_type`, `channel_type_id`, `commission_cycle`, `start_date`, `end_date`, `is_recurrent`, `recurrent_type`, `is_ev_disbursement`, `is_pos_disbursement`, `ev_disbursement_time`, `sms_content`, `approval_flow_id`. On first save the row is created with `is_setup_complete = false`, `status = 'ON'`, and an **IR skeleton** in `definition` (the `report` block only — name/channel/cycle/dates).

**Process logic.**
1. **Create draft** — `POST /reports` (basics only) inserts the row and returns **`{ id }`**, used by every later PUT.
2. **Edit basics** — `PUT /reports/{id}/basics` updates the same row. The `report.*` portion of the IR (name / channel / cycle / dates) is mirrored into `definition.report` so the IR stays self-describing.

**Validation / business rules.**
- **BR3** report name unique system-wide → `ux_report_setups_report_name` → **409** on duplicate.
- **BR4** `end_date >= start_date` → **422**.
- **EV/POS mutually exclusive** (`is_ev_disbursement` XOR `is_pos_disbursement`) → **422** if both set.
- **Recurrent ON requires** a valid `recurrent_type` → **422** if missing.
- **Channel must exist** in `channels` → else **422**.

### 6.4.2 Step 2 — Supporting Uploads

**Overview.** The Maker uploads CSV files (targets, slab tables, agent lists, exclusion lists, prior-run outputs for de-dup). Each confirmed file becomes a **real DB table** that Steps 3–4 can read as a `source` — the engine's #1 capability (external-config join). Ingestion mechanics are specified in §5.7 (CSV→DB ingestion).

**UI.** Drag-drop or browse CSV (first row = headers). Multiple files allowed, each listed with serial no., file name, generated source name, and **Remove** (before save). A preview shows the first rows with **auto-detected column types**; the Maker may override a type, and any value not matching its type is flagged as a warning (does not block).

**Data model.** On confirm, one **`report_supporting_uploads`** row per file: `report_setup_id`, `db_schema`, `db_table_name`, `object_bucket`, `object_key`, `file_name`, `row_count`, `uploaded_at`, `uploaded_by`. `ix_report_supporting_uploads_report_setup` indexes the FK; the ingestion routine ensures the generated table name is unique within the report.

**Process logic.**
1. `POST /reports/{id}/uploads` (multipart) → BE stages the object in SeaweedFS (`object_bucket`/`object_key`), sanitises identifiers, infers types, creates a physical table (`db_schema`.`db_table_name`), bulk-loads it via `COPY`, records `row_count`, and writes the `report_supporting_uploads` row (§5.7.5).
2. The new table is now selectable as a `source` of type `upload` in Steps 3–4 (referenced by `db_table_name`).

**Validation / business rules.** Per §5.7 ingestion rules. Removing an upload before the report is locked deletes the registry row and the physical table.

### 6.4.3 Step 3 — Achievements (builds the IR `achievements[]`)

**Overview.** The Maker builds one or more **Achievement blocks** (`ACH1`, `ACH2`, …), each computing a performance figure per recipient (e.g. Recharge %, GA %). **This step writes `definition.achievements[]` of the IR.**

**UI.** For each block: pick a **source** (a registered `data_source`, a Step-2 `upload`, or another existing achievement block). The block is a pipeline of expandable cards in order:
- **Filter** — Column, Operator (Is one of / Equals / Not equals / Greater than / Less than / ≥ / ≤ / Between / Is null / Not null), Value(s); AND/OR combine; optional negate.
- **Combine Data** — Get-Data-From (data source / upload / existing block); join condition (current column = other column); how (inner / left / right / full); columns to bring.
- **Summarize** — group-by column(s), result column name, calculation (Count / Count Distinct / Sum / Avg / Min / Max). **This sets the block's grain.**
- **Calculate** — Math Formula, IF/CASE (slab / category / tier), or rounding/map.
- **Modify** — cast / rename a column.

The block's **Outputs panel** lists the columns it produces. Blocks can only be removed **from the end**.

**Data model / IR mapping.** Each card maps **directly** to an IR stage op: Filter→`filter`, Combine Data→`combine`, Summarize→`summarize`, Calculate→`calculate` (mode `formula`/`ifcase`/`map`), Modify→`modify`. The block object (`block_id`, `source`, `stages[]`, `output_grain`, `outputs[]`) is the annex Block (`IR_Schema_and_MultiKPI_Join.md` §2–§3). The Maker's blocks are persisted into `report_setups.definition.achievements[]`.

**Process logic.**
1. The FE builds each block's `stages[]` as the Maker adds cards.
2. **Save Step 3** — `PUT /reports/{id}/achievements` with `{ achievements: [...blocks] }`; the backend merges this into `definition.achievements` and persists (still `is_setup_complete = false`, **no SQL yet**).
3. **Reference-resolution check** (on save and again at Final Save): every column a stage refers to must exist in the prior stage's output or in a joined block/source; **the last stage of every block must be a `summarize`** (grain rule). Failures ⇒ **422** with a per-block, per-stage message.

**Validation / business rules.** Each block ends in `summarize` → `output_grain` = its `group_by`. A block-to-block join must match on the grain key; guardrail **G1** (pre-join uniqueness) runs at execute time. All referenced columns must resolve; unknown column ⇒ 422. At least one achievement block is required before Final Save.

### 6.4.4 Step 4 — Incentives (builds the IR `incentives[]` + `final_mapping`)

**Overview.** The Maker builds one or more **Incentive blocks** (`INC1`, …) that turn achievement(s) into a payout per recipient, then defines the **Final mapping** that produces `final_commissions`. **This step writes `definition.incentives[]` and `definition.final_mapping`.**

**UI.** The same five cards as Step 3. An incentive block reads one or more **achievement** blocks (or a data source / upload); an existing incentive can feed another. Typical shape: `combine` ACH1↔ACH2 on the grain key → `calculate/ifcase` Category → `calculate/ifcase` Amount. The **Final mapping** card maps the chosen block's columns onto the commission output: **Channel Code column** (the per-recipient payee key) and **Commission Amount column**, grouped per recipient.

**Data model / IR mapping.** Incentive blocks → `definition.incentives[]`. The Final mapping → `definition.final_mapping` (`from_block`, `channel_code_column`, `commission_amount_column`, `channel_scope`). At run time the engine groups `from_block` by `channel_code_column` and writes one `final_commissions` row per recipient — `channel_code` = that grouped key, `channel_id` = the report's `channel_type_id`.

**Process logic.**
1. **Save Step 4** — `PUT /reports/{id}/incentives` with `{ incentives: [...blocks], final_mapping: {...} }`; backend merges into `definition.incentives` + `definition.final_mapping` (still `is_setup_complete = false`).
2. Same reference-resolution and grain checks as Step 3.

**Validation / business rules.** `final_mapping.from_block` must reference an existing block; the two mapped columns must exist in that block's outputs ⇒ else 422. `final_mapping.channel_code_column` must be **unique per row** in `from_block` (one commission per recipient) — verified by guardrail **G1**. Incentive blocks obey the same grain rule (end in `summarize`).

### 6.4.5 Step 5 — Review / Run / Schedule (Success)

**Overview.** Review the whole setup, perform the **Final Save** (validate the IR + compile to SQL), then choose: go back to Reports, **Demo Run**, **submit for Approval**, or **Schedule**.

**UI.** Read-only summary of Steps 1–4 + the Outputs of the final block. Buttons: **Final Save**, then (after a successful Final Save) **Demo Run**, **Submit for Approval**, and **Schedule for Later**. A **Success** confirmation screen offers **Back to Reports** / **Schedule for Later**.

**Process logic.** Final Save triggers the Draft→Final transition in §6.5 (validate IR → SQL Generator compiles into `section_wise_report_sqls` → `is_setup_complete = true`). Scheduling and Run-Now are detailed in §6.9 and §6.6.

---

## 6.5 Report Save / Publish State Machine

The lifecycle is driven by three things: the **`is_setup_complete`** flag (wizard finished + SQL compiled), the presence of **`section_wise_report_sqls`** rows, and the **approval progress** in `report_approvals` / `report_approval_details` (§7). The report-list status label is **derived** from approval progress (§6.2); there is no separate stored status string for the approval phase beyond `report_approvals.overall_status`.

| State | Flag / approval | Meaning | What's allowed |
|---|---|---|---|
| **DRAFT** | `is_setup_complete = false`, no `section_wise_report_sqls` | Saved, IR may be incomplete. **No SQL, no demo, no approval.** | Edit any step; Save as Draft; soft-delete. |
| **FINAL-SAVED** | `is_setup_complete = true`, `section_wise_report_sqls` populated, no `report_approvals` yet | IR validated and compiled. | Edit (→ re-validate + regenerate SQL); **Demo Run**; **Submit for Approval**. |
| **APPROVAL PENDING** | `report_approvals.overall_status` = 1 (Pre Approval Pending) or 3 (Post Approval Pending) | Submitted; walking the flow levels. | **Editable** — any IR edit re-validates and **regenerates** `section_wise_report_sqls`, and voids the in-flight approval (back to status 0). |
| **APPROVED (LOCKED)** | `overall_status` = 2 (Pre Approved) / 4 (Post Approved) | Final approval done. | **No IR edits.** Run Now, Schedule, Demo, Clone, Stop/Start only. |
| **REJECTED / DRAFT-FOR-EDIT** | `overall_status` = 0 (Pending for Editing & Resubmission) | An approver rejected (BR7 comment required); bounced back to the Maker. | Maker edits and resubmits → restarts the pending phase. |

**Transition logic:**

1. **Draft Save** (`POST /reports`, `PUT /reports/{id}/{step}`) — persists basics / IR fragments. Does **not** validate the full IR and does **not** generate SQL. Keeps `is_setup_complete = false`.
2. **Final Save** (`POST /reports/{id}/final-save`):
   - **(a) Validate IR** against the annex JSON Schema + semantic checks (every block ends in `summarize`; all column references resolve; `final_mapping` targets exist; grain-join rule). On failure ⇒ **422**; nothing is compiled, `is_setup_complete` stays false.
   - **(b) Compile** — publish `report.saved`; the Python **SQL Generator** builds SQL per stage with **SQLGlot (AST, no string concatenation)** and writes `section_wise_report_sqls` rows, replacing any prior compile transactionally (delete-then-insert under `ux_section_wise_report_sqls_setup_order`).
   - **(c)** Set **`is_setup_complete = true`**. The report is now FINAL-SAVED: Demo-runnable and approval-eligible.
3. **Submit for Approval** (`POST /reports/{id}/submit`) — requires `is_setup_complete = true` and a bound `approval_flow_id`. Opens one `report_approvals` row (`overall_status = 1` Pre Approval Pending, `current_level_order` = first level) — see §7. BR5 enforced when resolving level actors.
4. **Edit while pending** — allowed; any IR change forces re-validation + SQL regeneration into `section_wise_report_sqls`, and voids the in-flight `report_approvals` (reset to `overall_status = 0`).
5. **Final Approval → LOCKED** (event from §7) — `overall_status` advances to 2 (Pre Approved) or 4 (Post Approved). The report is LOCKED: wizard PUTs and `final-save` return **409**. Only Run Now / Schedule / Demo / Clone / Stop-Start remain.
6. **Reject** (event from §7) — `overall_status = 0`; Maker notified, edits, resubmits.

> **SQL freeze vs. setup edits.** `section_wise_report_sqls` holds the *current* compiled SQL. A **run** does not read it live — §6.6 step 4 **snapshots** it into `run_stages`, so editing the setup after a run starts never affects a run already in flight (decision D2).

---

## 6.6 Run Execution Lifecycle

Connects `report_setups` → `section_wise_report_sqls` → `report_runs` → `run_stages` → `final_commissions` → EV/POS. Runs are **single-at-a-time** with a priority queue (decision D1): **Run Now = high, Demo = mid, Schedule = low**. (Full async/worker mechanics are in §11.)

| # | Step | What happens | Tables touched |
|---|---|---|---|
| 1 | **Trigger** | Run Now (after full approval), Demo Run, or the Scheduler fires. Sets `run_type` (1=Demo / 2=Final) and `triggered_by` (`user_name`, or **NULL** for system/schedule). | — |
| 2 | **Queue** | Publish `run.requested` to a priority queue (high / mid / low). The Executor processes **one run at a time** platform-wide (single advisory lock); others wait. | — |
| 3 | **Create run** | Insert `report_runs` (`run_status = 'QUEUED'` → `'RUNNING'`, `started_at = now()`, `disburse_status = 'NONE'`). | `report_runs` |
| 4 | **Snapshot section SQL** | Copy every `section_wise_report_sqls` row into `run_stages` (`sql_text` copied, `sort_order` from `stage_order`, `run_status = 0` Pending). **Freezes the SQL for this run.** | `run_stages` |
| 5 | **Execute each stage** | Run each `run_stages.sql_text` sequentially under a **least-privilege** role, after an **execute-time allowlist re-parse** (D2: only SELECT/WITH/JOIN/aggregate/CASE + CREATE/DROP TEMP TABLE, bound literals, identifier whitelist). Each stage materialises a temp/output table → `output_table_name`. `run_status` walks 0→1→2/3. Guardrails G1 (pre-join uniqueness) / G2 (post-join fan-out). | `run_stages` |
| 6 | **Export outputs** | Each succeeded stage is written as CSV to SeaweedFS; record `bucket`, `object_url`, `file_name`, `file_generated_at`, `document_type`. (Demo: powers per-stage row-count visibility, G3.) | `run_stages` |
| 7 | **Write final commissions** | The **trusted backend path** (not generated SQL) reads the final block's output, groups by `final_mapping.channel_code_column`, and inserts one `final_commissions` row per recipient — `channel_id` = the report's `channel_type_id` (constant), `channel_code` = the grouped recipient key, `commission_amount` rounded. Idempotent: `INSERT … ON CONFLICT DO NOTHING` on `ux_final_commissions_run_code (report_run_id, channel_code)`. G4 reconciliation (disbursed total == sum of block outputs). | `final_commissions` |
| 8 | **Cleanup** | Drop all temp tables; set each `run_stages.cleanup_status = 1` (Cleaned). Set `report_runs.run_status = 'SUCCEEDED'` (`ended_at`), or `'FAILED'`. | `run_stages`, `report_runs` |
| 9 | **Disbursement** (FINAL + fully approved only) | EV: enqueue payout → `ev_disburse` per recipient + SMS. POS: build CSV → `pos_disbursement`. `report_runs.disburse_status` walks `NONE → PENDING → DONE/FAILED`. EV/POS mutually exclusive. (Full chain in §10.) | `ev_disburse` **or** `pos_disbursement`, `report_runs` |

**Failure / recovery.** A failed stage sets `run_stages.run_status = 3` (Failed), aborts the run, sets `report_runs.run_status = 'FAILED'`, and **still runs cleanup**. **No `final_commissions` is written on failure** (no partial payout). **Stale-run recovery:** because only one run executes at a time, a `report_runs` row left `RUNNING` after an Executor crash is detected (no active lease), marked `FAILED`, its temp tables dropped, and the queue resumes. Re-running creates a **new** `report_runs` row (runs are never resumed mid-way).

**Idempotency of disbursement.** Step 9 is guarded by `ux_ev_disburse_run_code (report_run_id, channel_code)` on `ev_disburse` and `ux_pos_disbursement_run (report_run_id)` on `pos_disbursement`, all written `ON CONFLICT DO NOTHING` (at-most-once payout per recipient per run; see §6.11 and §10).

---

## 6.7 Demo Run

A Demo Run exercises the full compile→execute pipeline **without approval and without disbursement** — so the Maker can verify numbers and catch fan-out before submitting.

- Requires **FINAL-SAVED** (`is_setup_complete = true`, `section_wise_report_sqls` rows exist). Available in any state from FINAL-SAVED onward (including LOCKED).
- `run_type = 1` (Demo); `triggered_by` = the Maker's `user_name`; never disburses; `report_runs.disburse_status` stays `'NONE'`.
- A Demo writes `final_commissions` for the demo run id (so the Maker sees per-recipient results) but **never** triggers Step 9.
- **Demo SKIPS all pre-run checks** (see §6.10) — it does not require approval, a past end date, or completed ETL. It is a sandbox preview.
- **G3 visibility:** the Demo run surfaces `run_stages.output_table_name` row counts after every stage in the Run Log, so unexpected row multiplication is visible **before** any real run.

---

## 6.8 Clone

Clone copies a whole report setup into a **new draft**.
- **Copies:** basics, the full `definition` IR (all blocks/references), EV/POS settings, recurrence, and the `approval_flow_id` binding.
- **Does NOT copy:** uploaded files (`report_supporting_uploads`), `section_wise_report_sqls`, past runs, approvals, or disbursements.
- The new report starts in **DRAFT** (`is_setup_complete = false`) with a **new, unique `report_name`** (the Maker must supply/confirm a non-colliding name; BR3).
- Because uploads are not copied, any IR `source` of type `upload` is flagged in the new draft as "needs re-upload" until re-added in Step 2.

**Endpoint:** `POST /reports/{id}/clone` → returns the new draft `{ id }` (201).

---

## 6.9 Schedule

Only the **Maker (owner)** or **Administrator** can create / edit / cancel a schedule, and **every change is audited** (BR9 → an `audit_logs` row per call).

- A schedule requires the report to be **fully approved** (LOCKED).
- **Non-recurrent** report: runs **once** at a single scheduled date/time. **Recurrent** report: runs repeatedly on its `recurrent_type` frequency between `run_start_date` and `run_end_date`.
- Scheduled runs enter the queue at **low** priority and set **`triggered_by = NULL`** (system-triggered).
- **Report Stop/Start** toggles `report_setups.status` (`ON`/`STOP`); a `STOP` report's scheduled runs do not fire. Toggling is audited.

**SCHEDULE VALIDATION (2026-06-16 decision):** the **schedule run date must be ≥ the report `end_date`**. A commission report can only be run after its measurement period has ended, so any schedule date earlier than `end_date` is rejected with **422**. This is checked on `POST /reports/{id}/schedules` for both the single date (non-recurrent) and the `run_start_date` (recurrent).

**Endpoint:** `POST /reports/{id}/schedules` (create / edit / cancel) — Maker/Admin only; writes `run_start_date`/`run_end_date`, registers the Hangfire job, and writes an `audit_logs` row.

---

## 6.10 Pre-Run Checks (Final runs only)

Before a **Scheduled** or **Run-Now FINAL** run is allowed to enqueue, **all** of the following must pass; otherwise the trigger is rejected (403/409/422) and **no `report_runs` row is created**. A **Demo run SKIPS every one of these checks** (§6.7).

1. **Report period ended** — `report_setups.end_date <= today`. A FINAL run cannot start before the measurement window closes. Else **422**.
2. **Setup approval fully done** — the report is fully approved (`report_approvals.overall_status` = 2 Pre Approved or 4 Post Approved as applicable to its flow). Else **403** (precursor to BR8 — money only moves after full approval).
3. **Source ETL complete** — every **system data source** used in the IR (every `source.type = "data_source"`) has its ETL finished **up to the report `end_date`** (the prepared source tables are loaded through the report period). Else **422** ("source `<table>` not loaded through `<end_date>`").

Additional structural gates also apply to any FINAL run:
4. **Active** — `report_setups.status = 'ON'`. Else **409**.
5. **Compiled** — `section_wise_report_sqls` rows exist (`is_setup_complete = true`). Else **409**.
6. **Sources present** — every IR `upload` source has a `report_supporting_uploads` row. Missing upload ⇒ **422**.
7. **Single-run guard** — no run for this report is already `QUEUED`/`RUNNING` (duplicate Run-Now click ⇒ **409**).

> A **Demo** run requires only the structural gates 5–6 and is subject to no period/approval/ETL checks.

---

## 6.11 Idempotency

| Concern | Constraint / mechanism |
|---|---|
| **Run creation** | Not auto-deduplicated, but the single-run priority queue (D1) + Executor lease means only one run per report executes at a time; a duplicate Run-Now while a run is `QUEUED`/`RUNNING` ⇒ **409**. |
| **Per-stage snapshot** | `ux_run_stages_run_order` = UNIQUE(`run_id`, `sort_order`) on `run_stages`. |
| **Final commission output** | `ux_final_commissions_run_code` = UNIQUE(`report_run_id`, `channel_code`); written `ON CONFLICT DO NOTHING`. The recipient key is **`channel_code`**, not `channel_id` (which is the constant report channel TYPE). |
| **EV disbursement** | `ux_ev_disburse_run_code` = UNIQUE(`report_run_id`, `channel_code`); `ON CONFLICT DO NOTHING`. |
| **POS disbursement** | `ux_pos_disbursement_run` = UNIQUE(`report_run_id`) — one CSV handoff per run; `ON CONFLICT DO NOTHING`. |
| **Compile** | Re-running Final Save rebuilds `section_wise_report_sqls` transactionally (delete-then-insert under `ux_section_wise_report_sqls_setup_order`). |

Together with BR8 (disburse only after full approval), these constraints guarantee **at-most-once payout per recipient per run**.

---

## 6.12 API Endpoints

All paths under **`/api/v1`**. JWT on every request. Standard error envelope and HTTP codes. Role column: **M** = Maker (owner), **A** = Administrator, **C** = Checker.

| Method | Path | Purpose | Role | Success |
|---|---|---|---|---|
| GET | `/reports` | List with status / period / channel filter + search + paging | M/A/C | 200 |
| POST | `/reports` | **Create draft (Step 1 basics)** → returns draft id | M/A | 201 |
| GET | `/reports/{id}` | Read full report + IR + derived status | M/A/C | 200 |
| PUT | `/reports/{id}/basics` | Save Step 1 | M/A | 200 |
| POST | `/reports/{id}/uploads` | Register + ingest a supporting CSV | M/A | 201 |
| DELETE | `/reports/{id}/uploads/{uploadId}` | Remove a supporting upload (before lock) | M/A | 204 |
| PUT | `/reports/{id}/achievements` | **Save Step 3 (writes IR `achievements[]`)** | M/A | 200 |
| PUT | `/reports/{id}/incentives` | **Save Step 4 (writes IR `incentives[]` + `final_mapping`)** | M/A | 200 |
| POST | `/reports/{id}/final-save` | **Validate IR → compile SQL → populate `section_wise_report_sqls`, set `is_setup_complete=true`** | M/A | 200 |
| POST | `/reports/{id}/submit` | Submit for approval (FINAL-SAVED → Pre Approval Pending) | M/A | 200 |
| POST | `/reports/{id}/clone` | Clone to a new draft | M/A | 201 |
| POST | `/reports/{id}/runs` | Run Now (FINAL) or Demo Run (DEMO) | M/A | 202 |
| POST | `/reports/{id}/schedules` | Create / edit / cancel a schedule | M/A | 200 |
| PATCH | `/reports/{id}/status` | Stop / Start (`status` ON/STOP) | M/A | 200 |
| GET | `/runs/{id}` | Read a run with all `run_stages` rows | M/A/C | 200 |
| GET | `/runs/{id}/stages/{stageId}/output` | Download a stage output (CSV) | M/A/C | 200 |

**`POST /reports`** — create draft (Step 1).
```json
// Request
{ "reportName": "RSO Campaign GA Recharge LSO Apr26", "reportType": "COMMISSION",
  "channelTypeId": 2, "commissionCycle": "Apr 2026",
  "startDate": "2026-04-01", "endDate": "2026-04-30",
  "isRecurrent": false, "recurrentType": null,
  "disbursement": "EV", "evDisbursementTime": "18:00:00",
  "smsContent": "Apnar commission {amount} BDT credit kora hoyeche.",
  "approvalFlowId": 3 }
// Response 201
{ "id": 1187, "isSetupComplete": false, "status": "ON", "derivedStatus": "Draft" }
```
Errors: **409** (duplicate `reportName`), **422** (end<start; both EV+POS; recurrent without type; unknown channel), **403**.

**`PUT /reports/{id}/achievements`** — Step 3 (carries the IR achievements).
```json
// Request
{ "achievements": [ /* annex Block objects: ACH1, ACH2, ... */ ] }
// Response 200
{ "id": 1187, "blockCount": 2, "valid": true, "issues": [] }
```
Errors: **422** (`{ blockId, stageIndex, message }` — column reference unresolved, block does not end in `summarize`, schema-invalid block), **409** (report LOCKED).

**`PUT /reports/{id}/incentives`** — Step 4 (IR incentives + final mapping).
```json
// Request
{ "incentives": [ /* INC1, ... */ ],
  "finalMapping": { "fromBlock": "INC1", "channelCodeColumn": "RSO_CODE",
    "commissionAmountColumn": "Incentive", "channelScope": "RSO" } }
// Response 200
{ "id": 1187, "incentiveCount": 1, "finalMappingValid": true, "issues": [] }
```
Errors: **422** (`fromBlock`/columns not found; grain-join violation), **409** (LOCKED).

**`POST /reports/{id}/final-save`** — validate + compile.
```json
// Request: {}
// Response 200
{ "id": 1187, "irValid": true, "stagesGenerated": 7,
  "isSetupComplete": true, "demoRunnable": true }
// Response 422 (nothing compiled, is_setup_complete stays false)
{ "irValid": false, "issues": [
    { "path": "achievements[1].stages[2]", "code": "COLUMN_NOT_FOUND",
      "message": "TARGET not produced by ACH2" } ] }
```
Errors: **422** (IR invalid), **409** (LOCKED).

**`POST /reports/{id}/runs`** — Run Now / Demo.
```json
// Request
{ "runType": "FINAL" }   // "FINAL" (run_type=2) | "DEMO" (run_type=1)
// Response 202 (queued)
{ "runId": 90412, "runType": "FINAL", "runStatus": "QUEUED", "queuePriority": "high" }
```
Errors (FINAL, from §6.10 pre-run checks): **403** (not fully approved), **422** (end_date>today, source ETL incomplete, missing upload), **409** (report STOP, not compiled, run already active). DEMO skips period/approval/ETL checks.

**`POST /reports/{id}/schedules`** — schedule (Maker/Admin, audited).
```json
// Request
{ "action": "CREATE",
  "runStartDate": "2026-05-01T18:00:00Z", "runEndDate": "2026-05-31T18:00:00Z" }
// Response 200
{ "id": 1187, "scheduled": true,
  "runStartDate": "2026-05-01T18:00:00Z", "runEndDate": "2026-05-31T18:00:00Z" }
```
Errors: **403** (not Maker/Admin), **409** (report not fully approved), **422** (schedule date < report `end_date`).

**`GET /runs/{id}`** — run detail.
```json
// Response 200
{ "runId": 90412, "reportId": 1187, "runType": "FINAL", "runStatus": "SUCCEEDED",
  "disburseStatus": "DONE", "triggeredBy": "muntakim.r",
  "startedAt": "2026-05-01T18:00:03Z", "endedAt": "2026-05-01T18:01:11Z",
  "stages": [
    { "sortOrder": 1, "runStatus": 2, "outputTableName": "tmp_ach1_filter_90412",
      "fileName": "stage_1.csv", "objectUrl": "s3://salescom/runs/90412/stage_1.csv",
      "cleanupStatus": 1 } ],
  "finalCommissionRowCount": 318 }
```

---

## 6.13 Validation and Business Rules (summary)

| Rule | Where enforced |
|---|---|
| **BR3** report name unique system-wide | `ux_report_setups_report_name` → **409** at Step-1 create; Clone forces a new unique name |
| **BR4** `start_date <= end_date` | Step-1 validation → **422** |
| **EV/POS mutually exclusive** | App guard (`is_ev_disbursement` XOR `is_pos_disbursement`) → **422** + pre-run |
| **Recurrent needs a type** | App validation → **422** |
| **IR must be schema-valid + grain-correct** | Final Save validation → **422** |
| **Every block ends in `summarize`; block-joins on grain key** | Final Save + run guardrails G1–G2 |
| **Schedule date ≥ report `end_date`** | Schedule endpoint check → **422** |
| **Pre-run checks (FINAL only; Demo skips)**: end_date ≤ today; fully approved; source ETL complete to end_date | §6.10 → 403/409/422 |
| **Run-Now only after full approval (BR8 precursor)** | Pre-run check 2 → **403** |
| **Edit locked once fully/post-approved** | LOCKED state → wizard PUTs / final-save return **409** |
| **Only Maker/Admin manages schedule; all changes audited (BR9)** | Schedule endpoint role check → **403** + `audit_logs` row |
| **BR5** same user can't be maker + checker of a run | Enforced at submit/approval (§7) |
| **No double-pay** | UNIQUE(report_run_id, channel_code) on `final_commissions` / `ev_disburse`; UNIQUE(report_run_id) on `pos_disbursement`; all `ON CONFLICT DO NOTHING` |
| **No partial disbursement** | `final_commissions` written atomically by the trusted path; null / duplicate / unmapped `channel_code` = hard run failure |

---

# §7 Approval (Maker-Checker)

## 7.1 Overview

Every commission report passes through a **maker-checker approval** before its money can move. The **Maker** (the Business User who created or last edited the report) builds and submits it; one or more **Checkers** (Approvers) review it **level by level** in strict order. Only after the last required level approves does the report become runnable (setup / pre-run approval) and, for non-recurrent reports, its run results become disbursable (result / post-run approval).

The approval engine is **flow-driven and reusable**. An Administrator defines a small library of named **Approval Flows** (`approval_flows`); each flow is an ordered list of **Levels** (`approval_flow_levels`); each level is staffed by one or more eligible **Users** (`approval_flow_level_users`) and carries an **`approval_type`** `int4` code that encodes *when in the lifecycle* the level acts:

- **PRE_RUN** (`approval_type = 1`) — approve the **setup** (the wizard / IR configuration) **before** the report is allowed to run.
- **POST_RUN** (`approval_type = 2`) — approve the **results** of a specific Final run **after** it produces `final_commissions`, and **before** disbursement.

A report is bound to exactly one flow at creation/edit time (`report_setups.approval_flow_id`). When the report is submitted, the system opens **one** `report_approvals` row and walks it up the levels in ascending `level_order`. Each individual decision is permanently recorded in `report_approval_details`.

> **No `approval_type` lookup table.** Phase is an `int4` enum stored directly on `approval_flow_levels.approval_type` (`1 = PRE_RUN`, `2 = POST_RUN`). The app owns the code→meaning mapping (Appendix B).

**`report_approvals.overall_status` (int4) is the single lifecycle driver** (2026-06-16 decision):

| Code | Meaning | When |
|---|---|---|
| **0** | Pending for Editing & Resubmission (Draft) | After a reject, or while the Maker is editing — the report is back with the Maker. |
| **1** | Pre Approval Pending | A PRE_RUN setup request is walking its levels. |
| **2** | Pre Approved | All PRE_RUN levels passed; the report is runnable. |
| **3** | Post Approval Pending | A POST_RUN result request (for one Final run) is walking its levels. |
| **4** | Post Approved | All POST_RUN levels passed; the run is cleared for disbursement. |

Key locked behaviours (these resolve the contradictions in the earlier SRS/LLD):

| Topic | Decision |
|---|---|
| **Reject destination** | A reject at **any** level returns the request to the **Maker** — *not* to the previous level. `overall_status` is set to **0** (Pending for Edit & Resubmission). |
| **Resubmit** | The Maker fixes the report and resubmits; approval **restarts from level 1** with **full re-validation**. No "resume from where it failed." |
| **Edit while pending** | Any edit to a report with a live pending request **voids** the in-flight progress and forces a restart from level 1 (status back to 0). A fully Pre-Approved setup is not editable (Edit hidden). |
| **POST_RUN target** | A POST_RUN approval is tied to **one specific Final run**. (The `report_approvals` row's `overall_status` moves 2 → 3 → 4 for that run; identity of the run is carried on `report_runs`/the event.) |
| **Recurrent reports** | Must use a **PRE_RUN-only** flow. Setup is approved once (status reaches 2); every later scheduled run executes and pays out automatically with no per-run approval. |
| **Non-recurrent reports** | May use PRE_RUN and/or POST_RUN levels. After a Final run, the POST_RUN levels approve the **results** (status 3 → 4) before disbursement. |
| **Demo run** | **Never** needs approval and **never** disburses. The orchestrator skips all approval logic for `run_type = 1` (Demo). |
| **Segregation (BR5)** | The Maker = `COALESCE(report_setups.updated_by, report_setups.created_by)` can never be a Checker on the same report; one user occupies at most one level per request; no self-approval. |

---

## 7.2 UI

Four screens under the **Approvals** area. The first three are **Administrator-only** flow setup; the fourth is the day-to-day **Approver** workspace.

**(a) Approval Flow list** — serial, flow name, description, row actions (Edit, Manage Levels, Deactivate). The **"Add a New Flow"** dialog asks for a **unique flow name** and a description. Backed by `approval_flows`.

**(b) Approval Level list** (inside a flow) — serial, flow, level name, **order** (a unique whole number within the flow), the level's **phase** (PRE_RUN / POST_RUN, from `approval_flow_levels.approval_type`), actions (Edit, Manage Users, Remove). **"Add Level"** asks for level name, **order** (unique within the flow), and the phase. Levels render sorted by `level_order` ascending. Backed by `approval_flow_levels`.

**(c) Approval Level User list** (inside a level) — serial, flow, level, and the assigned approver (full name, username, email), with Add/Remove. Users are looked up from the synced company directory (`users`); the assignment stores the **external** `users.user_id` (varchar) in `approval_flow_level_users.user_id`. Backed by `approval_flow_level_users`.

**(d) Approver queue** — the signed-in user's **pending approvals**: one card per `report_approvals` row currently sitting at a level they staff. Each card shows report name, Maker, **phase badge** (Setup / Result, derived from the current level's `approval_type`), submitted-at, **level X of N**, and — for POST_RUN — a results summary (recipient count, total amount, link to the run detail and its per-stage row counts / G3 output). Buttons: **Approve**, **Reject**. A **comment box is mandatory** on Reject (BR7). An **Approval History** view lists every `report_approval_details` row.

The Maker sees status on the **Report list / detail** as a **derived label** (§7.4.8) computed from `report_approvals.current_level_order` + `report_approval_details` (e.g. *"Approved by L1, now Pending at L2"*). **Edit is hidden once a setup is Pre-Approved** (`overall_status >= 2`); **Run Now / schedule activation** are enabled only after Pre Approval (status 2); **Disburse** runs only after Post Approval (status 4), or immediately for recurrent PRE_RUN-only reports.

---

## 7.3 Data Model

All tables are defined in the canonical DDL (§3.6). The approval module uses six tables (no `approval_type` lookup table — phase is an int enum):

| Table | Role in approval |
|---|---|
| **`approval_flows`** | A reusable named flow. `flow_name`, `description`. Bound to a report via `report_setups.approval_flow_id`. |
| **`approval_flow_levels`** | One ordered level in a flow. `level_order` (sequential ascending, unique per flow via `ux_approval_flow_levels_flow_order`), `level_name`, and **`approval_type` (int4 phase: 1=PRE_RUN, 2=POST_RUN)**. A single flow can mix PRE_RUN and POST_RUN levels. |
| **`approval_flow_level_users`** | Eligible approvers per level. `user_id` is the **external** `users.user_id` (varchar — stored by value, **not** a FK to `users.id`). UNIQUE(`approval_flow_level_id`, `user_id`). |
| **`report_approvals`** | The **one live approval instance** per report. Holds `report_setup_id`, `approval_flow_id`, `current_level_order`, **`overall_status` (int4 0–4)**, `initiated_by`, `initiated_at`. |
| **`report_approval_details`** | One row per decision: `approval_request_id` (→ `report_approvals.id`), `level_order`, **`approval_status` (int4: 1=Approved, 2=Rejected)**, `comments` (required on reject — BR7), `approval_by`, `approval_at`. The permanent audit trail and the source of the derived status label. |

> **Naming note.** The FK column on `report_approval_details` keeps its physical name **`approval_request_id`** even though it now references the renamed `report_approvals.id`. Only the table name (`approval_requests` → `report_approvals`, `approval_decisions` → `report_approval_details`) and the three decision columns (`decision` → `approval_status`, `decided_by` → `approval_by`, `decided_at` → `approval_at`) changed in the 2026-06-16 decision.

**Phase resolution rule.** For a live `report_approvals` row, the **active phase = the `approval_type` of the `approval_flow_levels` row at `current_level_order`** (looked up via `approval_flow_id` + `current_level_order`). The engine never assumes phase from anything else — this is what lets one flow mix PRE_RUN and POST_RUN levels. Equivalently: `overall_status = 1` means the current level is a PRE_RUN level; `overall_status = 3` means it is a POST_RUN level.

**Flow well-formedness** (validated at flow-save and re-checked at report Final-Save / binding):
- Within a flow, **all PRE_RUN levels (`approval_type=1`) must order strictly before all POST_RUN levels (`approval_type=2`)**.
- Each in-use level must have **≥1 active user**.
- A flow bound to a **recurrent** report (`report_setups.is_recurrent = true`) must be **PRE_RUN-only**.

**One live instance.** A report has at most one `report_approvals` row in flight. A non-recurrent report with both phases uses **one row across its lifetime**: it walks PRE_RUN levels (status 1) → reaches Pre Approved (status 2); then after a Final run, the same row is advanced into the POST_RUN block (status 3) → Post Approved (status 4). The setup-vs-result distinction is the `overall_status` value and the phase of `current_level_order`.

---

## 7.4 Process Logic

### 7.4.1 Flow setup (Administrator)
1. Admin creates an `approval_flows` row (unique `flow_name`, description).
2. Admin adds `approval_flow_levels` rows (`level_order` 1..N unique within the flow; each row's `approval_type` = 1 PRE_RUN or 2 POST_RUN).
3. Admin assigns `approval_flow_level_users` rows per level (≥1 active user per level before the flow can be used); `user_id` = the external `users.user_id`.
4. On save, the system runs **flow well-formedness** checks: all PRE_RUN levels precede all POST_RUN levels; each level has ≥1 user.

### 7.4.2 Binding a flow to a report
At report creation/edit the Maker picks `report_setups.approval_flow_id`. At **Final Save** (`is_setup_complete = true`): if `is_recurrent = true`, the bound flow **must be PRE_RUN-only** (else hard validation error `FLOW_NOT_WELLFORMED`). A flow with POST_RUN levels is only meaningful for non-recurrent reports.

### 7.4.3 Submit for approval (open the PRE_RUN setup request)
Trigger: Maker clicks **Submit for Approval** on a report whose wizard is complete (`is_setup_complete = true`) and which has no live `report_approvals` row (or whose last one is `overall_status = 0`).
1. **Segregation pre-check (BR5):** resolve the report's Maker = `COALESCE(report_setups.updated_by, report_setups.created_by)`. For **every** level of the bound flow, ensure the Maker is **not the only** eligible user (a Maker-only level would deadlock). If any level is Maker-only → reject submit `409 MAKER_IS_SOLE_APPROVER`.
2. **Validate** IR shape (`definition`), flow well-formedness, BR4 dates, and EV/POS mutual exclusion.
3. Open / reset the `report_approvals` row: `report_setup_id` = the report; `approval_flow_id` = bound flow; `current_level_order` = the **lowest** `level_order`; `overall_status = 1` (Pre Approval Pending); `initiated_by` = Maker `user_name`; `initiated_at = now()`. (If a row already exists at status 0, advance it back to status 1 and reset `current_level_order`; otherwise insert a new one.)
4. Set `report_setups.status` to a "Pending Approval" value.
5. Publish `approval.requested` → email level-1 users. Write `audit_logs` (`action_type = 2 Update`, `entity_name = 'report_approvals'`).

### 7.4.4 Acting on a request — Approve
Trigger: an eligible user of the **current** level calls the decision endpoint with Approve.
1. **Authorize:** caller must be listed in `approval_flow_level_users` for the level at `report_approvals.current_level_order` (matched by `users.user_id`). Else `403 NOT_CURRENT_LEVEL_APPROVER`.
2. **Segregation (BR5):** caller ≠ Maker (`COALESCE(updated_by, created_by)`); caller must **not** already appear in `report_approval_details` for any earlier level of this instance. Else `409 SELF_OR_DUPLICATE_APPROVER`.
3. **Sequential guard (BR6):** the request must be sitting at this level (`current_level_order` matches, under `SELECT … FOR UPDATE`); a higher level cannot pre-act. Else `409 STALE_LEVEL`.
4. Insert `report_approval_details`: `approval_request_id` = the `report_approvals.id`, `level_order` = current, `approval_status = 1` (Approved), `comments` optional, `approval_by` = caller `user_name`, `approval_at = now()`.
5. **Advance:**
   - **If a next higher level exists within the current phase** (next `approval_flow_levels` row with same `approval_type`): set `current_level_order` = next order; `overall_status` stays 1 (PRE_RUN) or 3 (POST_RUN). Publish `approval.level.advanced` → email next-level users.
   - **If this was the last PRE_RUN level:** set `overall_status = 2` (Pre Approved). The report is now **runnable**. Publish `approval.completed` (phase=2). (Recurrent report → done; each scheduled Final run pays out automatically.)
   - **If this was the last POST_RUN level:** set `overall_status = 4` (Post Approved). The run is cleared for payout. Publish **`approval.completed`** (phase=4) carrying the `report_run_id` → the **DisbursementWorker** consumes it (gated by `is_ev_disbursement` / `is_pos_disbursement` and `ev_disbursement_time`), and the API sets `report_runs.disburse_status = 'PENDING'` (canonical chain, §10).
6. Write `audit_logs` (`action_type = 2`, `entity_name = 'report_approvals'`).

### 7.4.5 Triggering the POST_RUN (result) request
For a **non-recurrent** report whose flow has POST_RUN levels, after a **Final** `report_runs` row finishes (`run_status = 'SUCCEEDED'`, `final_commissions` written):
1. The Run Orchestrator advances the report's `report_approvals` row from `overall_status = 2` (Pre Approved) to **`3`** (Post Approval Pending), sets `current_level_order` = the flow's **first POST_RUN level** (`approval_type = 2`) order, and records the run being approved (`report_run_id` on the event / on `report_runs`).
2. `report_runs.disburse_status` stays `'NONE'` until POST_RUN fully approves.
3. Publish `approval.requested` (result phase) → email first POST_RUN level users.
4. The request advances via §7.4.4 until its final POST_RUN level publishes `approval.completed`.

> For a **recurrent** report (PRE_RUN-only flow), no POST_RUN block exists; on each scheduled Final run completion the orchestrator sets `report_runs.disburse_status = 'PENDING'` directly (still BR8-gated on the report being Pre Approved, `overall_status = 2`).

### 7.4.6 Reject (→ Maker, restart from level 1)
Trigger: an eligible current-level user calls the decision endpoint with Reject **and a non-empty comment** (BR7).
1. Authorize + segregation as in §7.4.4 (steps 1–3).
2. Insert `report_approval_details`: `approval_status = 2` (Rejected), `comments` **required** (non-empty), `approval_by`, `approval_at`.
3. **Return the whole instance to the Maker:** set `report_approvals.overall_status = 0` (Pending for Editing & Resubmission) and `current_level_order` back to the lowest level order. The instance does **not** drop to the previous level.
   - Reject in the **PRE_RUN** block (was status 1) → the setup becomes editable again; `report_setups.status` reflects "Rejected — pending edit".
   - Reject in the **POST_RUN** block (was status 3) → `report_runs.disburse_status = 'NONE'`; **no disbursement**. The Maker may re-run (new Final run) or edit the setup.
4. Publish `approval.rejected` → email the Maker (level, decider, comment). Write `audit_logs` (`action_type = 2`).
5. **Resubmit = restart from level 1 with full re-validation.** When the Maker resubmits (§7.4.3), the instance is reset to `overall_status = 1` at the lowest level; the system re-runs **all** validations (IR validity, flow well-formedness, segregation, BR4 dates, EV/POS flags) before reopening. No partial-approval state is carried forward — only the `report_approval_details` audit history persists.

### 7.4.7 Edit-while-pending (void + restart)
If the Maker (or Admin) edits a report while its `report_approvals` row is in a PRE_RUN-pending state (`overall_status = 1`):
1. The edit is allowed only while not yet Pre-Approved (`overall_status < 2`; Edit hidden once `>= 2`).
2. On save, the in-flight progress is **voided**: set `overall_status = 0`, append a system `report_approval_details` row (`approval_status = 2`, `approval_by = 'SYSTEM'`, `comments = 'Voided: setup edited while pending'`), reset `current_level_order` to the lowest level.
3. The Maker must **Submit for Approval again**, restarting from level 1 (§7.4.3) with full re-validation. This guarantees no one ever approves a stale configuration.

> Editing the setup of a non-recurrent report that already has a POST_RUN-pending run (`overall_status = 3`) also invalidates that pending result approval — the orchestrator voids it the same way (back to status 0) and the run's `disburse_status` returns to `'NONE'`.

### 7.4.8 Derived report-list status label
The Report list/detail status shown to the Maker is **computed, not stored**. It is derived from `report_approvals.current_level_order` + the `report_approval_details` rows (newest decision per level), using `overall_status` and the level's `approval_type`. Examples:

| Situation | Derived label |
|---|---|
| Status 1, L1 approved, now at L2 | **"Approved by L1, now Pending at L2"** |
| Status 3 (post-run), L2 rejected, returned to L1 | **"Rejected by L2, now Pending at L1"** |
| Status 0, L1 rejected | **"Rejected by L1, now pending for edit & resend"** |
| Status 1, no decisions yet | **"Pending at L1"** |
| Status 2 | **"Pre Approved"** |
| Status 4 | **"Post Approved"** |

Algorithm: take `current_level_order` = N; the highest level with an Approved `report_approval_details` row = K (if any). If the latest decision was a Reject (`approval_status = 2`) → `"Rejected by L{rejectLevel}, now Pending at L{N}"` (or `"…, now pending for edit & resend"` when `overall_status = 0`). Else if K exists → `"Approved by L{K}, now Pending at L{N}"`. Else → `"Pending at L{N}"`. Terminal labels map directly from `overall_status` (2 = Pre Approved, 4 = Post Approved). The FE may render this from the `GET /approvals/{id}` payload or a precomputed `statusLabel` field on the report-list DTO.

### 7.4.9 Demo runs
A `run_type = 1` (Demo) run **never** touches `report_approvals` and **never** disburses. The Run Orchestrator skips all approval logic for Demo. Demo also skips the pre-run checks; a Final run requires the report to be **Pre Approved** (`overall_status >= 2`).

---

## 7.5 API Endpoints

Base path `/api/v1`. All require a valid JWT (3-hour inactivity logout); role enforced per endpoint. Standard error envelope. Money fields are `numeric(18,4)` BDT, serialized as strings.

### Flow / level / user administration (Administrator only)

**`GET /approval-flows`** — list flows. Query: `?isActive=true`, `?page=`, `?pageSize=`. Response `200`:
```json
{ "items": [ { "id": 4, "flowName": "EV Standard 2-Level",
  "levelCount": 2, "phases": ["PRE_RUN","POST_RUN"], "description": "Setup + result for EV reports" } ], "total": 7 }
```
Roles: Administrator. Status: `200`.

**`POST /approval-flows`** — create. Request:
```json
{ "flowName": "EV Standard 2-Level", "description": "Setup + result for EV reports" }
```
Response `201`: `{ "id": 4, "flowName": "EV Standard 2-Level" }`. Roles: Administrator. Status: `201`; `409 DUPLICATE_FLOW_NAME`; `422 VALIDATION_ERROR`.

**`GET /approval-flows/{id}`** — flow detail with levels + users. Response `200`:
```json
{ "id": 4, "flowName": "EV Standard 2-Level", "description": "Setup + result for EV reports",
  "levels": [
    { "id": 11, "levelOrder": 1, "levelName": "Setup Review", "approvalType": 1, "phase": "PRE_RUN",
      "users": [ { "userId": "rahim.u", "fullName": "Rahim Uddin", "email": "rahim@bl.net" } ] },
    { "id": 12, "levelOrder": 2, "levelName": "Result Sign-off", "approvalType": 2, "phase": "POST_RUN",
      "users": [ { "userId": "karim.h", "fullName": "Karim Hasan", "email": "karim@bl.net" } ] } ] }
```
Roles: Administrator. Status: `200`; `404 FLOW_NOT_FOUND`.

**`POST /approval-flows/{id}/levels`** — add a level. Request:
```json
{ "levelName": "Result Sign-off", "levelOrder": 2, "approvalType": 2 }
```
`approvalType` ∈ `1` (PRE_RUN) | `2` (POST_RUN). Response `201`: `{ "id": 12, "levelOrder": 2 }`. Roles: Administrator. Status: `201`; `409 DUPLICATE_LEVEL_ORDER`; `422 PHASE_ORDER_VIOLATION` (a PRE_RUN level ordered after a POST_RUN level).

**`PUT /approval-flows/{flowId}/levels/{levelId}`** — edit level name / order / approvalType. Request `{ "levelName": "...", "levelOrder": 2, "approvalType": 2 }`. Response `200`. Status: `200`; `409 DUPLICATE_LEVEL_ORDER`; `422 PHASE_ORDER_VIOLATION`; `404`.

**`DELETE /approval-flows/{flowId}/levels/{levelId}`** — remove a level. Response `204`. Status: `204`; `409 LEVEL_IN_USE` (a live `report_approvals` row sits on it or a report binds the flow); `404`.

**`POST /approval-flows/{flowId}/levels/{levelId}/users`** — assign approvers. Request:
```json
{ "userIds": ["rahim.u", "karim.h"] }
```
(`userIds` are external `users.user_id` values.) Response `200`: `{ "added": 2 }`. Status: `200`; `422 UNKNOWN_OR_INACTIVE_USER`; `409 DUPLICATE_USER_ON_LEVEL`.

**`DELETE /approval-flows/{flowId}/levels/{levelId}/users/{userId}`** — unassign. Response `204`. Status: `204`; `409 LAST_USER_ON_LEVEL` (would leave 0 active users while the flow is in use); `404`.

### Submission, queue & decisions (Maker / Approver)

**`POST /reports/{reportId}/submit-approval`** — Maker submits a completed report's setup (§7.4.3). Request `{}`. Response `201`:
```json
{ "approvalId": 88, "reportSetupId": 17, "phase": "PRE_RUN", "currentLevelOrder": 1, "overallStatus": 1 }
```
Roles: Maker (the report's Maker), Administrator. Status: `201`; `409 ALREADY_PENDING` (a row already at status 1/3); `409 MAKER_IS_SOLE_APPROVER`; `422 FLOW_NOT_WELLFORMED` / `IR_INVALID` / `SETUP_INCOMPLETE` (`is_setup_complete = false`); `404`.

> **Note:** §6.12 exposes the same submission action as `POST /reports/{id}/submit`. Both routes map to the single Submit-for-Approval handler (§7.4.3); `submit-approval` is the canonical name used by the Approvals module. Implement one handler, alias the route.

**`GET /approvals/queue`** — the signed-in user's pending approvals (one card per `report_approvals` row at a level they staff). Query: `?phase=PRE_RUN|POST_RUN`, `?page=`, `?pageSize=`. Response `200`:
```json
{ "items": [
  { "approvalId": 88, "reportSetupId": 17, "reportName": "EV Recharge June",
    "phase": "POST_RUN", "overallStatus": 3, "reportRunId": 540, "levelOrder": 2, "levelCount": 2,
    "maker": "rahim.u", "submittedAt": "2026-06-16T09:12:00Z",
    "resultSummary": { "recipientCount": 312, "totalAmount": "1875400.0000", "currency": "BDT" } } ], "total": 3 }
```
Roles: Approver, Administrator. (Membership matched on `users.user_id`. A Maker calling this sees only rows where they are a staffed approver.) Status: `200`.

**`GET /approvals/{approvalId}`** — full request header + decision trail + derived label. Response `200`:
```json
{ "approvalId": 88, "reportSetupId": 17, "reportName": "EV Recharge June",
  "approvalFlowId": 4, "currentLevelOrder": 2, "overallStatus": 3, "phase": "POST_RUN",
  "reportRunId": 540, "maker": "rahim.u", "initiatedAt": "2026-06-16T09:12:00Z",
  "statusLabel": "Approved by L1, now Pending at L2",
  "decisions": [
    { "levelOrder": 1, "approvalStatus": 1, "approvalBy": "salim.k", "approvalAt": "2026-06-16T10:01:00Z", "comments": null } ] }
```
Roles: any party to the request (Maker, any staffed Approver, Administrator). Status: `200`; `404`.

**`POST /approvals/{approvalId}/decisions`** — Approve or Reject (§7.4.4 / §7.4.6). Request:
```json
{ "approvalStatus": 2, "comments": "Cluster GA target wrong for North." }
```
`approvalStatus` ∈ `1` (Approve) | `2` (Reject). `comments` **required and non-empty** when `approvalStatus = 2` (BR7). Response `200` (approve, not final level):
```json
{ "approvalId": 88, "approvalStatus": 1, "newOverallStatus": 3, "currentLevelOrder": 2, "phase": "POST_RUN", "completed": false }
```
On final-level approve: `"newOverallStatus": 4, "completed": true` (POST_RUN) or `"newOverallStatus": 2, "completed": true` (PRE_RUN). On reject: `"newOverallStatus": 0, "completed": false, "returnedToMaker": true`.
Roles: Approver (must staff the current level), Administrator. Status: `200`; `400 COMMENT_REQUIRED`; `403 NOT_CURRENT_LEVEL_APPROVER`; `409 SELF_OR_DUPLICATE_APPROVER` (BR5); `409 REQUEST_NOT_IN_PROGRESS` (status not 1/3); `409 STALE_LEVEL`; `404`.

**`GET /reports/{reportId}/approval-history`** — every `report_approval_details` row across the report's approval lifetime (setup + result decisions), newest first. Response `200`: `{ "items": [ { "levelOrder", "approvalStatus", "approvalBy", "approvalAt", "comments", "phase" } ], "total": N }`. Roles: Maker (own report), Approver (if a party), Administrator. Status: `200`; `404`.

> **Optimistic concurrency.** The decision endpoint must be transactional: re-read the `report_approvals` row `FOR UPDATE` and re-check `current_level_order` + `overall_status`. If either changed since the caller loaded the card (another current-level user acted, or an edit-void fired), return `409 STALE_LEVEL` / `409 REQUEST_NOT_IN_PROGRESS`. This prevents double-advancing.

---

## 7.6 Validation / Business Rules

| ID | Rule | Where enforced |
|---|---|---|
| **BR5** | Maker (`COALESCE(report_setups.updated_by, report_setups.created_by)`) ≠ Checker on the same report; one user acts at most one level per request; no self-approval. | App: submit pre-check (`MAKER_IS_SOLE_APPROVER`) + decision check (`SELF_OR_DUPLICATE_APPROVER`) against `report_approval_details`. |
| **BR6** | Approval is **sequential ascending** (`level_order`); a higher level cannot act before lower levels approved. | App: request exposes only `current_level_order`; `STALE_LEVEL` / `NOT_CURRENT_LEVEL_APPROVER` otherwise. `ux_approval_flow_levels_flow_order` guarantees unique order. |
| **BR7** | Reject requires a non-empty comment. | App `400 COMMENT_REQUIRED`; `report_approval_details.comments` populated on every `approval_status = 2`. |
| **BR8** | Disburse only after full approval. | DisbursementWorker consumes `approval.completed` only; `report_runs.disburse_status` reaches `'PENDING'` solely via the final POST_RUN approve (`overall_status = 4`) or the recurrent Pre-Approved (`overall_status = 2`) path. |
| **BR9** | Only the Maker manages schedule; all approval / schedule / payout actions audited. | App role check + `audit_logs` row on every submit / approve / reject / void. |
| Flow well-formed | All PRE_RUN levels (`approval_type=1`) precede all POST_RUN levels (`approval_type=2`); each in-use level has ≥1 active user; recurrent report ⇒ PRE_RUN-only flow. | Flow-save validation + report Final-Save binding check (`FLOW_NOT_WELLFORMED` / `PHASE_ORDER_VIOLATION`). |
| One live instance | At most one live `report_approvals` row (status 1 or 3) per report. | App invariant; the orchestrator reuses the single row across PRE_RUN → POST_RUN. |
| Reject → Maker | Reject sets `overall_status = 0` and returns to the Maker — **not** to the previous level. Resubmit restarts at level 1 with full re-validation. | App (§7.4.6); resolves the SRS §7.4 contradiction. |
| Edit voids approval | Editing a not-yet-Pre-Approved report (`overall_status < 2`) voids the in-flight approval (`overall_status = 0`, system Reject detail row) → restart at level 1. A Pre-Approved setup (`>= 2`) is not editable. | App (§7.4.7). |
| POST_RUN ⇄ run | A result approval (status 3 → 4) is bound to the specific Final run being approved; a run is disbursable only when its POST_RUN block reaches `overall_status = 4`. | App orchestrator + `approval.completed` carrying `report_run_id`. |
| Demo exempt | Demo runs (`run_type = 1`) never open/advance an approval and never disburse. | Run Orchestrator skips approval for Demo. |
| Phase from current level | Active phase = `approval_type` of the level at `current_level_order` (never assumed elsewhere). | App phase-resolution rule (§7.3). |
| Derived status label | Report-list status is computed from `report_approvals.current_level_order` + `report_approval_details` (§7.4.8), not stored. | App (`statusLabel` in the report-list / approval-detail DTO). |

**Cross-references:** approval tables and constraints — §3.6. Events `approval.requested` / `approval.level.advanced` / `approval.completed` / `approval.rejected` are the RabbitMQ contracts (§11), consumed by the Notification path and the **DisbursementWorker** (`approval.completed` → §10, gated by `report_setups.is_ev_disbursement` / `is_pos_disbursement` and `ev_disbursement_time`). Run lifecycle and the disbursement chain — §6 / §10.

---

# §8 Dashboard

## 8.1 Overview

The **Dashboard** is the read-only landing screen after login. It gives each role a single, at-a-glance view of system activity: how many reports and runs exist and in what state, how much commission has been computed and disbursed, who logged in (and who failed), and short trends over time. It is **purely a read model** — it issues no writes and triggers no business action. Every figure it shows is derived from operational tables that other modules own:

- `report_runs`, `run_stages` — run volume, success/failure, in-flight runs.
- `final_commissions`, `ev_disburse`, `pos_disbursement` — money computed vs money paid.
- `report_approvals`, `report_approval_details` — approval backlog (pending at each level).
- `login_log` — sign-in success/failure (security card).
- `audit_logs` — recent activity feed.

Because it only reads, the Dashboard is **role-scoped** rather than role-gated: every role can open it, but each role sees a different slice of the same queries (§8.4).

## 8.2 UI

A single responsive page, three bands top to bottom:

1. **KPI cards (top row)** — compact number tiles, each clickable to drill into the underlying list (filtered):
   - **My Reports / Total Reports** — count of `report_setups` the user owns (Maker) or all (Admin), with a sub-count of `is_setup_complete = true`.
   - **Runs Today** — `report_runs` with `run_date::date = today`, split Demo (`run_type=1`) / Final (`run_type=2`).
   - **Pending Approvals** — for an Approver, the count of `report_approvals` whose current level the user is eligible to act on; for Maker/Admin, total in-flight (`overall_status IN (1,3)`).
   - **Commission This Cycle** — `SUM(final_commissions.commission_amount)` for the current `commission_cycle`.
   - **Disbursed vs Pending** — paid (`ev_disburse.status='SUCCESS'` + POS `HANDED_OFF`) against `report_runs.disburse_status='PENDING'`.
   - **Failed Runs (7d)** — `report_runs.run_status='FAILED'` in the last 7 days (red tile, links to the failures list).
2. **Login-attempt card (security)** — a small panel sourced from `login_log`: today's **Success** (`status=1`) and **Failed** (`status=2`) attempt counts, plus the last 5 attempts (`user_name`, `login_time`, `status`, `remarks`). For Admin only it shows all users; for non-Admin it shows that user's own attempts.
3. **Trend charts (bottom band)** — three time-series, default last 30 days, selectable 7/30/90 days:
   - **Runs trend** — daily Final vs Demo run counts (stacked bars) with a success/failure overlay line.
   - **Commission trend** — daily `SUM(final_commissions.commission_amount)` (line), to spot spikes.
   - **Disbursement trend** — daily disbursed amount (EV `SUCCESS` + POS handed-off), overlaid on commission computed, so the gap = outstanding.
4. **Recent activity feed (side panel)** — latest ~20 `audit_logs` rows (`action_type`, `entity_name`, `changed_by`, `changed_at`) scoped to the role.

All money is rendered as a BDT string with two-decimal display; counts are integers; empty states show "No data for this period."

## 8.3 Data Model (reference §3)

The Dashboard defines **no new tables**. It reads:

| Card / chart | Source table(s) | Key columns used |
|---|---|---|
| Reports KPIs | `report_setups` | `created_by`, `status`, `is_setup_complete`, `commission_cycle` |
| Runs KPIs / trend | `report_runs` | `run_date`, `run_type`, `run_status`, `disburse_status`, `triggered_by` |
| Stage health (drill-in) | `run_stages` | `run_status`, `ended_at` |
| Commission KPIs / trend | `final_commissions` | `commission_amount`, `report_run_id` |
| Disbursement KPIs / trend | `ev_disburse`, `pos_disbursement` | `amount`, `status`, `dump_status`, `disburse_at` |
| Approval backlog | `report_approvals`, `report_approval_details` | `overall_status`, `current_level_order`, `approval_status` |
| Login-attempt card | `login_log` | `user_name`, `login_time`, `status`, `remarks` |
| Activity feed | `audit_logs` | `action_type`, `entity_name`, `changed_by`, `changed_at` |

> **Performance note.** These are read-only aggregations. Keep them cheap with the indexes from §3 (`ix_report_runs_report_setup`, `ix_final_commissions_report_run`, `ix_ev_disburse_report_run`) plus a partial index on `report_runs(run_date)` for the date-bucketed trends. Trend queries `GROUP BY run_date::date`. At the real scale (~200 runs/month) no materialized view is needed; if needed later, a nightly summary table is the upgrade path — not required for Phase 1.

## 8.4 Process Logic (role scoping)

The same SQL templates run for every role; a **scope predicate** is injected from the JWT identity and `user_rights`:

1. Resolve caller from JWT → `users.id`, `users.user_name`, and `rights_code` set from `user_rights`.
2. Build the scope filter:
   - **Administrator** — no owner filter: sees all reports, runs, commissions, all `login_log` rows, full activity feed.
   - **Business User (Maker)** — owner filter `report_setups.created_by = :userName` cascaded into runs/commissions (join through `report_runs.report_setup_id`); login card shows only their own `login_log` rows.
   - **Approver (Checker)** — sees reports/runs they participate in (their `users.user_id` appears in an `approval_flow_level_users` row for that report's flow) **and** the Pending-Approvals card counts only levels they can act on (`report_approvals.current_level_order` matches a level whose `approval_flow_level_users.user_id = :externalUserId`).
3. Run KPI queries (single round-trip each, parameterised); run trend queries with the chosen window.
4. Assemble a single `DashboardDto` and return. No caching is required, but a short server-side cache (30–60s) per (role, window) is acceptable.

## 8.5 API Endpoints

All under `/api/v1`, JWT required on every call, standard error envelope. Money fields are BDT decimal strings.

| Method | Path | Purpose | Role | Success |
|---|---|---|---|---|
| GET | `/dashboard/summary` | All KPI cards in one payload | M/A/C | 200 |
| GET | `/dashboard/logins?days=7` | Login-attempt card (counts + last N) | M/A/C | 200 |
| GET | `/dashboard/trends?metric=runs\|commission\|disbursement&days=30` | One trend series | M/A/C | 200 |
| GET | `/dashboard/activity?limit=20` | Recent `audit_logs` feed (role-scoped) | M/A/C | 200 |

**`GET /dashboard/summary`** — Response `200`:
```json
{
  "scope": "MAKER",
  "reports":    { "total": 41, "complete": 33 },
  "runsToday":  { "demo": 6, "final": 2 },
  "pendingApprovals": 3,
  "commissionThisCycle": "1245000.0000",
  "disbursement": { "disbursed": "1180500.00", "pending": "64500.0000" },
  "failedRuns7d": 1
}
```

**`GET /dashboard/logins?days=7`** — Response `200`:
```json
{ "windowDays": 7,
  "today": { "success": 18, "failed": 2 },
  "recent": [
    { "userName": "tarik.h", "loginTime": "2026-06-17T03:11:09Z", "status": 1, "remarks": "OTP verified" },
    { "userName": "rajib.m", "loginTime": "2026-06-17T03:02:44Z", "status": 2, "remarks": "OTP expired" } ] }
```
(`status`: 1=Success, 2=Failed — `login_log` enum.)

**`GET /dashboard/trends?metric=runs&days=30`** — Response `200`:
```json
{ "metric": "runs", "windowDays": 30,
  "points": [
    { "date": "2026-06-15", "final": 1, "demo": 4, "succeeded": 4, "failed": 1 },
    { "date": "2026-06-16", "final": 2, "demo": 3, "succeeded": 5, "failed": 0 } ] }
```
For `metric=commission`: points carry `{ "date", "amount" }`. For `metric=disbursement`: `{ "date", "computed", "disbursed" }`.

**`GET /dashboard/activity?limit=20`** — Response `200`:
```json
{ "items": [
  { "actionType": 2, "entityName": "report_setups", "entityId": "1187",
    "changedBy": "tarik.h", "changedAt": "2026-06-17T02:55:01Z" } ] }
```
(`actionType`: 1=Create, 2=Update, 3=Delete — `audit_logs` enum.)

Errors across all: `401` (no/expired JWT), `403` (only if a non-Admin requests an Admin-only widened scope via query override), `422` (bad `metric`/`days`).

## 8.6 Validation / Business Rules

- **BR1 (role scoping):** every query is filtered by the caller's role/rights; a Maker cannot widen scope to other Makers' data, an Approver only sees flows they belong to. Enforced server-side from the JWT — never trust a client-supplied scope.
- **Read-only:** the Dashboard issues `SELECT` only; it never writes, never disburses, never advances approvals.
- **`days` is bounded** to `{7,30,90}`; any other value → `422`.
- **Login card visibility:** non-Admin sees only their own `login_log` rows (BR1); only Admin sees the system-wide success/failure totals.
- **No money mutation:** commission and disbursement figures are aggregations of existing rows; the Dashboard cannot trigger a run or a payout.

---

# §9 Notification

## 9.1 Overview

SalesCom sends two kinds of outbound messages — **SMS** and **Email** — through a single internal **Notification path** owned by the Web API (there is no separate Notification Worker; the API enqueues and a Hangfire job drains the outbox). Every message is persisted in **`notification_logs`** before and after sending, so delivery is auditable and retryable.

**When notifications fire:**

| Event | Channel | Recipient | Trigger point |
|---|---|---|---|
| **Approval requested** | Email | the eligible approver(s) at the now-current level | a report is submitted / advances to a new level (§7) |
| **Approval rejected** | Email | the Maker (report owner) | an approver rejects a level (`report_approval_details.approval_status=2`) |
| **Approval completed** | Email | the Maker | final level approves (`report_approvals.overall_status` reaches 2 or 4) |
| **Run failed** | Email | the Maker + ops | `report_runs.run_status='FAILED'` |
| **Disbursement complete** | Email | the Maker + ops | reconciliation passes, `disburse_status='DONE'` (§10.5) |
| **EV payout (per recipient)** | SMS | the paid channel recipient | each `ev_disburse` row reaches `status='SUCCESS'` (§10.4) |

The **EV-payout SMS** is the only recipient-facing message; all others are internal-staff emails. SMS content is taken from the report's `report_setups.sms_content` template (recipient-facing), email content from server-side templates keyed by `notification_logs.template_code`.

## 9.2 UI

Notification has a light UI footprint:

- **SMS template field** in the wizard (Step 1 / disbursement settings) → persisted to `report_setups.sms_content`. Supports placeholders like `{amount}`, `{channel_code}`, `{cycle}` resolved at send time. A live preview shows a sample rendering.
- **Notification log viewer (Admin)** — a filterable table over `notification_logs` (`channel`, `status`, `template_code`, date range) showing `to_address`/`phone_number`, `status`, `attempt_count`, `error_message`, `sent_at`. Failed rows expose a **Retry** action (Admin only).
- In-app toast/badge for the in-product events (approval requested, run failed) is optional and out of the notification persistence path.

## 9.3 Data Model (reference §3)

All notifications are recorded in **`notification_logs`** (§3.8). Key columns and their **int4 enums**:

- `channel int4` — **1 = Email, 2 = SMS**.
- `status int4` — **0 = Pending, 1 = Sent, 2 = Failed**.
- `template_code` — identifies the template (e.g. `APPROVAL_REQUESTED`, `APPROVAL_REJECTED`, `APPROVAL_COMPLETED`, `RUN_FAILED`, `DISBURSE_COMPLETE`, `EV_PAID`).
- `phone_number` — MSISDN (SMS); `to_address` / `cc` / `bcc` — email; `subject` / `body` / `from_address`.
- `attempt_count int4`, `error_message`, `scheduled_at`, `sent_at`, `created_at`.

No FK to `users`: recipients are stored denormalised (`to_address`, `phone_number`) so a later user rename/deactivation never rewrites delivery history.

## 9.4 Process Logic

**Enqueue (synchronous, fast):** when a business event occurs, the Web API renders the template and **inserts one `notification_logs` row** with `status=0` (Pending), `attempt_count=0`, `created_at=now()`, `scheduled_at=now()` (or a future time for batched sends). This insert is part of the originating transaction's after-commit step so a rolled-back business action never emits a message.

**Send (Hangfire drain job):** a recurring Hangfire job picks Pending rows ordered by `scheduled_at`:

1. Select rows `WHERE status=0 AND scheduled_at <= now()` (indexed scan), small batch.
2. For each row, route by `channel`:
   - `channel=2` (SMS) → call the **SMS gateway** (`172.16.7.210:13082`) with `phone_number` + `body`.
   - `channel=1` (Email) → send via **SMTP** with `to_address`/`cc`/`bcc`/`subject`/`body`/`from_address`.
3. On success → `status=1` (Sent), `sent_at=now()`.
4. On failure → `status=2` (Failed), increment `attempt_count`, store `error_message`. Eligible for retry while `attempt_count < MAX_ATTEMPTS` (config, e.g. 3), with backoff via `scheduled_at` advanced forward; a row that exhausts attempts stays `status=2` and surfaces in the log viewer for manual Retry.

**EV-payout SMS specifics (§10 cross-link):** after an `ev_disburse` row reaches `status='SUCCESS'`, the disburser enqueues one SMS row using `report_setups.sms_content` as the template with `{amount}` bound to the paid amount and `{channel_code}` to the recipient. **SMS delivery is a side-channel:** a failed SMS is logged (`status=2`) but never changes money state or `disburse_status` — the payout already succeeded.

**Idempotency:** the enqueue step is part of the event handler; to avoid duplicate sends on handler retry, the EV path enqueues SMS keyed off the already-idempotent `ev_disburse` row (one SMS per successful payee), and approval/run emails are enqueued once per state transition (the transition itself is idempotent in §7/§10).

## 9.5 API Endpoints

All under `/api/v1`, JWT required, standard error envelope.

| Method | Path | Purpose | Role | Success |
|---|---|---|---|---|
| GET | `/notifications?channel=&status=&template=&from=&to=&page=` | List/filter notification log | A (M own-report scoped) | 200 |
| GET | `/notifications/{id}` | Read one notification row | A | 200 |
| POST | `/notifications/{id}/retry` | Re-queue a failed notification | A | 202 |
| POST | `/reports/{id}/sms-preview` | Render `sms_content` against sample values | M/A | 200 |

**`GET /notifications`** — Response `200` (paged):
```json
{ "page": 1, "pageSize": 25, "total": 312,
  "items": [
    { "id": 88120, "templateCode": "EV_PAID", "channel": 2, "status": 1,
      "phoneNumber": "8801XXXXXXXXX", "toAddress": "", "attemptCount": 1,
      "sentAt": "2026-06-17T12:00:11Z", "errorMessage": null },
    { "id": 88121, "templateCode": "APPROVAL_REQUESTED", "channel": 1, "status": 2,
      "toAddress": "checker@banglalink.net", "subject": "Approval needed: RSO Apr26",
      "attemptCount": 3, "errorMessage": "SMTP 451 greylisted" } ] }
```
(`channel`: 1=Email, 2=SMS; `status`: 0=Pending, 1=Sent, 2=Failed.)

**`POST /notifications/{id}/retry`** — Admin only. Valid only when `status=2`. Sets `status=0`, `scheduled_at=now()` (does **not** reset `attempt_count`). Responses: `202`; `409 NOT_RETRYABLE` (row not Failed); `404`.

**`POST /reports/{id}/sms-preview`** — Request `{ "sample": { "amount": "500.00", "channel_code": "RSO-10231", "cycle": "Apr 2026" } }`; Response `200` `{ "rendered": "Apnar commission 500.00 BDT credit kora hoyeche." }`. Errors: `422` (unknown placeholder in `sms_content`).

## 9.6 Validation / Business Rules

- **Channel/status are int4 enums** (`channel` 1/2, `status` 0/1/2) — the app owns the code→meaning map (Appendix B); reject any other value at write time.
- **Email requires a subject/recipient; SMS requires a phone number** — validated before insert; a row missing the channel-required fields is rejected, not silently dropped.
- **Persist-before-send:** every message is written to `notification_logs` (`status=0`) before any gateway call, so nothing is sent without an audit row.
- **Retry is bounded** (`attempt_count < MAX_ATTEMPTS`) with backoff; exhausted rows require manual Admin Retry.
- **SMS never gates money (BR8 unaffected):** EV-payout SMS failure is logged but never reverses or blocks a successful disbursement.
- **No recipient FK:** delivery targets are stored as denormalised values so history survives user changes.
- **BR9:** the notification log itself is the audit of what was sent; Admin Retry actions are recorded in `audit_logs`.

---

# §10 Disbursement

## 10.1 Overview

Disbursement is the **final, money-moving step**: after a Final run's results are fully approved, SalesCom pays each recipient the amount computed in **`final_commissions`**. Two mutually exclusive routes exist per report:

- **EV** — automatic per-recipient electronic payout via the EV API, followed by a confirmation **SMS** to each recipient.
- **POS** — a single **CSV** built from `final_commissions` and handed off to the POS system (no automated payment from SalesCom).

The hard rule is **BR8: disburse only after full approval.** Disbursement is **never** part of the run; it is a separate, gated, idempotent step triggered only after the post-run approval completes. Amounts are read from `final_commissions` (the trusted-path output), and the disbursement tables (`ev_disburse`, `pos_disbursement`) are written by a **trusted backend path under a least-privilege role — never by generated SQL.**

## 10.2 EV vs POS

| Aspect | EV | POS |
|---|---|---|
| Setup flag | `report_setups.is_ev_disbursement = true` | `report_setups.is_pos_disbursement = true` |
| Action | Automatic EV API call per recipient | Build one CSV, hand off |
| Timing | At `report_setups.ev_disbursement_time`, fired by Hangfire | After approval; CSV `PENDING→GENERATED→HANDED_OFF` |
| Output table | `ev_disburse` (one row per `channel_code`) | `pos_disbursement` (one row per run) |
| Idempotency key | `UNIQUE(report_run_id, channel_code)` | `UNIQUE(report_run_id)` |
| External call | EV API `10.13.2.7:9898` | none (CSV handoff) |
| Recipient notify | SMS per recipient after success (§9) | none |

**Mutual exclusion:** exactly one of `is_ev_disbursement` / `is_pos_disbursement` is true per report (app-enforced at wizard save and re-checked by the disburser; neither-set is a `DISBURSE_MODE_CONFLICT` config error).

## 10.3 Data Model (reference §3)

- **`final_commissions`** — source of truth for amounts owed. `report_run_id`, `channel_id` (the report's channel TYPE constant), `channel_code` (the individual payee key from IR `final_mapping`), `commission_amount numeric(18,4)`. Idempotency: `ux_final_commissions_run_code UNIQUE(report_run_id, channel_code)`. Written by the trusted path, read by the disburser.
- **`ev_disburse`** — `report_run_id` (FK→`report_runs.id`), `channel_id` (FK→`channels.id`, the report TYPE), `channel_code` (payee), `amount numeric(18,4)` (= `final_commissions.commission_amount`), `status` (`PENDING|SENT|SUCCESS|FAILED`), `message`, `disburse_at`. Idempotency: `ux_ev_disburse_run_code UNIQUE(report_run_id, channel_code)`.
- **`pos_disbursement`** — `report_run_id` (FK→`report_runs.id`), `object_url` (CSV in SeaweedFS), `dump_status` (`PENDING|GENERATED|HANDED_OFF|FAILED`), `disburse_at`. Idempotency: `ux_pos_disbursement_run UNIQUE(report_run_id)` (one CSV per run).
- **`report_runs.disburse_status`** (`NONE|PENDING|DONE|FAILED`, with an internal `IN_PROGRESS` working state) tracks the run's overall disbursement lifecycle.

> **Recipient key = `channel_code`, not `channel_id`.** `channel_id` is the report's channel TYPE (Distributor/RSO/Retailer) and is constant for every row of one report; the individual payee is `channel_code`. The idempotency UNIQUEs are therefore on `(report_run_id, channel_code)` — using `channel_id` would collapse all payees into one row.

## 10.4 Process Logic — the canonical disbursement chain

Disbursement follows a single **canonical event chain** (Errata E3), so the trigger is unambiguous:

```
post-run approval completes (report_approvals.overall_status = 4 Post Approved)
   → API sets report_runs.disburse_status = PENDING   (BR8 gate satisfied; no payout yet)
   → Hangfire DisbursementWorker, at report_setups.ev_disbursement_time, publishes:
         ev.disburse   (if is_ev_disbursement)   →  q.ev-disburse  → EV Worker
         pos.disburse  (if is_pos_disbursement)  →  q.pos-disburse → POS Worker
   → worker executes the payout idempotently and runs reconciliation
```

**Precondition gate (both routes, BR8):** the disburser proceeds only when the run is `run_type=2` (Final) **and** `run_status='SUCCEEDED'`, the report's post-run approval is complete (`report_approvals.overall_status=4`), and `disburse_status IN ('PENDING')` (or `IN_PROGRESS` on retry). **Demo runs (`run_type=1`) can never disburse** (`disburse_status` stays `NONE`). If the gate fails → no rows written, skip / `409`.

### EV path (EV Worker, consumes `ev.disburse`)

1. Set `report_runs.disburse_status='IN_PROGRESS'`.
2. **Seed `ev_disburse` idempotently** — one PENDING row per `channel_code`:
   ```sql
   INSERT INTO salescomdbtst.ev_disburse
       (report_run_id, channel_id, channel_code, amount, status)
   SELECT fc.report_run_id, fc.channel_id, fc.channel_code, fc.commission_amount, 'PENDING'
   FROM salescomdbtst.final_commissions fc
   WHERE fc.report_run_id = :runId
   ON CONFLICT (report_run_id, channel_code) DO NOTHING;   -- ux_ev_disburse_run_code
   ```
3. For each PENDING `ev_disburse` row, call the **EV API** (`10.13.2.7:9898`) with the recipient + `amount`. On submit → `status='SENT'`; on confirmed payment → `status='SUCCESS'`, `disburse_at=now()`. On failure → `status='FAILED'` with `message`; a bounded retry sweep re-attempts only that same `(report_run_id, channel_code)` row — **never** a new row, so no double-pay.
4. For each `status='SUCCESS'` row, enqueue an **EV-payout SMS** (§9) from `report_setups.sms_content`. SMS failure does not affect money state.
5. After all recipients resolve, run **reconciliation** (§10.5). On pass → `disburse_status='DONE'` and a `DISBURSE_COMPLETE` email; on mismatch → hard-block (§10.5).

### POS path (POS Worker, consumes `pos.disburse`)

1. Set `disburse_status='IN_PROGRESS'` and insert one `pos_disbursement` row idempotently:
   ```sql
   INSERT INTO salescomdbtst.pos_disbursement (report_run_id, object_url, dump_status)
   VALUES (:runId, '', 'PENDING')
   ON CONFLICT (report_run_id) DO NOTHING;                 -- ux_pos_disbursement_run
   ```
2. Build the CSV from `final_commissions` for that run — one line per `channel_code`: `channel_code, commission_amount (2-dp), commission_cycle, report_name`.
3. Upload to SeaweedFS; store the URL in `object_url`; set `dump_status='GENERATED'`.
4. Hand off to POS (pickup / SFTP / shared bucket per ICD); on confirmed handoff → `dump_status='HANDED_OFF'`, `disburse_at=now()`.
5. Run **reconciliation** (CSV total vs `final_commissions` sum); on pass → `disburse_status='DONE'`; on mismatch → hard-block.

## 10.5 Reconciliation

Reconciliation runs at the **end of the disbursement step** (worker-enforced, not a nightly job). Per `report_run` the invariant is:

```
SUM(final_commissions.commission_amount)  ==  money actually disbursed
```

- **EV:** `SUM(final_commissions.commission_amount WHERE report_run_id=:r)` must equal `SUM(ev_disburse.amount WHERE report_run_id=:r AND status IN ('SENT','SUCCESS'))`.
- **POS:** `SUM(final_commissions.commission_amount WHERE report_run_id=:r)` must equal the **CSV total**, and the CSV row count must equal `COUNT(*)` of `final_commissions` for the run.

**On match:** `disburse_status='DONE'`, `DISBURSE_COMPLETE` notification (§9).
**On mismatch (hard block):** do **not** mark DONE, do **not** auto-retry; set `disburse_status='FAILED'`, write an `audit_logs` row (`action_type=2`, entity `report_runs`, with a `RECONCILE_FAILED` note in `changed_columns`/`new_values`), and raise an **alert email** to ops + the Maker. A human must investigate before any further disbursement on that run.

## 10.6 API Endpoints

All under `/api/v1`, JWT required, standard error envelope. Disbursement is mostly system-triggered (EV by Hangfire at `ev_disbursement_time`); these endpoints expose status, an Admin manual trigger, EV retry, and the POS download.

| Method | Path | Purpose | Role | Success |
|---|---|---|---|---|
| GET | `/runs/{runId}/disbursement` | Disbursement status + reconciliation | M(own)/A/C | 200 |
| POST | `/runs/{runId}/disburse` | Manually trigger (gated, idempotent) | A (M own per config) | 202 |
| POST | `/runs/{runId}/ev/{channelCode}/retry` | Retry one failed EV recipient | A | 202 |
| GET | `/runs/{runId}/pos-file` | Download/redirect to POS CSV | A, M(own) | 200/302 |
| POST | `/runs/{runId}/pos/confirm-handoff` | Mark POS batch handed off + reconcile | A | 200 |

**`GET /runs/{runId}/disbursement`** — Response `200` (EV run):
```json
{ "reportRunId": 9921, "mode": "EV", "disburseStatus": "DONE",
  "reconciliation": { "status": "MATCHED", "commissionTotal": "1245000.0000", "disbursedTotal": "1245000.0000" },
  "ev": { "recipients": 1820, "success": 1816, "failed": 4, "pending": 0 }, "pos": null }
```
For POS: `"mode":"POS"`, `"ev":null`, `"pos":{ "dumpStatus":"HANDED_OFF", "objectUrl":"s3://salescom/pos/9921.csv", "disburseAt":"…" }`.

**`POST /runs/{runId}/disburse`** — manual trigger (Admin; Maker for own report per config). Request `{ "mode": "EV" }` (optional; defaults to the report's configured mode). Enforces the BR8 gate + EV/POS mutual exclusion; idempotent (re-fills only unpaid recipients). Responses: `202 Accepted`; `409 NOT_APPROVED`; `409 ALREADY_DISBURSED`; `409 DISBURSE_MODE_CONFLICT`; `403`.

**`POST /runs/{runId}/ev/{channelCode}/retry`** — retry one failed EV recipient (Admin). Valid only when that `ev_disburse` row `status='FAILED'`. Re-uses the same `(report_run_id, channel_code)` row → no double pay. Responses: `202`; `409 NOT_RETRYABLE`; `404`.

**`GET /runs/{runId}/pos-file`** — download/redirect to the POS CSV (Admin, Maker own). Returns a `302` redirect to the SeaweedFS `object_url` (signed) or `404` if no `pos_disbursement` row / not yet `GENERATED`.

**`POST /runs/{runId}/pos/confirm-handoff`** — mark POS batch handed off (Admin). Sets `dump_status='HANDED_OFF'`, `disburse_at=now()`, runs reconciliation. Responses: `200`; `409` if `dump_status` not `GENERATED`; `404`.

## 10.7 Validation / Business Rules

- **BR8 — disburse only after full approval:** `report_approvals.overall_status=4` (Post Approved) **and** the run is Final (`run_type=2`) + `run_status='SUCCEEDED'`. Demo runs never disburse (`disburse_status` stays `NONE`).
- **EV/POS mutually exclusive:** exactly one of `is_ev_disbursement` / `is_pos_disbursement` true; the disburser rejects neither-set as `DISBURSE_MODE_CONFLICT`.
- **Idempotent / no double-pay:** `ux_ev_disburse_run_code` (`report_run_id, channel_code`) + `ON CONFLICT DO NOTHING` on EV; `ux_pos_disbursement_run` (`report_run_id`) on POS. Re-running disbursement only fills unpaid recipients. The recipient key is **`channel_code`**, not `channel_id`.
- **Trusted path only:** `final_commissions`, `ev_disburse`, `pos_disbursement` are written by the backend disburser under a least-privilege role — **never** by generated SQL.
- **Reconciliation is a hard gate:** a mismatch → `disburse_status='FAILED'`, no auto-retry, alert + `audit_logs`; manual investigation required before any further payout on the run.
- **SMS is a side-channel:** an EV-payout SMS failure is logged in `notification_logs` (`status=2`) but never fails or reverses the disbursement.
- **BR9 (audit):** all disbursement actions (manual trigger, EV retry, POS handoff confirm, reconcile-failed) are written to `audit_logs` with the actor `changed_by` snapshot.

---

# §11 Asynchronous Services & Events

SalesCom is **event-driven** for everything slow, retriable, or that must survive a process restart: IR→SQL generation, run execution, EV/POS disbursement, and notifications. The .NET Web API never blocks on these — it writes a row, publishes a message to **RabbitMQ**, and returns (`202 Accepted` for async actions). Background workers consume the messages. **Hangfire** is used only as a *trigger* (cron schedules, disbursement-timing, retry/stale sweeps, user-sync); it never executes heavy SQL and never makes a routing decision — at the right moment it just enqueues a RabbitMQ message or runs a short DB sweep.

This section is **normative** for the broker: any producer or consumer must match these exact exchange names, routing keys, queue names, and JSON contracts.

## 11.1 Broker topology

One **durable direct exchange per domain**. Messages are `delivery_mode=2` (persistent); consumers use **manual ack** (ack only after the unit of work commits to PostgreSQL). Every work queue declares `x-dead-letter-exchange` and has a paired **dead-letter queue (DLQ)**.

| Exchange (direct, durable) | Routing key | Queue (durable) | Consumer service | Server |
|---|---|---|---|---|
| `salescom.report` | `report.saved` | `q.sql-generate` | SQL Generator (Python) | AI01 |
| `salescom.run` | `run.runnow` | `q.run.high` | SQL Executor (Python) | AI01 |
| `salescom.run` | `run.demo` | `q.run.mid` | SQL Executor (Python) | AI01 |
| `salescom.run` | `run.schedule` | `q.run.low` | SQL Executor (Python) | AI01 |
| `salescom.run` | `run.completed` | `q.run-completed` | Web API | APP01/02 |
| `salescom.approval` | `approval.requested`, `approval.level.advanced`, `approval.rejected`, `approval.completed` | `q.notification` (binds `requested`/`rejected`/`completed`) | Notification consumer (Web API host) | APP01/02 |
| `salescom.approval` | `approval.completed` | `q.approval-completed` | Web API **disbursement-arming** consumer (E3) | APP01/02 |
| `salescom.disburse` | `ev.disburse` | `q.ev-disburse` | EV Worker (Python) | AI01 |
| `salescom.disburse` | `pos.disburse` | `q.pos-disburse` | POS Worker (.NET) | AI01 |
| `salescom.notify` | `notify.sms`, `notify.email` | `q.notify` | Notification consumer (Web API host) | APP01/02 |
| (dead-letter, fanout) | — | `q.<name>.dlq` (one per work queue) | Ops / manual replay | AI02 |

**Why three run lanes, not one priority field (D1).** D1 = one run executes at a time, ordered RunNow > Demo > Schedule. The Executor implements that by draining `q.run.high` first, then `q.run.mid`, then `q.run.low` (a **weighted drain** so Schedule never starves). There is no broker-level concurrency: a single Executor consumer holds **one PostgreSQL advisory lock** (§11.7), so only one run is ever in flight platform-wide. `eventType` is the constant `"run.requested"` for **all three** lanes — the lane is carried only in the **routing key** (`run.runnow | run.demo | run.schedule`) and `payload.lane`; consumers route by **queue binding**, never by reading `eventType`.

## 11.2 Common message envelope

Every message body is JSON with this envelope; `payload` is event-specific.

```json
{
  "messageId":   "b1d3...-uuid",          // unique per publish; a redelivery REUSES it
  "eventType":   "report.saved",          // constant per logical event
  "occurredAt":  "2026-06-17T09:00:00Z",  // ISO-8601 UTC
  "correlationId":"c0ff...-uuid",         // ties back to the originating HTTP request (Serilog/Loki)
  "actor":       "rahman.m",              // user_name snapshot, or "SYSTEM"
  "payload":     { /* per-contract below */ }
}
```

## 11.3 Event message contracts

**`report.saved`** — published by the Web API at **Final Save**, after `report_setups.definition` (the IR) is persisted and `is_setup_complete` is being finalized. Triggers IR→SQL generation into `section_wise_report_sqls`.
```json
{ "eventType": "report.saved",
  "payload": { "reportSetupId": 4012, "irVersion": "1.0", "regenerate": true } }
```

**`run.requested`** (lanes `run.runnow` / `run.demo` / `run.schedule`) — published when a run is requested. The Web API has already inserted the `report_runs` row (`run_status = 'QUEUED'`). `runType` matches `report_runs.run_type` (`1=Demo`, `2=Final`); `lane` ∈ `high|mid|low`.
```json
{ "eventType": "run.requested",
  "payload": { "reportRunId": 88231, "reportSetupId": 4012, "runType": 2,
               "lane": "high", "triggeredBy": "rahman.m" } }
```

**`run.completed`** — published by the Executor on run completion; consumed by the Web API (`q.run-completed`) to surface status and, for a POST_RUN result flow, open the result `report_approvals` instance.
```json
{ "eventType": "run.completed",
  "payload": { "reportRunId": 88231, "status": "SUCCEEDED",
               "lastStageTable": "run_88231.stage_7_out", "rowCount": 318 } }
```

**`approval.completed`** — published by the Web API when a `report_approvals` instance reaches a fully-approved terminal state (`overall_status = 2 Pre Approved` for a PRE_RUN setup flow, or `4 Post Approved` for a POST_RUN result flow). Consumed by **both** `q.notification` (notify the Maker) and `q.approval-completed` (arm disbursement, E3). `reportRunId` is null for a PRE_RUN-only setup approval.
```json
{ "eventType": "approval.completed",
  "payload": { "reportSetupId": 4012, "reportRunId": 88231,
               "approvalRequestId": 5501, "phase": 4 } }
```

**`approval.requested` / `approval.level.advanced` / `approval.rejected`** — published on open, on each level advance, and on a reject. Consumed by `q.notification` only (the approval state machine itself is synchronous in the Web API, §7). `levelOrder` = `report_approvals.current_level_order`.
```json
{ "eventType": "approval.rejected",
  "payload": { "approvalRequestId": 5501, "reportSetupId": 4012, "levelOrder": 2,
               "rejectedBy": "karim.a", "comment": "Slab table outdated." } }
```

**`ev.disburse`** — published by the Hangfire `DisbursementWorker` (E3) once a Final run is approved, EV-mode, and `ev_disbursement_time <= now()`. One message per run; the worker fans out per recipient internally.
```json
{ "eventType": "ev.disburse",
  "payload": { "reportRunId": 88231, "reportSetupId": 4012,
               "smsContent": "Your commission of {amount} BDT is paid." } }
```

**`pos.disburse`** — published by the Hangfire `DisbursementWorker` once a Final run is approved and POS-mode.
```json
{ "eventType": "pos.disburse", "payload": { "reportRunId": 88231, "reportSetupId": 4012 } }
```

**`notify.sms` / `notify.email`** — consumed by the notification consumer, which writes a `notification_logs` row and calls the SMS gateway / SMTP. `channel` matches `notification_logs.channel` (`1=Email`, `2=SMS`).
```json
{ "eventType": "notify.sms",
  "payload": { "templateCode": "EV_PAID", "channel": 2, "phoneNumber": "8801XXXXXXXXX",
               "body": "Your commission of 1500 BDT is paid.", "refType": "report_run", "refId": 88231 } }
```

## 11.4 At-least-once delivery & idempotency

RabbitMQ guarantees **at-least-once**, so any message can be redelivered. Every consumer is **idempotent**, enforced two ways:

1. **DB unique constraints make a replayed write a no-op** (from §3):
   - `ux_section_wise_report_sqls_setup_order (report_setup_id, stage_order)` — regenerating SQL is an upsert.
   - `ux_run_stages_run_order (run_id, sort_order)` — re-snapshotting stages is safe.
   - `ux_final_commissions_run_code (report_run_id, channel_code)` — recipient key is **`channel_code`** (NOT `channel_id`, which is the constant report TYPE). Writes use `INSERT … ON CONFLICT DO NOTHING`.
   - `ux_ev_disburse_run_code (report_run_id, channel_code)` — same recipient key; `INSERT … ON CONFLICT DO NOTHING`.
   - `ux_pos_disbursement_run (report_run_id)` — one CSV handoff per run.
2. **State guards.** A consumer re-reads the row and skips work a previous delivery already finished (e.g. `report_runs.run_status` already `SUCCEEDED`; `ev_disburse.status` already `SUCCESS`). Each message carries a `messageId` (GUID) for log correlation; the DB constraints are the source of truth.

## 11.5 Retry & dead-lettering

- **Transient failures** (DB deadlock, broker blip, EV gateway timeout): the consumer **nacks (`requeue=false`)** after incrementing an attempt count, and a **delayed retry** is applied. Per-domain caps:
  - **run** = 1 attempt then DLQ — a failed run is a *business event*: it lands `report_runs.run_status = 'FAILED'` and is surfaced to the Maker.
  - **sql-generate** = 3.
  - **EV per recipient** = 3 (tracked via `ev_disburse.status = 'RETRY'`).
  - **notification** = 3 (`notification_logs.attempt_count`, `status = 2 Failed` at cap).
- **Backoff:** exponential with jitter (≈ 30 s → 2 min → 10 min), via a RabbitMQ delayed-message setup (or a `q.<name>.retry` with per-message TTL that dead-letters back to the work queue).
- **DLQ:** after the cap, the message routes to `q.<name>.dlq` (each work queue declares `x-dead-letter-exchange`). DLQ messages are **never auto-replayed** — an operator inspects and replays manually after fixing the root cause. The originating DB row already reflects the failure, so the system is never left ambiguous.
- **Poison-message protection:** a message that throws on *parse* goes straight to DLQ — never requeued.

## 11.6 SQL Generator service (Python, AI01)

**Purpose.** Compile the IR (`report_setups.definition`) into per-stage SQL and persist it into **`section_wise_report_sqls`** (frozen at Final Save). Consumes `q.sql-generate`.

**Process logic.**
1. Read `report_setups` by `reportSetupId`; load `definition` (IR) and the `report_supporting_uploads` rows (to resolve IR `source.type = "upload"` references → their `db_schema` / `db_table_name`).
2. **Validate the IR** against the JSON Schema (see `IR_Schema_and_MultiKPI_Join.md` — referenced, not duplicated). On schema error → publish a failure notification, write `audit_logs`, stop.
3. **Build SQL with the SQLGlot AST — never string concatenation (D2).** Each IR block compiles to an ordered list of stages; each stage becomes one statement that materializes a temp output table (`output_table_name`). Every block ends in a `summarize` that fixes its grain; block-to-block joins are emitted **only on the grain key**, and the generator emits the **G1 pre-join uniqueness** guard.
4. **Re-parse safety pass (D2 allowlist).** Re-parse every generated statement and assert it contains **only** allowed node types: `SELECT / WITH / JOIN / GROUP BY / aggregates / CASE / window functions`, plus `CREATE TEMP TABLE` / `DROP TABLE` for stage materialization. Block any DML/DDL outside that set, any function not on the allowlist, and any non-whitelisted identifier — **only** registered `data_sources` tables + this report's `report_supporting_uploads` tables are reachable. **All literals are bound parameters.**
5. Write one `section_wise_report_sqls` row per stage `(report_setup_id, stage_order, sql_text)`; `ux_section_wise_report_sqls_setup_order` makes regeneration idempotent (upsert by `(report_setup_id, stage_order)`, delete orphaned higher orders).
6. Emit `audit_logs` (`action_type = 2 Update`, `entity_name = 'section_wise_report_sqls'`) and a success notification. The Web API may set `report_setups.is_setup_complete = true` once generation succeeds.

> **`final_commissions` is never produced by generated SQL.** The last stage produces the final per-recipient projection table; the **trusted Executor path** (§11.7) reads it and writes `final_commissions` itself (D2).

## 11.7 SQL Executor service (Python, AI01)

**Purpose.** Run a report end-to-end: snapshot the frozen SQL, execute stage-by-stage in an isolated temp namespace, write per-recipient `final_commissions`, clean up. Consumes the three `q.run.*` lanes (high → mid → low weighted drain).

**Single-active-run lock (D1).** Before executing, the Executor takes a **PostgreSQL session advisory lock** for "the one global run slot":
```sql
SELECT pg_try_advisory_lock(hashtext('salescom:global-run-slot'));
```
If `false`, another run holds the slot → the consumer **nacks with requeue** (the message returns to its lane) and is retried when the slot frees. This guarantees exactly one run executes at a time platform-wide. The lock is released (`pg_advisory_unlock`) in a `finally` block and auto-releases if the Executor's DB session dies (crash safety).

**Pre-run checks (Scheduled / Run-Now FINAL only — Demo SKIPS all; 2026-06-16 decision 7).** A FINAL run is admitted only if:
- `report_setups.end_date <= today`,
- the report-setup approval is fully done (the report's `report_approvals.overall_status` has reached `2 Pre Approved`), and
- every **system** `data_sources` table used by the report has its Airflow ETL finished **up to the report End Date**.
A **Demo** run skips every pre-run check (it is a sandbox preview). If a check fails on a FINAL run, the Executor marks the run `FAILED` with the reason and releases the slot.

**Run lifecycle.**
1. **Dedupe / claim.** Re-read `report_runs`. If `run_status` is already `SUCCEEDED`/`FAILED`, ack and stop (redelivery). Otherwise transition `QUEUED → RUNNING`, set `started_at = now()`.
2. **Snapshot SQL.** Copy each `section_wise_report_sqls` row into `run_stages` (`run_id`, `sort_order`, `sql_text`, `run_status = 0 Pending`, `cleanup_status = 0 Not Cleaned`, `file_generated_at = now()`). `ux_run_stages_run_order` makes this idempotent. This **freezes** the SQL so editing the setup later cannot affect a run already underway.
3. **Per-run temp namespace.** Create a dedicated temp schema `run_<reportRunId>` (or session-temp tables). Every stage's `output_table_name` lives here → collisions impossible; cleanup is a single `DROP SCHEMA … CASCADE`.
4. **Execute stages in `sort_order`.** For each `run_stages` row: `0 Pending → 1 Running` (`started_at`), execute the bound-parameter SQL under a **least-privilege DB role** (read-only on source/upload schemas, write only inside the temp namespace, **no access to `final_commissions` / `ev_disburse` / `pos_disbursement`**), record `output_table_name`, run the **G1/G2 guardrails** (pre-join uniqueness, post-join fan-out / row-count), set `2 Succeeded` (`ended_at`). On error → `3 Failed` and abort the run.
5. **Write `final_commissions` (trusted path).** Read the final projection table; for each recipient `INSERT (report_run_id, channel_id, channel_code, commission_amount) ON CONFLICT (report_run_id, channel_code) DO NOTHING`. `channel_id` = the report's channel TYPE (`report_setups.channel_type_id`, constant for the report); `channel_code` = the per-recipient code from the IR `final_mapping`. Null / unmapped / duplicate `channel_code` = hard error → run fails (no partial output).
6. **Demo vs Final.** A **Demo** run (`run_type = 1`) writes `final_commissions` for preview and exposes per-stage row counts (**guardrail G3**) but **never** touches approval or disbursement. **Demo cap:** ≤ 5 demo runs per report (`COUNT(*) WHERE run_type = 1` while holding the run slot) → `429` at the API if exceeded.
7. **Complete.** `report_runs.run_status = 'SUCCEEDED'`, `ended_at = now()`. For a FINAL run, leave `disburse_status = 'NONE'` (disbursement is armed later on approval, §11.8 / E3). Publish `run.completed`.
8. **Cleanup.** `DROP SCHEMA run_<id> CASCADE`; set each `run_stages.cleanup_status = 1 Cleaned`. On cleanup failure → leave `cleanup_status = 0 Not Cleaned` for the sweeper. Then `pg_advisory_unlock`, ack the message.

**Crash / stale-run recovery.** If the Executor dies mid-run, the advisory lock auto-releases and the never-acked message is redelivered. A **Hangfire stale-run sweeper** (every 5 min) finds `report_runs` stuck `RUNNING` whose `started_at` is older than a threshold (≈ 30 min) with no live heartbeat, marks them `FAILED`; any orphan `run_<id>` temp schema and any `run_stages.cleanup_status = 0` left behind is dropped/retried. Because all writes are idempotent (step 1 + the UNIQUE keys), a redelivered-but-finished run is detected and skipped.

## 11.8 Disbursement arming & EV / POS workers (E3 canonical chain)

**RabbitMQ arms it → Hangfire enforces timing & publishes → workers execute.** A Hangfire timer must *not* also consume a broker event; the one true chain:

1. **`approval.completed`** (RabbitMQ) → **Web API `q.approval-completed` consumer** sets `report_runs.disburse_status = 'PENDING'` on the relevant Final run (for a recurrent PRE_RUN-only setup, each scheduled Final run is created already disburse-eligible).
2. **Hangfire `DisbursementWorker`** (~every 5 min) selects runs where `disburse_status = 'PENDING'` AND (EV: `report_setups.ev_disbursement_time <= now()`; POS: immediately), publishes `ev.disburse` / `pos.disburse`, and sets `disburse_status = 'IN_PROGRESS'`.
3. **EV / POS worker** executes; on full success → `disburse_status = 'DONE'`, else `'FAILED'`.

### EV disbursement worker (Python, AI01)

**Purpose.** Pay each recipient via the EV API (`10.13.2.7:9898`) and send the SMS. Consumes `q.ev-disburse`. Gated on full approval (BR8) and EV-mode (`is_ev_disbursement = true`, mutually exclusive with POS).
1. Re-read `report_runs`; require `run_status = 'SUCCEEDED'`, `disburse_status = 'IN_PROGRESS'`, and the run's report fully approved.
2. Materialize per-recipient rows in `ev_disburse` from `final_commissions`: `INSERT (report_run_id, channel_id, channel_code, amount, status) … ON CONFLICT (report_run_id, channel_code) DO NOTHING`, `status = 'PENDING'`.
3. For each `PENDING`/`RETRY` row: call the EV API with the BDT amount. On success → `status = 'SUCCESS'`, store `message`, `disburse_at`, then publish `notify.sms` (`templateCode = 'EV_PAID'`, body from `report_setups.sms_content`). On failure → `status = 'FAILED'`/`'RETRY'`, store `message`.
4. **Reconciliation (G4):** assert `SUM(ev_disburse.amount WHERE status='SUCCESS') == SUM(final_commissions.commission_amount)` (within rounding). Set `report_runs.disburse_status = 'DONE'` if all recipients succeeded, else `'FAILED'`.
5. **Idempotency:** a redelivered message re-reads statuses and only re-attempts rows not already `SUCCESS`.

### POS disbursement worker (.NET, AI01)

**Purpose.** Produce one CSV per run and hand it off to POS. Consumes `q.pos-disburse`. POS-mode only (`is_pos_disbursement = true`).
1. Re-read `report_runs`; require `SUCCEEDED` + fully approved + POS-mode. Upsert one `pos_disbursement` row (`ux_pos_disbursement_run` ⇒ one batch per run), `dump_status = 'PENDING'`.
2. Build the CSV from `final_commissions` (`channel_code`, `commission_amount`, run metadata); upload to SeaweedFS; store `object_url`. Set `dump_status = 'GENERATED'`.
3. Hand off to POS. On success → `dump_status = 'HANDED_OFF'`, `disburse_at = now()`, `report_runs.disburse_status = 'DONE'`; on failure → `dump_status = 'FAILED'`.
4. **Reconciliation (G4):** CSV record count and amount-sum must equal the `final_commissions` count/sum for the run.

## 11.9 Hourly user-sync (Hangfire recurring, APP01)

**Purpose.** Keep the local `users` cache aligned with Central Login. Every hour: pull the active user list from Central Login and **upsert** into `users` keyed on the external `user_id` (TEXT, `ux_users_user_id`) — updating `user_name`, `full_name`, `email`, `mobile_no`, `department`. Users no longer returned are **soft-handled**: their `user_rights` rows are removed (deactivation in this schema, §4.3), never hard-deleting the `users` row, so `approval_flow_level_users.user_id` references (by value) and audit history stay intact. Each change writes an `audit_logs` row (`action_type = 2 Update`, `entity_name = 'users'`). The sync is read-only against the IdP and idempotent.

---

# §12 Cross-cutting Concerns

## 12.1 Audit log (`audit_logs`) — append-only

- **Append-only.** The application performs `INSERT` only — there is **no** UPDATE/DELETE code path for `audit_logs`. The Executor and worker DB roles are granted `INSERT` only on this table. This is the primary tamper-evidence control at this scale; an optional hash-chain can be added later without a schema change.
- **Actor is a snapshot.** `changed_by` (a `user_name`) is an immutable snapshot with **no FK** — a later rename or deactivation never rewrites or breaks history. **Note:** `audit_logs.changed_by_user_id` is **`uuid`** (the external identity), while every internal PK/FK in the schema is `bigint` — this is intentional and is the one deliberate type exception in §3; it is **not** a FK to `users.id`.
- **Coverage (BR9 — all changes audited).** One `audit_logs` row for every: config create / update / soft-delete (`report_setups`, `data_sources`, `channels`, the approval-flow tables, `users` / `user_rights`); IR Final Save and SQL generation; run trigger / completion / failure / cancellation; each approval decision (in addition to the `report_approval_details` row); each disbursement (EV per recipient, POS batch); schedule changes. Login events are captured separately in the `login_log` table.
- **Shape (§3.8).** `application_name`, `entity_name`, `entity_id`, `action_type` (int enum), `changed_columns`, `old_values` / `new_values` (JSONB), `changed_by`, `changed_at`. The Web API writes the audit row in the **same DB transaction** as the change, so an action and its audit row commit atomically.

## 12.2 Error handling

All non-2xx API responses use a single **standard error envelope** with the matching HTTP status. `correlationId` echoes the request's correlation id (also logged to Serilog/Loki).

```json
{
  "error": {
    "code": "VALIDATION_ERROR",
    "message": "One or more fields are invalid.",
    "correlationId": "c0ff...-uuid",
    "fieldErrors": [
      { "field": "endDate", "message": "endDate must be on or after startDate." },
      { "field": "reportName", "message": "A report with this name already exists." }
    ]
  }
}
```
`fieldErrors` is present only for 400/422 validation failures; `[]` or omitted otherwise.

**HTTP status code table.**

| Status | `error.code` (example) | When |
|---|---|---|
| 200 OK | — | Successful GET / action with body |
| 201 Created | — | Resource created (returns new `id`) |
| 202 Accepted | — | Async accepted (run requested, disbursement queued, Final Save) |
| 204 No Content | — | Successful action with no body (soft-delete, ack) |
| 400 Bad Request | `VALIDATION_ERROR` | Malformed body / bad params / business-rule violation (BR3/BR4, schedule date < End Date) |
| 401 Unauthorized | `UNAUTHENTICATED` | Missing / expired / invalid JWT (incl. 3-hour inactivity logout) |
| 403 Forbidden | `FORBIDDEN` | Authenticated but role/right lacks permission (BR1); maker == checker (BR5) |
| 404 Not Found | `NOT_FOUND` | Unknown id |
| 409 Conflict | `CONFLICT` | Duplicate unique key, illegal state transition, a run already active |
| 422 Unprocessable | `UNPROCESSABLE_ENTITY` | IR / SQL validation failure, reconciliation mismatch |
| 429 Too Many Requests | `RATE_LIMITED` | Throttle (demo-run cap, OTP retries) |
| 500 Internal | `INTERNAL_ERROR` | Unhandled server fault (no internal details leaked; `correlationId` only) |
| 502 / 503 | `UPSTREAM_ERROR` / `SERVICE_UNAVAILABLE` | EV / SMS / Central Login / SMTP upstream failure |

Canonical `error.code` values: `VALIDATION_ERROR, UNAUTHENTICATED, FORBIDDEN, NOT_FOUND, CONFLICT, UNPROCESSABLE_ENTITY, RATE_LIMITED, INTERNAL_ERROR, UPSTREAM_ERROR, SERVICE_UNAVAILABLE`.

## 12.3 Pagination, filtering & sorting

All list endpoints share one model.

**Request (query string):** `?page=1&pageSize=25&sort=createdAt:desc&filter[status]=ON&filter[search]=campaign`
- `page` (1-based, default 1), `pageSize` (default 25, max 100).
- `sort` = `field:asc|desc`, comma-separated for multi-sort; only **whitelisted** sortable fields per endpoint.
- `filter[<field>]` = equality; `filter[search]` = free-text over the endpoint's searchable columns; date ranges via `filter[from]` / `filter[to]`.

**Response envelope:**
```json
{ "items": [ /* DTOs */ ], "page": 1, "pageSize": 25, "totalItems": 134, "totalPages": 6, "sort": "createdAt:desc" }
```
Filtering and sorting are always applied **server-side with parameterized queries** (never string-built), and the sort/filter field names are mapped through a per-endpoint allowlist to the real columns.

## 12.4 Data retention

- **Hot window — 3 months.** Source / operational data needed for runs is kept in the **active (hot)** source tables; data older than 3 months is moved to **archive** tables/schema by the Airflow ETL. Reports needing >3-month history (cohort M1–M12) read pre-computed archive/DWH tables, not the hot tables.
- **Object purge — 30 days.** Transient run artifacts in SeaweedFS — `run_stages` stage outputs and Demo-run exports (`run_stages.object_url` / `bucket`) — are purged after **30 days** by a Hangfire sweeper. **Disbursement artifacts are excluded:** POS handoff CSVs (`pos_disbursement.object_url`) are retained per finance policy.
- **DB rows are kept (soft-delete, never hard-delete)** for `report_setups`, `final_commissions`, `ev_disburse`, `pos_disbursement`, the `approval_*` / `report_approval*` tables, `audit_logs`, `login_log`, `email_notifications`, and `sms_notifications` — these are the financial / audit record. `is_active = false` hides config rows (`data_sources`) from pick-lists without losing history (BR2).
- **Temp namespaces** (`run_<id>` schemas) are dropped at end-of-run; the stale-run sweeper drops any orphans within minutes.

---

# §13 Module & Service Architecture

## 13.1 Backend Layering (.NET 4-layer)

The backend is **Clean Architecture**: each layer depends only on the one below it, and the inner layers (Domain, Application) have **no infrastructure dependencies**. This keeps the IR handling, the approval state machine, and the trusted-path money writes testable and isolated from PostgreSQL/RabbitMQ/SeaweedFS specifics.

| Layer | Project | Responsibility | Concrete contents |
|---|---|---|---|
| **Presentation** | `SalesCom.Api` | HTTP surface | ASP.NET Core controllers (one per feature), request/response DTOs, model binding, JWT auth + RBAC middleware, exception → error-envelope filter, DI composition root, Hangfire server registration, Swagger. |
| **Use-case** | `SalesCom.Application` | Orchestration & rules | Command/query handlers (one per use case), DTOs, FluentValidation validators, and the **ports** (interfaces) the inner layers call: `IReportRepository`, `IRunRepository`, `IApprovalService`, `IEventPublisher`, `IObjectStore`, `ICentralLoginClient`, `INotificationGateway`, `IIrValidator`. Publishes events; enforces BR1–BR9. |
| **Core model** | `SalesCom.Domain` | Pure business model | Entities (`ReportSetup`, `SectionSql`, `ReportRun`, `RunStage`, `FinalCommission`, `ApprovalFlow`, `ReportApproval`, `ReportApprovalDetail`, `EvDisburse`, `PosDisbursement`), value objects (`Money`, `GrainKey`), and **int enums** (`RunType`, `RunStatus`, `OverallStatus`, `ApprovalStatus`, `ApprovalType`, `RecurrentType`). No external dependencies. |
| **Adapters** | `SalesCom.Infrastructure` | Integrations | Implementations of the Application ports: PostgreSQL access (**Dapper** for reads, **EF Core** `SalesComDbContext` + migrations for writes/schema against `salescomdbtst`), SeaweedFS S3 client, RabbitMQ publisher + consumers, Central Login client, SMS + SMTP gateways, Hangfire job definitions. |

**Dependency direction:** `Api → Application → Domain` and `Infrastructure → Application/Domain` (Infrastructure implements the ports declared in Application). The API composes everything via DI at startup.

### One controller per feature

| Controller | Base route | Owns endpoints for |
|---|---|---|
| `AuthController` | `/api/v1/auth` | login, SSO callback, logout, `/me` |
| `DataSourcesController` | `/api/v1/data-sources` | register / list / read / update / activate-deactivate |
| `ReportsController` | `/api/v1/reports` | wizard draft + steps, supporting uploads, Final Save, clone, list/details |
| `RunsController` | `/api/v1/runs` | demo run, run-now, schedule on/off, run status, run-stage outputs, disbursement status |
| `ApprovalsController` | `/api/v1/approvals` | flow config, pending queue, approve/reject decisions, approval history |
| `DisbursementsController` | `/api/v1/disbursements` (+ `/runs/{id}/disburse`) | EV/POS status, retry, POS CSV download |
| `DashboardController` | `/api/v1/dashboard` | login attempts, run summary, approval/disbursement widgets |
| `NotificationsController` | `/api/v1/notifications` | template management, send log, retry, DLR callback |

Each controller is **thin**: bind DTO → dispatch a command/query handler in `Application` → map the result to a response DTO. No business logic in controllers.

## 13.2 Background Workers (Hangfire)

Hangfire runs **inside the .NET app** (PostgreSQL-backed storage). Workers are timed/triggered, publish events, or drive integrations — they never run the calc SQL (that's the Python Executor).

| Worker | Trigger | Owns | Publishes / calls |
|---|---|---|---|
| **SchedulerWorker** | Cron (per recurrent report) | For each due, approved, scheduled report, run the **pre-run checks** (report `end_date <= today`; setup approval fully done; every used `data_sources` ETL finished up to `end_date`; the schedule date `>= end_date`), then create a `report_runs` row (`run_type=2` Final, `triggered_by=NULL` = system) and enqueue it. | Publishes `run.requested` → `q.run.low`. |
| **DisbursementWorker** | Approval completed (**`report_approvals.overall_status=4`** Post Approved, arming `disburse_status=PENDING`) **+** `report_setups.ev_disbursement_time` reached | For an approved FINAL run with payout on: EV path drives per-recipient `ev_disburse` (trusted-path, keyed on `channel_code`) + SMS; POS path builds the CSV → `pos_disbursement`. Honors EV/POS mutual exclusion. | Publishes `ev.disburse` → `q.ev-disburse` / `pos.disburse` → `q.pos-disburse`; writes POS CSV to SeaweedFS. |
| **UserSyncWorker** | Hourly cron | Pull users + rights from Central Login → upsert `users` and `user_rights`. | Calls `ICentralLoginClient`. |
| **NotificationWorker** | On enqueue / retry timer | Drain `notification_logs` rows in `status=0` (Pending), send via SMS or SMTP, update `status`/`attempt_count`/`error_message`/`sent_at`. | Calls SMS + SMTP gateways. |


> The two **Python services** (SQL Generator, SQL Executor) are **separate processes**, not Hangfire workers — they are RabbitMQ consumers. The .NET side only *publishes* `report.saved` and `run.requested`, and *consumes* `run.completed`.

## 13.3 Frontend Module Map (Next.js, App Router)

```
salescom-web/
├─ app/
│  ├─ (auth)/
│  │  ├─ login/page.tsx               # username/password → SSO + OTP → POST /auth/login
│  │  └─ callback/page.tsx            # SSO return → exchanges authToken, stores JWT
│  ├─ (app)/                          # authenticated shell (sidebar, JWT guard, 3h inactivity)
│  │  ├─ dashboard/page.tsx
│  │  ├─ data-sources/                # list + add/edit (Admin)
│  │  ├─ reports/
│  │  │  ├─ page.tsx                  # report list (DERIVED status label, filters, row actions)
│  │  │  ├─ new/                      # the 5-step wizard
│  │  │  │  ├─ step-1-basic/          # report_setups basics + channel_type_id
│  │  │  │  ├─ step-2-uploads/        # report_supporting_uploads
│  │  │  │  ├─ step-3-achievements/   # achievement blocks (IR)
│  │  │  │  ├─ step-4-incentives/     # incentive blocks + final_mapping (IR)
│  │  │  │  └─ step-5-review/         # Final Save / Demo / Schedule
│  │  │  └─ [id]/                     # read-only details tabs + run history
│  │  ├─ approvals/                   # pending queue + decision + history
│  │  └─ disbursements/               # EV/POS status, POS CSV download
│  └─ api/                            # Next route handlers (server-side proxy to backend)
├─ src/
│  ├─ features/                       # one module per feature
│  │  ├─ wizard/                      # IR builder UI, stage editors, live preview, guardrail warnings
│  │  ├─ approvals/  ├─ data-sources/  ├─ runs/  └─ dashboard/
│  ├─ lib/
│  │  ├─ apiClient.ts                 # typed fetch wrapper, attaches JWT, error-envelope parsing
│  │  ├─ ir/                          # IR TypeScript types (mirror IR_Schema), client-side validation
│  │  └─ auth/                        # JWT storage, 3-hour inactivity logout
│  └─ components/                     # shared UI (tables, paginator, form controls)
└─ ...
```

- **`(auth)`** routes are unauthenticated; everything in **`(app)`** is guarded by the JWT (3-hour frontend inactivity → token cleared → re-login).
- The **wizard** module is the heart of the FE: it builds the **IR object** client-side (typed from Appendix A / `IR_Schema_and_MultiKPI_Join.md`), validates shape locally, shows the Demo preview and **guardrail warnings (G1–G3)**, and posts the IR to the backend at each step / Final Save.
- The **report list status is a DERIVED label** computed by the FE from the API response (`report_approvals.current_level_order` + `report_approval_details` rows), e.g. "Approved by L1, now Pending at L2" / "Rejected by L2, now Pending at L1" / "Rejected by L1, now pending for edit & resend". It is **not** a stored column.
- `src/lib/ir/` types **must stay in sync** with the IR schema and the canonical `salescomdbtst` column names in §3.

---

# §14 Getting Started / Dev Setup

## 14.1 Repository Layout

A **polyrepo** (or one mono-repo with these top-level folders) — three deployables + shared contracts:

```
salescom/
├─ salescom-web/            # Next.js frontend (see §13.3)
├─ salescom-api/            # .NET solution
│  ├─ SalesCom.Api/
│  ├─ SalesCom.Application/
│  ├─ SalesCom.Domain/
│  ├─ SalesCom.Infrastructure/
│  └─ SalesCom.sln
├─ salescom-calc/           # Python calc engine
│  ├─ generator/            # IR → SQL (SQLGlot), writes section_wise_report_sqls
│  ├─ executor/             # runs run_stages, temp tables, → final_commissions handoff
│  ├─ common/               # IR models, allowlist validator, DB session (SQLAlchemy)
│  ├─ pyproject.toml
│  └─ requirements.txt
├─ contracts/               # source of truth shared by all three
│  ├─ ir_schema.json        # JSON Schema for report_setups.definition (from IR_Schema doc)
│  ├─ openapi.yaml          # /api/v1 contract (DTOs, error envelope)
│  ├─ events/               # RabbitMQ message schemas (report.saved, run.requested, run.completed, ...)
│  └─ ddl/                  # canonical salescomdbtst DDL (§3) + seed scripts
├─ infra/
│  └─ docker-compose.yml    # local stack: postgres, rabbitmq, seaweedfs, api, web, calc
└─ docs/                    # this LLD, IR_Schema, Commission_Logic_Catalog, SDD
```

> **Everything runs in Docker except the production DB** (bare-metal Percona PostgreSQL 18). Locally, Postgres runs in the compose stack for convenience.

## 14.2 Environment Variables

Set these per service (`.env` locally; secrets via the platform secret store in prod). Names are illustrative but stable.

**`salescom-api` (.NET)**
```
ASPNETCORE_ENVIRONMENT=Development
DB__ConnectionString=Host=postgres;Port=5432;Database=salescom;Username=salescom_app;Password=...;SearchPath=salescomdbtst
DB__ExecutorRole=salescom_exec          # least-privilege role the Python Executor uses
RABBITMQ__Uri=amqp://guest:guest@rabbitmq:5672/
S3__Endpoint=http://seaweedfs:8333
S3__AccessKey=...    S3__SecretKey=...
S3__Buckets__Uploads=salescom-uploads
S3__Buckets__StageOut=salescom-stageout
S3__Buckets__PosCsv=salescom-pos
JWT__Issuer=salescom    JWT__SigningKey=...    JWT__InactivityLogoutHours=3
CENTRAL_LOGIN__BaseUrl=https://blposapi.banglalink.net
CENTRAL_LOGIN__AppName=...    CENTRAL_LOGIN__AppKey=...
EV_API__BaseUrl=http://10.13.2.7:9898
SMS__Host=172.16.7.210   SMS__Port=13082
SMTP__Host=...   SMTP__Port=587   SMTP__From=salescom@banglalink.net
HANGFIRE__DashboardEnabled=true
```

**`salescom-calc` (Python)**
```
DB_DSN=postgresql+psycopg://salescom_exec:...@postgres:5432/salescom   # least-privilege
DB_SCHEMA=salescomdbtst
RABBITMQ_URI=amqp://guest:guest@rabbitmq:5672/
S3_ENDPOINT=http://seaweedfs:8333   S3_ACCESS_KEY=...   S3_SECRET_KEY=...
STAGE_TEMP_SCHEMA=run_temp          # schema for per-run temp/output tables
SQL_ALLOWLIST_STRICT=true           # reject anything outside the read-only allowlist
PYTHONUTF8=1
```

**`salescom-web` (Next.js)**
```
NEXT_PUBLIC_API_BASE=https://localhost:5001/api/v1
NEXT_PUBLIC_INACTIVITY_LOGOUT_MINUTES=180
```

## 14.3 Local Wiring (.NET ↔ Python ↔ RabbitMQ ↔ Postgres ↔ SeaweedFS)

Bring the whole stack up with `infra/docker-compose.yml`:

1. **Postgres** starts first; an init job creates schema `salescomdbtst` and applies `contracts/ddl/` (schema + seed). Two DB roles are created: `salescom_app` (full DML on the 20 tables) and **`salescom_exec`** (least-privilege: `SELECT` on source/upload tables, `CREATE/DROP` only in `run_temp`, **no write** on the money tables `final_commissions` / `ev_disburse` — enforces D2).
2. **RabbitMQ** starts; queues declared on first connect: `q.sql-generate`, `q.run.high`, `q.run.mid`, `q.run.low`, `q.ev-disburse`, `q.pos-disburse`, `q.notify`, plus `q.run-completed`, `q.approval-completed`, `q.notification`, and each `q.<name>.dlq`.
3. **SeaweedFS** starts; buckets `salescom-uploads`, `salescom-stageout`, `salescom-pos` are created.
4. **salescom-api** connects to Postgres (both roles configured), RabbitMQ, SeaweedFS; runs EF Core migrations on startup in dev; starts the Hangfire server (Scheduler, EV Disbursement, Notification).
5. **salescom-calc** starts **two consumers**:
   - *Generator* binds `q.sql-generate` ← `report.saved`. On message: load `report_setups.definition`, compile IR → SQL via SQLGlot, write `section_wise_report_sqls` (one row per `stage_order`).
   - *Executor* binds `q.run.high/mid/low` (priority order) ← `run.requested`. On message: snapshot `section_wise_report_sqls` → `run_stages` (frozen `sql_text`), re-parse each SQL through the allowlist, execute stage-by-stage in `run_temp` (writing `run_stages.output_table_name`), record per-stage row counts, then publish `run.completed` (the API consumes it for the trusted-path `final_commissions` write and the post-run approval trigger).
6. **salescom-web** points at the API base URL.

**Message contracts** (`contracts/events/`):

| Event | Queue(s) | Payload (minimal) | Producer → Consumer |
|---|---|---|---|
| `report.saved` | `q.sql-generate` | `{ reportSetupId, irVersion }` | Reports API → SQL Generator |
| `run.requested` | `q.run.high` (RunNow) · `q.run.mid` (Demo) · `q.run.low` (Schedule) | `{ runId, reportSetupId, runType }` | Runs API / Scheduler → SQL Executor |
| `run.completed` | `q.run-completed` | `{ runId, status, lastStageTable, rowCount }` | Executor → Runs API (trusted-path `final_commissions`) |
| `approval.completed` | `q.approval-completed`, `q.notification` | `{ reportSetupId, reportRunId, phase }` | Approval API → DisbursementWorker / Notification |
| `ev.disburse` | `q.ev-disburse` | `{ runId }` | DisbursementWorker → EV Worker |
| `pos.disburse` | `q.pos-disburse` | `{ runId }` | DisbursementWorker → POS Worker |
| `notify.sms` / `notify.email` | `q.notify` | `{ templateCode, recipient, body, ... }` | any → Notification consumer |

The **single-run model** is realized by the Executor processing one `run.requested` at a time (advisory lock §11.7), draining high before mid before low.

## 14.4 EF Core Migration Command

Schema is owned by EF Core migrations in `SalesCom.Infrastructure` (`SalesComDbContext`, default schema `salescomdbtst`). The canonical DDL in §3 is the **target**; the initial migration must reproduce it exactly: `int8 GENERATED BY DEFAULT AS IDENTITY` PKs, `int8` FKs, the `int4` enum columns, and the idempotency UNIQUE indexes (`final_commissions(report_run_id, channel_code)`, `ev_disburse(report_run_id, channel_code)`, `pos_disbursement(report_run_id)`, `run_stages(run_id, sort_order)`, `section_wise_report_sqls(report_setup_id, stage_order)`, `report_setups(report_name)`).

```bash
# from salescom-api/
dotnet ef migrations add InitialSchema \
  --project SalesCom.Infrastructure \
  --startup-project SalesCom.Api \
  --context SalesComDbContext

dotnet ef database update \
  --project SalesCom.Infrastructure \
  --startup-project SalesCom.Api \
  --context SalesComDbContext
```

After migrating, run the **seed script** (`contracts/ddl/seed.sql`) to load lookups and config: `channels` (Distributor / RSO / Retailer), default `approval_flows` + `approval_flow_levels` (with `approval_type` 1=Pre-Run / 2=Post-Run) + `approval_flow_level_users`, notification templates (for `notification_logs.template_code`), and the `user_rights.rights_code` → role mapping (Business User=10 / Approver=20 / Administrator=30 — Appendix B / §4.6 RBAC matrix).

## 14.5 Recommended Build Order

Build features in dependency order so each is testable before the next depends on it. The architecture supports the whole system now; this is the **construction sequence**, not a scope cut.

| # | Module | Why this order | Done when |
|---|---|---|---|
| **1** | **Auth & session** | Nothing else works without identity/RBAC. Central Login SSO+OTP → JWT, `/auth/me`, hourly UserSync (`users`/`user_rights`), `login_log` logging. | A user can sign in, get a JWT, and `/me` returns the resolved role; failed/successful attempts land in `login_log`. |
| **2** | **Seed / lookup** | Reports, flows, channels all FK into lookups. Load `channels`, default `approval_flows`/`approval_flow_levels`/`approval_flow_level_users`, notification templates, `user_rights.rights_code`→role. | All FK targets exist; seed script idempotent. |
| **3** | **Data Source management** | The wizard's sources come from here (BR2: never delete; can't deactivate if in use). | Admin can register/activate a `data_sources` row; a business user lists only active ones. |
| **4** | **Supporting upload** | The wizard joins uploaded files as tables; needed before the wizard's `combine` / external-config join works. | File → SeaweedFS → `report_supporting_uploads` row → ingested DB table (`db_schema.db_table_name`). |
| **5** | **Wizard / IR** | Produces `report_setups.definition`. Validate the IR shape against `ir_schema.json` on save; persist draft + steps; **Final Save sets `is_setup_complete=true`**, initiates `report_approvals`, and publishes `report.saved`. | A complete IR is saved and validated; `report.saved` is published; `is_setup_complete=true`. |
| **6** | **SQL Generator (Python)** | Consumes `report.saved`; compiles IR → SQL → `section_wise_report_sqls`. Build Phase-1 ops first (`filter/combine/summarize/calculate/modify`), then add ops in later phases. | For a worked IR (e.g. the RSO GA+Recharge example), `section_wise_report_sqls` holds correct per-stage SQL. |
| **7** | **Run Executor (Python)** | Consumes `run.requested`; snapshots `section_wise_report_sqls`→`run_stages`, allowlist re-parse, stage-by-stage temp tables, row-count guardrails, demo cap, `run.completed`. Then the .NET trusted path writes `final_commissions` (`ON CONFLICT (report_run_id, channel_code) DO NOTHING`). Pre-run checks run for Final/Scheduled only; Demo **skips** them. | A FINAL run produces correct per-recipient `final_commissions`; a DEMO run previews row counts and is never disbursable. |
| **8** | **Approval** | Gates disbursement (BR8). Configurable flow/levels/users; one `report_approvals` instance per report; sequential ascending (BR6); reject needs `comments` (BR7); maker≠checker (BR5). `overall_status` walks 0→1→2→3→4; each decision is a `report_approval_details` row. | A report walks its flow to Post Approved (4) or back to Draft (0) with a full `report_approval_details` trail. |
| **9** | **Disbursement** | Only after full approval (`overall_status=4`, BR8). EV (auto + SMS, `ev_disburse`) or POS (CSV → `pos_disbursement`), mutually exclusive; idempotent per `(report_run_id, channel_code)` / per run. | Approved run pays out via the chosen channel exactly once; SMS/CSV produced. |
| **10** | **Dashboard & Notification** | Cross-cuts everything above; built last because it reads all prior tables. Login attempts, run/approval/disbursement summaries; SMS + email send + `notification_logs`. | Dashboard widgets render real data; notifications logged and delivered. |

**Cross-cutting** (audit log `audit_logs`, error envelope, pagination, RBAC middleware) is built **alongside step 1** and used by every later module.

---

# Appendix A — IR Reference

> **Authority.** The full, authoritative IR contract — the `report_setups.definition` JSONB shape, the formal JSON Schema (draft 2020-12), every enum value list, the operation-type catalogue, the multi-KPI section-join rule, and a complete worked example — lives in **`IR_Schema_and_MultiKPI_Join.md`** (the LLD annex). This appendix **summarises** the shape so the LLD is self-navigable; it does **not** duplicate the annex. Where this appendix and the annex differ, **the annex wins**.

## A.1 IR top-level shape

The IR is stored in `report_setups.definition` (JSONB). At the top level it is:

```jsonc
{
  "report":      { "name": "...", "channel": "RSO", "cycle": "Apr 2026",
                   "start_date": "2026-04-01", "end_date": "2026-04-30" },   // mirrors Step-1 basics
  "achievements": [ /* Block, Block, ... */ ],   // Step-3: performance figures
  "incentives":   [ /* Block, Block, ... */ ],   // Step-4: payout logic
  "final_mapping": {                              // Step-4: how the last block becomes final_commissions
    "from_block": "INC1",
    "channel_code_column": "RSO_CODE",            // → final_commissions.channel_code (per-recipient payee key)
    "commission_amount_column": "Incentive",      // → final_commissions.commission_amount
    "channel_scope": "RSO"
  }
}
```

The report's **channel TYPE** comes from `report_setups.channel_type_id` (a `channels` row), and is written to `final_commissions.channel_id` (constant for the report). The **per-recipient code** is `final_mapping.channel_code_column`, written to `final_commissions.channel_code`. (See §6.3 channel model.)

## A.2 Block shape

A **Block** is the same shape for an achievement and an incentive — a `source` plus an ordered list of `stages`, ending in a `summarize` that fixes the block's **grain**:

```jsonc
{
  "block_id": "ACH1",
  "source": { "type": "data_source" | "upload" | "block", "ref": "ev_recharge_daily" },
  "stages": [
    { "op": "filter",    "...": "..." },
    { "op": "combine",   "join_with": { "type": "...", "ref": "..." }, "match_on": ["RSO_CODE"], "how": "left" },
    { "op": "summarize", "group_by": ["RSO_CODE"], "aggregates": [ { "fn": "sum", "col": "amount", "as": "Recharge" } ] },
    { "op": "calculate", "mode": "formula" | "ifcase" | "map", "...": "..." },
    { "op": "modify",    "...": "..." }
  ],
  "output_grain": ["RSO_CODE"],     // = the final summarize group_by
  "outputs": [ "RSO_CODE", "Recharge", "RechargePct" ]
}
```

**Operation types** (Phase 1 → grows in later phases): `filter`, `combine`, `summarize`, `calculate` (`formula` / `ifcase` / `map`), `modify`; **Phase 3 adds** `rank` (window functions: `NTILE`, `PERCENT_RANK`). The IR *shape* is final now; only the op-list grows (Phase 1→3).

## A.3 Multi-KPI section-join rule

The core safety contract for multi-KPI commissions:

- **Every block ends in a `summarize`** that fixes its grain (e.g. "1 row per RSO"). `output_grain` = that `summarize`'s `group_by`.
- **Block-to-block joins must be on the grain key** (1:1). A block reading two achievement blocks (`source.type: "block"` + a `combine`) joins them on the shared grain key, never on a finer column.

This guarantees no accidental row multiplication (fan-out) when combining KPIs.

## A.4 Guardrails (G1–G4)

| Guard | What it checks | When |
|---|---|---|
| **G1** | **Pre-join uniqueness** — the join key is unique in each side before a grain-key join. | Generated into the SQL; checked at execute time. |
| **G2** | **Post-join fan-out** — output row count did not exceed the expected grain cardinality. | Execute time, per stage. |
| **G3** | **Demo-run per-stage row counts** — every stage's `output_table_name` row count is surfaced in the Demo Run Log so the Maker catches fan-out before a real run. | Demo runs (§6.7). |
| **G4** | **Reconciliation** — disbursed total == sum of the final block's outputs (`SUM(final_commissions.commission_amount)`). | End of disbursement (§10.5). |

## A.5 IR → schema column mapping

| IR element | Lands in |
|---|---|
| `report.*` (name/channel/cycle/dates) | `report_setups` basics (mirror of Step-1) |
| `achievements[]`, `incentives[]`, `final_mapping` | `report_setups.definition` (JSONB) |
| each block stage (compiled) | one `section_wise_report_sqls` row (`stage_order`, `sql_text`) |
| `final_mapping.channel_code_column` | `final_commissions.channel_code` (per-recipient) |
| `final_mapping.commission_amount_column` | `final_commissions.commission_amount` |
| (report's) `channel_type_id` | `final_commissions.channel_id` (constant TYPE) |

> For the full JSON Schema, enum lists, operation catalogue and the complete worked RSO GA+Recharge example, see **`IR_Schema_and_MultiKPI_Join.md`**.

---

# Appendix B — Enum & State-Transition Reference

Every `int4` enum column in §3 is listed here with its **code → meaning** map. The application owns code→meaning; these lists are authoritative and must stay in sync with §3 comments. `*` = terminal state. Transitions not listed are illegal and rejected with **409 Conflict**.

> **Note on mixed types.** Run-level lifecycle columns `report_runs.run_status` and `report_runs.disburse_status` are `varchar(50)` in the real schema (string codes), as are the disbursement `status` / `dump_status`. The columns below typed `int4` use **numeric codes**. Both are documented here so writers use the correct literal type.

## B.1 `login.status` (int4)
| Code | Meaning |
|---|---|
| 1 | Success |
| 2 | Failed |

## B.2 `report_runs.run_type` (int4)
| Code | Meaning |
|---|---|
| 1 | Demo |
| 2 | Final |

## B.3 `run_stages.run_status` (int4)
| Code | Meaning |
|---|---|
| 0 | Pending |
| 1 | Running |
| 2 | Succeeded |
| 3 | Failed |
```
0 Pending → 1 Running → 2 Succeeded*
1 Running → 3 Failed*     (aborts the parent run)
```

## B.4 `run_stages.cleanup_status` (int4)
| Code | Meaning |
|---|---|
| 0 | Not Cleaned |
| 1 | Cleaned |
```
0 Not Cleaned → 1 Cleaned*     (temp output dropped)
0 Not Cleaned → 0 Not Cleaned  (drop failed; sweeper retries until 1)
```

## B.5 `approval_flow_levels.approval_type` (int4) — approval **phase**
| Code | Meaning |
|---|---|
| 1 | Pre-Run (setup approval) |
| 2 | Post-Run (result approval) |

## B.6 `report_approvals.overall_status` (int4) — 2026-06-16 lifecycle
| Code | Meaning |
|---|---|
| 0 | Pending for Editing & Resubmission (Draft) |
| 1 | Pre Approval Pending |
| 2 | Pre Approved |
| 3 | Post Approval Pending |
| 4 | Post Approved |
```
0 Draft               → 1 Pre Approval Pending     (Maker submits setup for pre-approval)
1 Pre Approval Pending→ 1 Pre Approval Pending     (a pre-level approves; advance current_level_order — BR6)
1 Pre Approval Pending→ 2 Pre Approved             (final pre-level approves → report runnable)
1 Pre Approval Pending→ 0 Draft                    (any pre-level rejects → back to Maker for edit & resend, BR7 comment)
2 Pre Approved        → 3 Post Approval Pending     (Final run produced results; result approval opens)
3 Post Approval Pending→3 Post Approval Pending     (a post-level approves; advance current_level_order)
3 Post Approval Pending→4 Post Approved*            (final post-level approves → disbursement armed)
3 Post Approval Pending→0 Draft                     (a post-level rejects → back to Maker, BR7 comment)
```
> The report-list **status label is DERIVED** (2026-06-16 decision 5) from `current_level_order` + the `report_approval_details` rows — e.g. *"Approved by L1, now Pending at L2"*, *"Rejected by L2, now Pending at L1"*, *"Rejected by L1, now pending for edit & resend"*. It is **not** a stored column.

## B.7 `report_approval_details.approval_status` (int4)
| Code | Meaning |
|---|---|
| 1 | Approved |
| 2 | Rejected |

`2 Rejected` requires a non-empty `comments` (BR7). One row per level action (append-only decision record).

## B.8 `notification_logs.channel` (int4)
| Code | Meaning |
|---|---|
| 1 | Email |
| 2 | SMS |

## B.9 `notification_logs.status` (int4)
| Code | Meaning |
|---|---|
| 0 | Pending |
| 1 | Sent |
| 2 | Failed |
```
0 Pending → 1 Sent*               (gateway / SMTP accepted)
0 Pending → 2 Failed → 0 Pending  (retry up to cap; attempt_count++)
2 Failed  → 2 Failed*             (cap reached)
```

## B.10 `audit_logs.action_type` (int4)
| Code | Meaning |
|---|---|
| 1 | Create |
| 2 | Update |
| 3 | Delete |

## B.11 String-coded run / disbursement states (varchar in §3, for completeness)
- **`report_runs.run_status`** — `QUEUED, RUNNING, SUCCEEDED, FAILED`
  ```
  QUEUED  → RUNNING        (executor claims the global run slot)
  QUEUED  → FAILED*        (pre-run check fails: not approved / End Date / ETL not done)
  RUNNING → SUCCEEDED*     (all stages 2 Succeeded + final_commissions written)
  RUNNING → FAILED*        (a stage failed / mapping error / stale-run reclaim)
  ```
- **`report_runs.disburse_status`** — `NONE, PENDING, IN_PROGRESS, DONE, FAILED`
  ```
  NONE        → PENDING       (approval.completed arms the Final run — E3 step 1)
  PENDING     → IN_PROGRESS   (DisbursementWorker publishes ev/pos.disburse — E3 step 2)
  IN_PROGRESS → DONE*         (all recipients SUCCESS / POS HANDED_OFF + G4 reconciled)
  IN_PROGRESS → FAILED        (one or more failed) → IN_PROGRESS (re-drive)
  ```
  (`NONE` is also the resting state for Demo runs, which never disburse.)
- **`ev_disburse.status`** — `PENDING, SENT, SUCCESS, FAILED, RETRY`
  ```
  PENDING → SENT     (EV API call issued)
  SENT    → SUCCESS* (gateway confirmed)
  SENT    → FAILED   (gateway error) → RETRY (attempts remain) → SENT
  RETRY   → FAILED*  (attempts exhausted)
  ```
- **`pos_disbursement.dump_status`** — `PENDING, GENERATED, HANDED_OFF, FAILED`
  ```
  PENDING   → GENERATED    (CSV built + uploaded to SeaweedFS)
  GENERATED → HANDED_OFF*  (delivered to POS)
  PENDING/GENERATED → FAILED → PENDING  (retry)
  ```

## B.12 Other fixed enums (for reference)
- **`report_setups.recurrent_type`** (int4): `1=Daily, 2=Weekly, 3=Monthly, 4=Quarterly, 5=Yearly` (used only when `is_recurrent = true`).
- **`user_rights.rights_code`** (int4): role code — `10=MAKER (Business User)`, `20=CHECKER (Approver)`, `30=ADMIN (Administrator)`. Resolved role = highest active code (§4.6).
- **`report_setups.status`** (varchar): `ON` / `STOP` (whether the report is runnable / schedulable).

---

# Appendix C — Business Rules (BR1–BR9) + Enforcement

| BR | Rule | Primary enforcement point |
|---|---|---|
| **BR1** | Access by assigned role/right. | JWT + RBAC middleware; per-endpoint role check; sensitive actions live-re-check `user_rights` (§4.4.4c, §4.6). |
| **BR2** | A data source is never deleted, only deactivated; cannot deactivate while in use. | No DELETE route; `data_sources.is_active=false`; in-use scan of `report_setups.definition` → `409 SOURCE_IN_USE` (§5.4, §5.6 R5/R6). |
| **BR3** | Report name system-wide unique. | `ux_report_setups_report_name` → `409` at Step-1 create; Clone forces a new unique name (§6.4.1, §6.8). |
| **BR4** | Start date ≤ End date. | Step-1 validation → `422` (§6.4.1). |
| **BR5** | Same user can't be Maker + Checker of the same report/run. | Maker = `COALESCE(report_setups.updated_by, report_setups.created_by)`; submit pre-check `MAKER_IS_SOLE_APPROVER`; decision check `SELF_OR_DUPLICATE_APPROVER` (§7.4.3–7.4.4, §7.6). |
| **BR6** | Approval sequential ascending (`level_order`). | Request exposes only `current_level_order`; `STALE_LEVEL` / `NOT_CURRENT_LEVEL_APPROVER`; `ux_approval_flow_levels_flow_order` (§7.4.4, §7.6). |
| **BR7** | Reject requires a comment. | `400 COMMENT_REQUIRED`; `report_approval_details.comments` populated on every `approval_status=2` (§7.4.6, §7.5). |
| **BR8** | Disburse only after full approval. | DisbursementWorker consumes `approval.completed`; `disburse_status='PENDING'` only via `overall_status=4` (post) or `=2` (recurrent pre); precondition gate in §10.4; pre-run check 2 in §6.10. |
| **BR9** | Only Maker manages schedule; all changes audited. | Schedule endpoint role check → `403`; every config/approval/schedule/payout action writes an `audit_logs` row in the same transaction (§6.9, §12.1). |

---

# API Conventions

These conventions apply to **every** endpoint in this LLD; per-feature sections do not repeat them.

- **Versioning & base path.** All endpoints are under **`/api/v1`** (e.g. `/api/v1/reports`). Breaking changes bump the path segment (`/api/v2`).
- **Authentication.** Every call (except the login/callback handshake) requires a SalesCom **JWT** in `Authorization: Bearer <token>`. Missing/expired/invalid → `401 UNAUTHENTICATED`. **Inactivity logout = 3 hours** (sliding); the explicit absolute token expiry is configured at deployment (left unspecified here). **Sensitive actions** (approve, disburse, schedule, all config writes) additionally re-check the role **live** against `user_rights` in the DB — the JWT `role` claim alone is not trusted for money/approval paths (anti-stale-authorization) → `403` on failure.
- **Correlation.** Each request gets a `correlationId` (from an inbound `X-Correlation-Id` header or generated); it is returned in error envelopes, propagated onto every published RabbitMQ message, and logged to Serilog/Loki.
- **Async actions** return **`202 Accepted`** with the created/affected resource id and the new state (e.g. a run returns `reportRunId` + `run_status: "QUEUED"`); the client polls the status endpoint or reacts to a notification.
- **Pagination / filtering / sorting** on all list endpoints use the shared model in §12.3 (`page`, `pageSize`, `sort=field:dir`, `filter[...]`) and the shared response envelope.
- **Errors** always use the standard envelope and status table in §12.2.
- **Money** is `numeric(18,4)` BDT and is serialized as a string in JSON to avoid float drift.
- **Timestamps** are ISO-8601 UTC (`timestamptz` stored UTC); the client localizes for display.
- **Idempotency on writes** to money/run/disbursement paths is backstopped by the §3 UNIQUE keys (`report_run_id, channel_code` etc.); safe to retry.

---

## §15 — Errata & Reconciliation (final critic pass)

> **Authority:** resolves the cross-section gaps a final consistency review found. Where §1–§14 disagree with the below, **§15 is canonical.** Apply when writing the code.

### High

**E1 — Reject is phase-aware; a result-reject never voids setup approval (§7.4.6).**
- **PRE_RUN reject** → `report_approvals.overall_status = 0` (back to Maker for edit), `current_level_order` → first PRE_RUN level; on resubmit, re-enter at status **1** (Pre Approval Pending).
- **POST_RUN reject** → does **NOT** touch the (already Pre-Approved) setup. The reject is recorded in `report_approval_details`; `current_level_order` resets to the **first POST_RUN level**; on resubmit (Maker re-runs a new Final run or edits) the POST block re-enters at status **3** (Post Approval Pending), **never** at status 1/PRE. Setup (PRE) approval is never re-required by a result-reject.

**E2 — `channel_code` is the recipient identifier for EV (resolves the EV-SMS/MSISDN gap).** For an EV-disbursing report, the IR `final_mapping.channel_code_column` is set to the recipient's **MSISDN / EV-account number**, so `ev_disburse.channel_code` **is** the dialable recipient id. The EV API call and the payout SMS (`notification_logs.phone_number`) both use `ev_disburse.channel_code` directly — no extra lookup, no new column. (A report whose channel_code is a non-dialable code cannot use EV — it must use POS.) Document this in §9.4 and §11.8.

### Medium

**E3 — Validation HTTP code = `422` everywhere.** BR4 (start ≤ end) and schedule-date ≥ end-date both return **422 UNPROCESSABLE_ENTITY** (fix the §12.2 table row that said 400). All field-level business validation = 422; only duplicate report/flow name (BR3) = 409.

**E4 — Submit = `201 Created`, one canonical path.** `POST /reports/{id}/submit-approval` is canonical (it creates a new `report_approvals` row → 201). `POST /reports/{id}/submit` is an alias of the same handler. Fix §6.12 success code 200 → 201.

**E5 — `ev_disburse.status` enum = `PENDING | SENT | SUCCESS | FAILED | RETRY`** (add `RETRY` in §10.2/§10.3 to match Appendix B.11 / §11.5 / §11.8).

**E6 — Add the `GET /reports` list-item DTO (§6.12):** `{ id, reportName, channelTypeId, channelName, startDate, endDate, recurrence, isEv, isPos, statusLabel (derived, §7.4.8), isSetupComplete }`, wrapped in the §12.3 pagination envelope.

**E7 — One canonical pagination envelope everywhere:** `{ items, page, pageSize, totalItems, totalPages }`. Rename `total` → `totalItems` and add `totalPages` in the §5.5 / §7.5 / §9.5 / §6.12 examples.

**E8 — `report_setups.status` holds ONLY `ON` / `STOP`** (the run/schedule toggle) — it never stores an approval phase. The "Pending Approval / Rejected" wording in §7.4.3/§7.4.6 means the **derived status label** (§7.4.8), not this column. Approval state lives in `report_approvals.overall_status` (0–4).

### Low

**E9 — `report_runs.disburse_status` enum = `NONE | PENDING | IN_PROGRESS | DONE | FAILED`** (add `IN_PROGRESS` to the §3.5 comment).

**E10 — BR8 approval-gate returns `409 NOT_APPROVED` everywhere** (an illegal-transition precondition). Align §6.10 / §10.6 / §12.2. `403` is reserved for RBAC/role denials and the BR5 maker==checker block.

**E11 — `/auth/me` returns both `lastLoginAt` and `lastFailedLoginAt`** (add the latter) so the dashboard "last successful + last refused sign-in" card (§4.2/§8.2) is satisfied from one DTO.

---

*End of SalesCom Low-Level Design (LLD) v2.0 — FINAL. Grounded in the real `salescomdbtst` schema (§3). Authoritative IR contract: `IR_Schema_and_MultiKPI_Join.md`.*
