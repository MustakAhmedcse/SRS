# SalesCom — IR Specification & Multi-KPI Join Rule

> **LLD Annex.** This document defines `report_setup.definition` (JSONB) — the **IR (Intermediate Representation)**, the no-code report configuration that is the heart of SalesCom. The 5-step wizard *produces* the IR; at **Final Save** the SQL Generator *compiles* the IR into SQL (stored in `stages`); at run time the Executor runs that SQL stage-by-stage and writes `final_commission`.
>
> Worked example = the real campaign **"RSO Campaign GA Recharge LSO Apr26"** (2 achievement KPIs → category → amount).
> Version: IR v1.0. Audience: FE, BE (.NET), and Calc-engine (Python) developers.

---

## 0. Mental model (read this first)

A report is built from **blocks**. There are two kinds, but they have the **same shape**:
- **Achievement block** — computes a performance number per recipient (e.g. Recharge %, GA %).
- **Incentive block** — turns achievement(s) into a payout per recipient.

Each block is a **pipeline of stages** (Filter → Combine → Summarize → Calculate → Modify). A block reads from a **source** (a registered data source, an uploaded CSV, or *another block*). The last `summarize` in a block fixes the block's **grain** (e.g. "1 row per RSO"). Blocks are then **joined on that grain key** to combine KPIs (this is how multi-KPI works — see §6).

```
report
 ├─ achievements: [ ACH1 (Recharge %), ACH2 (GA %) ]   ← each = pipeline, grain = RSO
 ├─ incentives:   [ INC1 (joins ACH1+ACH2 → Category → Amount) ]
 └─ final_mapping: INC1 → (channel_code = RSO, commission_amount = Incentive)
```

---

## 1. Top-level IR structure

```json
{
  "ir_version": "1.0",
  "report": {
    "name": "RSO Campaign GA Recharge LSO Apr26",
    "channel": "RSO",
    "commission_cycle": "Apr 2026",
    "start_date": "2026-04-01",
    "end_date": "2026-04-30"
  },
  "achievements": [ /* Block, Block, ... */ ],
  "incentives":   [ /* Block, Block, ... */ ],
  "final_mapping": { /* see §5 */ }
}
```

---

## 2. Block

```json
{
  "block_id": "ACH1",                 // unique within the report (ACH1, ACH2, INC1...)
  "name": "Recharge Achievement",
  "source": { "type": "data_source", "ref": "ev_lifting_daily_com" },
  "stages": [ /* Stage, Stage, ... in order */ ],
  "output_grain": ["RSO_CODE"],        // set by the block's final summarize (§6 rule)
  "outputs": [                          // declared output columns (for validation + UI preview)
    { "name": "RSO_CODE",        "type": "text" },
    { "name": "TARGET",          "type": "numeric" },
    { "name": "ACHIEVEMENT",     "type": "numeric" },
    { "name": "RE_PCT",          "type": "numeric" }
  ]
}
```

`source.type` ∈ `data_source` (registered) | `upload` (Step-2 CSV → table) | `block` (another block's output).

**Column references inside a block:** bare name (`ACHIEVEMENT`, `TARGET`).
**Column references across blocks** (in `combine.match_on` / `bring` when joining a block): qualified `BLOCK_ID.COLUMN` (`ACH1.RSO_CODE`, `ACH2.GA_PCT`).

---

## 3. Stages (the operation set)

Every stage is `{ "op": "<type>", ... }`. The engine is built as a **growing list of operation types** — Phase 1 ships the ops below; Phase 2/3 *add* ops (§7) without changing this structure.

### 3.1 `filter` — keep rows that match
```json
{ "op": "filter", "combine": "AND",
  "conditions": [
    { "column": "FCD",          "operator": "between",    "value": ["2026-04-01","2026-04-30"] },
    { "column": "CHANNEL_TYPE", "operator": "is_one_of",  "value": ["D2RT","R2RT","SUD12AGENT"] },
    { "column": "SERVICE_CLASS_ID", "operator": "gte",    "value": 1000, "negate": true }
  ]
}
```
`operator` ∈ `equals, not_equals, gt, lt, gte, lte, between, is_one_of, not_in, is_null, not_null`. `combine` ∈ `AND, OR`. `negate` optional. (Exclusions like Ryze SC141 / Postpaid SC≥1000 / employee-pool are just filter conditions or a `combine` against an uploaded exclusion list.)

### 3.2 `combine` — join in more data
```json
{ "op": "combine",
  "join_with": { "type": "upload", "ref": "temp_for_rso_agent" },
  "how": "inner",
  "match_on": [ { "left": "receiver_number", "operator": "=", "right": "ret_msisdn" } ],
  "bring": ["RSO_CODE","RECHARGE_TARGET","SEND_AMOUNT"]
}
```
`join_with.type` ∈ `data_source | upload | block`. `how` ∈ `inner | left | right | full`.
⚠️ When `join_with.type == "block"`, the join **must** be on the grain key — see §6.

### 3.3 `summarize` — aggregate (this stage sets the block grain)
```json
{ "op": "summarize",
  "group_by": ["RSO_CODE"],
  "aggregations": [
    { "result_column": "ACHIEVEMENT", "calc": "sum", "source_column": "SEND_AMOUNT" }
  ],
  "filter": null
}
```
`calc` ∈ `count | count_distinct | sum | avg | min | max`. `group_by` = the block's **grain**.

### 3.4 `calculate` — derive a new column
Three modes:

**(a) formula** — safe math expression:
```json
{ "op": "calculate", "result_column": "RE_PCT", "mode": "formula",
  "formula": "round( (ACHIEVEMENT / nullif(TARGET,0)) * 100 )" }
```
Allowed in `formula`: `+ - * /`, parentheses, numbers, column names, and functions `round, floor, ceil, abs, least, greatest, coalesce, nullif`. Anything else is rejected by the validator. (`least(x, 200)` = capping at 200%; `nullif(TARGET,0)` = div-by-zero guard.)

**(b) ifcase** — slab / category / tier logic:
```json
{ "op": "calculate", "result_column": "Category", "mode": "ifcase", "else": null,
  "cases": [
    { "combine": "AND",
      "when": [ { "column": "GA_PCT", "operator": "gte", "value": 100 },
                { "column": "RE_PCT", "operator": "gte", "value": 100 } ],
      "then": "Platinum" },
    { "combine": "AND",
      "when": [ { "column": "GA_PCT", "operator": "gte", "value": 95 },
                { "column": "RE_PCT", "operator": "gte", "value": 95 } ],
      "then": "Gold" }
  ]
}
```
Cases evaluated top-to-bottom (first match wins). `then` is a literal or `{ "column": "X" }`. `else` is the fallback (often `0` for amounts).

**(c) map** — round/cast a column for the final output:
```json
{ "op": "calculate", "result_column": "Incentive_final", "mode": "map",
  "map": { "value": "Incentive", "decimals": 0, "rounding": "half_up" } }
```

### 3.5 `modify` — cast / rename a column before passing on
```json
{ "op": "modify", "changes": [ { "column": "RSO_CODE", "cast": "text" } ] }
```

### 3.6 `rank` — **Phase 3** (top-N% / quartile)
```json
{ "op": "rank", "partition_by": ["DH_CODE"], "order_by": [{ "column":"ACHIEVEMENT","dir":"desc" }],
  "method": "ntile", "buckets": 4, "result_column": "QUARTILE" }
```
Compiles to a PostgreSQL window function (`NTILE(4) OVER (PARTITION BY dh_code ORDER BY achievement DESC)`). `method` ∈ `ntile | rank | row_number | percent_rank`. *Not built in Phase 1 — listed so the IR shape is final now.*

---

## 4. final_mapping (produces `final_commission`)

```json
{
  "from_block": "INC1",
  "channel_code_column": "RSO_CODE",
  "commission_amount_column": "Incentive",
  "channel_scope": "RSO"
}
```
The engine groups `from_block` per `channel_code_column` and writes one `final_commission` row per channel (`channel_code`, `commission_amount`). **Money rules:** `commission_amount` is `NUMERIC(18,4)` internally, rounded to 2 dp half-up at write; a null / unmapped / duplicate channel_code = hard validation error → run fails (no partial disbursement).

---

## 5. §2-Rule — Multi-KPI by joining sections (the safe pattern)

This is how multi-KPI commissions are built — **and how double-paying is prevented.**

**The rule (the engine enforces it):**
1. **Every block ends with a `summarize`** → this fixes the block's **grain** (`output_grain` = the `group_by`). Example: ACH1 and ACH2 are both summarized to **1 row per RSO_CODE**.
2. **A block-to-block join (`combine` with `join_with.type == "block"`) must match on the grain key** of both blocks → it is **1 row ↔ 1 row** (no fan-out, no double count).
3. **Raw-data joins happen inside a block, before its summarize** → any fan-out is absorbed by the aggregation that follows.

**Why this is safe:** double-counting happens only when a "1 row per RSO" value is joined to a "many rows per RSO" table and then summed. By forcing *summarize → then key-join*, the dangerous case cannot occur.

**Engine guardrails (automatic — implement these):**
- **G1 — pre-join uniqueness check:** before a block-to-block join, verify the grain key is **UNIQUE** on each block's output. If not unique → block the run with a clear error ("join key RSO_CODE is not unique in ACH2 — this may double-count").
- **G2 — post-join fan-out check:** after any join, if the row count exceeds the expected max, flag it.
- **G3 — demo-run visibility:** a Demo Run shows the **row count after every stage**; the Maker sees any unexpected multiplication *before* disbursement.
- **G4 — reconciliation:** final disbursed total must equal the sum of the `from_block` output amounts.

> Net: the user keeps full flexibility (any columns, any KPIs as separate sections), but the engine is **grain-aware** — so "letting users join freely" is safe.

---

## 6. Phase mapping (build order; IR shape is final now)

| Op / capability | Phase |
|---|---|
| `filter`, `combine` (data/upload), `summarize`, `calculate` (formula + ifcase + map), `modify`, block-to-block join, `final_mapping`, guardrails G1–G4 | **Phase 1** (single + multi-KPI) |
| weighted multi-KPI pool, gate-zeroes-component, VLR penalty case, external-config-driven advanced slabs | **Phase 2** |
| `rank` (window functions), historical/cohort source reads, period-versioned rate lookup, cumulative-deduction | **Phase 3** |

The **data model and IR structure do not change between phases** — Phase 2/3 only *add operation types and source kinds*. This is what makes "do Phase 1 well → little left later" true.

---

## 7. Formal JSON Schema (draft 2020-12, abridged but usable)

```json
{
  "$schema": "https://json-schema.org/draft/2020-12/schema",
  "title": "SalesCom Report IR",
  "type": "object",
  "required": ["ir_version", "report", "achievements", "incentives", "final_mapping"],
  "properties": {
    "ir_version": { "const": "1.0" },
    "report": {
      "type": "object",
      "required": ["name","channel","start_date","end_date"],
      "properties": {
        "name": { "type": "string" },
        "channel": { "type": "string" },
        "commission_cycle": { "type": "string" },
        "start_date": { "type": "string", "format": "date" },
        "end_date": { "type": "string", "format": "date" }
      }
    },
    "achievements": { "type": "array", "items": { "$ref": "#/$defs/block" }, "minItems": 1 },
    "incentives":   { "type": "array", "items": { "$ref": "#/$defs/block" } },
    "final_mapping": { "$ref": "#/$defs/finalMapping" }
  },
  "$defs": {
    "block": {
      "type": "object",
      "required": ["block_id","source","stages","output_grain"],
      "properties": {
        "block_id": { "type": "string", "pattern": "^(ACH|INC)[0-9]+$" },
        "name": { "type": "string" },
        "source": { "$ref": "#/$defs/source" },
        "stages": { "type": "array", "items": { "$ref": "#/$defs/stage" }, "minItems": 1 },
        "output_grain": { "type": "array", "items": { "type": "string" }, "minItems": 1 },
        "outputs": { "type": "array", "items": {
          "type": "object", "required": ["name","type"],
          "properties": { "name": {"type":"string"},
            "type": {"enum":["text","numeric","integer","date","boolean"]} } } }
      }
    },
    "source": {
      "type": "object", "required": ["type","ref"],
      "properties": { "type": { "enum": ["data_source","upload","block"] }, "ref": { "type":"string" } }
    },
    "condition": {
      "type": "object", "required": ["column","operator"],
      "properties": {
        "column": { "type": "string" },
        "operator": { "enum": ["equals","not_equals","gt","lt","gte","lte","between","is_one_of","not_in","is_null","not_null"] },
        "value": {},
        "negate": { "type": "boolean" }
      }
    },
    "stage": {
      "type": "object",
      "required": ["op"],
      "oneOf": [
        { "properties": { "op": { "const": "filter" },
            "combine": { "enum": ["AND","OR"] },
            "conditions": { "type": "array", "items": { "$ref": "#/$defs/condition" } } },
          "required": ["op","conditions"] },
        { "properties": { "op": { "const": "combine" },
            "join_with": { "$ref": "#/$defs/source" },
            "how": { "enum": ["inner","left","right","full"] },
            "match_on": { "type": "array", "items": {
              "type":"object","required":["left","operator","right"],
              "properties": { "left":{"type":"string"},
                "operator":{"enum":["=","!=",">","<",">=","<="]}, "right":{"type":"string"} } } },
            "bring": { "type": "array", "items": { "type": "string" } } },
          "required": ["op","join_with","how","match_on"] },
        { "properties": { "op": { "const": "summarize" },
            "group_by": { "type": "array", "items": { "type": "string" }, "minItems": 1 },
            "aggregations": { "type": "array", "items": {
              "type":"object","required":["result_column","calc"],
              "properties": { "result_column":{"type":"string"},
                "calc":{"enum":["count","count_distinct","sum","avg","min","max"]},
                "source_column":{"type":"string"} } } } },
          "required": ["op","group_by","aggregations"] },
        { "properties": { "op": { "const": "calculate" },
            "result_column": { "type": "string" },
            "mode": { "enum": ["formula","ifcase","map"] },
            "formula": { "type": "string" },
            "cases": { "type": "array", "items": {
              "type":"object","required":["when","then"],
              "properties": { "combine":{"enum":["AND","OR"]},
                "when": { "type":"array","items": { "$ref":"#/$defs/condition" } }, "then": {} } } },
            "else": {},
            "map": { "type":"object",
              "properties": { "value":{"type":"string"}, "decimals":{"type":"integer"},
                "rounding":{"enum":["half_up","half_even","down","up"]} } } },
          "required": ["op","result_column","mode"] },
        { "properties": { "op": { "const": "modify" },
            "changes": { "type":"array","items": {
              "type":"object","required":["column"],
              "properties": { "column":{"type":"string"},
                "cast":{"enum":["text","numeric","integer","date","boolean"]},
                "rename_to":{"type":"string"} } } } },
          "required": ["op","changes"] },
        { "properties": { "op": { "const": "rank" },
            "partition_by": { "type":"array","items":{"type":"string"} },
            "order_by": { "type":"array","items": {
              "type":"object","required":["column"],
              "properties": { "column":{"type":"string"}, "dir":{"enum":["asc","desc"]} } } },
            "method": { "enum": ["ntile","rank","row_number","percent_rank"] },
            "buckets": { "type":"integer" },
            "result_column": { "type":"string" } },
          "required": ["op","order_by","method","result_column"] }
      ]
    },
    "finalMapping": {
      "type": "object",
      "required": ["from_block","channel_code_column","commission_amount_column"],
      "properties": {
        "from_block": { "type": "string" },
        "channel_code_column": { "type": "string" },
        "commission_amount_column": { "type": "string" },
        "channel_scope": { "type": "string" }
      }
    }
  }
}
```

---

## 8. Worked example — full IR for "RSO Campaign GA Recharge LSO Apr26"

This is the exact campaign from the `RSO_Report_Detail_HandDrawn.html` mockup, written as a complete, valid IR. (Supporting uploads used: `TARGET_RSO_BUNDLE_APR26`, `RSO_WISE_LSO_DETAILS`, etc. — registered as data sources in Step 2.)

```json
{
  "ir_version": "1.0",
  "report": {
    "name": "RSO Campaign GA Recharge LSO Apr26",
    "channel": "RSO",
    "commission_cycle": "Apr 2026",
    "start_date": "2026-04-01",
    "end_date": "2026-04-30"
  },

  "achievements": [
    {
      "block_id": "ACH1",
      "name": "Recharge Achievement",
      "source": { "type": "data_source", "ref": "ev_lifting_daily_com" },
      "stages": [
        { "op": "filter", "combine": "AND",
          "conditions": [
            { "column": "RECHARGE_DATE", "operator": "between", "value": ["2026-04-01","2026-04-30"] },
            { "column": "CHANNEL_TYPE",  "operator": "is_one_of", "value": ["D2RT","R2RT","SUD12AGENT"] }
          ] },
        { "op": "combine",
          "join_with": { "type": "upload", "ref": "temp_for_rso_agent" },
          "how": "inner",
          "match_on": [ { "left": "receiver_number", "operator": "=", "right": "ret_msisdn" } ],
          "bring": ["RSO_CODE","RECHARGE_TARGET","SEND_AMOUNT"] },
        { "op": "summarize",
          "group_by": ["RSO_CODE"],
          "aggregations": [
            { "result_column": "ACHIEVEMENT", "calc": "sum", "source_column": "SEND_AMOUNT" },
            { "result_column": "TARGET",      "calc": "max", "source_column": "RECHARGE_TARGET" }
          ] },
        { "op": "calculate", "result_column": "RE_PCT", "mode": "formula",
          "formula": "round( (ACHIEVEMENT / nullif(TARGET,0)) * 100 )" }
      ],
      "output_grain": ["RSO_CODE"],
      "outputs": [
        { "name": "RSO_CODE",    "type": "text" },
        { "name": "TARGET",      "type": "numeric" },
        { "name": "ACHIEVEMENT", "type": "numeric" },
        { "name": "RE_PCT",      "type": "numeric" }
      ]
    },

    {
      "block_id": "ACH2",
      "name": "GA Achievement",
      "source": { "type": "data_source", "ref": "simrepository" },
      "stages": [
        { "op": "filter", "combine": "AND",
          "conditions": [
            { "column": "FCD",          "operator": "between",   "value": ["2026-04-01","2026-04-30"] },
            { "column": "SERVICE_TYPE", "operator": "is_one_of", "value": ["PREPAID","MNP_REGISTRATION"] },
            { "column": "RETAILER_TYPE","operator": "is_one_of", "value": ["DRC"], "negate": true }
          ] },
        { "op": "combine",
          "join_with": { "type": "upload", "ref": "temp_rso_tar_camp_bundle" },
          "how": "inner",
          "match_on": [ { "left": "RETAILER_CODE", "operator": "=", "right": "retailer_code" } ],
          "bring": ["RSO_CODE","GA_TARGET","MSISDN"] },
        { "op": "summarize",
          "group_by": ["RSO_CODE"],
          "aggregations": [
            { "result_column": "ACHIEVEMENT", "calc": "count_distinct", "source_column": "MSISDN" },
            { "result_column": "TARGET",      "calc": "max",            "source_column": "GA_TARGET" }
          ] },
        { "op": "calculate", "result_column": "GA_PCT", "mode": "formula",
          "formula": "round( (ACHIEVEMENT / nullif(TARGET,0)) * 100 )" }
      ],
      "output_grain": ["RSO_CODE"],
      "outputs": [
        { "name": "RSO_CODE",    "type": "text" },
        { "name": "TARGET",      "type": "numeric" },
        { "name": "ACHIEVEMENT", "type": "numeric" },
        { "name": "GA_PCT",      "type": "numeric" }
      ]
    }
  ],

  "incentives": [
    {
      "block_id": "INC1",
      "name": "Categorisation Incentive",
      "source": { "type": "block", "ref": "ACH1" },
      "stages": [
        { "op": "combine",
          "join_with": { "type": "block", "ref": "ACH2" },
          "how": "inner",
          "match_on": [ { "left": "ACH1.RSO_CODE", "operator": "=", "right": "ACH2.RSO_CODE" } ],
          "bring": ["ACH2.GA_PCT"] },

        { "op": "calculate", "result_column": "Category", "mode": "ifcase", "else": "None",
          "cases": [
            { "combine": "AND",
              "when": [ { "column": "GA_PCT", "operator": "gte", "value": 100 },
                        { "column": "RE_PCT", "operator": "gte", "value": 100 } ],
              "then": "Platinum" },
            { "combine": "AND",
              "when": [ { "column": "GA_PCT", "operator": "gte", "value": 95 },
                        { "column": "RE_PCT", "operator": "gte", "value": 95 } ],
              "then": "Gold" },
            { "combine": "AND",
              "when": [ { "column": "GA_PCT", "operator": "lt", "value": 95 },
                        { "column": "RE_PCT", "operator": "lt", "value": 95 } ],
              "then": "Silver" }
          ] },

        { "op": "calculate", "result_column": "Incentive", "mode": "ifcase", "else": 0,
          "cases": [
            { "when": [ { "column": "Category", "operator": "equals", "value": "Platinum" } ], "then": 8000 },
            { "when": [ { "column": "Category", "operator": "equals", "value": "Gold" } ],     "then": 7000 },
            { "when": [ { "column": "Category", "operator": "equals", "value": "Silver" } ],   "then": 6000 }
          ] }
      ],
      "output_grain": ["RSO_CODE"],
      "outputs": [
        { "name": "RSO_CODE",  "type": "text" },
        { "name": "RE_PCT",    "type": "numeric" },
        { "name": "GA_PCT",    "type": "numeric" },
        { "name": "Category",  "type": "text" },
        { "name": "Incentive", "type": "numeric" }
      ]
    }
  ],

  "final_mapping": {
    "from_block": "INC1",
    "channel_code_column": "RSO_CODE",
    "commission_amount_column": "Incentive",
    "channel_scope": "RSO"
  }
}
```

**Why this example is safe (the §6 rule in action):** ACH1 and ACH2 both `summarize` to grain `RSO_CODE` (1 row per RSO). INC1 joins ACH1↔ACH2 on `RSO_CODE` (block-to-block, 1:1) → no fan-out. Guardrail **G1** checks `RSO_CODE` is unique in both before the join.

---

## 9. How the engine compiles the IR (brief — full IR→SQL is a separate annex)

- Each block compiles to **one or more SQL stages**, materialized as temp tables (`output_table_name`), in `stages` order. A `summarize` becomes `GROUP BY`; `filter` → `WHERE`; `combine` → `JOIN`; `calculate/formula` → a `SELECT` expression; `calculate/ifcase` → `CASE WHEN … THEN … END`; `rank` → a window function.
- A block whose `source.type == "block"` reads the referenced block's output temp table.
- The SQL is built with **SQLGlot (AST builder, not string concat)** and validated before execution: only `SELECT/WITH/JOIN/aggregate/CASE` (+ `CREATE/DROP TEMP TABLE`), all literals bound, all identifiers checked against the snapshot source schema. (This is decision **D2**.)
- The Executor runs as a least-privilege role; `final_commission` is written by a trusted system path, never by generated SQL.

## 10. Implementation checklist for the dev team

- [ ] Validate every saved IR against the §7 JSON Schema (reject on Final Save if invalid).
- [ ] Validate column references resolve (a referenced column exists in the prior stage / joined block output).
- [ ] Enforce the §6 rule: each block ends in a `summarize`; block-to-block joins are on the grain key; run guardrails G1–G4.
- [ ] Build the Phase-1 operation set only (§6 table); leave `rank` and Phase-2/3 ops unimplemented but schema-valid.
- [ ] Keep `ir_version` — migrate IRs forward when the shape evolves.
