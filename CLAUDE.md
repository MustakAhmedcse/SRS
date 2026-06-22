# CLAUDE.md — SalesCom Project Context

> Persistent project brain. Auto-loaded each session so context survives summarization.
> Last meaningful update: 2026-06-18. Keep this current as the project evolves.

---

## 1. What the system is

**SalesCom** = an internal **Sales Commission Automation Platform** for **Banglalink** (telco). It replaces the old **manual SQL / SRF** commission process with a **self-service, config-driven** web app: a Business User builds a commission report through a **5-step no-code wizard**, the system turns that setup into SQL, runs it over prepared sales data, produces per-channel commission, routes it through a **maker-checker approval**, then pays out via **EV** (auto + SMS) or **POS** (CSV handoff).

**End-to-end flow:** wizard → JSON **IR** (`report_setup.definition`) → **SQLGlot** builds SQL at Final Save (stored in `stages`) → run trigger (schedule / demo / run-now) → **Executor** runs stage-by-stage (`run_stage`) → per-channel **`final_commission`** → sequential approval → **EV / POS** disbursement.

**Roles (one each):** Business User (**Maker**) · Approver (**Checker**) · **Administrator** (can do all).

---

## 2. Scale, infrastructure, deployment

- **Scale (real):** modest internal tool — **300–500 total users, 30–50 peak concurrent**. ⚠️ The earlier "**200 concurrent**" was illustrative, NOT a requirement — do NOT size for 200.
- **Servers:** **5 dedicated + 1 shared Airflow** (user's words: "5 servers and 1 shared"):
  - 2 App servers — `CMVLSALCOMAPP01/02` (Web/Next.js, .NET Web API, Public API Gateway, Hangfire)
  - 2 AI servers — `CMVLSALCOMAI01` (SQLGlot builder, Execution Service, EV worker) / `AI02` (RabbitMQ, SeaweedFS, Loki/Prometheus/Grafana)
  - 1 DB server — bare-metal **Percona PostgreSQL 18** (not Docker)
  - 1 **shared** Airflow/ETL — `GZCLSALAPPDATA02` (172.22.37.16)
- **DC/DR:** **Cumilla = active (100%)**, **Gazipur = warm standby (0%)** (GZVWSALCOMxx); DB async streaming replication; failover via **client F5/VIP** LB (Cumilla VIP 172.19.10.6, Gazipur 172.16.10.6). **Client bears all server cost.**
- **Deployment:** everything in **Docker** except the DB. Logging = **Serilog + Loki** now, **Grafana = future**.

---

## 3. Tech stack

Next.js / React / TypeScript · C# / .NET (ASP.NET Core, **Dapper + EF Core**) · **Python** (SQLGlot + SQLAlchemy) calc engine · **PostgreSQL** · **RabbitMQ** · **SeaweedFS** (S3) · **Hangfire** · **Apache Airflow** (ETL). Auth = Central Login **SSO + OTP** → SalesCom **JWT** (team LLD v2, 2026-06-17: **inactivity logout 3 hr**; explicit JWT expiry duration now left unspecified — my `SalesCom_LLD.md` assumed 1-day + 4-hr, to reconcile in the final LLD). 4-layer .NET (Api / Application / Domain / Infrastructure).

> **Team LLD v2** (`E:\...\Architecture Design\LLD\LLD for Sales Commission Automation_26_v2.docx`, dated 2026-06-17) = latest TEXT of the team's LLD. Only real changes vs the earlier version: date, Deployment += Docker, JWT inactivity 4hr→3hr, JWT expiry duration removed. **Its DB-schema section is OLD — `DB_Schema.md` (salescomdbtst) is the authoritative schema.** The user wants to discuss a **final LLD** = reconcile my detailed `SalesCom_LLD.md` with the team's REAL schema (`DB_Schema.md`) + the v2 text decisions.

**External systems:** Central Login (`blposapi.banglalink.net`) · EV API (`10.13.2.7:9898`) · SMS (`172.16.7.210:13082`) · Email/SMTP · Source systems for ETL (DWH `172.16.10.210`, In-house `172.16.8.58`, vPeople `172.16.10.130:8084/api/Employee`, POSDMSDB `cmplsalrac-scan.banglalink.net`) · POS (disbursement CSV handoff) · RSO App (consumes SalesCom public API).

---

## 4. Locked architecture decisions (with current nuance)

- **D1 — Run model:** original LLD model = **one run at a time** + priority queue (RunNow=high, Demo=mid, Schedule=low). We earlier proposed bounded-parallel (N≈3–4) — but that was for the *wrong* 200-user assumption. **At the real 30–50 scale, the simple single-run model is the correct choice. Do NOT over-engineer parallelism.**
- **D2 — Calc engine:** IR → **SQLGlot** AST builder → execute-time validate (read-only / allowlist). SQL built at Final Save, stored in `stages`, snapshotted into `run_stage` at run time (freezes SQL so editing setup doesn't affect a running run).
- **D3 — Infra/HA-DR:** Cumilla active + Gazipur warm standby (see §2). Client F5 LB.
- **D4 — Process:** contracts-first — the external integration contracts (EV/POS/sources/SMS/SMTP/Central Login) should be pinned before building those modules.
- **D5 — RSO App system-to-system API (2026-06-18):** RSO App calls SalesCom approval APIs via **dedicated endpoints** (separate from the UI API). Auth = `application_name` + `secret_key` per call (NOT user JWT). RSO App can submit approval decisions on behalf of checkers through these endpoints. UI endpoints are not shared.
- **D6 — POS disbursement via Airflow nightly job (2026-06-18):** POS disbursement is NOT event-driven via RabbitMQ. A nightly Airflow job checks: (1) `is_pos_disbursement = true`, (2) report fully approved (`overall_status = 4`), (3) `disburse_status != 'DONE'`. If all 3 pass → dump commission data to POS location → set `disburse_status = 'DONE'`. No Hangfire/RabbitMQ involvement.
- **D7 — Monthly commission dump to RSO App (2026-06-18):** A separate month-end Airflow DAG extracts RSO and Retailer commission amounts from `final_commissions` in a specific format and pushes them to RSO App (so RSO App can show monthly earnings to RSOs/Retailers). This is a one-way data push, separate from POS disbursement.
- **D8 — Permission-based RBAC, roles in POS (2026-06-22):** Access is **per-action permission**, NOT role-hardcoded. Each action = one permission code (e.g. `report_view`=25, `report_create`=26, `report_approval`=31…). `user_rights.rights_code` = a **permission code** (NOT a role code 10/20/30); a user has one row per granted permission. **Roles live in POS**: a POS admin creates a role → assigns permission codes → assigns the role to users. The hourly Airflow sync flattens each user's effective permission codes into `user_rights`. SalesCom has **no role table**. 3 standard roles (Maker/Checker/Admin) + their default permission bundles are documented, but the system is **flexible** (POS can define any role/permission mix). Enforcement is per-permission in BOTH frontend (encrypted blob → Next.js BFF decrypts → checks code) and backend (live `user_rights` lookup by `users.id`). JWT carries NO role/permission. `/auth/me` = profile only; `/auth/permissions` = encrypted permission codes.

---

## 5. Document set (and authority)

Working dir: `F:\Claude Root\Salescom Automaton\SRS\`. Other files on `E:\...\HLD\` and `C:\Users\Mustak\Downloads\`.

| Doc | Role |
|---|---|
| `Salescom_System_Documentation_Bangla.md` | combined SRS+LLD spec (Bangla) — **authoritative business spec** |
| `Salescom Platform (Banglalink).pdf` | 43-page **UI mockup** (Figma) — visual reference |
| `SRS_for_Sales_commission_Automation_V1.5.docx` | older SRS — superseded by the .md |
| `salescom-deployment-plan.md` | 6-server Docker plan (Bangla) — sized for 50; scale info superseded by §2 |
| `HLD for Sales Commission Automation 26_v2.0.docx` (E:\...\HLD\) | original HLD (diagram-album) |
| **`HLD for Sales Commission Automation 26_v3.0.docx`** (E:\...\HLD\ + SRS\) | **HLD I authored** — added narrative + sections around existing diagrams; user must refresh the Word ToC |
| `Salescom_System_Design_Document.md` (SRS\) | **SDD v1.1**, ~3900 lines, §0–§11 — the deep design I authored (architecture, data model, calc engine, run orchestration, state/approval/money, security, ICDs, DevOps/DR, schema reconciliation §11). Use as the authoritative deep-design reference; HLD is the lighter client-facing layer. |
| `LLD for Sales Commission Automation_26_v2.docx` (Downloads\) | **LLD** (team's, by MD. Muntakimur Rahman) — detailed, good structure |
| `commission_system_erd_5.drawio` (Downloads\) | **ERD** — 22 tables |

---

## 6. Data model — `DB_Schema.md` is AUTHORITATIVE (real schema) + LLD §3

> ⚠️ The team's **REAL implemented schema** is in **`DB_Schema.md`** (PostgreSQL schema `salescomdbtst`, **21 tables** as of 2026-06-18). **When LLD §3 and DB_Schema.md conflict, DB_Schema.md wins.**

**Current 21 tables:**
| Module | Tables |
|---|---|
| Identity & Access | `users`, `user_rights`, `login_log` |
| Catalog | `data_sources`, `channels` |
| Report | `report_setups`, `report_supporting_uploads`, `section_wise_report_sqlss` |
| Run & Output | `report_runs`, `run_stages`, `final_commissions` |
| Approval | `approval_flows`, `approval_flow_levels`, `approval_flow_level_users`, `report_approvals`, `report_approval_details` |
| Disbursement | `ev_disburse`, `pos_disbursement` |
| Notification | `email_notifications`, `sms_notifications` |
| Cross-cutting | `audit_logs` |

**Key schema facts (2026-06-18 schema):**
- PK = `int8 GENERATED BY DEFAULT AS IDENTITY(...)` — NOT `BIGSERIAL`, NOT UUID. FK = `int8`.
- Status/type fields = `int4` enums (not text+CHECK). `report_runs.disburse_status` = `varchar(50)` exception.
- `report_setups.is_setup_complete` and `final_commissions.msisdn` are **all-lowercase** (changed in DB 2026-06-22 from the old mixed-case `"IsSetupComplete"` / `"Msisdn"`) — no quoting needed.
- `report_setups.is_report_stop bool` replaces old `status varchar` (ON/STOP).
- `ev_disburse.ev_msisdn varchar(15)` = recipient phone for EV API + payout SMS.
- `run_stages.sort_order` (not `stage_order`); `run_stages.cleanup_status int4`.
- `pos_disbursement.dump_status int4` (not `disburse_status`).
- `approval_flow_level_users.user_id int8` FK → `users.id` (was varchar/TEXT).
- `notification_logs` is GONE → replaced by `email_notifications` (approval emails) + `sms_notifications` (EV payout + approval SMS).
- `audit_logs.changed_by_user_id` = uuid (rest bigint). Append-only table.

---

## 7. Business rules (BR1–BR9)

BR1 access by assigned permission (per-action, see D8) · BR2 data source never deleted (only deactivate) · BR3 report name system-wide unique · BR4 start ≤ end date · ~~BR5 maker≠checker~~ **REVERSED 2026-06-22: the Maker CAN also be an approver of the same report — no segregation of duties** · BR6 approval sequential ascending · BR7 reject needs comment · BR8 disburse only after full approval · BR9 only Maker manages schedule, all changes audited.

> **Approval reject (2026-06-22):** a reject **steps back one level** (to the previous approver), NOT a full restart from level 1. A reject at the **first level** of a phase returns to the Maker (`overall_status=0`). A reject at the first POST_RUN level keeps the setup approval (never drops to PRE). `overall_status` table wording finalized per the team's image (0 Pending-Edit/Resubmit, 1 Pre-Approval-Pending, 2 Pre-Approved, 3 Post-Approval-Pending, 4 Post-Approved).

---

## 8. LLD + ERD review (done 2026-06-16) — verdict: **NOT-READY as-is for a zero-context team; needs additions (not a rewrite)**

Structure is good (relational backbone sound, stages→run_stage snapshot smart, approval normalized, consistent per-feature layout). But a team with ONLY the LLD+ERD will get stuck on the core. **Must-add before handover, in order:**

1. **IR (`definition` JSONB) JSON Schema + 2 worked examples + all enum value lists** — #1 blocker; without it FE/BE/Python can't start.
2. **IR→SQL Generator mapping** (each step type → SQL pattern, stage-decomposition, `output_table_name` naming) + **safe/read-only SQL validation contract** (allowlist, blocked tokens, allowed schema/table).
3. **Reconciled canonical DDL** — fix in one pass: FK type bugs, LLD↔ERD deltas, typos, idempotency UNIQUE, enum CHECK.
4. **API request/response DTOs** + error envelope + per-endpoint role (especially wizard save endpoints that carry the IR; `POST /reports` draft-id response).
5. **Central enum + state-transition appendix** (canonical values + CHECK/ENUM).
6. **RBAC permission matrix** + `user_right` INT semantics + `/auth/me` DTO.
7. **Seed/lookup data** (channel, recurrent_type, approval_type+phase, notification templates, user_right→role) + **CSV→DB ingestion mechanics** (naming, type mapping, sanitization).
8. **Run-failure/cleanup/stale-run recovery** + fix the **reject-flow contradiction** (§7.4: "previous level" vs "restart from first") + demo cap + Final run-gate + EV/POS mutual-exclusion guard.
9. **Getting-Started / dev-setup** (repo layout, env, local wiring, migration cmd, RabbitMQ message contracts, build order: auth→seed→data source→upload→wizard/IR→SQL gen→run→approval→disburse).

### Critical bugs to fix in DDL
- **FK type mismatch:** `ev_disburse.channel_id` & `approval_flow_level_user.user_id` = TEXT but FK→UUID (PostgreSQL will break). Also `login.login_time` & `run_stage.file_generated_at` = TEXT (should be TIMESTAMPTZ). `audit_log.user_name` FK→natural key (not PK).
- **No idempotency** → double-pay risk: add UNIQUE(report_run_id, channel_id) on `final_commission` & `ev_disburse`; UNIQUE(report_run_id) on `pos_disbursement`; UNIQUE(run_id, order) on `run_stage`; UNIQUE(report_setup_id, stage_order) on `stages`.
- **LLD↔ERD deltas:** ERD has `report_setup.is_pos_dibursement` + `approval_flow_id`, `approval_type.phase`, `approval_flow_level.approval_type_id`, `user.user_id/mobile_no/depertment` — LLD tables omit these. ERD is more complete → make LLD match ERD.
- **Typos that will become permanent:** `dibursement`, `depertment`, `INTERGER`, `Monthyly`, `faild`; `run_stage.order` is a reserved word.
- `recurrent_type` is both a free-text column and a lookup table (inconsistent). Duplicate heading "6.4.6". POS event-driven vs timed-job unclear.

---

## 9. How the user works & doc-writing rules (IMPORTANT — these are commitments)

- User writes in **Banglish**; is the developer/lead building this himself; works **iteratively** (one diagram/section at a time, reviews each).
- **Docs are binding client commitments + only ~2-month timeline.** Do NOT over-commit. Keep security/audit/HA-DR/NFR/SLA/DevOps **high-level & descriptive, never committal** (no uptime %, RPO/RTO, pentest, concurrency SLA). **No explicit Out-of-Scope list**, **no timeline/concurrency numbers in client docs**. Assumptions = infra only (Docker, client F5, single active DC + warm standby).
- **Audience = technical but the client POC is NOT deeply technical** → write technical content in **simple, clear English**, detailed but digestible. (Client docs in English; chat in Bangla.)
- **LLD-level implementation detail is fine** (it's an internal build spec, not an external commitment) — so adding IR schema / DDL / API DTOs does NOT create the liability the client-facing HLD must avoid.
- **Diagrams:** user wants **editable mermaid `flowchart`** style (NOT the `C4Context/C4Container` types, which aren't drag-editable in mermaid.ai) — use `---config ... layout: elk---` frontmatter + classDef (person #08427B, container #438DD5, external #8A8A8A). C1, C2, and a simple 6-block deployment diagram are already done in this style. (C2 = SQLGlot Builder and Execution Service are SEPARATE; notifications sent directly by Web API, no Notification Worker; externals grouped in an "External Systems" box.)

---

## 10. Tooling / environment notes (Windows, working dir on F:)

- Python: use **`py -3`** (3.13). `python`/`python3` are MS-Store stubs. Installed: pymupdf, python-docx, defusedxml, lxml. (pypdf not installed.)
- **docx skill** (anthropic-skills:docx) for Word: unpack → edit XML → pack (preserves images byte-perfect via `--original`). Needs `defusedxml`+`lxml`. ⚠️ Set `PYTHONUTF8=1`/`PYTHONIOENCODING=utf-8` or pack/validate crashes on cp1252 (the `→` char).
- PDF text: Git's `pdftotext.exe` (`C:\Program Files\Git\mingw64\bin\`). No poppler `pdftoppm` (so the Read tool can't render PDFs; extract text instead).
- `.drawio` decode: `<diagram>` inner is base64 + raw-deflate + urldecode (Python `base64`+`zlib.decompress(data,-15)`+`urllib.parse.unquote`).
- Node v22 available. Detailed memory also lives in the `.claude` projects memory folder (MEMORY.md index).

---

## 11. PENDING / next steps

- **Commission data analysis (in progress):** folder `commission_with unique kpi data\` is in the working dir. **34 commission types**, each = `<Type> Data\<Type>\` (monthly campaign PDFs, ~1392 total) + a **`<TYPE>_Unique_KPI_Conditions.docx` summary** (the distilled logic). Also `Deno Campaign data\deno_commissions.json` (1.77MB structured commission catalog). The 34 summaries were extracted to `_kpi_txt\<TYPE>.txt`. The summaries are EXCELLENT — each catalogs every unique KPI condition with codes (e.g. Deno: HIT-01..07, RCH-01..03, RSO-01..02, RET-01, EXC-01..07). **Real commission patterns** = KPI types (HIT/Recharge/Bundle/GA/Participation/Lifting); recipients (Distributor/RSO/Retailer/BDO/MDO/Partner); incentive structures (flat BDT/unit, near-miss tiers, %-slabs, %-of-recharge slabs, multi-cluster rates, binary HIT-or-Miss + A/B/C/D category prizes, points contests); universal exclusions (Ryze SC 141, Postpaid SERVICE_CLASS_ID≥1000, BL employee-pool, cross-cluster >10%); capping (200%/300%), agent-list date-lock, rounding. These map directly to the SalesCom pipeline (Filter→Combine→Summarize→Calculate→IF/CASE→final mapping) — ideal grounding for the **IR schema + real IR examples** (the #1 LLD must-add from §8). **DONE → `Commission_Logic_Catalog.md`** (660 lines): per-type catalog, **24 cross-cutting patterns**, **20 calc-engine capabilities**, pattern→IR-stage mapping, **4 worked IR JSON examples**, LLD impl notes.
  - **KEY INSIGHT — real commission logic is FAR richer than the simple 5-stage wizard model.** Common patterns (flat BDT/unit, near-miss tiers, %-of-target slab, %-of-recharge, category prizes, exclusion stack, capping, rounding) fit the wizard's Filter→Combine→Summarize→Calculate→IF/CASE→final-map. But ~10 **advanced** patterns need special engine support: nested aggregation (SSO→BTS→DD Hit/Miss), within-DH quartile ranking, period-versioned rate-lookup, weekly cumulative-deduction, weighted multi-KPI variable pools, VLR/quality penalty modifiers (multiplicative, post-payout), gate engine (binary zeroes), cohort M1–M12 time-series, priority de-duplication across concurrent campaigns, higher-of fallback, AIT incl/excl dual-computation.
  - **Much complexity lives in external B2C Excel target/config files (DD-wise target, KPI weights, slab tables, category A/B/C/D lists), NOT in the SRF text** — so **"target-file / external-config join" is the #1 engine capability**.
  - **Strategic (given 2-month timeline):** do NOT try to make the no-code wizard express all 24 patterns. MVP = the common patterns; advanced patterns → external-config-driven or phase-2. Prioritize before building.
  - These 4 worked IR examples are the concrete grounding for the **#1 LLD must-add (IR schema + examples)** from §8.

## 12. Calc-engine design resolutions (user-confirmed 2026-06-16)

The user's strategy = **modular "sections + join"**: break any complex commission into small blocks (sections), each ending in a Summarize that fixes its grain (e.g. 1 row per RSO), then join sections on that grain key. Works for BOTH achievement and incentive. The `RSO_Report_Detail_HandDrawn.html` mockup (E:\...\Figma\...\Tarik Bhais Problem Statement & Solution\) proves it: 2 achievement sections (Recharge%, GA%) joined on RSO-Code → IF/CASE category (Platinum/Gold/Silver) → amount.

**Phased build (architecture must support ALL now; build in order):** Phase 1 (~2mo) = single-KPI + easy commissions; Phase 2 (~1.5mo) = multi-KPI; Phase 3 (~2mo) = complex. Design the engine as **blocks + a growing list of operation types + join** — Phase 1 builds the easy operations; Phase 2/3 ADD operation types (rank, history-read, multiplier) without redesign.

**Hard-pattern resolutions (all confirmed workable):**
- **Priority de-dup** (don't double-pay across campaigns): download the prior commission's output, upload it as a supporting file in the new report, and join/filter against it. No global engine awareness needed.
- **VLR/quality penalty**: a separate VLR section → join to the KPI section → final `CASE WHEN vlr_pct < 65 THEN 0`. Just another joined section + CASE.
- **Historical/cohort data**: short windows (≤3 months) → already in the 3-month-hot tables, normal join; long cohorts (M1–M12, >3 months) → **pre-compute in DWH/ETL** and bring as a source table.
- **Ranking / quartile (top 25%)**: GROUP BY+ORDER BY is NOT enough; use a PostgreSQL **window function** `NTILE(4) OVER (PARTITION BY dh ORDER BY ach DESC)` (or `PERCENT_RANK()`). Add a "Rank/Quartile" operation to the wizard + allow window functions in the safe-SQL allowlist. Phase 3.
- **External config (B2C Excel: DD_Code, Slab1_Target, category, weights)**: uploaded as supporting files → tables → joined. This is the #1 engine capability.
- **Double-count safety (the key one)**: prevent NOT by restricting joins but by enforcing **"each section ends in a Summarize (defines grain) → section-to-section joins are on the grain key (1:1)"** + automatic guardrails: (1) pre-join uniqueness check on the join key, (2) post-join row-count/fan-out check, (3) demo-run shows per-stage row counts so the user catches fan-out before disbursement, (4) final reconciliation (disbursed total == sum of section outputs). Engine must be **grain-aware**.
- **Big-data join speed**: table-to-table (uploads loaded into tables, not CSV), 3-month hot + archive; just add **indexes on join keys** + **filter-early-then-join** + index temp tables. Single-run model → no concurrency issue.
- **DONE — IR contract written → `IR_Schema_and_MultiKPI_Join.md`** (LLD annex): full `report_setup.definition` IR (report → achievement/incentive blocks → stages `filter/combine/summarize/calculate{formula|ifcase|map}/modify/rank`, enum lists, formal **JSON Schema draft-2020-12**), the **multi-KPI section-join rule** (every block ends in a `summarize` that fixes its grain; block-to-block joins must be on the grain key = 1:1) + **guardrails G1–G4** (pre-join uniqueness, post-join fan-out, demo-run per-stage row counts, reconciliation), a **phase map** (IR shape is FINAL now; only the op-list grows Phase 1→3), and a **complete worked IR** = the RSO GA+Recharge HTML campaign (2 achievement blocks joined on RSO_CODE → category → amount). **This closes the #1 LLD blocker** (§8.1).
- **DONE — FINAL LLD authored → `SalesCom_LLD.md`** (v2.0 FINAL, ~3,100 lines, **grounded in the REAL `salescomdbtst` schema**): real plural table names (`report_setups`, `report_runs`, `run_stages`, `final_commissions`, `section_wise_report_sqls`, `report_approvals`, `report_approval_details`, `ev_disburse`, `pos_disbursement`, …), `int8 GENERATED BY DEFAULT AS IDENTITY` PKs, `int8` FKs, `int4` enums. All **2026-06-16 team decisions** applied (renames, `is_setup_complete`, `overall_status` 0–4, `UNIQUE(report_run_id, channel_code)` idempotency, derived report-list status, schedule-validation, Final-run pre-checks) + **v2 text** (Docker, JWT 3hr inactivity). §3 DDL + §4–§14 all features + Appendix A (IR ref), B (int-enum reference), C (BR1–BR9). **§15 Errata** resolves 11 final critic findings — E1 phase-aware reject (POST_RUN reject never voids setup approval), E2 `channel_code` = recipient MSISDN for EV/SMS, E3 validation→422, E4 submit→201, E5 ev_disburse.status += RETRY, E6 GET /reports list DTO, E7 one pagination envelope, E8 report_setups.status = ON/STOP only, E9 disburse_status += IN_PROGRESS, E10 BR8 gate = 409, E11 /auth/me += lastFailedLoginAt. §15 is authoritative where §1–§14 conflict. (This FINAL real-schema version replaced the earlier UUID-era draft.)
- **Only big LLD gap left: IR→SQL mapping detail (§8.2)** — how each IR stage compiles to SQL.
- **Biggest doc gap to close:** IR JSON Schema + worked examples + IR→SQL mapping (offered to author).
- HLD v3.0 done; user should refresh the Word ToC. LLD review done (§8).
