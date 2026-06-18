## 3. Database Schema

This is the **authoritative, runnable PostgreSQL DDL** for SalesCom — the single source of truth for all 22 tables. It reconciles the team LLD (§3–§12) against the ERD (22 tables), adopts the more-complete ERD columns, and fixes every known type-mismatch, typo, reserved-word, and idempotency bug. The other LLD authors (API DTOs, IR→SQL, enums/state) rely on the exact column names defined here.

**Engine target:** Percona PostgreSQL 18. **Conventions:** UUID primary keys (`gen_random_uuid()`), `TIMESTAMPTZ` stored in UTC (ISO-8601), money as `NUMERIC` BDT, soft-delete via `is_active` on catalog tables, and standard audit columns (`created_on`, `updated_on`, `created_by`, `updated_by`). Enumerated text columns are constrained with `CHECK` against canonical value lists (the canonical value source is the *Enum & State-Transition appendix*; the same lists are inlined here as `CHECK`s so the schema is self-contained).

### 3.0 Prerequisites

```sql
-- pgcrypto provides gen_random_uuid(); PostgreSQL 18 also exposes uuidv4()/uuidv7().
CREATE EXTENSION IF NOT EXISTS pgcrypto;

-- Application schema. Generated CSV-upload tables and per-run temp tables live in
-- separate schemas (e.g. uploads.*, runtmp.*) and are NOT part of this core DDL.
CREATE SCHEMA IF NOT EXISTS salescom;
SET search_path = salescom, public;
```

### 3.1 Module grouping

| Group | Tables | Purpose |
|---|---|---|
| **Identity & Access** | `user`, `user_right`, `login` | People, their permission bits, and sign-in audit. Synced hourly from Central Login. |
| **Data Catalog** | `data_source`, `channel`, `recurrent_type` | Registered source tables, payout channels, and the recurrence lookup. |
| **Report** | `report_setup`, `report_supporting_upload`, `stages` | The wizard output: the IR (`definition`), uploaded CSV→table registrations, and the compiled per-stage SQL. |
| **Run & Output** | `report_run`, `run_stage`, `final_commission` | One execution of a report, its frozen per-stage SQL snapshot, and the per-channel result. |
| **Approval** | `approval_type`, `approval_flow`, `approval_flow_level`, `approval_flow_level_user`, `approval_request`, `approval_decision` | Reusable maker-checker flows, their ordered levels, level membership, and the per-report request + decision trail. |
| **Disbursement** | `ev_disburse`, `pos_disbursement` | EV per-channel payout records (auto + SMS) and the POS CSV handoff record. |
| **Cross-cutting** | `audit_log`, `notification_log` | Immutable change/action audit and every SMS/email sent. |

---

### 3.2 Identity & Access

#### `user`

Purpose: one row per person, mastered in Central Login and refreshed hourly by the User Sync service.
Relationships: parent of `user_right` (1:N), referenced by `approval_flow_level_user.user_id`. `audit_log`/`login` reference users by the natural key `user_name` as an immutable snapshot (see notes).

```sql
CREATE TABLE "user" (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name     TEXT         NOT NULL,              -- login id (Central Login)
    user_id       TEXT         NOT NULL,              -- external/HR id from Central Login (ERD-adopted)
    full_name     TEXT         NOT NULL,
    mobile_no     TEXT,                               -- ERD-adopted (nullable: not all dir users have it)
    email         TEXT         NOT NULL,
    department    TEXT,                               -- ERD-adopted; FIXED typo: depertment -> department
    is_active     BOOLEAN      NOT NULL DEFAULT TRUE, -- soft-delete / directory deactivation
    created_on    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_on    TIMESTAMPTZ,
    created_by    TEXT         NOT NULL DEFAULT 'system',
    updated_by    TEXT,
    CONSTRAINT uq_user_user_name UNIQUE (user_name),
    CONSTRAINT uq_user_user_id   UNIQUE (user_id)
);

CREATE INDEX ix_user_email     ON "user" (email);
CREATE INDEX ix_user_is_active ON "user" (is_active);
```

> Note: `user` is a reserved word; it must always be quoted as `"user"`. Consider an EF Core table-mapping alias if quoting becomes error-prone.

#### `user_right`

Purpose: the permission bits a user holds (RBAC). One user can hold several rights; the `user_right` INT is a permission/role code resolved by the API into a role (Maker / Checker / Administrator).
Relationships: child of `user` (FK → `user.id`, cascade delete with the user).

```sql
CREATE TABLE user_right (
    id          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),  -- added surrogate PK (ERD had no PK)
    user_id     UUID    NOT NULL,
    user_right  INTEGER NOT NULL,                                -- FIXED typo: INTERGER -> INTEGER
    CONSTRAINT fk_user_right_user
        FOREIGN KEY (user_id) REFERENCES "user" (id) ON DELETE CASCADE,
    CONSTRAINT uq_user_right UNIQUE (user_id, user_right)        -- no duplicate right per user
);

CREATE INDEX ix_user_right_user_id ON user_right (user_id);
```

#### `login`

Purpose: append-only sign-in attempt log feeding the Dashboard "Login Attempts" card.
Relationships: references a user by `user_name` snapshot only (no FK — see notes).

```sql
CREATE TABLE login (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name   TEXT         NOT NULL,                 -- snapshot, not FK (an attempt may be an unknown user)
    full_name   TEXT,                                  -- relaxed to NULL: unknown user has no profile
    login_time  TIMESTAMPTZ  NOT NULL DEFAULT now(),   -- FIXED type: TEXT -> TIMESTAMPTZ
    status      TEXT         NOT NULL,                 -- FIXED typo in domain: faild -> failed
    remarks     TEXT,
    CONSTRAINT ck_login_status CHECK (status IN ('SUCCESS','FAILED'))
);

CREATE INDEX ix_login_user_time ON login (user_name, login_time DESC);  -- "last success / last failed" per user
CREATE INDEX ix_login_time      ON login (login_time DESC);
```

> **audit/login user reference — decision:** `login.user_name` and `audit_log.user_name` are kept as **immutable text snapshots with NO foreign key**. Rationale: (1) a failed login may reference a username that does not exist in `user`; (2) audit rows must survive even if a user is later removed/renamed; (3) the snapshot preserves the name as it was at the time of the action. The application resolves to `user.id` at read time when needed.

---

### 3.3 Data Catalog

#### `data_source`

Purpose: a source table a Business User may read from in the wizard. Never deleted, only deactivated (BR2).
Relationships: referenced logically by the IR `source` refs; no hard FK from `report_setup` (the IR holds the reference inside JSONB).

```sql
CREATE TABLE data_source (
    id                 UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    source_table_name  TEXT         NOT NULL,
    table_description  TEXT,
    is_active          BOOLEAN      NOT NULL DEFAULT FALSE,  -- "off by default" per wizard
    created_on         TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_on         TIMESTAMPTZ,
    created_by         TEXT         NOT NULL,
    updated_by         TEXT,
    CONSTRAINT uq_data_source_table UNIQUE (source_table_name)  -- one registration per source table
);

CREATE INDEX ix_data_source_active ON data_source (is_active);
```

#### `channel`

Purpose: a payout channel (the commission recipient identity, e.g. Distributor/RSO/Retailer). `channel_type` distinguishes the kind.
Relationships: referenced by `report_setup.channel_type_id`, `final_commission.channel_id`, `ev_disburse.channel_id`.

```sql
CREATE TABLE channel (
    id            UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    channel_name  TEXT    NOT NULL,
    channel_type  TEXT    NOT NULL,
    is_active     BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_channel_name UNIQUE (channel_name)
);

CREATE INDEX ix_channel_type ON channel (channel_type);
```

#### `recurrent_type`

Purpose: lookup of recurrence frequencies; `report_setup.recurrent_type` is an FK to this table (no longer free text).
Relationships: referenced by `report_setup.recurrent_type_id`.

```sql
CREATE TABLE recurrent_type (
    id        UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    code      TEXT    NOT NULL,    -- canonical machine code
    label     TEXT    NOT NULL,    -- display label
    is_active BOOLEAN NOT NULL DEFAULT TRUE,
    CONSTRAINT uq_recurrent_type_code UNIQUE (code),
    -- FIXED typo: Monthyly -> MONTHLY ; canonical set
    CONSTRAINT ck_recurrent_type_code CHECK (code IN ('ONETIME','DAILY','WEEKLY','MONTHLY'))
);
```

Seed:
```sql
INSERT INTO recurrent_type (code, label) VALUES
  ('ONETIME','One-time'), ('DAILY','Daily'), ('WEEKLY','Weekly'), ('MONTHLY','Monthly')
ON CONFLICT (code) DO NOTHING;
```

---

### 3.4 Report

#### `report_setup`

Purpose: the wizard output — one report definition. Holds the IR in `definition` (JSONB), schedule window, payout settings, and the bound approval flow.
Relationships: FK → `channel` (channel_type_id), FK → `recurrent_type`, FK → `approval_flow` (ERD-adopted). Parent of `report_supporting_upload`, `stages`, `report_run`, `approval_request`.

```sql
CREATE TABLE report_setup (
    id                     UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    report_name            TEXT         NOT NULL,
    report_type            TEXT         NOT NULL,
    channel_type_id        UUID         NOT NULL,
    commission_cycle       TEXT         NOT NULL,
    start_date             DATE         NOT NULL,
    end_date               DATE         NOT NULL,
    is_recurrent           BOOLEAN      NOT NULL DEFAULT FALSE,
    recurrent_type_id      UUID,                              -- FK to recurrent_type (was free text); null when not recurrent
    is_ev_disbursement     BOOLEAN      NOT NULL DEFAULT FALSE, -- FIXED typo: dibursement -> disbursement
    ev_disbursment_time    TIME,
    is_pos_disbursement    BOOLEAN      NOT NULL DEFAULT FALSE, -- ERD-adopted; FIXED typo
    definition             JSONB        NOT NULL,             -- the IR (report_setup.definition)
    run_start_date         TIMESTAMPTZ,
    run_end_date           TIMESTAMPTZ,
    status                 TEXT         NOT NULL DEFAULT 'ON', -- schedule on/off (stop/on)
    sms_content            TEXT,
    setup_approval_status  TEXT         NOT NULL DEFAULT 'DRAFT',
    approval_flow_id       UUID         NOT NULL,             -- ERD-adopted: the flow this report uses
    is_active              BOOLEAN      NOT NULL DEFAULT TRUE,
    created_on             TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_on             TIMESTAMPTZ,
    created_by             TEXT         NOT NULL,
    updated_by             TEXT,
    CONSTRAINT uq_report_setup_name UNIQUE (report_name),     -- BR3 system-wide unique
    CONSTRAINT fk_report_setup_channel
        FOREIGN KEY (channel_type_id)   REFERENCES channel (id)        ON DELETE RESTRICT,
    CONSTRAINT fk_report_setup_recurrent
        FOREIGN KEY (recurrent_type_id) REFERENCES recurrent_type (id) ON DELETE RESTRICT,
    -- fk_report_setup_flow (-> approval_flow) is added in §3.9.1 after approval_flow exists (forward ref)
    CONSTRAINT ck_report_setup_dates  CHECK (start_date <= end_date),                       -- BR4
    CONSTRAINT ck_report_setup_status CHECK (status IN ('ON','STOP')),
    CONSTRAINT ck_report_setup_appr   CHECK (setup_approval_status IN
        ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED','CHANGES_REQUESTED')),
    -- EV/POS mutual exclusion guard (BR: only one payout channel active at a time)
    CONSTRAINT ck_report_setup_payout_xor CHECK (NOT (is_ev_disbursement AND is_pos_disbursement)),
    -- if recurrent, a frequency must be chosen
    CONSTRAINT ck_report_setup_recurrent CHECK (is_recurrent = FALSE OR recurrent_type_id IS NOT NULL)
);

CREATE INDEX ix_report_setup_channel    ON report_setup (channel_type_id);
CREATE INDEX ix_report_setup_flow       ON report_setup (approval_flow_id);
CREATE INDEX ix_report_setup_appr       ON report_setup (setup_approval_status);
CREATE INDEX ix_report_setup_active     ON report_setup (is_active);
CREATE INDEX ix_report_setup_definition ON report_setup USING GIN (definition);  -- IR queries/validation
```

#### `report_supporting_upload`

Purpose: registers each CSV the Maker uploaded in Step 2 — the raw object in SeaweedFS plus the DB table built from its rows.
Relationships: child of `report_setup` (cascade delete).

```sql
CREATE TABLE report_supporting_upload (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    report_setup_id UUID         NOT NULL,
    db_table_name   TEXT         NOT NULL,    -- generated table created from the CSV
    db_schema       TEXT         NOT NULL,
    object_bucket   TEXT         NOT NULL,    -- SeaweedFS bucket for the raw file
    object_key      TEXT         NOT NULL,
    file_name       TEXT         NOT NULL,    -- original filename
    row_count       INTEGER,
    uploaded_at     TIMESTAMPTZ  NOT NULL DEFAULT now(),
    uploaded_by     TEXT         NOT NULL,
    CONSTRAINT fk_supporting_upload_report
        FOREIGN KEY (report_setup_id) REFERENCES report_setup (id) ON DELETE CASCADE,
    CONSTRAINT uq_supporting_upload_table UNIQUE (db_schema, db_table_name)  -- generated table name is unique
);

CREATE INDEX ix_supporting_upload_report ON report_supporting_upload (report_setup_id);
```

#### `stages`

Purpose: the compiled IR→SQL, one row per pipeline stage, written by the Python SQL Generator at Final Save.
Relationships: child of `report_setup` (cascade delete). Snapshotted into `run_stage` at run time.

```sql
CREATE TABLE stages (
    id              UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    report_setup_id UUID    NOT NULL,
    stage_order     INTEGER NOT NULL,
    sql_text        TEXT,                     -- nullable until SQL Generator fills it
    output_table_name TEXT,                   -- the temp/output table this stage materialises
    created_on      TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_on      TIMESTAMPTZ,
    CONSTRAINT fk_stages_report
        FOREIGN KEY (report_setup_id) REFERENCES report_setup (id) ON DELETE CASCADE,
    CONSTRAINT uq_stages_order UNIQUE (report_setup_id, stage_order),   -- idempotency: one SQL per stage slot
    CONSTRAINT ck_stages_order CHECK (stage_order >= 1)
);

CREATE INDEX ix_stages_report ON stages (report_setup_id, stage_order);
```

---

### 3.5 Run & Output

#### `report_run`

Purpose: one execution of a report (Demo or Final), with run status and disburse status.
Relationships: child of `report_setup`. Parent of `run_stage`, `final_commission`, `ev_disburse`, `pos_disbursement`.

```sql
CREATE TABLE report_run (
    id              UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    report_setup_id UUID         NOT NULL,
    run_date        TIMESTAMPTZ  NOT NULL DEFAULT now(),
    run_type        TEXT         NOT NULL,                 -- was "type(Demo/Final)"
    triggered_by    TEXT         NOT NULL,                 -- was "triggered_by(UN/System)"; who/what started it
    triggered_by_user TEXT,                                -- snapshot of the Maker's user_name when UN-triggered
    run_status      TEXT         NOT NULL DEFAULT 'PENDING',
    disburse_status TEXT         NOT NULL DEFAULT 'NONE',
    error_message   TEXT,                                  -- failure cause (run-failure recovery)
    started_at      TIMESTAMPTZ,
    ended_at        TIMESTAMPTZ,
    CONSTRAINT fk_report_run_setup
        FOREIGN KEY (report_setup_id) REFERENCES report_setup (id) ON DELETE CASCADE,
    CONSTRAINT ck_report_run_type     CHECK (run_type IN ('DEMO','FINAL')),
    CONSTRAINT ck_report_run_trigger  CHECK (triggered_by IN ('USER','SYSTEM')),
    CONSTRAINT ck_report_run_status   CHECK (run_status IN
        ('PENDING','QUEUED','RUNNING','COMPLETED','FAILED','CANCELLED')),
    CONSTRAINT ck_report_run_disburse CHECK (disburse_status IN
        ('NONE','PENDING','IN_PROGRESS','COMPLETED','FAILED'))
);

CREATE INDEX ix_report_run_setup   ON report_run (report_setup_id, run_date DESC);
CREATE INDEX ix_report_run_status  ON report_run (run_status);          -- queue / stale-run sweep
CREATE INDEX ix_report_run_dash    ON report_run (run_type, run_status, run_date DESC); -- dashboard rollups
```

#### `run_stage`

Purpose: the frozen per-stage SQL snapshot for a run (copied from `stages` at run start) plus per-stage execution status and output-file metadata.
Relationships: child of `report_run` (cascade delete).

```sql
CREATE TABLE run_stage (
    id                UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    run_id            UUID         NOT NULL,
    sql_text          TEXT         NOT NULL,                 -- frozen SQL (snapshot of stages.sql_text)
    stage_order       INTEGER      NOT NULL,                 -- FIXED reserved word: "order" -> stage_order
    run_status        TEXT         NOT NULL DEFAULT 'NOT_RUN',
    started_at        TIMESTAMPTZ,
    ended_at          TIMESTAMPTZ,
    row_count         INTEGER,                               -- per-stage row count (demo-run guardrail G3)
    document_type     TEXT,                                  -- output file kind (CSV/XLSX)
    bucket            TEXT,
    object_url        TEXT,
    file_name         TEXT,
    file_generated_at TIMESTAMPTZ,                           -- FIXED type: TEXT -> TIMESTAMPTZ
    output_table_name TEXT,                                  -- materialised temp table for this stage
    cleanup_status    TEXT         NOT NULL DEFAULT 'NONE',  -- temp-table drop lifecycle
    CONSTRAINT fk_run_stage_run
        FOREIGN KEY (run_id) REFERENCES report_run (id) ON DELETE CASCADE,
    CONSTRAINT uq_run_stage_order UNIQUE (run_id, stage_order),  -- idempotency: one snapshot per stage slot
    CONSTRAINT ck_run_stage_status CHECK (run_status IN
        ('NOT_RUN','RUNNING','SUCCEEDED','FAILED','SKIPPED')),
    CONSTRAINT ck_run_stage_cleanup CHECK (cleanup_status IN
        ('NONE','PENDING','DONE','FAILED')),
    CONSTRAINT ck_run_stage_doctype CHECK (document_type IS NULL OR document_type IN ('CSV','XLSX'))
);

CREATE INDEX ix_run_stage_run ON run_stage (run_id, stage_order);
```

#### `final_commission`

Purpose: the per-channel result of a run — written only by the trusted system path (never by generated SQL). The basis for disbursement and dashboard totals.
Relationships: child of `report_run`; FK → `channel`.

```sql
CREATE TABLE final_commission (
    id                UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    report_run_id     UUID          NOT NULL,
    channel_id        UUID          NOT NULL,
    channel_code      TEXT          NOT NULL,
    commission_amount NUMERIC(18,4) NOT NULL,    -- internal precision; rounded to 2dp at disbursement write
    created_on        TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT fk_final_commission_run
        FOREIGN KEY (report_run_id) REFERENCES report_run (id) ON DELETE CASCADE,
    CONSTRAINT fk_final_commission_channel
        FOREIGN KEY (channel_id)    REFERENCES channel (id)    ON DELETE RESTRICT,
    -- idempotency / no double-pay: one row per (run, channel)
    CONSTRAINT uq_final_commission_run_channel UNIQUE (report_run_id, channel_id),
    CONSTRAINT ck_final_commission_amount CHECK (commission_amount >= 0)
);

CREATE INDEX ix_final_commission_run     ON final_commission (report_run_id);
CREATE INDEX ix_final_commission_channel ON final_commission (channel_id);
```

---

### 3.6 Approval

#### `approval_type`

Purpose: a category of approval; each type carries a `phase` deciding whether it gates the setup (PRE_RUN) or the results (POST_RUN).
Relationships: referenced by `approval_flow_level.approval_type_id` and `approval_request.approval_type_id`.

```sql
CREATE TABLE approval_type (
    id          UUID    PRIMARY KEY DEFAULT gen_random_uuid(),
    type_name   TEXT    NOT NULL,
    description TEXT,                                  -- kept (LLD had it; ERD dropped it)
    phase       TEXT    NOT NULL,                      -- ERD-adopted
    sort_order  INTEGER NOT NULL,
    CONSTRAINT uq_approval_type_name UNIQUE (type_name),
    CONSTRAINT ck_approval_type_phase CHECK (phase IN ('PRE_RUN','POST_RUN'))
);
```

#### `approval_flow`

Purpose: a reusable, named, ordered approval chain.
Relationships: parent of `approval_flow_level`; referenced by `report_setup.approval_flow_id` and `approval_request.approval_flow_id`.

```sql
CREATE TABLE approval_flow (
    id          UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    flow_name   TEXT         NOT NULL,
    description TEXT,
    is_active   BOOLEAN      NOT NULL DEFAULT TRUE,
    created_on  TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_on  TIMESTAMPTZ,
    created_by  TEXT         NOT NULL,
    updated_by  TEXT,
    CONSTRAINT uq_approval_flow_name UNIQUE (flow_name)
);
```

> Note: the LLD `approval_flow` carried `approval_type_id`, but per the ERD the type belongs on each **level** (`approval_flow_level.approval_type_id`), so a flow can mix PRE_RUN and POST_RUN levels. The flow-level FK was therefore removed and lives on the level.

#### `approval_flow_level`

Purpose: an ordered step inside a flow, each bound to an approval type (which sets its phase).
Relationships: child of `approval_flow`; FK → `approval_type` (ERD-adopted); parent of `approval_flow_level_user`.

```sql
CREATE TABLE approval_flow_level (
    id               UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_flow_id UUID         NOT NULL,
    approval_type_id UUID         NOT NULL,            -- ERD-adopted: per-level type/phase
    level_order      INTEGER      NOT NULL,
    level_name       TEXT         NOT NULL,
    created_on       TIMESTAMPTZ  NOT NULL DEFAULT now(),
    updated_on       TIMESTAMPTZ,
    created_by       TEXT         NOT NULL,
    updated_by       TEXT,
    CONSTRAINT fk_aflevel_flow
        FOREIGN KEY (approval_flow_id) REFERENCES approval_flow (id) ON DELETE CASCADE,
    CONSTRAINT fk_aflevel_type
        FOREIGN KEY (approval_type_id) REFERENCES approval_type (id) ON DELETE RESTRICT,
    CONSTRAINT uq_aflevel_order UNIQUE (approval_flow_id, level_order),  -- order unique within flow
    CONSTRAINT ck_aflevel_order CHECK (level_order >= 1)
);

CREATE INDEX ix_aflevel_flow ON approval_flow_level (approval_flow_id, level_order);
CREATE INDEX ix_aflevel_type ON approval_flow_level (approval_type_id);
```

#### `approval_flow_level_user`

Purpose: which users may act at a given level.
Relationships: child of `approval_flow_level`; FK → `user` (FIXED to real UUID FK).

```sql
CREATE TABLE approval_flow_level_user (
    id                    UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_flow_level_id UUID NOT NULL,
    user_id               UUID NOT NULL,               -- FIXED type: TEXT -> UUID, real FK
    CONSTRAINT fk_aflevel_user_level
        FOREIGN KEY (approval_flow_level_id) REFERENCES approval_flow_level (id) ON DELETE CASCADE,
    CONSTRAINT fk_aflevel_user_user
        FOREIGN KEY (user_id) REFERENCES "user" (id) ON DELETE CASCADE,
    CONSTRAINT uq_aflevel_user UNIQUE (approval_flow_level_id, user_id)  -- no duplicate assignment
);

CREATE INDEX ix_aflevel_user_level ON approval_flow_level_user (approval_flow_level_id);
CREATE INDEX ix_aflevel_user_user  ON approval_flow_level_user (user_id);  -- "my queue" lookup
```

#### `approval_request`

Purpose: the single approval request opened per report; it walks all levels in order and tracks the current open level.
Relationships: child of `report_setup`; FK → `approval_type` (current phase), FK → `approval_flow`; parent of `approval_decision`.

```sql
CREATE TABLE approval_request (
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    report_setup_id     UUID         NOT NULL,
    approval_type_id    UUID         NOT NULL,         -- the current level's type/phase
    approval_flow_id    UUID         NOT NULL,
    report_run_id       UUID,                          -- set for POST_RUN result approval; null for PRE_RUN setup
    current_level_order INTEGER      NOT NULL,
    overall_status      TEXT         NOT NULL DEFAULT 'IN_PROGRESS',
    initiated_by        TEXT         NOT NULL,          -- user_name snapshot
    initiated_at        TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT fk_approval_request_report
        FOREIGN KEY (report_setup_id) REFERENCES report_setup (id) ON DELETE CASCADE,
    CONSTRAINT fk_approval_request_type
        FOREIGN KEY (approval_type_id) REFERENCES approval_type (id) ON DELETE RESTRICT,
    CONSTRAINT fk_approval_request_flow
        FOREIGN KEY (approval_flow_id) REFERENCES approval_flow (id) ON DELETE RESTRICT,
    CONSTRAINT fk_approval_request_run
        FOREIGN KEY (report_run_id) REFERENCES report_run (id) ON DELETE SET NULL,
    CONSTRAINT ck_approval_request_status CHECK (overall_status IN
        ('IN_PROGRESS','APPROVED','REJECTED','CHANGES_REQUESTED','CANCELLED'))
);

CREATE INDEX ix_approval_request_report ON approval_request (report_setup_id);
CREATE INDEX ix_approval_request_flow   ON approval_request (approval_flow_id);
CREATE INDEX ix_approval_request_open   ON approval_request (overall_status, current_level_order);  -- queue
```

> Note: `report_run_id` was added so a POST_RUN result approval can point at the exact Final run being approved. BR8 (disburse only after full approval) is enforced by the application: disbursement checks `overall_status = 'APPROVED'` on the matching request.

#### `approval_decision`

Purpose: the immutable per-level decision trail (who decided what, when, with what comment). Comment required on reject (BR7).
Relationships: child of `approval_request`. `decided_by` is a `user_name` snapshot (no FK — same rationale as audit).

```sql
CREATE TABLE approval_decision (
    id                  UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    approval_request_id UUID         NOT NULL,
    level_order         INTEGER      NOT NULL,
    decided_by          TEXT         NOT NULL,          -- user_name snapshot (was "FK"; kept as snapshot)
    decision            TEXT         NOT NULL,
    comment             TEXT,
    decided_at          TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT fk_approval_decision_request
        FOREIGN KEY (approval_request_id) REFERENCES approval_request (id) ON DELETE CASCADE,
    CONSTRAINT ck_approval_decision_value CHECK (decision IN ('APPROVED','REJECTED','CHANGES_REQUESTED')),
    -- BR7: a reject / changes-requested must carry a comment
    CONSTRAINT ck_approval_decision_comment CHECK (
        decision = 'APPROVED' OR (comment IS NOT NULL AND length(btrim(comment)) > 0))
);

CREATE INDEX ix_approval_decision_request ON approval_decision (approval_request_id, level_order);
```

---

### 3.7 Disbursement

#### `ev_disburse`

Purpose: one EV payout record per channel for a Final run, plus the provider transaction id and status lifecycle (auto + SMS).
Relationships: child of `report_run`; FK → `channel` (FIXED to UUID FK).

```sql
CREATE TABLE ev_disburse (
    id               UUID          PRIMARY KEY DEFAULT gen_random_uuid(),
    report_run_id    UUID          NOT NULL,
    channel_id       UUID          NOT NULL,             -- FIXED type: TEXT -> UUID, real FK
    channel_code     TEXT          NOT NULL,
    amount           NUMERIC(18,2) NOT NULL,             -- legacy column; populated = amount_disbursed
    amount_disbursed NUMERIC(18,2) NOT NULL,             -- added: amount actually sent to EV
    provider_txn_id  TEXT,                               -- added: EV API transaction id (idempotency / reconcile)
    status           TEXT          NOT NULL DEFAULT 'PENDING',
    message          TEXT,
    sms_status       TEXT          NOT NULL DEFAULT 'PENDING',  -- the recipient SMS lifecycle
    disburse_at      TIMESTAMPTZ,
    created_on       TIMESTAMPTZ   NOT NULL DEFAULT now(),
    CONSTRAINT fk_ev_disburse_run
        FOREIGN KEY (report_run_id) REFERENCES report_run (id) ON DELETE CASCADE,
    CONSTRAINT fk_ev_disburse_channel
        FOREIGN KEY (channel_id)    REFERENCES channel (id)    ON DELETE RESTRICT,
    -- idempotency / no double-pay: one EV payout per (run, channel)
    CONSTRAINT uq_ev_disburse_run_channel UNIQUE (report_run_id, channel_id),
    CONSTRAINT ck_ev_disburse_amount CHECK (amount_disbursed >= 0),
    CONSTRAINT ck_ev_disburse_status CHECK (status IN
        ('PENDING','SENT','SUCCESS','FAILED','RETRYING')),
    CONSTRAINT ck_ev_disburse_sms CHECK (sms_status IN ('PENDING','SENT','FAILED','SKIPPED'))
);

CREATE INDEX ix_ev_disburse_run     ON ev_disburse (report_run_id);
CREATE INDEX ix_ev_disburse_channel ON ev_disburse (channel_id);
CREATE INDEX ix_ev_disburse_status  ON ev_disburse (status);     -- retry sweep
```

#### `pos_disbursement`

Purpose: the POS CSV handoff record for a Final run (one CSV per run).
Relationships: child of `report_run`.

```sql
CREATE TABLE pos_disbursement (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    report_run_id UUID         NOT NULL,
    object_bucket TEXT,                                  -- added: where the CSV lives
    object_url    TEXT         NOT NULL,
    file_name     TEXT,                                  -- added
    row_count     INTEGER,                               -- added: rows in the handoff
    dump_status   TEXT         NOT NULL DEFAULT 'PENDING',
    disburse_at   TIMESTAMPTZ,
    created_on    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT fk_pos_disbursement_run
        FOREIGN KEY (report_run_id) REFERENCES report_run (id) ON DELETE CASCADE,
    -- idempotency / no double-handoff: one POS dump per run
    CONSTRAINT uq_pos_disbursement_run UNIQUE (report_run_id),
    CONSTRAINT ck_pos_disbursement_status CHECK (dump_status IN
        ('PENDING','GENERATED','HANDED_OFF','FAILED'))
);

CREATE INDEX ix_pos_disbursement_run ON pos_disbursement (report_run_id);
```

---

### 3.8 Cross-cutting

#### `audit_log`

Purpose: immutable record of every add/change/delete and every approval/schedule/payout action (BR9).
Relationships: `user_name` is an immutable snapshot (no FK — see §3.2 note).

```sql
CREATE TABLE audit_log (
    id             UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    user_name      TEXT         NOT NULL,                -- snapshot, NOT a FK (immutable; survives user removal)
    action         TEXT         NOT NULL,
    entity_type    TEXT         NOT NULL,
    entity_id      TEXT         NOT NULL,
    diff           JSONB,
    ip             TEXT,
    user_agent     TEXT,
    created_on_utc TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ck_audit_action CHECK (action IN ('CREATE','UPDATE','DELETE','APPROVE','REJECT',
        'SCHEDULE','RUN','DISBURSE','LOGIN','OTHER'))
);

CREATE INDEX ix_audit_entity ON audit_log (entity_type, entity_id, created_on_utc DESC);
CREATE INDEX ix_audit_user   ON audit_log (user_name, created_on_utc DESC);
CREATE INDEX ix_audit_time   ON audit_log (created_on_utc DESC);
```

#### `notification_log`

Purpose: every SMS and email the system tries to send, with delivery status and retry count.
Relationships: standalone log.

```sql
CREATE TABLE notification_log (
    id            UUID         PRIMARY KEY DEFAULT gen_random_uuid(),
    template_code TEXT         NOT NULL,
    channel       TEXT         NOT NULL,                 -- EMAIL / SMS
    number        TEXT,                                  -- relaxed: only set for SMS
    to_address    TEXT,                                  -- relaxed: only set for EMAIL
    cc            TEXT,
    bcc           TEXT,
    subject       TEXT,                                  -- email only
    body          TEXT         NOT NULL,
    from_address  TEXT,
    status        TEXT         NOT NULL DEFAULT 'PENDING',
    attempt_count INTEGER      NOT NULL DEFAULT 0,
    error_message TEXT,
    scheduled_at  TIMESTAMPTZ,
    sent_at       TIMESTAMPTZ,
    created_on    TIMESTAMPTZ  NOT NULL DEFAULT now(),
    CONSTRAINT ck_notification_channel CHECK (channel IN ('EMAIL','SMS')),
    CONSTRAINT ck_notification_status  CHECK (status IN ('PENDING','SENT','FAILED')),
    -- a body is always required; an email must carry a subject and to_address; an SMS a number
    CONSTRAINT ck_notification_target CHECK (
        (channel = 'SMS'   AND number IS NOT NULL) OR
        (channel = 'EMAIL' AND to_address IS NOT NULL AND subject IS NOT NULL))
);

CREATE INDEX ix_notification_status  ON notification_log (status, scheduled_at);  -- send/retry sweep
CREATE INDEX ix_notification_channel ON notification_log (channel, created_on);   -- filter by channel
CREATE INDEX ix_notification_created ON notification_log (created_on DESC);
```

---

### 3.9.0 Deferred constraint (load order)

The tables above create cleanly **in document order** with one exception: `report_setup` references `approval_flow`, which is defined later. Run all 22 `CREATE TABLE`s first, then add the one forward-reference FK:

```sql
ALTER TABLE report_setup
    ADD CONSTRAINT fk_report_setup_flow
    FOREIGN KEY (approval_flow_id) REFERENCES approval_flow (id) ON DELETE RESTRICT;
```

> All other FKs reference an already-created table, so the migration order is simply: §3.0 prerequisites → §3.2 → §3.3 → §3.4 → §3.5 → §3.6 → §3.7 → §3.8 → this deferred FK. (EF Core migrations handle this automatically; the note matters for a hand-run `.sql` file.)

### 3.9 Key relationships

**The run/payout spine (one chain, top to bottom):**
```
report_setup ──< stages                         (compiled SQL per stage; UNIQUE per stage_order)
report_setup ──< report_run                     (each execution, Demo or Final)
report_run   ──< run_stage                      (frozen SQL snapshot per stage; UNIQUE per stage_order)
report_run   ──< final_commission               (per-channel result; UNIQUE per (run, channel))
report_run   ──< ev_disburse                    (per-channel EV payout; UNIQUE per (run, channel))
report_run   ──1 pos_disbursement               (one CSV handoff; UNIQUE per run)
channel      ──< final_commission / ev_disburse (channel identity, UUID FK)
```

**The approval chain (definition → request → decision):**
```
approval_flow        ──< approval_flow_level           (ordered steps; UNIQUE per level_order)
approval_flow_level  ──1 approval_type                 (each level's type sets its PRE_RUN/POST_RUN phase)
approval_flow_level  ──< approval_flow_level_user ──1 "user"   (who may act; UUID FK)
report_setup         ──1 approval_flow                 (the report's bound flow)
report_setup         ──< approval_request              (the single walking request; may point at a report_run for POST_RUN)
approval_request     ──< approval_decision             (immutable per-level trail; comment required on reject)
```

**Identity:** `"user" ──< user_right` (RBAC bits); `login` and `audit_log` reference users by `user_name` **snapshot** (no FK, immutable history).

**Disbursement gate:** EV/POS run only when the matching `approval_request.overall_status = 'APPROVED'` (BR8) and exactly one of `report_setup.is_ev_disbursement` / `is_pos_disbursement` is true (enforced by `ck_report_setup_payout_xor`).

---

### 3.10 Column reference (exact names for other authors)

The list below is the authoritative `TABLE: columns` reference. Use these exact names in API DTOs, IR→SQL output, and EF Core/Dapper mappings.

```
user:                     id, user_name, user_id, full_name, mobile_no, email, department, is_active,
                          created_on, updated_on, created_by, updated_by
user_right:               id, user_id, user_right
login:                    id, user_name, full_name, login_time, status, remarks
data_source:              id, source_table_name, table_description, is_active,
                          created_on, updated_on, created_by, updated_by
channel:                  id, channel_name, channel_type, is_active
recurrent_type:           id, code, label, is_active
report_setup:             id, report_name, report_type, channel_type_id, commission_cycle, start_date, end_date,
                          is_recurrent, recurrent_type_id, is_ev_disbursement, ev_disbursment_time,
                          is_pos_disbursement, definition, run_start_date, run_end_date, status, sms_content,
                          setup_approval_status, approval_flow_id, is_active,
                          created_on, updated_on, created_by, updated_by
report_supporting_upload: id, report_setup_id, db_table_name, db_schema, object_bucket, object_key,
                          file_name, row_count, uploaded_at, uploaded_by
stages:                   id, report_setup_id, stage_order, sql_text, output_table_name, created_on, updated_on
report_run:               id, report_setup_id, run_date, run_type, triggered_by, triggered_by_user,
                          run_status, disburse_status, error_message, started_at, ended_at
run_stage:                id, run_id, sql_text, stage_order, run_status, started_at, ended_at, row_count,
                          document_type, bucket, object_url, file_name, file_generated_at,
                          output_table_name, cleanup_status
final_commission:         id, report_run_id, channel_id, channel_code, commission_amount, created_on
approval_type:            id, type_name, description, phase, sort_order
approval_flow:            id, flow_name, description, is_active, created_on, updated_on, created_by, updated_by
approval_flow_level:      id, approval_flow_id, approval_type_id, level_order, level_name,
                          created_on, updated_on, created_by, updated_by
approval_flow_level_user: id, approval_flow_level_id, user_id
approval_request:         id, report_setup_id, approval_type_id, approval_flow_id, report_run_id,
                          current_level_order, overall_status, initiated_by, initiated_at
approval_decision:        id, approval_request_id, level_order, decided_by, decision, comment, decided_at
ev_disburse:              id, report_run_id, channel_id, channel_code, amount, amount_disbursed,
                          provider_txn_id, status, message, sms_status, disburse_at, created_on
pos_disbursement:         id, report_run_id, object_bucket, object_url, file_name, row_count,
                          dump_status, disburse_at, created_on
audit_log:                id, user_name, action, entity_type, entity_id, diff, ip, user_agent, created_on_utc
notification_log:         id, template_code, channel, number, to_address, cc, bcc, subject, body,
                          from_address, status, attempt_count, error_message, scheduled_at, sent_at, created_on
```

### 3.11 Fix log (bugs resolved vs. the LLD/ERD)

| # | Issue | Fix |
|---|---|---|
| a | `ev_disburse.channel_id` TEXT, FK→UUID mismatch | → `UUID` with real FK to `channel.id` |
| a | `approval_flow_level_user.user_id` TEXT, FK→UUID mismatch | → `UUID` with real FK to `"user".id` |
| a | `login.login_time` TEXT | → `TIMESTAMPTZ` |
| a | `run_stage.file_generated_at` TEXT | → `TIMESTAMPTZ` |
| a | `audit_log.user_name` FK to natural key | → snapshot text, **no FK** (immutable history); same for `login.user_name`, `approval_*.decided_by/initiated_by` |
| b | double-pay risk (no idempotency) | UNIQUE: `final_commission(report_run_id,channel_id)`, `ev_disburse(report_run_id,channel_id)`, `pos_disbursement(report_run_id)`, `run_stage(run_id,stage_order)`, `stages(report_setup_id,stage_order)` |
| c | ERD-richer columns the LLD omitted | added `report_setup.is_pos_disbursement` + `approval_flow_id`; `approval_type.phase`; `approval_flow_level.approval_type_id`; `user.user_id` + `mobile_no` + `department` |
| d | typos becoming permanent | `dibursement→disbursement`, `depertment→department`, `INTERGER→INTEGER`, `faild→FAILED`, `Monthyly→MONTHLY` |
| d | `run_stage."order"` reserved word | renamed `stage_order` |
| e | EV record too thin | added `amount_disbursed`, `provider_txn_id`, `sms_status`, status lifecycle CHECK |
| e | money precision | internal money = `NUMERIC(18,4)` (`final_commission`); disbursed = `NUMERIC(18,2)` |
| f | recurrence as free text | `report_setup.recurrent_type_id` FK → `recurrent_type` lookup; `Monthyly→MONTHLY` |
| g | unconstrained status enums | `CHECK` on every status/phase/decision/channel column (login, run, run_stage, approval, disburse, notification) |
| — | EV/POS both-on possible | `ck_report_setup_payout_xor` (mutual exclusion, BR) |
| — | `user_right` had no PK | added surrogate `id` PK + `UNIQUE(user_id, user_right)` |
```
