## 5. Data Source Management

This section covers two related-but-distinct things the rest of the system reads from:

1. **Registered data sources** (`data_source` table) — the curated list of *permanent* source tables (ETL-loaded, refreshed by Airflow) that a Business User may pick from in the wizard. Managed by Administrators only.
2. **Supporting-CSV → DB-table ingestion** — the *per-report*, ad-hoc tables a Maker creates by uploading a CSV in **Report Step-2 (Supporting Uploads)**. These are registered in `report_supporting_upload` and become joinable just like a data source (the IR refers to them as `source.type = "upload"`).

Both feed the IR (`report_setup.definition`): a block's `source`/`join_with` is either `{ "type": "data_source", "ref": "<source_table_name>" }` or `{ "type": "upload", "ref": "<logical upload name>" }`. The Python SQL Generator resolves each `ref` to a real, schema-qualified table at Final Save.

> **Naming used throughout:** registered source tables live in the **`source`** PostgreSQL schema (ETL target), upload tables live in the **`uploads`** schema, and per-run temp tables live in **`runtmp`** (see §7 Run Orchestration). The core SalesCom tables are in **`salescom`** (§3).

---

### 5.1 Registered Data Sources

#### 5.1.1 Overview

A **data source** is a registration row that exposes one physical source table (e.g. `source.ev_lifting_daily_com`, `source.simrepository`) to the wizard. The Administrator curates this list: only registered + active tables appear in the Step-3/4 "pick a source" dropdown. Registration also carries a human description and, optionally, **per-column aliases** so business-friendly labels can be shown in the wizard instead of raw DB column names.

Key invariants (BR2):

- A data source is **never hard-deleted** — only deactivated (`is_active = FALSE`).
- A data source **cannot be deactivated while any report still uses it** (referenced inside a `report_setup.definition` IR). The check is described in §5.1.4.

#### 5.1.2 UI

**List view** (`/data-sources`):

- Columns: SL (serial) · Source Table Name · Description · Status (Active/Inactive pill) · Edit action.
- 10 rows per page, `Showing N of M` counter, name search box (matches `source_table_name`).
- `Add a New Data Source` button (top-right) — Administrator only; hidden/disabled for Business Users.

**Add / Edit form** (drawer or modal):

- **Source table** (required, single-select dropdown). On Add, the dropdown lists *only physical tables in the `source` schema that are not already registered* (one registration per table — `uq_data_source_table`). On Edit, the table is fixed (read-only).
- When a table is selected, the form **fetches and lists its columns** (name + detected PG type) below the dropdown. The Administrator may optionally set a **display alias** and short note per column (stored as `column_meta` JSON inside `table_description`, see §5.1.3) — this is the "column alias support".
- **Description** (free text).
- **Status** toggle — **off (inactive) by default** on Add.
- Save / Cancel.

Business Users see the list **read-only** (no Add/Edit/Status controls) — they consume it inside the wizard.

#### 5.1.3 Data Model

Backed by `data_source` (§3.3). Exact columns:

```
data_source: id, source_table_name, table_description, is_active,
             created_on, updated_on, created_by, updated_by
```

| Column | Use |
|---|---|
| `id` | UUID PK. |
| `source_table_name` | The bare physical table name in the `source` schema (e.g. `ev_lifting_daily_com`). This is also the value used as the IR `ref` for `{ "type": "data_source" }`. `uq_data_source_table` keeps it unique. |
| `table_description` | Human description **plus** optional column-alias metadata. The first line(s) are the plain description; column aliases are stored as a JSON block under a delimiter so no schema change is needed. Recommended shape: `table_description` holds a small JSON document `{ "description": "...", "columns": [ { "name": "RSO_CODE", "alias": "RSO Code", "note": "..." } ] }`. The API serialises/deserialises this; the UI shows `description` and aliases. (If a later migration wants a dedicated `column_meta JSONB`, this is forward-compatible.) |
| `is_active` | Soft on/off. Default **FALSE** (per the "off by default" wizard rule). Inactive sources are hidden from the wizard but stay registered. |
| `created_on/by`, `updated_on/by` | Audit columns; `created_by`/`updated_by` = acting Admin `user_name`. |

> **Column metadata is NOT physically validated against the live table at registration** beyond the column-list fetch — the alias list is descriptive. The SQL Generator always uses real DB column names; aliases are UI-only.

#### 5.1.4 Process Logic

**Register a new data source (Admin):**

1. Admin opens Add form → selects a physical table from the `source` schema.
2. API introspects the table (via `information_schema.columns`, see §5.2.6 for the same mechanism) and returns its columns + detected types.
3. Admin fills description, optionally sets per-column aliases, leaves status off (default).
4. On Save: API validates `source_table_name` exists in the `source` schema and is not already registered (`uq_data_source_table`), then inserts a `data_source` row with `is_active = FALSE` and `created_by = <admin user_name>`.
5. Writes an `audit_log` row (`action = 'CREATE'`, `entity_type = 'data_source'`).

**Activate / deactivate (Admin):**

1. Admin toggles status on the list or via `PATCH /status`.
2. **Activate:** set `is_active = TRUE` (no usage check needed).
3. **Deactivate (guarded by BR2):** before flipping to `FALSE`, run the **in-use check** — the source is "in use" if its `source_table_name` appears as a `data_source` `ref` inside any **active, non-draft** report's IR:

   ```sql
   SELECT 1
   FROM report_setup rs,
        LATERAL jsonb_path_query(
          rs.definition,
          '$.**.source ? (@.type == "data_source").ref'
        ) AS ref(val)
   WHERE rs.is_active = TRUE
     AND rs.setup_approval_status <> 'DRAFT'
     AND ref.val #>> '{}' = :source_table_name
   LIMIT 1;
   ```

   (Also check `$.**.join_with ? (@.type == "data_source").ref` for sources brought in via `combine`.) If any row is returned → reject with `409 CONFLICT`. Otherwise set `is_active = FALSE`.
4. Audit the change (`action = 'UPDATE'`).

**Never delete:** there is no `DELETE` endpoint. Removal = deactivate only.

#### 5.1.5 API Endpoints

All under `/api/v1`. JWT required on every request. Roles: **A** = Administrator, **BU** = Business User (Maker), **AP** = Approver (Checker). "Read" endpoints are allowed for all authenticated roles; mutating endpoints are Administrator-only.

| Method | Path | Purpose | Role | Success | Errors |
|---|---|---|---|---|---|
| GET | `/data-sources` | List with search + pagination | A, BU, AP | 200 | 401 |
| GET | `/data-sources/{id}` | Read one (incl. column aliases) | A, BU, AP | 200 | 401, 404 |
| GET | `/data-sources/available-tables` | List physical `source.*` tables not yet registered (for the Add dropdown) | A | 200 | 401, 403 |
| GET | `/data-sources/table-columns?table={name}` | Introspect a physical table's columns + detected types | A | 200 | 401, 403, 404 |
| POST | `/data-sources` | Register a new data source | A | 201 | 400, 401, 403, 409 |
| PUT | `/data-sources/{id}` | Update description / aliases | A | 200 | 400, 401, 403, 404 |
| PATCH | `/data-sources/{id}/status` | Activate / deactivate (BR2 guard) | A | 200 | 401, 403, 404, 409 |

**DTOs**

`GET /data-sources` — query: `?search=&page=1&pageSize=10&status=all|active|inactive`. Response:

```jsonc
{
  "items": [
    {
      "id": "8e2c…",
      "sourceTableName": "ev_lifting_daily_com",
      "description": "Daily EV lifting (recharge) fact table",
      "isActive": true,
      "updatedOn": "2026-06-10T08:15:00Z",
      "updatedBy": "admin.rahman"
    }
  ],
  "page": 1, "pageSize": 10, "total": 23
}
```

`GET /data-sources/available-tables` — Response:

```jsonc
{ "tables": ["ev_lifting_daily_com", "simrepository", "ga_daily", "b2c_target_apr26"] }
```

`GET /data-sources/table-columns?table=ev_lifting_daily_com` — Response:

```jsonc
{
  "table": "ev_lifting_daily_com",
  "columns": [
    { "name": "rso_code",      "pgType": "text",          "nullable": false },
    { "name": "send_amount",   "pgType": "numeric(18,4)",  "nullable": true  },
    { "name": "fcd",           "pgType": "date",           "nullable": true  }
  ]
}
```

`POST /data-sources` — Request:

```jsonc
{
  "sourceTableName": "ev_lifting_daily_com",
  "description": "Daily EV lifting (recharge) fact table",
  "columns": [
    { "name": "rso_code",    "alias": "RSO Code",   "note": "" },
    { "name": "send_amount", "alias": "Recharge Amount", "note": "BDT" }
  ],
  "isActive": false
}
```

Response `201`:

```jsonc
{ "id": "8e2c…", "sourceTableName": "ev_lifting_daily_com", "isActive": false }
```

`PUT /data-sources/{id}` — Request: same shape as POST minus `sourceTableName` (immutable) and `isActive` (use PATCH). Response `200`: the full updated DTO.

`PATCH /data-sources/{id}/status` — Request:

```jsonc
{ "isActive": false }
```

Response `200`:

```jsonc
{ "id": "8e2c…", "isActive": false }
```

On a BR2 violation (deactivating an in-use source), response `409`:

```jsonc
{
  "error": {
    "code": "DATA_SOURCE_IN_USE",
    "message": "Cannot deactivate: this source is used by 3 active report(s).",
    "details": { "reportCount": 3, "reportNames": ["RSO GA+Recharge Apr26", "…"] }
  }
}
```

> Error envelope = the project-standard `{ "error": { "code", "message", "details" } }` (defined in the API-DTO section). All 4xx use it.

#### 5.1.6 Validation & Business Rules

| Rule | Enforcement |
|---|---|
| BR2 — data source never deleted | No DELETE endpoint exists. |
| BR2 — cannot deactivate while in use | §5.1.4 IR-scan check → `409 DATA_SOURCE_IN_USE`. |
| Admin-only mutation (BR1) | POST/PUT/PATCH require Administrator role (JWT claim); others get `403`. |
| One registration per physical table | DB `uq_data_source_table` + pre-insert existence check → `409` on duplicate. |
| `source_table_name` must exist physically | Validate against `information_schema.tables` in the `source` schema before insert → `400 SOURCE_TABLE_NOT_FOUND` if missing. |
| Off by default | New rows insert `is_active = FALSE` regardless of payload (or honor `isActive=false` default). |
| Every mutation audited (BR9) | `audit_log` row on CREATE/UPDATE/status change. |

---

### 5.2 Supporting CSV → DB Table Ingestion (Report Step-2)

#### 5.2.1 Overview

In **Report Step-2 (Supporting Uploads)** a Maker uploads campaign-config CSVs (DD-wise targets, KPI weight tables, slab tables, A/B/C/D category lists, exclusion lists, prior-commission outputs for priority de-dup — see the Commission Logic Catalog). Each uploaded CSV is:

1. stored as a **raw object** in SeaweedFS (so it can be re-downloaded / re-loaded),
2. **profiled** (header row → column names + detected types),
3. loaded into a **freshly created, per-report table** in the `uploads` schema via PostgreSQL `COPY`,
4. **registered** as one `report_supporting_upload` row.

From that point the table is just another joinable source: the IR refers to it as `{ "type": "upload", "ref": "<logical upload name>" }`, and the SQL Generator resolves the logical name → the physical `uploads.<table>` via the `report_supporting_upload` row.

#### 5.2.2 UI

Step-2 panel:

- Drag-drop / file-picker (accepts `.csv` only in Phase 1; `.xlsx` may be converted to CSV client- or server-side later).
- A **logical name** field per file (defaults to a slug of the filename, e.g. `temp_for_rso_agent`) — this is what the wizard shows in Step-3/4 join pickers and what the IR stores as `ref`.
- After upload: a row appears showing file name · logical name · detected row count · detected columns (name + type) · a **re-upload / remove** action.
- A type-mismatch or sanitisation warning is shown inline (e.g. "column `Send Amount` → `send_amount`", "duplicate header `code` renamed to `code_2`").

#### 5.2.3 Data Model

Backed by `report_supporting_upload` (§3.4). Exact columns:

```
report_supporting_upload: id, report_setup_id, db_table_name, db_schema, object_bucket, object_key,
                          file_name, row_count, uploaded_at, uploaded_by
```

| Column | Written with |
|---|---|
| `id` | UUID PK. |
| `report_setup_id` | The draft report this upload belongs to (FK, cascade-delete with the report). |
| `db_table_name` | The generated physical table name (see naming convention §5.2.4), e.g. `up_3f9a2c_target_rso_bundle`. |
| `db_schema` | Always `'uploads'` in Phase 1. |
| `object_bucket` | SeaweedFS bucket holding the raw CSV, e.g. `salescom-uploads`. |
| `object_key` | Object key of the raw CSV, e.g. `reports/{reportId}/uploads/{uuid}/target_rso_bundle.csv`. |
| `file_name` | Original filename as uploaded. |
| `row_count` | Data rows loaded (excludes header). |
| `uploaded_at` | `now()`. |
| `uploaded_by` | Maker `user_name`. |

`uq_supporting_upload_table (db_schema, db_table_name)` guarantees the generated table name is unique, so re-uploading regenerates a distinct name (old table is dropped — §5.2.7).

> **Logical name ↔ physical name:** the IR `ref` (e.g. `temp_for_rso_agent`) is the **logical** name shown in the UI; the **physical** table is `db_table_name`. The SQL Generator maps `ref → db_table_name` by matching, for the report being compiled, the `report_supporting_upload` row whose logical name equals the `ref`. To make that lookup unambiguous, the logical name is stored as the **suffix** of `db_table_name` after the report hash (see §5.2.4) and ALSO carried verbatim in `file_name`-independent UI state. If a dedicated `logical_name` column is desired later it is additive; in Phase 1 the suffix encodes it.

#### 5.2.4 Table & column naming convention

**Table name** (the `db_table_name`):

```
up_<reportShort>_<logicalSlug>
```

- `up_` — fixed prefix marking an upload table.
- `<reportShort>` — first 8 hex chars of the `report_setup.id` UUID (stable per report; ties all of a report's upload tables together and disambiguates the same filename across reports).
- `<logicalSlug>` — the sanitised logical name (the IR `ref`), lower_snake_case.

Example: report `3f9a2c4d-…`, logical name `target_rso_bundle` → `up_3f9a2c4d_target_rso_bundle`. Fully qualified: `uploads.up_3f9a2c4d_target_rso_bundle`.

Rules:
- Total identifier length is clamped to PostgreSQL's **63-char** limit; if the slug would overflow, it is truncated and a 4-char hash of the full slug is appended to keep uniqueness.
- The final name is validated against `^[a-z_][a-z0-9_]*$`.

**Column names** — the header row is sanitised (§5.2.5).

#### 5.2.5 Column-name sanitisation

Each header cell → a safe PG identifier:

1. **Trim** surrounding whitespace.
2. **Lowercase**.
3. Replace any run of non-`[a-z0-9]` (spaces, dashes, dots, slashes, BOM, etc.) with a single `_`.
4. Strip leading/trailing `_`.
5. If the result is empty or starts with a digit → prefix `col_` (e.g. `2026` → `col_2026`).
6. **Reserved-word guard:** if the name is a PostgreSQL reserved word (`order`, `user`, `select`, `from`, `group`, `table`, `desc`, …) → append `_col` (e.g. `order` → `order_col`).
7. **Duplicate guard:** if a sanitised name collides with one already used in this file → append `_2`, `_3`, … (e.g. two `code` columns → `code`, `code_2`).
8. Clamp to 63 chars (truncate + numeric suffix on overflow).

The mapping `original header → final column` is returned to the UI (the Step-2 warnings) and is also written to the audit log on first load. All loaded columns are typed (§5.2.6); the raw text is preserved in SeaweedFS so a re-profile is always possible.

#### 5.2.6 Detected-type → PostgreSQL-type mapping

The loader profiles a **sample** of rows (first ~1,000, plus a streamed full-pass count) per column and picks the **narrowest type that fits every non-empty value**, falling back to `text`:

| Detected pattern (all non-empty values match) | PG column type |
|---|---|
| Integer, fits 64-bit (`^-?\d+$`) | `BIGINT` |
| Decimal / money (`^-?\d+(\.\d+)?$`, has a `.`) | `NUMERIC(18,4)` |
| ISO date `YYYY-MM-DD` (or `DD/MM/YYYY`, `DD-MON-YYYY`) | `DATE` |
| ISO timestamp `YYYY-MM-DD[ T]HH:MM[:SS]` | `TIMESTAMP` (no tz — config data is wall-clock) |
| `true/false`, `yes/no`, `0/1` only | `BOOLEAN` |
| anything else / mixed / any value fails the narrow type | `TEXT` |

Rules:
- **Empty / null cells** (`''`, `NULL`, `N/A`) do not force `text`; they load as SQL `NULL` and are ignored for type inference.
- **Leading-zero strings** (e.g. MSISDNs `017…`, codes `0042`) are kept as `TEXT` (a leading-zero integer is treated as text to avoid losing the zero) — detect via a leading-`0` + length-preservation check.
- When in doubt → `TEXT`. The wizard's `calculate`/`filter` ops can still cast at query time.
- Money/target columns land as `NUMERIC(18,4)` to match `final_commission` internal precision and keep joins/arithmetic exact.

The detected schema is shown to the Maker in Step-2; there is no manual type override in Phase 1 (a future enhancement).

#### 5.2.7 Load process (step-by-step)

On a Step-2 file upload (`POST /reports/{reportId}/uploads`):

1. **Pre-checks (reject early):**
   - Report must be a **DRAFT** owned by the caller (Maker). Uploads to an approved/locked report → `409`.
   - File ≤ **500 MB** (streamed size guard) → else `413 FILE_TOO_LARGE`.
   - Content-type / extension is CSV → else `400 UNSUPPORTED_FILE_TYPE`.
2. **Store raw object:** stream the file to SeaweedFS at `salescom-uploads / reports/{reportId}/uploads/{uuid}/{originalName}`. Capture `object_bucket`, `object_key`.
3. **Profile header:** read the first line → sanitise headers (§5.2.5). Enforce the **≤ 30 columns** limit → else `400 TOO_MANY_COLUMNS`. Reject an empty header / zero data rows → `400 EMPTY_FILE`.
4. **Infer types:** sample rows → per-column PG type (§5.2.6).
5. **Generate table name:** `up_<reportShort>_<logicalSlug>` (§5.2.4). If a previous upload used the same logical name for this report, **drop the old table** (`DROP TABLE IF EXISTS uploads.<old>`) and delete its `report_supporting_upload` row (re-upload semantics) inside the same transaction.
6. **Create table:** `CREATE TABLE uploads.<name> ( <col> <type>, … )`. (No PK — it is a staging/config table; indexes on join keys are added later by the run engine per §12, not here.)
7. **COPY load:** stream the stored object through `COPY uploads.<name> (<cols>) FROM STDIN WITH (FORMAT csv, HEADER true, NULL '')`. Use the server-side `COPY … FROM STDIN` via the Npgsql binary/`COPY` API (no row-by-row INSERTs). On any COPY error (bad row, type cast failure) → roll back, drop the table, return `422 CSV_LOAD_FAILED` with the failing line number.
8. **Count rows:** `SELECT count(*)` → `row_count`.
9. **Register:** insert one `report_supporting_upload` row (`db_schema='uploads'`, `db_table_name`, object refs, `file_name`, `row_count`, `uploaded_by`, `uploaded_at`).
10. **Audit:** `audit_log` (`action='CREATE'`, `entity_type='report_supporting_upload'`, `entity_id=<id>`), including the header-sanitisation map in `diff`.
11. **Respond** with the registration + detected schema (so the UI shows columns/types/warnings).

Steps 5–9 run in **one DB transaction** so a partial load never leaves an orphan table or an unbacked registration row. The raw SeaweedFS object is written first (step 2) and is the recovery source if the DB transaction is retried.

> **Who creates these tables:** the .NET **Application/Infrastructure** layer drives this (file handling, SeaweedFS, COPY) — *not* the generated calc SQL. The least-privilege executor role used at run time only `SELECT`s from `uploads.*`; DDL on `uploads` is done by the ingestion role. This keeps table creation on the trusted path (consistent with D2).

#### 5.2.8 API Endpoints

| Method | Path | Purpose | Role | Success | Errors |
|---|---|---|---|---|---|
| POST | `/reports/{reportId}/uploads` | Upload + load one CSV (multipart) | BU (owner), A | 201 | 400, 401, 403, 409, 413, 422 |
| GET | `/reports/{reportId}/uploads` | List a report's supporting uploads | BU, A, AP | 200 | 401, 404 |
| GET | `/reports/{reportId}/uploads/{id}` | One upload + its detected columns | BU, A, AP | 200 | 401, 404 |
| GET | `/reports/{reportId}/uploads/{id}/download` | Pre-signed URL to the raw CSV | BU, A, AP | 200 | 401, 404 |
| DELETE | `/reports/{reportId}/uploads/{id}` | Remove an upload (drops the table) — DRAFT only | BU (owner), A | 204 | 401, 403, 404, 409 |

**DTOs**

`POST /reports/{reportId}/uploads` — `multipart/form-data`: `file` (the CSV) + `logicalName` (optional; defaults to a filename slug). Response `201`:

```jsonc
{
  "id": "c41b…",
  "reportSetupId": "3f9a2c4d-…",
  "logicalName": "target_rso_bundle",
  "dbSchema": "uploads",
  "dbTableName": "up_3f9a2c4d_target_rso_bundle",
  "fileName": "TARGET_RSO_BUNDLE_APR26.csv",
  "rowCount": 4821,
  "columns": [
    { "original": "DD Code",       "name": "dd_code",       "pgType": "text" },
    { "original": "Slab1 Target",  "name": "slab1_target",  "pgType": "numeric(18,4)" },
    { "original": "Category",      "name": "category",      "pgType": "text" }
  ],
  "warnings": [
    "Header 'DD Code' sanitised to 'dd_code'.",
    "Duplicate header 'code' renamed to 'code_2'."
  ],
  "uploadedAt": "2026-06-16T09:40:00Z"
}
```

`GET /reports/{reportId}/uploads` — Response:

```jsonc
{
  "items": [
    {
      "id": "c41b…",
      "logicalName": "target_rso_bundle",
      "dbTableName": "up_3f9a2c4d_target_rso_bundle",
      "fileName": "TARGET_RSO_BUNDLE_APR26.csv",
      "rowCount": 4821,
      "uploadedAt": "2026-06-16T09:40:00Z",
      "uploadedBy": "maker.tarik"
    }
  ]
}
```

`GET /reports/{reportId}/uploads/{id}/download` — Response:

```jsonc
{ "url": "https://seaweedfs…/salescom-uploads/…?X-Amz-Expires=300", "expiresInSeconds": 300 }
```

`DELETE …/{id}` — `204 No Content`. On a non-draft report → `409 REPORT_LOCKED`.

Error examples:

```jsonc
{ "error": { "code": "TOO_MANY_COLUMNS", "message": "CSV has 34 columns; limit is 30." } }
{ "error": { "code": "FILE_TOO_LARGE",   "message": "File is 540 MB; limit is 500 MB." } }
{ "error": { "code": "CSV_LOAD_FAILED",  "message": "COPY failed at line 2207.", "details": { "line": 2207, "reason": "invalid input syntax for type numeric" } } }
```

#### 5.2.9 Validation & Business Rules

| Rule | Enforcement |
|---|---|
| CSV only (Phase 1) | extension/content-type check → `400 UNSUPPORTED_FILE_TYPE`. |
| ≤ 30 columns | header count check → `400 TOO_MANY_COLUMNS`. |
| ≤ 500 MB | streamed size guard → `413 FILE_TOO_LARGE`. |
| Non-empty | ≥ 1 header + ≥ 1 data row → else `400 EMPTY_FILE`. |
| Safe identifiers | sanitisation (§5.2.5); only `^[a-z_][a-z0-9_]*$` names ever reach DDL. |
| Atomic load | DDL + COPY + registration in one transaction; failure drops the table and rolls back. |
| Re-upload semantics | same logical name on the same report → drop+recreate (one table per logical name per report). |
| Owner + draft only | upload/delete require the report be a DRAFT owned by the caller (or Administrator) → `403`/`409`. |
| Idempotent registration | `uq_supporting_upload_table (db_schema, db_table_name)` prevents duplicate registration of a generated table. |
| Audited (BR9) | `audit_log` on create/delete, with the sanitisation map. |
| Trusted-path DDL (D2) | upload tables are created by the ingestion role, never by generated calc SQL; the run executor only `SELECT`s from `uploads.*`. |

#### 5.2.10 How the IR resolves an upload

At **Final Save**, the SQL Generator walks the IR; for every `source`/`join_with` with `type = "upload"` and `ref = "<logical>"`, it looks up the `report_supporting_upload` row for this `report_setup_id` whose logical name = `<logical>` and substitutes the schema-qualified `db_schema.db_table_name` into the generated SQL (via SQLGlot identifiers — never string concat). If no matching upload exists → Final Save fails validation with a clear "referenced upload `<logical>` not found" error, so a report can never compile against a missing table.

---

**Cross-references:** the IR contract is in `IR_Schema_and_MultiKPI_Join.md` (source/join `ref` resolution, §2/§3); the table/column names are the canonical ones from `Reconciled_Canonical_DDL.md` §3.3 (`data_source`) and §3.4 (`report_supporting_upload`); run-time temp tables, indexing of join keys, and cleanup are in §7 / §12.
