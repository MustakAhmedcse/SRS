## 7. Approval (Maker-Checker)

This section is the build spec for the maker-checker approval feature. It is implemented in the `SalesCom.Api` (controllers), `SalesCom.Application` (command/query handlers, the `ApprovalService` use-case logic), `SalesCom.Domain` (the `ApprovalFlow` / `ApprovalRequest` / `ApprovalDecision` entities and the phase/decision enums), and `SalesCom.Infrastructure` (Dapper/EF Core repositories, RabbitMQ publisher, SMS/Email gateways) layers. It uses the exact tables and columns from the canonical DDL (§3.6): `approval_type`, `approval_flow`, `approval_flow_level`, `approval_flow_level_user`, `approval_request`, `approval_decision`.

---

### 7.1 Overview

Every commission report must be reviewed by someone other than its author before it has any financial effect. SalesCom implements this as a **maker-checker** workflow:

- The **Maker** (Business User, the report author) builds a report in the wizard and submits it for approval.
- One or more **Checkers** (Approvers) review it level-by-level. Only when *every* level has approved can the report run on schedule / Run-Now and pay out.
- An **Administrator** sets up the reusable approval machinery (flows, levels, level membership) and can act in any role.

The approval machinery has two parts:

1. **Definition (admin, set up once):** A reusable **Approval Flow** = an ordered list of **Levels**; each level is bound to an **Approval Type** (which carries a **phase** — `PRE_RUN` or `POST_RUN`), and each level has a set of **assigned users** who may act at that level. A report is bound to exactly one flow when it is created (`report_setup.approval_flow_id`).

2. **Runtime (per report):** When the Maker submits a report, the system opens exactly **one** `approval_request` for that report. That single request **walks all the levels of the flow in ascending order**. Each level produces one `approval_decision`. The request also carries which **phase** it is currently in (the current level's type/phase), so the same request can first gate the *setup* (`PRE_RUN` levels) and later gate the *results* (`POST_RUN` levels).

**Two phases, one request:**

| Phase | What it gates | When it runs | Linked to |
|---|---|---|---|
| `PRE_RUN` | The report **setup** (the IR/definition, schedule, payout config) | After the Maker submits the report, before any Final run | `report_setup` only (`approval_request.report_run_id` is `NULL`) |
| `POST_RUN` | The **results** of one specific Final run | After a Final run produces `final_commission`, before disbursement | the exact `report_run` (`approval_request.report_run_id` is set) |

**Phase rules (locked):**

- A **recurrent** report (`report_setup.is_recurrent = TRUE`) **must** use a flow whose levels are all `PRE_RUN`. The setup is approved once; thereafter every scheduled run executes and disburses automatically, with **no** per-run result approval. (You cannot ask a human to approve every nightly/weekly run.)
- A **non-recurrent** report may use a flow that has `PRE_RUN` levels, `POST_RUN` levels, or both. If it has `POST_RUN` levels, then after a Final run produces results, the **same** request continues into the `POST_RUN` levels, and disbursement only runs once those approve.
- A **Demo** run **never** needs approval and **never** disburses. Demo is for the Maker to preview numbers before submitting.

**Key business rules enforced here:** BR5 (segregation — Maker ≠ Checker on the same run), BR6 (sequential ascending approval), BR7 (reject requires a comment), BR8 (disburse only after full approval), BR9 (every decision audited). These are detailed in §7.6.

---

### 7.2 User Interface

There are two audiences: **Administrators** who build flows, and **Approvers / Makers** who act on requests.

**A. Admin — Approval Flow management** (under Settings → Approvals)

- **Flow list:** serial #, flow name, description, active toggle, "levels" count, actions (Edit, Add Level, Activate/Deactivate). A **"Add a New Flow"** button asks for a unique flow name + description.
- **Level list (inside a flow):** serial #, level order, level name, **approval type** (with its phase badge `PRE_RUN`/`POST_RUN`), assigned-user count, actions. **"Add Level"** asks for the level name, the order (a unique whole number within the flow), and the approval type (which fixes the phase). Drag to reorder is allowed only while no live request references the flow at that order (see §7.6, edit-while-live rules).
- **Level user list (inside a level):** serial #, the assigned user (full name, username, email, department), actions (Add, Remove). Users are looked up from the company directory (the synced `"user"` table); a typeahead searches `full_name` / `user_name` / `email`.
- A **phase consistency hint:** when the admin saves a level, the UI warns if the resulting flow mixes phases in a way that conflicts with the reports already bound to it (e.g. a recurrent report bound to a flow that now has a `POST_RUN` level). This is a soft warning in the UI; the hard guard is server-side (§7.6).

**B. Maker — submit & track** (on the Report details / list)

- On the report row/details, a **"Send for Approval"** action (enabled only when the report is a saved draft owned by the Maker and not already pending). Submitting shows a confirmation with the flow name and the list of levels/approvers it will route to.
- An **"Approval History"** action opens a read-only timeline: the request, each level in order, each decision (approver, decision, comment, timestamp), and the current open level highlighted. This is driven by `GET /approvals/{id}` + `GET .../decisions`.
- A rejected report shows a banner ("Changes requested by <approver> at <level>: <comment>") and re-enables **Edit** + **Send for Approval**.

**C. Approver — the approval queue** (the "My Approvals" page)

- A list of requests waiting on **the current user at the current open level**: report name, channel, phase badge (PRE_RUN = "Setup", POST_RUN = "Results"), submitted-by, submitted-at, level name, and — for POST_RUN — a link to the run's results (per-channel `final_commission`, totals, downloadable output).
- Row actions: **Review** (opens setup tabs read-only, or the run results for POST_RUN), **Approve**, **Reject**, **Request Changes**.
- **Approve** is a one-click confirm. **Reject / Request Changes** open a dialog with a **mandatory comment box** (the Submit button is disabled until a non-empty comment is entered — BR7).
- The queue is the user's own; a request never appears for a user who is not assigned to its current open level, and never for the Maker of that request (self-approval is blocked — BR5).

---

### 7.3 Data Model

All approval state lives in the six tables of the **Approval** group (§3.6). The columns below are quoted exactly from the canonical DDL.

**`approval_type`** — a category of approval; its `phase` decides whether the level it is attached to gates the setup or the results.

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `type_name` | TEXT, UNIQUE | e.g. "Setup Review", "Finance Sign-off" |
| `description` | TEXT | |
| `phase` | TEXT, `CHECK IN ('PRE_RUN','POST_RUN')` | the phase this type imposes on its level |
| `sort_order` | INTEGER | display order in admin lists |

**`approval_flow`** — a reusable named chain.

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `flow_name` | TEXT, UNIQUE | |
| `description` | TEXT | |
| `is_active` | BOOLEAN | soft-delete / availability |
| `created_on`/`updated_on`/`created_by`/`updated_by` | audit | |

> Note (per DDL §3.6): the approval **type** lives on each **level**, not on the flow. There is **no** `approval_flow.approval_type_id`. This lets a single flow interleave `PRE_RUN` and `POST_RUN` levels — which is exactly what a non-recurrent "approve setup → run → approve results" flow needs.

**`approval_flow_level`** — one ordered step inside a flow, bound to a type (which sets its phase).

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `approval_flow_id` | UUID FK → `approval_flow.id` (CASCADE) | |
| `approval_type_id` | UUID FK → `approval_type.id` (RESTRICT) | sets this level's phase |
| `level_order` | INTEGER, `CHECK >= 1` | `UNIQUE(approval_flow_id, level_order)` |
| `level_name` | TEXT | |
| audit cols | | |

**`approval_flow_level_user`** — which users may act at a level.

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `approval_flow_level_id` | UUID FK → `approval_flow_level.id` (CASCADE) | |
| `user_id` | UUID FK → `"user".id` (CASCADE) | real UUID FK (DDL fix) |
| | | `UNIQUE(approval_flow_level_id, user_id)` |

**`approval_request`** — the single request opened per submission; it walks all levels and tracks the current open level and current phase.

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `report_setup_id` | UUID FK → `report_setup.id` (CASCADE) | the report under review |
| `approval_type_id` | UUID FK → `approval_type.id` (RESTRICT) | the **current** level's type/phase |
| `approval_flow_id` | UUID FK → `approval_flow.id` (RESTRICT) | snapshot of which flow was used |
| `report_run_id` | UUID FK → `report_run.id` (SET NULL), nullable | **set for POST_RUN** result approval; `NULL` for PRE_RUN setup |
| `current_level_order` | INTEGER | the level currently waiting on a decision |
| `overall_status` | TEXT, `CHECK IN ('IN_PROGRESS','APPROVED','REJECTED','CHANGES_REQUESTED','CANCELLED')` | |
| `initiated_by` | TEXT | `user_name` snapshot (the Maker) — no FK |
| `initiated_at` | TIMESTAMPTZ | |

> `report_run_id` lets a POST_RUN result approval point at the **exact** Final run being approved. BR8 (disburse only after full approval) is enforced in the application: disbursement checks `overall_status = 'APPROVED'` on the request whose `report_run_id` matches the run.

**`approval_decision`** — the immutable per-level decision trail.

| Column | Type | Notes |
|---|---|---|
| `id` | UUID PK | |
| `approval_request_id` | UUID FK → `approval_request.id` (CASCADE) | |
| `level_order` | INTEGER | the level this decision was made at |
| `decided_by` | TEXT | `user_name` snapshot — no FK (immutable history) |
| `decision` | TEXT, `CHECK IN ('APPROVED','REJECTED','CHANGES_REQUESTED')` | |
| `comment` | TEXT | **required** when decision ≠ `APPROVED` (DDL `ck_approval_decision_comment`, BR7) |
| `decided_at` | TIMESTAMPTZ | |

**How a single request maps to levels and decisions (one PRE_RUN flow, 2 levels):**

```
approval_flow "Std Setup Review"
  └─ level 1 (order=1, type "Setup Review" / PRE_RUN) → users {U_checker1}
  └─ level 2 (order=2, type "Finance Sign-off" / PRE_RUN) → users {U_finance}

report_setup R ──1 approval_flow "Std Setup Review"

submit →
  approval_request AR  { report_setup_id=R, approval_flow_id=flow,
                         approval_type_id = (level 1's type), report_run_id=NULL,
                         current_level_order=1, overall_status='IN_PROGRESS',
                         initiated_by='maker_uname' }
  U_checker1 approves → approval_decision { AR, level_order=1, APPROVED }
                        AR.current_level_order → 2 ; AR.approval_type_id → level 2's type
  U_finance approves  → approval_decision { AR, level_order=2, APPROVED }
                        AR.overall_status → 'APPROVED'  → publish approval.completed
```

---

### 7.4 Process Logic

The logic lives in `ApprovalService` (Application layer). All multi-row state changes (insert decision + advance request + flip statuses) run inside **one database transaction** with a row lock on the `approval_request` (`SELECT ... FOR UPDATE`) so two approvers acting at once cannot double-advance.

#### 7.4.1 Bind a flow at report creation

When a report is created/saved, the wizard's "Basic details" step requires the Maker to pick an active `approval_flow`; the chosen `approval_flow.id` is stored as `report_setup.approval_flow_id` (NOT NULL). `report_setup.setup_approval_status` starts at `'DRAFT'`.

**Validation at bind time** (and again at submit):
- The flow must be `is_active = TRUE`.
- If `report_setup.is_recurrent = TRUE`, **every** level of the flow must be `PRE_RUN` (no `POST_RUN` level allowed). Otherwise reject with `422` (see §7.6 V-1).
- The flow must have ≥ 1 level, and every level must have ≥ 1 assigned user.

#### 7.4.2 Submit (open the request)

On `POST /reports/{id}/submit` (Maker only):

1. **Pre-checks** (else `409`/`422`): report is in `DRAFT` or `CHANGES_REQUESTED`; the caller is the report's Maker (creator / last editor); the report has compiled `stages` (Final-Saved); the flow passes §7.4.1 validation; there is no other live request (`overall_status = 'IN_PROGRESS'`) for this report.
2. Resolve the flow's levels ordered by `level_order ASC`. Take **level 1** (lowest order). Its `approval_type_id` sets the starting phase.
3. Insert **one** `approval_request`:
   - `report_setup_id` = report; `approval_flow_id` = the bound flow; `approval_type_id` = level 1's type; `report_run_id` = `NULL` (PRE_RUN starts on the setup); `current_level_order` = level 1's `level_order`; `overall_status` = `'IN_PROGRESS'`; `initiated_by` = Maker `user_name`; `initiated_at` = now.
4. Set `report_setup.setup_approval_status = 'PENDING_APPROVAL'`.
5. **Audit** (`audit_log`, action `OTHER`/`APPROVE`-submit) + **email** the level-1 assigned users ("Approval requested") and publish `approval.requested`.

The request now appears only in the queues of level 1's assigned users.

#### 7.4.3 Approve (advance one level — BR6 sequential ascending)

On `POST /approvals/{id}/decisions` with `decision = 'APPROVED'`:

1. Lock the request (`FOR UPDATE`). Re-validate: `overall_status = 'IN_PROGRESS'`; the caller is assigned to **the current open level** (`current_level_order`); the caller is **not** the Maker (`initiated_by`) — BR5; the caller has not already decided this request at any level (one user, one level per request).
2. Insert `approval_decision { approval_request_id, level_order = current_level_order, decided_by = caller, decision = 'APPROVED', comment = optional, decided_at = now }`.
3. **Is there a higher level in the flow?**
   - **Yes** → set `current_level_order` to the next level's order and `approval_type_id` to that next level's type (this may switch the phase, but only PRE_RUN→PRE_RUN or, post-run, POST_RUN→POST_RUN — phase order is monotone by §7.6 V-2). The request now appears for the next level's users. Email them. Publish `approval.advanced`.
   - **No (this was the final level of the whole flow, OR the final level of the current phase)** → branch on phase:
     - **All remaining work was PRE_RUN and there are no POST_RUN levels** (recurrent, or non-recurrent setup-only): set `overall_status = 'APPROVED'`, `report_setup.setup_approval_status = 'APPROVED'`. The report may now Run-Now / schedule. Publish **`approval.completed`** (phase = setup).
     - **Final PRE_RUN level approved but the flow also has POST_RUN levels** (non-recurrent setup-then-result): set `report_setup.setup_approval_status = 'APPROVED'` so the report can do its **one** Final run, but keep `overall_status = 'IN_PROGRESS'` and **pause** the request — it will resume into the first POST_RUN level once the Final run produces results (see §7.4.5). Publish `approval.setup_approved`.
     - **Final POST_RUN level approved:** set `overall_status = 'APPROVED'`. Publish **`approval.completed`** (phase = results) → the DisbursementWorker is now allowed to pay out this `report_run` (BR8).
4. Audit + email as above.

#### 7.4.4 Reject / Request Changes (back to the Maker — resolves the SRS contradiction)

> The team SRS §7.4 had a contradiction: "reject goes back to the **previous level**" vs. "resubmit **restarts from the first level**." **Resolution (locked): a reject always returns the request to the Maker, not to the previous level.** The previous-level idea is dropped. A reject at *any* level terminates the current request; the Maker fixes the report and resubmits, which opens a **fresh** request that **restarts at level 1 with full re-validation**. This is simpler, audit-clean, and matches "the Maker must re-prove the whole thing after a change."

On `POST /approvals/{id}/decisions` with `decision = 'REJECTED'` or `'CHANGES_REQUESTED'`:

1. **Comment is mandatory** (BR7) — reject the call `422` if the comment is empty/whitespace. (The DB also enforces this via `ck_approval_decision_comment`.)
2. Lock the request, validate caller as in §7.4.3 step 1 (assigned to current level, not the Maker, not already decided).
3. Insert `approval_decision { level_order = current_level_order, decided_by = caller, decision, comment }`.
4. Set the request's `overall_status` to `'REJECTED'` (or `'CHANGES_REQUESTED'`) — this **terminates** the request.
5. Set `report_setup.setup_approval_status = 'REJECTED'` (or `'CHANGES_REQUESTED'`). This **re-enables Edit + Send-for-Approval** for the Maker.
6. Audit (action `REJECT`) + **email the Maker** with the level, approver, and comment. Publish `approval.rejected`.

**Resubmit:** the Maker edits the report, then calls `POST /reports/{id}/submit` again → §7.4.2 opens a **brand-new** `approval_request` starting at **level 1** with full re-validation. The old (rejected) request stays in the table as immutable history.

#### 7.4.5 POST_RUN: linking the result approval to a specific run

For a **non-recurrent** report whose flow has POST_RUN levels:

1. PRE_RUN levels approve the setup (§7.4.3 → `setup_approval_status = 'APPROVED'`, request paused, `overall_status` still `IN_PROGRESS`).
2. The Maker triggers the **one** Final run. The run completes and writes `final_commission`. The run's `report_run.id` is now known.
3. The run-completion handler **resumes the paused request**: it sets `approval_request.report_run_id = <that run id>`, advances `current_level_order` to the first POST_RUN level's order, and sets `approval_type_id` to that level's type. It emails the POST_RUN level-1 approvers and publishes `approval.result_pending`.
4. POST_RUN levels approve in order (§7.4.3). When the final POST_RUN level approves, `overall_status = 'APPROVED'` and **`approval.completed`** is published with the `report_run_id`. Only then is the DisbursementWorker permitted to read that run's `final_commission` and pay out (BR8).

> If a flow is purely PRE_RUN (recurrent reports, or non-recurrent setup-only), `report_run_id` stays `NULL` and disbursement (when configured) is gated only on `setup_approval_status = 'APPROVED'`; each recurrent scheduled run pays out automatically with no further approval.

#### 7.4.6 Edit-while-pending → void & restart

If the Maker edits a report (any wizard change, re-upload, or schedule change) **while a request is live** (`overall_status = 'IN_PROGRESS'`):

1. The current request is **cancelled**: `overall_status = 'CANCELLED'`, a system `approval_decision`-equivalent note is audited (no human decision row is forged), and `report_setup.setup_approval_status` returns to `'DRAFT'`.
2. The (re-)compiled `stages` are regenerated by the SQL Generator at Final Save.
3. The Maker must **Send for Approval** again → a fresh request restarts at level 1 with full re-validation (§7.4.2).

This guarantees approvers never approve a stale setup. (The hard backstop: editing is blocked entirely once `setup_approval_status = 'APPROVED'`; per the Reports feature §6, the Edit action is hidden on a fully-approved report.)

#### 7.4.7 Demo runs

Demo runs (`report_run.run_type = 'DEMO'`) bypass approval entirely: no `approval_request` is opened, no decision is needed, and a Demo run **never** disburses. Demo is allowed on a `DRAFT` report so the Maker can preview numbers before submitting.

#### 7.4.8 Notifications & events (summary)

| Trigger | Recipient | Channel | RabbitMQ event |
|---|---|---|---|
| Request opened (submit / resume into POST_RUN) | current-level approvers | Email | `approval.requested` / `approval.result_pending` |
| Level approved, more levels remain | next-level approvers | Email | `approval.advanced` |
| Setup fully approved (PRE_RUN done) | Maker | Email | `approval.setup_approved` |
| Whole flow approved (final level) | Maker | Email | `approval.completed` (carries `report_run_id` if POST_RUN) |
| Rejected / changes requested | **Maker** | Email | `approval.rejected` |

The **DisbursementWorker** subscribes to `approval.completed` (and the disbursement-time schedule) and only then publishes `ev_disburse` / `pos_disbursement` work — enforcing BR8.

---

### 7.5 API Endpoints

Base path `/api/v1`. All endpoints require a valid SalesCom JWT (1-day). Roles in the **Role** column: **Admin** = Administrator, **Maker** = Business User, **Checker** = Approver. The Administrator may call any endpoint. All timestamps are ISO-8601 UTC. Error responses use the standard envelope:

```json
{ "error": { "code": "VALIDATION_ERROR", "message": "human readable", "details": [ { "field": "comment", "message": "Comment is required to reject." } ] } }
```

Common status codes: `200` OK, `201` Created, `204` No Content, `400` malformed, `401` no/invalid JWT, `403` wrong role / not assigned to level / self-approval, `404` not found, `409` state conflict (e.g. already decided, not the open level), `422` business-rule violation (e.g. missing reject comment, recurrent flow has POST_RUN level).

#### A. Flow / level administration (Admin)

**`GET /approval-flows`** — list flows. Role: **Admin** (Maker may GET for the wizard dropdown, read-only). Query: `?active=true&phase=PRE_RUN&search=`.
- `200` →
```json
{ "items": [ { "id": "uuid", "flowName": "Std Setup Review", "description": "...", "isActive": true, "levelCount": 2, "phases": ["PRE_RUN"], "createdOn": "2026-06-16T08:00:00Z" } ], "total": 1 }
```

**`POST /approval-flows`** — create a flow. Role: **Admin**.
- Request: `{ "flowName": "Std Setup Review", "description": "Setup then finance" }`
- `201` → `{ "id": "uuid", "flowName": "Std Setup Review", "isActive": true }`
- `409` if `flowName` already exists (`uq_approval_flow_name`).

**`PUT /approval-flows/{id}`** — rename/describe/activate. Role: **Admin**. Request `{ "flowName?", "description?", "isActive?" }`. `200` updated flow. `409` if a deactivation would leave a report bound to no active flow (soft-blocked; warn).

**`GET /approval-flows/{id}/levels`** — list a flow's levels (ordered). Role: **Admin/Maker(read)**.
- `200` →
```json
{ "items": [ { "id": "uuid", "levelOrder": 1, "levelName": "Setup Review",
    "approvalType": { "id": "uuid", "typeName": "Setup Review", "phase": "PRE_RUN" },
    "userCount": 1 } ] }
```

**`POST /approval-flows/{id}/levels`** — add a level. Role: **Admin**.
- Request: `{ "levelName": "Finance Sign-off", "levelOrder": 2, "approvalTypeId": "uuid" }`
- `201` → the created level. `409` if `levelOrder` is taken in this flow (`uq_aflevel_order`). `422` if adding a POST_RUN level to a flow already bound to a recurrent report (V-1), or if it would break phase monotonicity (V-2).

**`PUT /approval-flows/{flowId}/levels/{levelId}`** / **`DELETE …`** — edit/remove a level. Role: **Admin**. Blocked (`409`) if any live (`IN_PROGRESS`) request references this flow.

**`GET /approval-types`** — list approval types (for the level form). Role: **Admin**. `200` → `[ { "id", "typeName", "description", "phase", "sortOrder" } ]`.

**`POST /approval-flows/{flowId}/levels/{levelId}/users`** — assign users to a level. Role: **Admin**.
- Request: `{ "userIds": ["uuid", "uuid"] }`
- `200` → `{ "levelId": "uuid", "users": [ { "userId": "uuid", "fullName": "...", "userName": "...", "email": "..." } ] }`
- `404` if a `userId` is not in `"user"`; duplicates are idempotent (`uq_aflevel_user`).

**`DELETE /approval-flows/{flowId}/levels/{levelId}/users/{userId}`** — unassign. Role: **Admin**. `204`. `422` if it would leave the level with zero users while a live request sits at that level.

#### B. Maker — submit / resubmit

**`POST /reports/{reportId}/submit`** — open an approval request for the report. Role: **Maker** (must be the report's Maker).
- Request: `{}` (the flow is already bound on the report).
- `201` →
```json
{ "approvalRequestId": "uuid", "reportSetupId": "uuid", "approvalFlowId": "uuid",
  "currentLevelOrder": 1, "phase": "PRE_RUN", "overallStatus": "IN_PROGRESS",
  "setupApprovalStatus": "PENDING_APPROVAL" }
```
- `403` caller is not the Maker. `409` a live request already exists, or report not in `DRAFT`/`CHANGES_REQUESTED`. `422` flow invalid for this report (e.g. recurrent flow has POST_RUN level; a level has no users; no compiled stages).

> Resubmit after a reject uses this same endpoint; it opens a fresh request at level 1.

#### C. Checker — act on a request

**`GET /approvals/queue`** — the current user's pending approvals (requests open at a level they're assigned to). Role: **Checker/Admin**.
- Query: `?phase=PRE_RUN|POST_RUN&search=`.
- `200` →
```json
{ "items": [ {
   "approvalRequestId": "uuid", "reportSetupId": "uuid", "reportName": "Deno June",
   "phase": "POST_RUN", "currentLevelOrder": 1, "levelName": "Finance Sign-off",
   "reportRunId": "uuid", "initiatedBy": "maker_uname", "initiatedAt": "2026-06-16T09:00:00Z",
   "resultSummary": { "channelCount": 120, "totalAmount": "4523000.0000" } } ], "total": 1 }
```
- `resultSummary`/`reportRunId` present only for POST_RUN items. The handler returns only rows where the caller is in `approval_flow_level_user` for the request's current open level **and** the caller is not `initiated_by` (BR5).

**`GET /approvals/{id}`** — full request detail (header + levels + decisions so far + current open level). Role: **Checker/Admin/Maker(own)**.
- `200` →
```json
{ "id": "uuid", "reportSetupId": "uuid", "reportName": "Deno June", "approvalFlowId": "uuid",
  "reportRunId": null, "currentLevelOrder": 2, "phase": "PRE_RUN", "overallStatus": "IN_PROGRESS",
  "initiatedBy": "maker_uname", "initiatedAt": "2026-06-16T09:00:00Z",
  "levels": [
    { "levelOrder": 1, "levelName": "Setup Review", "phase": "PRE_RUN", "status": "APPROVED",
      "decision": { "decidedBy": "checker1", "decision": "APPROVED", "comment": null, "decidedAt": "..." } },
    { "levelOrder": 2, "levelName": "Finance Sign-off", "phase": "PRE_RUN", "status": "OPEN", "decision": null } ] }
```

**`POST /approvals/{id}/decisions`** — approve / reject / request changes at the current open level. Role: **Checker** (assigned to the current level, not the Maker).
- Request:
```json
{ "decision": "APPROVED" }
```
or
```json
{ "decision": "REJECTED", "comment": "Slab table for cluster B is wrong; re-upload." }
```
- `201` →
```json
{ "approvalRequestId": "uuid", "decisionId": "uuid", "decision": "APPROVED",
  "overallStatus": "IN_PROGRESS", "currentLevelOrder": 2, "phase": "PRE_RUN",
  "advancedTo": { "levelOrder": 2, "levelName": "Finance Sign-off" } }
```
  On final approval `overallStatus` is `"APPROVED"` and `advancedTo` is `null`. On reject `overallStatus` is `"REJECTED"`/`"CHANGES_REQUESTED"`.
- `403` caller not assigned to current level, or caller is the Maker (self-approval). `409` request not `IN_PROGRESS`, caller already decided, or the supplied level no longer the open level (concurrent decision won the lock). `422` reject/changes with empty comment (BR7), or `decision` not in the enum.

**`GET /approvals/{id}/decisions`** — the immutable decision audit trail for a request. Role: **Checker/Admin/Maker(own)**.
- `200` → `[ { "levelOrder": 1, "decidedBy": "checker1", "decision": "APPROVED", "comment": null, "decidedAt": "..." }, ... ]` ordered by `level_order`, then `decided_at`.

**`GET /reports/{reportId}/approval`** — the report's current/last request + history (drives the "Approval History" UI). Role: **Maker(own)/Checker/Admin**. `200` → same shape as `GET /approvals/{id}` for the latest request, plus a `"history": [...]` array of any superseded (rejected/cancelled) requests.

> **No DELETE on requests or decisions.** Requests are terminated by status (`REJECTED`/`CANCELLED`); decisions are append-only and immutable (BR9). There is no "un-approve" — to change course the Maker edits and resubmits.

---

### 7.6 Validation and Business Rules

**Business rules (enforced both in `ApprovalService` and, where possible, by DB constraints):**

- **BR5 — Segregation of duties (Maker ≠ Checker on the same run).**
  - The **Maker** = the report's creator / last editor (`report_setup.created_by` / `updated_by`, captured as `approval_request.initiated_by` at submit).
  - A user assigned to a level **cannot decide their own submission**: every decision call checks `caller != approval_request.initiated_by` → `403`. (Self-approval blocked.)
  - **One user, one level per request:** a user who already produced an `approval_decision` for a request (at any level) cannot decide it again → `409`. This stops the same person clearing multiple levels.
  - If the Maker happens to be assigned to a level of the flow, the request simply **skips showing it to them**; it does not auto-approve, and that level still requires a *different* assigned user. (Configure flows so the Maker pool and approver pool don't overlap; the runtime guard is the backstop.)

- **BR6 — Sequential ascending.** Approvals proceed strictly by ascending `level_order`. A higher level cannot act until every lower level has approved; the request is only ever open at exactly one level (`current_level_order`). Enforced by always routing the queue/decision to `current_level_order` and advancing one step at a time.

- **BR7 — Reject needs a comment.** `decision` ∈ {`REJECTED`,`CHANGES_REQUESTED`} requires a non-empty, non-whitespace `comment`. Enforced in the handler (`422`) **and** by DB `ck_approval_decision_comment` (`decision = 'APPROVED' OR comment is non-empty`).

- **BR8 — Disburse only after full approval.** Disbursement reads, for the run, the `approval_request` with matching `report_run_id` and requires `overall_status = 'APPROVED'` (POST_RUN flows). For PRE_RUN-only flows it requires `report_setup.setup_approval_status = 'APPROVED'`. The DisbursementWorker is only triggered by `approval.completed` + the configured disbursement time.

- **BR9 — Everything audited.** Every submit, approve, reject, changes-request, cancel, and flow/level/user admin change writes an `audit_log` row (`action` ∈ `APPROVE`/`REJECT`/`OTHER`). Decisions are also permanently stored in `approval_decision` (append-only).

**Validation rules:**

- **V-1 (recurrent ⇒ PRE_RUN-only):** if `report_setup.is_recurrent = TRUE`, the bound flow must have **no** `POST_RUN` level — checked at flow-bind, at submit, and when an admin adds a POST_RUN level to a flow already used by a recurrent report → `422`. Rationale: scheduled runs can't wait for a human per run.
- **V-2 (phase monotonicity):** within a flow, all `PRE_RUN` levels must come **before** all `POST_RUN` levels (ascending `level_order`). You cannot interleave (e.g. PRE, POST, PRE). The request walks PRE_RUN levels first (gating setup), then — for a non-recurrent report after its Final run — the POST_RUN levels (gating results). Checked when adding/reordering levels → `422`.
- **V-3 (flow must be actionable):** at submit, the flow must be `is_active`, have ≥ 1 level, and every level must have ≥ 1 assigned user; else `422`.
- **V-4 (single live request):** a report may have at most one `approval_request` with `overall_status = 'IN_PROGRESS'` at a time. A second submit while one is live → `409`. (Rejected/cancelled requests remain as history.)
- **V-5 (open-level decision only):** a decision is only valid at the request's `current_level_order`; the handler re-reads it under the row lock, so a stale "approve level N" loses to a concurrent decision that already advanced past N → `409`.
- **V-6 (POST_RUN needs a run):** the request only enters POST_RUN after a Final run exists; the resume step sets `report_run_id` before opening the first POST_RUN level. A POST_RUN request with `report_run_id IS NULL` is an invalid state and is rejected by the resume logic.
- **V-7 (edit voids approval):** any edit while a request is `IN_PROGRESS` cancels it (`CANCELLED`) and resets `setup_approval_status` to `DRAFT`; editing a fully-approved report is blocked upstream (the Edit action is hidden once `setup_approval_status = 'APPROVED'`).
- **V-8 (Demo bypass):** Demo runs never open a request and never disburse; the submit/decision endpoints have no effect on Demo runs.

**Concurrency:** all decision and resume operations take `SELECT ... FOR UPDATE` on the `approval_request` row inside one transaction, so simultaneous approvers (or an approver vs. an edit/cancel) are serialized and the request advances exactly once per accepted decision.
