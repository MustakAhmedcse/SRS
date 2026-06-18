# SalesCom Commission Logic Catalog

**Audience:** SalesCom developers building the calc engine + IR (report-definition JSON) compiler.
**Purpose:** Turn 34 real commission campaign types into (a) a unified vocabulary of incentive/exclusion patterns, (b) the concrete calc-engine capabilities those patterns require, (c) a pattern → IR-pipeline-stage mapping, and (d) runnable IR examples a new developer can copy and adapt.
**Status:** Derived from the per-type KPI summaries (`_kpi_txt/<TYPE>.txt`), verified against source for the worked examples (Deno_Campaign, Campaign_Somridi, BSP_CCR_KPI, Distribution_ROI, Campaign_Win_Together). All targets, amounts, slab tables, agent lists and category assignments are **B2C-supplied per campaign** unless stated; the engine ingests them as external config — it must never hard-code them.

---

## 0. How to read this catalog

- **Pattern codes** (e.g. `PAT-NEARMISS`, `EXC-RYZE`) are catalog-internal. The Deno_Campaign folder already ships a real code library (`HIT-01..07`, `RCH-01..03`, `RSO-01/02`, `RET-01`, `EXC-01..07`) — that folder is the canonical "pattern library" and the worked IR example #1 below is built directly from it.
- **IR pipeline recap.** A report = one or more *Achievement* blocks + one or more *Incentive* blocks. Each block runs stages in order:
  1. **Filter** — `column op value` (exclusion / eligibility).
  2. **Combine Data** — join (target file / agent-map / category list / DRC list / retailer list).
  3. **Summarize** — `Count / Sum / Avg / Min / Max` + `group by`.
  4. **Calculate** — math formula (`achievement% = achieved/target*100`) **and** IF/CASE slab (`When col op val And … Then val`).
  5. **Modify** — cast / transform.
  - Final **Mapping** — per-channel `channel_code` + `commission_amount`.
- Every numeric in the IR examples is a placeholder for a **config-bound** value; the `_meta.config_refs` block names the external file each value comes from.

---

## 1. Per-type catalog (34 types)

Format per type: **Recipients · KPI types · Incentive structures · Key exclusions · Unique calc requirement**.

### Batch 1

**1. BCL — Banglalink Champions League** (points-based ranking, 32 SRFs)
- Recipients: Cluster, Region, MDO, ZM/TO/SZM (recognition tiers, National Top-N).
- KPIs: RCH, GA, GAP (GA productivity), BSO/LSO/SSO, BND (bundle, phased out).
- Incentive: **points-per-KPI ranking** (weights in B2C "Detail Format"); winner = highest total points (NOT cash/unit). Q4'25 GA-Productivity bonus/penalty points (−2..+5). Post-Gate Averaging (Q1'26).
- Exclusions: DRC EV Secondary, Monobrand, BL Franchise Store retailers, disconnected SIMs, cross-border GA.
- **Unique:** only points/recognition campaign; two gate types (Campaign Eligibility + Point Eligibility, 100% of Slab-1); tie-break = higher % Retail Recharge; **period-dependent rounding** (Sep25 round vs Oct25+ 3-decimal no-round).

**2. BL_WIFI — FWA Device Installation** (17 SRFs)
- Recipients: Distributor (per-install + recharge bonus), RSO (premium per-install), Cluster/Region/SZM (GIFT).
- KPIs: FWA-INSTALL (integer count), follow-on Recharge (Distributor, Feb26+).
- Incentive: flat **BDT 166.6667/install** (Distributor), **BDT 277.7778/install** (RSO), no minimum; +10% of next-month recharge plan (retention). Recognition tiers = min-device gate → rank → Top-N GIFT.
- Exclusions: General Conditions almost entirely unchecked (only Product = Prepaid;FWA).
- **Unique:** pure count-based new-product campaign; fractional rates from BDT 500 base; B2C computes performance, IT only applies incentive.

**3. BL_Power — Digital Recharge Platform** (34 SRFs, categories A–M)
- Recipients: all/segmented retailers, self-retailers, DBPs, BDOs, BL employees, bKash Digital Distributor.
- KPIs: deno-hit recharge, hourly/weekend windowed recharge, monthly-accumulated slab, Add Balance (flat + %), New Customer Acquisition.
- Incentive: per-hit BDT on exact deno + time window (peak vs standard); flat-target; **% of add-balance (1.470664%)**; monthly highest-slab; **binary all-or-nothing** (BDO 30k→2k); NCA dual-rate (first recharge 50 vs 300) per-account slab, 250-account cap.
- Exclusions: exact-deno filter, time-window filter, ETSAF gate (DBP Jan26), valid EV/i-TOP only.
- **Unique:** most diverse (13 categories); **EV Platform** source (not DWH); AIT-aware (disburse excl-AIT); once-per-retailer date-lock cap.

**4. BL_Self_Channel — MyBL App Referral** (26 SRFs)
- Recipients: Device BPs (approved list), Monoband Agents (all active).
- KPI: successful MyBL referral (unique code + min 100MB usage within 7/8 days next month).
- Incentive: flat **BDT 10/referral**, no tier/cap/AIT. Customer reward 1GB (not agent commission).
- Exclusions: referral-code filter, new-user filter, Data/EV SIM excluded.
- **Unique:** 4-step usage-gated trigger; Prepaid AND Postpaid; validation data from IT-DSS; 7th→8th deadline shift.

**5. BP_GA — BP GA Campaign** (15 SRFs)
- Recipients: BP (Brand Promoters); RSO (report-extractor only).
- KPI: GA (FCD-based), achievement% = Actual/Target×100.
- Incentive: physical-gift slabs (Aug, dual weekly+monthly) → voucher phases (Nov-Jan) → **cash + AIT (Feb+)**; Booster binary 40GA→1500; Apr Phase2 makeup (combined − already-paid).
- Exclusions: Data/EV SIM, TCG/self-barred, agent-list END-DATE date-lock.
- **Unique:** hard **DUAL gate** (48hr-usage% AND VLR-active%) zeroes incentive; only prorated VLR (Apr North 75-85%); only Phase1+2 makeup with deduction.

**6. BP_Variable — BP Variable Incentive** (18 SRFs)
- Recipients: standard BPs (Regular/LUS), Freelancer BPs (Central), MDO/TechVan BPs.
- KPIs: KPI1 GA marginal-rate; KPI2 MyBL+Toffee referral; KPI3 48hr quality; KPI4 outlet coverage; KPI5 FR-qualified GA; KPI6 daily productivity; TechVan handset+GA slab.
- Incentive: **marginal BDT/GA on additional GA beyond monthly target** (fixed salary covers first N); heavily segmented range/floor/rate (Regular/LUS → Core/Emerging → New/Existing → West-special; Central BDT 120 premium). VLR 3-tier payout (full / "VLR Active GA only" / none).
- Exclusions: Data SIM, disconnected, TechVan needs BL-SIM tag.
- **Unique:** only true marginal/variable-rate GA campaign; multi-dimensional segmentation; separate TechVan physical-device slab.

### Batch 2

**7. BSP_CCR_KPI** — weighted-gate variable-pay (worked example #4)
- Recipients: BSP CCR; Monobrand CCR (handset SRF).
- KPIs: GA (+First Recharge), FVR, Stock, App install, Daily Participation, CSAT, handset units.
- Incentive: **base BDT 4,000 × Σ(KPI payout% × weight)**; per-gate independent threshold (below = 0% that gate); weights/thresholds evolve Apr25 (2-KPI, 50%) → Oct25/Jan26 (6-KPI, 100%); outperformance bands (Jan26 95/110/120%); separate flat handset SRF (Spark 40=500, GO3=400).
- Exclusions: ETSAF 98% by 7th, valid EV recharge, dual-app referral-code gate, Experience Center CCRs excluded from handset.
- **Unique:** FVR (unique-vs-total work code), CSAT (>97% min 20 feedback, Platinum&Signature vs Overall split); two calc models (weighted-gate + flat-per-unit) in one type.

**8. BSP_Retailer_GA**
- Recipients: BSP CCR/Retailer (retailer code), MMSTR.
- KPIs: GA (RYZE-only/BL/All by type), First Recharge, VLR, 48hr usage, daily participation, app install, pack purchase.
- Incentive: Type1 **absolute-GA-count slab** (RYZE, no target %: 15-29→50, 30-59→70, 60-100→100); Type2 monthly %-ach slab; Type3 **weekly per-GA flat** (MMSTR 20, others 30).
- **Unique:** absolute-count slab + **weekly cumulative-deduction** (each weekly SRF covers full month-to-date, subtracts prior week); Jun25 multi-modality (upfront + per-pack conditional + daily participation); AIT exempt some types, applicable others.

**9. Campaign_Somridi** — %-of-recharge pool (worked example #2)
- Recipients: Distributors/DH; sub-officers (deduction); Non-GA DH (CHICOX09/11).
- KPIs: Recharge, GA, VLR, GA Productivity, Bundle, Active LSO, BSO/SSO, DD/Retail SCR, Growth KPI.
- Incentive: **incentive pool = % of total Recharge** (Gold 0.6→0.7%, Platinum overlay 0.2→0.3%, Unified 1.0%); actual payout = Σ weighted-KPI achievement; Recharge fail → C2C+SC **higher-of-cumulative-vs-individual** fallback.
- Exclusions: DRC, employee pool, ETSAF 98%, active EV SIM (Jan26+), No-Play 93%.
- **Unique:** %-of-recharge pool; Platinum = bonus overlay reading Gold data; day-normalized Growth KPI; Best-N-days productivity; BSO/SSO/LSO shortfall-deduction.

**10. Campaign_Win_Together** — selective-retail dual-KPI (informs worked example #3)
- Recipients: Distributors; selective retailers (B2C-curated per DD).
- KPIs: C2S/C2C Volume; **Winning Retail** (count of selective retailers hitting their own target).
- Incentive: two independent slab KPIs on % achievement; cap 120% (11 Jan26+); amounts = distributor-wise total campaign incentive × slab fraction.
- Exclusions: selective-list scope only; Recharge & Product ticked active, Usage/SAF/Disconnection reference-only.
- **Unique:** "Winning Retail" count KPI (retailers hitting individual targets); decadal 10-day cycle; parallel C2S vs C2C cluster-split SRF same dates; ticked-active vs reference-only condition distinction.

**11. Deno_Campaign** — THE reference pattern library (worked example #1)
- Recipients: DD, RSO, Retailer, A/B/C/D category DDs.
- KPIs: Deno HIT (count), Deno Recharge (value), Bundle (HIT-or-Miss + points), Participation.
- Incentive: full taxonomy — `HIT-01..07` (flat 1/2/3-tier near-miss; %-slab 3/5-tier; multi-cluster differential; binary + A/B/C/D physical prize), `RCH-01..03` (%-of-recharge slab; +cluster bonus; points contest), `RSO-01/02`, `RET-01` (min N HITs → flat BDT 50).
- Exclusions: `EXC-01..07` (Ryze 141; Postpaid >=1000; employee pool; cross-cluster >10%; capping 200/300%; agent-list date-lock; rounding).
- **Unique:** richest single taxonomy — nearly every slab style + 3 recipient tiers + 300% cap exception in one folder.

**12. Device_Retail_Chain** — single-recipient weighted-KPI (SSE)
- KPIs: GA (+First Recharge), Recharge&Bundle (>=40% data/mix), Daily Participation, App, Attendance, FWA Home Internet (Jan26).
- Incentive: base BDT 4,000 weighted pool; May/Jul25 pro-rata → Oct25 tiered slabs → Jan26 **outperformance bands (110/120%)** + decimal breakpoints; Attendance binary.
- **Unique:** only type with **FWA Home Internet KPI**; pure pro-rata → tiered → outperformance evolution; >=40% bundle constraint.

### Batch 3

**13. Digital (Digital Distribution)** — mobile-banking distributors
- Recipients: bKash/Nagad/Rocket/Upay/Techno Index distributors; RSO; end-consumer (via distributor).
- Incentive: `DIG-01..11` — deno-specific flat BDT/txn; hourly/flash **second-level time gating**; **fastest-recharger-per-minute** contest; **100% cashback** (commission = deno); per-subscriber recharge-counter tiers; Nagad multi-base×multi-deno matrix; **Techno Index %-of-volume (0.75/0.833%), only BDT 40,000 cap**.
- Exclusions: IRIS excluded (bKash); authorized EV-MSISDN whitelist; lifting excluded.
- **Unique:** rarest patterns — time-of-day gating, minute-winner contest, cashback=deno, %-of-volume with monetary cap; Gross/Net AIT dual reporting.

**14. Distribution_Accelerate** — two programmes (Hygiene + Profitable)
- Hygiene: multi-KPI **cash** with **per-DD VARIABLE weightage** (not in SRF; B2C external file).
- Profitable: single-KPI (GA) **GIFT-VOUCHER** (Daraz, category A/B/C/D) with dual 48hr+VLR gates; East-Cluster >=70% exception.
- **Unique:** cash-vs-voucher + fixed-vs-variable-weight extremes in one folder; engine must ingest external weight file.

**15. Distribution_ROI** — SCR role-pivot (informs gate vocabulary)
- Recipients: distribution partners (DD-wise).
- Incentive: Phase1 (Apr-Aug) Bundle 50% + Recharge 50%, **SCR = binary GATE** (fail → 0%); **threshold-ladder** (<thr=0, thr-<100%=50%, =100%=100%). Phase2 (Sep+) **SCR = paid binary KPI 50%** + RSO Productivity 50%; Feb26 single-KPI; Mar26 RSO split into GA-Best-20-Days@40% + C2C-vs-C2S-GAP@70%.
- **Unique:** same metric (SCR) flips role gate→paid-KPI across period; distributor-specific KPI override (DHKDHK67, CHICOX11).

**16. Distributor_Scratch_Card** — supply-chain two ends
- Type A Warehouse→Distributor **lifting** (1% on Face value; Jan26 lifting-value basis ~96.25%); Type B Retailer→Subscriber **tertiary** (Hit/Miss + TK200/RSO celebration).
- **Unique:** per-SRF eligible card-set (1–27 codes); Face-value vs Lifting-value basis switch; dual-product min-lifting gate; qualifying-but-non-compensating card; **T-3 disconnection** (not T-6).

**17. GA_Bonanza** — one KPI (GA), 5 recipient tiers (68 SRFs)
- Recipients: RSO, RSO Supervisor, DD Manager, Distributor, Region.
- Incentive: `GAB-01..11` — physical-gift→cash (AIT trigger); multi-condition weekly+monthly; region-differentiated; **incremental 10%-per-5%-over-Slab2** (Distributor/Region only); Apr26 **combined-phase retroactive − already-paid**; MMSTR-differentiated per-GA.
- Gates: dual 48hr AND VLR (binary; North-Cluster prorated variant).
- **Unique:** every incentive dimension evolves; Dec25 winning-DD pass-through to all FF.

**18. Gross_Add** — 17 GA campaign families (88 SRFs)
- Umbrella of nearly every archetype: quartile-rank category (Platinum/Gold/Silver/Bronze), binary supervisor/manager, flat per-GA standing commission (30+5), **BTS-Biometric-tagged dual-target Hit/Miss** (Thunderstorm), deferred follow-up-recharge %, center-level gate (Monobrand), 4-component RBSP, per-deno retailer, pure cost-reimbursement (522.22, only postpaid SRF).
- **Unique mechanics:** DD-Mgr Mar26 **day-count-adjusted growth + VLR penalty gate**; RBSP max(weekly,monthly)-pack deferred; Monobrand **center-gate** (personal hit useless if center misses).

### Batch 4

**19. LUS (Low Usage Site)**
- Recipients: Retailer, DD Manager, Distributor, RSO (same site, different KPIs).
- Incentive: retailer GA HIT-or-Miss (Core 40/Emerging 35, cap 200%); DD-Mgr weighted multi-KPI per-gate; site-count slab (LSO/SSO 1K–4K); FWA flat (Dist 215/RSO 300); GA-Support **BTS-level incremental slab** (BDT 200/GA from 8th GA up to dynamic 30-80 max).
- **Unique:** BTS-level GA attribution chain (Biometric→FCD→null=drop); 48hr usage gate (95→90% relax); SSO = "2 GA/POS" here (different meaning).

**20. MDOs Variable** — fixed-pool weighted, 4 KPI-epochs
- Incentive: **BDT 10,000/month pool** × weighted KPI; KPI set changes per epoch; **Site Monetization** KPI (Phase1 revenue-bucket growth vs May25 base → Phase2 absolute LUS→Non-LUS conversion at 200K/150K).
- **Unique:** CHICOX09 per-distributor LUS-vs-Non-LUS weight tables; mid-month new-DD pro-rating (finest-grained per-DD weight).

**21. Mixed Category** — 21 distinct campaigns in one folder
- Every pattern coexists: slab+VLR, %-of-recharge hard-cap (BDSMARTPAY 1%/35K), Hit-or-Miss-with-cascade (DD Weekly → 4 roles), 3-KPI weighted-gate (RBSP), behavioural daily-productivity (Post-Eid 200/day), dual-gate (Utsob GA+C2S), TOS NID slab, Shera Partner mutual exclusion.
- **Unique:** diversity itself; no single KPI logic.

**22. Monobrand Officers Variable** — fixed-pool, 6 epochs
- Incentive: pool (CCR 4,000 / In-charge 6,000) × weighted KPI; over-performance payout (95/110/120%) first seen here.
- **Unique:** **dual SAF framework** (Manual Alternate-ID + ETSAF tracked separately); **SAF-zeroes-GA** (100% SAF → one unqualified SAF zeroes whole GA KPI); RYZE store-level 70% gate; CSAT Platinum/Signature >99.8%=120%; same-day security-deposit-per-EQUIPID.

**23. My BL** — subscriber-cashback engine
- Incentive: per **deno+date** cashback (+ time-window Hourly, base-list Segmented); BL Power per-hit fixed (AIT-excl & incl); referral slab/flat.
- **Unique:** **4-tier priority de-duplication** (Segmented>Hourly>Special Day>Regular); BL Self (BLAPP req) vs Portonics (no BLAPP) twins; cashback = subscriber reward, not agent commission.

**24. Postpaid BDO** — 10 commission models on GA+MNP
- Role-scoped roll-up (BDO own code → Supervisor 1 DDCode → Coordinator 3-5 DDCodes); proportional vs fixed-tier; count-based vs target-%-based; dual GA+VLR gate (proportional to GA% only).
- **Unique:** **Postpaid security-deposit framework** (per-bundle EQUIPID same-day min, 4 date-bands, TK200 from DWH, BL Power App source); **M2 Survival** KPI (M0 activation revenue in 3rd month); maturity 1st (M2) vs 8th.

### Batch 5

**25. Postpaid_GA**
- Incentive: **dual-condition gate** flat bonus — IF GA%>=93% AND participation_days>=Tier1 THEN 4000 ELSE Tier2 THEN 2000; within-month date-window slabs (20 GA in 1-10 → 1000, in 11-20 → 1000); Monobrand per-GA %-slab.
- **Unique:** participation-day threshold **resets each month by working days**; Monobrand center gate >=90%; three incentive models one folder.

**26. Postpaid_MNP**
- Incentive: **per-GA upfront flat by pack value, period-versioned** (mid-month rate splits 28-Jan/13-May/19-Oct/4-Nov — one SRF carries two rate tables by activation date); additional-deposit/overpayment tier-bonus; E-SIM = physical.
- **Unique:** activation-date-based rate-lookup table; backpay (retroactive named-CCR with historical rate tables).

**27. Recharge_Campaign** — most diverse recharge folder (51 SRFs)
- 7 coexisting models: %-of-recharge-slab-with-ceiling, flat-dual-KPI-gate, true-hit-or-miss, cumulative-phased-slab, Platinum/Gold/Silver tier, 3-sprint-with-catch-up, per-retailer/outlet payout.
- **Unique:** **cross-cluster collective penalty** (one retailer's cross-cluster recharge excludes ENTIRE cluster); **VLR-penalty-then-AIT-on-net**; DRC asymmetry (excluded in DD C2C, included in retailer C2S).

**28. Recharge_Data_Voice_Mix_Bundle**
- Incentive: mandatory **DUAL-GATE** (EV% AND Recharge% both, else zero; slab then by EV% alone; thresholds change monthly); Sep25+ **within-DH RELATIVE quartile ranking** (25/25/25/25 → Platinum/Gold/Silver/Bronze + absolute 95/90/80 EV-C2C floor); Distributor Extra Khatir 7-KPI variable weightage.
- **Unique:** relative (not absolute) ranking — payout depends on DH peers; Bundle uses Deno-timeline vs full-month recharge.

**29. Ryze** — self-contained Service Class 141 sub-ecosystem (8 channels)
- Incentive: `SLAB/PCT/FLAT/CATSLAB/PART/VAR/PREP/VOUCHER/BUNDLE` — per-GA volume/%-target slab; category A-E fixed; **VLR modifier** (>=80=100/60-80=prorated/<60=0, Supervisor exempt); customer-behavior conditional (BDT200/60-day rolling); preponement early-achievement bonus; once-per-MSISDN bundle; Daraz voucher tier.
- Gates: **ETSAF 98% binary (zeroes whole batch)** except Distributor flat (per-GA); RYZE-App-signup mandatory (even 100% gate / App-verified slab counting).
- **Unique:** product carve-out; App-gated slab counting; rolling-window customer recharge.

**30. SME** — most product-diverse channel (50+ programmes)
- Recipients: ESO/BDO, Coordinator, ESM, Adtech ESO, Device ESO.
- Incentive: GA achievement-slab (90%→60% / 91-150%→actual / >150%→cap 150%); **gated-overlay** quality (GA-gate → ARPU>=270 / survival>=85% / recharge>=200); device revenue-%+flat-target hybrid; **cohort time-series** (M1-M12 survival/portfolio/repurchase, M2 survival); weighted multi-KPI roll-up (Coordinator/ESM, evolving weights/tie-breaks).
- **Unique:** cohort-based KPIs (no other type); FRC insurance-bundle expansion; geo-restricted OTF.

### Batch 6

**31. SSO_LSO_GA (Project Thunderstorm)**
- Incentive: Thunderstorm Distributor **BTS Hit/Miss** (every target BTS needs >=2 Active SSO; Active SSO = retailer tagged to BTS AND >=2 FCD GA in that BTS); RSO 3-tier (Platinum 8000/Gold 7000/Silver 6000); Supervisor 2-tier (8889/7778); RSO incremental on Recharge above Slab-3 (+20% per 10%).
- **Unique:** **nested aggregation** (per-SSO GA → per-BTS active-SSO count → per-DD all-BTS pass/fail); cross-BTS GA discard; day-count-normalized growth; **VLR multiplicative penalty** (<65=forfeit/65-85=prorated/>=85=full).

**32. Shera Partner** — 7-KPI weighted-per-DH
- Incentive: multi-KPI weighted (per-DH weights from B2C); **DUAL assessment** (EV+SC evaluated both cumulatively AND separately, pay HIGHER); Recharge-fail fallback (bypass combined → fixed-weight individual, two-condition cross-achievement); Bundle "full-month hit"; Jul25 "Best 25 of 31 days" FF GA.
- **Unique:** higher-of dual assessment; date-parameterized trigger/cross-% threshold table.

**33. TOFFEE App** — install/referral-code commission
- Incentive: RSO BDT 20 per **Retail Participation** (retailer with >=2 installs = 1 participation); Retailer flat BDT 15/install; Jun25 Alt-Channel dual-component slab (Upfront + 20min/7day Conditional); Aug25 flat BDT 20.
- **Unique:** two-level participation count; AIT fully exempt; reactivated >=30-day base; single-IMEI/MSISDN dedup.

**34. Lifting (Retailer EV/Itop Lifting)** — discount/reimbursement model
- Incentive: **percentage-of-daily-lifting discount** (Tier A 0.78%/0.7%, Tier B 0.55%/0.5%); per-retailer per-day cap (778/700, 555.56/500); BDT 100,000/day lifting cap (no incremental above); **per-day independent single-day target gate** (no cross-day carry).
- **Unique:** discount not commission (RSO upfront → Distributor reimbursed); **FAD-based** (not FCD); per-day agent-list date-lock; Distributor Campaign Calendar (Feb26+); two rate tiers.

---

## 2. Cross-cutting pattern vocabulary

### 2.1 Incentive structure archetypes (the engine must support all)

| Code | Pattern | Canonical examples | Core formula shape |
|---|---|---|---|
| `PAT-FLAT` | Flat BDT per unit (HIT/GA/install/referral/txn) | Deno HIT-01, BL_WIFI, BL_Self, TOFFEE, Digital | `units × rate`, optionally gated `>=100%` or ungated from unit 1 |
| `PAT-NEARMISS` | 1/2/3-tier near-miss flat-per-unit | Deno HIT-02/03, GA_Bonanza | IF achv `<90`→0 / `90-99`→lo / `>=100`→full; ×units |
| `PAT-PCTSLAB` | Achievement-%-range → BDT/unit or payout% | Deno HIT-04/05, BSP CCR, Device Retail Chain, BSP_Retailer | IF/CASE on achievement% band |
| `PAT-OUTPERF` | Outperformance bands above 100% (95/110/120%) | BSP CCR Jan26, Device Retail Chain, Monobrand E6, GA_Bonanza | IF/CASE band extending past 100% |
| `PAT-PCTRCH` | % of recharge / amount (pool or per-txn) | Somridi 0.6-1.0%, BL_Power 1.470664%, Techno Index 0.75%, Recharge C2S 1% | `base_amount × rate%`, often per-slab BDT ceiling |
| `PAT-CLUSTERBONUS` | base slab + cluster-aggregate-100% bonus overlay | Deno RCH-02 (+0.10%), Somridi Platinum overlay | `slab_rate + bonus_rate` if cluster_achv>=100 AND self>=100 |
| `PAT-MULTICLUSTER` | per-cluster-group differential rate/cap | Deno HIT-06, Recharge, Retailer cluster-diff | rate/cap from cluster lookup |
| `PAT-CATEGORY` | binary 100% + A/B/C/D category prize | Deno HIT-07, Distribution Accelerate, GA_Bonanza voucher | category lookup → reward |
| `PAT-POINTS` | points contest / incremental over target | Deno RCH-03, GA_Bonanza incremental, SSO incremental | accumulator: per-N-over-target = pts/% |
| `PAT-WEIGHTPOOL` | fixed base × Σ(KPI payout% × weight) | BSP CCR (4000), MDO (10000), Monobrand (4000/6000), Device Retail Chain | `base × Σ(payout_i × weight_i)` |
| `PAT-GATE` | binary eligibility gate (zeroes component/all) | BP_GA dual gate, ROI SCR gate, ETSAF, SAF-zeroes-GA, center gate | IF gate fails → 0 |
| `PAT-VLRPENALTY` | multiplicative penalty after base payout | SSO_LSO_GA, GA_Bonanza, Recharge LSO | `payout × penalty_factor(VLR%)` |
| `PAT-RANK` | within-group relative quartile ranking | Recharge_DVM, Gross_Add | rank within DH → tier amount |
| `PAT-NESTED` | nested multi-level aggregation Hit/Miss | SSO_LSO_GA Thunderstorm, TOFFEE participation | group-by chain → pass/fail |
| `PAT-HIGHEROF` | cumulative-vs-individual / max-of branch | Somridi, Shera Partner, RBSP max(wk,mo) | `max(assessment_a, assessment_b)` |
| `PAT-PERIODRATE` | activation-date-versioned rate lookup | Postpaid_MNP, Scratch Card cap flag | rate table selected by activation date |
| `PAT-CUMDEDUCT` | cumulative-minus-already-paid | BSP_Retailer weekly, GA_Bonanza makeup, Ryze follow-up | `period_to_date − prior_paid` |
| `PAT-TIMEGATE` | exact-deno + time-window per-hit | BL_Power, Digital hourly/flash, My_Bl | filter on deno AND timestamp window |
| `PAT-PRIORITY` | priority de-duplication across campaigns | My_Bl (Segmented>Hourly>SpecialDay>Regular) | tag txn at highest priority, exclude lower |
| `PAT-ONCELOCK` | per-subscriber/MSISDN once/N-time lock | Digital Winback, Ryze bundle, Deno RET once | dedup/count-cap per subscriber |
| `PAT-COHORT` | M1-M12 time-series survival/portfolio | SME, Postpaid_BDO M2 survival | cohort join across activation months |
| `PAT-GROWTH` | day-count-normalized MoM growth | Somridi Mar26, SSO_LSO_GA, Gross_Add | `((CM/CMd)-(LM/LMd))/(LM/LMd)×100` |
| `PAT-MARGINAL` | variable rate only on units beyond target | BP_Variable | `(units − target) × rate`, capped max additional |
| `PAT-DISCOUNT` | %-of-lifting discount + reimbursement | Lifting | `lifting × rate%`, per-day cap, value-cap truncate |
| `PAT-TWOSTEP` | install/acquire + follow-on retention | BL_WIFI (+10% next-month), Ryze conditional, RBSP deferred | step1 + conditional step2 on later behavior |

### 2.2 Universal exclusion / filter vocabulary

| Code | Filter | Implementation |
|---|---|---|
| `EXC-RYZE` | Ryze Service Class 141 (excluded from non-Ryze; sole product for Ryze type) | `service_class != 141` |
| `EXC-POSTPAID` | Postpaid `SERVICE_CLASS_ID >= 1000` | `service_class_id < 1000` |
| `EXC-EMP` | BL employee-pool phone numbers (mapping date per campaign) | anti-join employee-pool list |
| `EXC-DRC` | Direct Recharge Channel (asymmetric: excluded DD C2C, may be included retailer C2S) | anti-join DRC list (context-dependent) |
| `EXC-XCLUSTER` | cross-cluster HITs > 10% of total (collective-penalty variant: whole cluster) | threshold filter / cluster-level exclusion |
| `EXC-FRANCHISE` | BL Franchise Store / DBP retailer type in DMS | `retailer_type != 'BL Franchise Store'` |
| `EXC-MONO` | Monobrand transactions | anti-join monobrand list |
| `EXC-DISC` | disconnection cutoff T-6 (T-5 SME/Postpaid_MNP, T-3 Scratch Card) | `disconnect_date >= report_date - N` |
| `EXC-DATELOCK` | agent-list date-snapshot (campaign-end / per-day for Lifting) | inner-join frozen agent list |
| `EXC-SIMTYPE` | Data SIM (MMSTDATA), active EV SIM (ITOPUPNUMBER) | type filter |
| `GATE-ETSAF` | 98% ETSAF vetted/accepted by 7th (8th some) of next month | eligibility gate |
| `GATE-SAF` | Manual SAF / SAF-zeroes-GA (Monobrand 100%) | per-agent gate |
| `GATE-USAGE` | No-Play >=93% / 48hr usage / >BDT5 within 7 days | usage gate |
| `GATE-VLR` | VLR active % (binary or prorated penalty) | gate or multiplicative modifier |

### 2.3 Cross-cutting policy knobs

- **Capping:** `min(achievement, cap)` where cap ∈ {110,120,130,150,200,300}%; per-slab BDT ceiling; **per-SRF cap flag** read from config.
- **Rounding:** default `<0.5 down, >=0.5 up`; **period-dependent** no-round 3-decimal (BCL Oct25+) → rounding is a **configurable policy**, not hard-coded.
- **AIT:** dual-compute incl-AIT & excl-AIT, disburse excl-AIT; **per-campaign AIT-applicable flag** (Sales-Team often exempt; cash usually applicable).
- **Maturity / data cutoff:** varies (1st/3rd/8th of next month, 45-day rolling, campaign-end+1) — a report parameter.
- **Best-N-days:** average the N highest daily values (Best 20/24/25) — configurable aggregation.

---

## 3. Calc-engine capability requirements (derived)

These are the concrete capabilities the engine/IR compiler MUST support; each is exercised by real patterns above.

1. **External-config join** — ingest B2C Excel/CSV (DD-wise target, per-DD KPI weight, slab table, agent list, category A/B/C/D, DRC list, retailer list, employee pool) and join into the pipeline. Many slabs/weights exist ONLY in external files (Somridi, Win_Together, Shera, Distribution_Accelerate, MDO).
2. **Achievement% + growth%** — `achieved/target*100`; day-count-normalized growth; Best-N-days averaging.
3. **Multi-tier IF/CASE slab** — `When col op val And … Then val`; 2/3/5/6-band; decimal breakpoints (81.5/87.2/91.6); near-miss AND outperformance in one ladder; band → BDT/unit or payout%.
4. **Capping via min()** — target-% caps and per-slab BDT ceilings; per-SRF cap flag from config.
5. **Category / lookup mapping** — category → reward/rate; cluster → rate/cap; Core/Emerging, LUS/Non-LUS, Platinum/Gold/Silver/Bronze.
6. **Exclusion filter stack** — service-class, retailer-type, channel (DRC/Monobrand), cross-cluster threshold, employee pool, date-snapshot — composable AND-chain.
7. **Date-snapshot / date-lock** — agent list frozen at a date (or per-day); RDB month-end; mid-month policy split (1-16 vs 17+).
8. **Configurable rounding policy** — math-round vs no-round-3-decimal by period.
9. **Points / incremental accumulator** — per-1000-over-target = 1 pt (fractional); per-5%-over-SlabN = +10/20%.
10. **Gate engine (binary, zeroes component)** — per-KPI independent threshold; dual-gate AND; ETSAF/SAF; center gate (cumulative center before individual).
11. **Multiplicative penalty modifier** — VLR penalty AFTER base payout; prorated variant; AIT-on-net (penalty before AIT).
12. **Weighted-sum aggregation** — `base × Σ(KPI payout% × weight)`; per-DD variable weights from external file.
13. **Nested / multi-level group-by** — per-SSO→per-BTS→per-DD; per-retailer→participation→per-RSO.
14. **Higher-of / max branch** — cumulative vs individual; max(weekly,monthly).
15. **Period-versioned rate-lookup** — activation-date → rate/deposit table; E-SIM = physical normalization.
16. **Cumulative-deduction** — weekly month-to-date − prior-week-paid; Phase1+2 − Phase1-paid.
17. **Priority de-duplication** — tag txn at highest matching priority, exclude from lower-priority counts.
18. **AIT dual-computation** — incl & excl amounts; per-campaign AIT flag.
19. **Cohort / time-series tracking** — M0→M2/M3 survival, M1-M12 portfolio, rolling 45/60-day window (non-calendar).
20. **Two-step / conditional follow-on** — install + retention; upfront + customer-behavior conditional; deferred post-campaign commission.

---

## 4. Pattern → IR pipeline-stage mapping

For each pattern, where it lands in the IR pipeline (Filter → Combine → Summarize → Calculate[math + IF/CASE] → Final mapping).

| Pattern | Filter | Combine | Summarize | Calculate (math) | Calculate (IF/CASE) | Final mapping |
|---|---|---|---|---|---|---|
| `PAT-FLAT` | EXC stack | agent-map | Count units | `units×rate` | (gate `>=100%` optional) | channel→amount |
| `PAT-NEARMISS` | EXC stack | target-file | Count units | `achv%` | `<90→0/90-99→lo/>=100→full` then ×units | channel→amount |
| `PAT-PCTSLAB` | EXC stack | target-file | Count/Sum | `achv%` | band → BDT/unit or payout% | channel→amount |
| `PAT-OUTPERF` | EXC stack | target-file | Sum | `achv%` | bands incl >100% (95/110/120) | channel→amount |
| `PAT-PCTRCH` | EXC + DRC | DRC/retailer list | Sum recharge | `base×rate%` | slab-rate select; ceiling via min | channel→amount |
| `PAT-CLUSTERBONUS` | EXC stack | target + cluster-agg | Sum (self + cluster) | `slab + bonus` | `if cluster>=100 AND self>=100 → +bonus` | channel→amount |
| `PAT-MULTICLUSTER` | EXC + xcluster | cluster-map | Count by cluster | per-cluster rate | rate/cap from cluster lookup | channel→amount |
| `PAT-CATEGORY` | EXC stack | category list | — | — | `if achv>=100 → category reward` | channel→reward |
| `PAT-POINTS` | EXC stack | target-file | Sum over-target | `(vol−target)/1000` | fractional pts; rank → winner | channel→prize |
| `PAT-WEIGHTPOOL` | per-KPI EXC | per-DD weight file | Sum/Count per KPI | per-KPI `achv%` | per-KPI payout% band | `base×Σ(payout×weight)` |
| `PAT-GATE` | EXC stack | gate-data join | gate metric | gate `achv%` | `if gate<thr → 0` (component/all) | gated amount |
| `PAT-VLRPENALTY` | EXC stack | VLR file | VLR% | base payout | `penalty_factor(VLR)`; ×base | net amount (AIT after) |
| `PAT-RANK` | EXC + EV-floor | DH peer set | rank within DH | percentile | quartile → tier amount | channel→amount |
| `PAT-NESTED` | EXC + BTS-tag | BTS-map | per-SSO→per-BTS→per-DD | counts | all-BTS pass/fail | DD→amount |
| `PAT-HIGHEROF` | EXC stack | target-file | Sum both ways | two assessments | `max(a,b)` | channel→amount |
| `PAT-PERIODRATE` | EXC + activation-date | rate-table by date | Count by pack | per-pack rate | date→rate-table select | channel→amount |
| `PAT-CUMDEDUCT` | EXC stack | prior-paid file | Sum to-date | `to_date − prior_paid` | (slab on to-date) | channel→delta |
| `PAT-TIMEGATE` | EXC + deno + time-window | — | Count hits | `hits×rate` | deno/time match | channel→amount |
| `PAT-PRIORITY` | EXC stack | concurrent-campaign tags | Count by priority | — | assign highest priority, dedup | channel→amount |
| `PAT-ONCELOCK` | EXC stack | subscriber-history | Count distinct | cap N/sub | once/N-time lock | channel→amount |
| `PAT-COHORT` | EXC stack | cohort (M0..M12) join | survival/revenue % | cohort ratios | gate → tier | channel→amount |
| `PAT-GROWTH` | EXC stack | LM vs CM join | Sum LM/CM | `((CM/CMd)-(LM/LMd))/(LM/LMd)×100` | growth-band slab | channel→amount |
| `PAT-MARGINAL` | EXC stack | target-file | Count GA | `min(GA−target, maxAdd)×rate` | segment range select | channel→amount |
| `PAT-DISCOUNT` | EXC + per-day list | per-day agent-list | Sum daily lifting | `min(lifting,100k)×rate%` | per-day target gate; per-day cap | retailer→discount |
| `PAT-TWOSTEP` | EXC stack | next-period behavior | Count step1/step2 | step1 + `step2_rate×plan` | conditional on later behavior | channel→amount |

---

## 5. Worked IR examples (runnable report definitions)

> These are concrete `report definition JSON` documents. The shape models: a report has `achievement_blocks` (Filter → Combine → Summarize → Calculate) and `incentive_blocks` (Calculate IF/CASE slab + capping), ending in `final_mapping`. All numbers are config placeholders resolved from `_meta.config_refs`. Adapt field names to the engine's actual schema — the **structure** (stage order, gate semantics, slab encoding) is the load-bearing part.

### Example 1 — Deno HIT %-slab (PAT-PCTSLAB + full EXC stack + capping)
Real basis: Deno_Campaign `HIT-04` (90-99%→BDT10, 100-109%→BDT15, 110%+→BDT20) with `EXC-01..07`, cap 200%.

```json
{
  "report_id": "DENO_HIT_PCTSLAB_DD",
  "_meta": {
    "type": "Deno_Campaign",
    "recipient": "Distributor",
    "pattern": ["PAT-PCTSLAB", "EXC-RYZE", "EXC-POSTPAID", "EXC-EMP", "EXC-XCLUSTER", "EXC-DATELOCK", "CAP-200", "ROUND-MATH"],
    "config_refs": {
      "target_file": "b2c_deno_target_<campaign>.xlsx (DD-wise HIT target)",
      "selected_denos": "SRF deno list e.g. [167,229,248]",
      "employee_pool": "b2c_employee_pool_<date>.csv",
      "agent_list": "agent_list_snapshot_<date>.csv",
      "cap_pct": 200,
      "rounding": "math"
    }
  },
  "achievement_blocks": [
    {
      "block_id": "deno_hit_achievement",
      "stages": [
        { "stage": "Filter", "conditions": [
          { "column": "service_class", "op": "!=", "value": 141, "note": "EXC-RYZE" },
          { "column": "service_class_id", "op": "<", "value": 1000, "note": "EXC-POSTPAID" },
          { "column": "deno", "op": "in", "value": "@config.selected_denos" },
          { "column": "msisdn", "op": "not_in", "value": "@config.employee_pool", "note": "EXC-EMP" }
        ]},
        { "stage": "Combine", "joins": [
          { "with": "@config.agent_list", "on": "distributor_code", "type": "inner", "note": "EXC-DATELOCK" },
          { "with": "@config.target_file", "on": "distributor_code", "type": "left", "brings": ["hit_target"] },
          { "with": "cluster_map", "on": "distributor_code", "brings": ["home_cluster", "txn_cluster"] }
        ]},
        { "stage": "Filter", "conditions": [
          { "expr": "cross_cluster_hit_ratio <= 0.10", "note": "EXC-XCLUSTER: drop if >10% cross-cluster" }
        ]},
        { "stage": "Summarize", "group_by": ["distributor_code"], "aggregations": [
          { "fn": "Count", "of": "hit_id", "as": "actual_hits" },
          { "fn": "Max", "of": "hit_target", "as": "hit_target" }
        ]},
        { "stage": "Calculate", "formulas": [
          { "as": "achievement_pct", "expr": "round(actual_hits / hit_target * 100, 'math')" },
          { "as": "capped_pct", "expr": "min(achievement_pct, @config.cap_pct)" },
          { "as": "eligible_hits", "expr": "floor(hit_target * capped_pct / 100)" }
        ]}
      ]
    }
  ],
  "incentive_blocks": [
    {
      "block_id": "deno_hit_incentive",
      "depends_on": "deno_hit_achievement",
      "stages": [
        { "stage": "Calculate", "if_case": {
          "var": "achievement_pct",
          "cases": [
            { "when": "achievement_pct < 90", "then_var": "rate_per_hit", "then": 0 },
            { "when": "achievement_pct >= 90 and achievement_pct <= 99", "then_var": "rate_per_hit", "then": 10 },
            { "when": "achievement_pct >= 100 and achievement_pct <= 109", "then_var": "rate_per_hit", "then": 15 },
            { "when": "achievement_pct >= 110", "then_var": "rate_per_hit", "then": 20 }
          ],
          "default": { "then_var": "rate_per_hit", "then": 0 }
        }},
        { "stage": "Calculate", "formulas": [
          { "as": "commission_amount", "expr": "rate_per_hit * eligible_hits" }
        ]}
      ]
    }
  ],
  "final_mapping": {
    "channel_code": "distributor_code",
    "commission_amount": "commission_amount",
    "ait": { "applicable": false, "note": "Deno DD slab Sales-Team category; RET-01 retailer SMS shows AIT-excl" }
  }
}
```

### Example 2 — %-of-recharge pool + cluster bonus (PAT-PCTRCH + PAT-CLUSTERBONUS)
Real basis: Campaign_Somridi Unified 1.0% pool / Deno `RCH-02` cluster +0.10% bonus. Shows pool base, slab rate, cluster-aggregate bonus, and the higher-of-cumulative-vs-individual fallback.

```json
{
  "report_id": "RECHARGE_PCT_POOL_CLUSTERBONUS_DD",
  "_meta": {
    "type": "Campaign_Somridi / Deno RCH-02",
    "recipient": "Distributor",
    "pattern": ["PAT-PCTRCH", "PAT-CLUSTERBONUS", "PAT-HIGHEROF", "EXC-DRC", "EXC-EMP", "GATE-ETSAF", "CAP-120", "ROUND-MATH"],
    "config_refs": {
      "target_file": "b2c_somridi_target_<month>.xlsx (DD-wise recharge target, slab assignment)",
      "drc_list": "b2c_drc_list_<month>.csv",
      "employee_pool": "b2c_employee_pool_<date>.csv",
      "slab_rates": { "Slab-1": 0.007, "Slab-2": 0.008, "Slab-3": 0.009 },
      "cluster_bonus_pct": 0.001,
      "recharge_min_threshold_pct": 70,
      "other_param_min_pct": 80,
      "ga_cap_pct": 120
    }
  },
  "achievement_blocks": [
    {
      "block_id": "recharge_achievement",
      "stages": [
        { "stage": "Filter", "conditions": [
          { "column": "recharge_type", "op": "in", "value": ["EV_SECONDARY", "SC_PRIMARY"] },
          { "column": "msisdn", "op": "not_in", "value": "@config.employee_pool", "note": "EXC-EMP (Oct25+)" }
        ]},
        { "stage": "Combine", "joins": [
          { "with": "@config.drc_list", "on": "transaction_id", "type": "anti", "note": "EXC-DRC EV Secondary" },
          { "with": "@config.target_file", "on": "distributor_code", "brings": ["recharge_target", "slab", "c2c_target", "sc_target"] },
          { "with": "cluster_map", "on": "distributor_code", "brings": ["cluster"] }
        ]},
        { "stage": "Summarize", "group_by": ["distributor_code", "cluster"], "aggregations": [
          { "fn": "Sum", "of": "recharge_amount_less_withdrawal", "as": "total_recharge" },
          { "fn": "Sum", "of": "c2c_amount", "as": "c2c_recharge" },
          { "fn": "Sum", "of": "sc_amount", "as": "sc_recharge" },
          { "fn": "Max", "of": "recharge_target", "as": "recharge_target" },
          { "fn": "Max", "of": "slab", "as": "slab" }
        ]},
        { "stage": "Calculate", "formulas": [
          { "as": "recharge_achv_pct", "expr": "round(total_recharge / recharge_target * 100, 'math')" },
          { "as": "c2c_achv_pct", "expr": "round(c2c_recharge / c2c_target * 100, 'math')" },
          { "as": "sc_achv_pct", "expr": "round(sc_recharge / sc_target * 100, 'math')" }
        ]}
      ]
    },
    {
      "block_id": "cluster_aggregate",
      "stages": [
        { "stage": "Summarize", "group_by": ["cluster"], "aggregations": [
          { "fn": "Sum", "of": "total_recharge", "as": "cluster_recharge" },
          { "fn": "Sum", "of": "recharge_target", "as": "cluster_target" }
        ]},
        { "stage": "Calculate", "formulas": [
          { "as": "cluster_achv_pct", "expr": "round(cluster_recharge / cluster_target * 100, 'math')" }
        ]}
      ]
    }
  ],
  "incentive_blocks": [
    {
      "block_id": "pool_incentive",
      "depends_on": ["recharge_achievement", "cluster_aggregate"],
      "stages": [
        { "stage": "Calculate", "if_case": {
          "comment": "Select slab rate from per-DD slab assignment",
          "var": "slab",
          "cases": [
            { "when": "slab == 'Slab-1'", "then_var": "base_rate", "then": 0.007 },
            { "when": "slab == 'Slab-2'", "then_var": "base_rate", "then": 0.008 },
            { "when": "slab == 'Slab-3'", "then_var": "base_rate", "then": 0.009 }
          ],
          "default": { "then_var": "base_rate", "then": 0 }
        }},
        { "stage": "Calculate", "if_case": {
          "comment": "PAT-CLUSTERBONUS: +0.10% only if cluster>=100 AND self>=100",
          "cases": [
            { "when": "cluster_achv_pct >= 100 and recharge_achv_pct >= 100", "then_var": "bonus_rate", "then": 0.001 }
          ],
          "default": { "then_var": "bonus_rate", "then": 0 }
        }},
        { "stage": "Calculate", "formulas": [
          { "as": "cumulative_amount", "expr": "total_recharge * (base_rate + bonus_rate)" }
        ]},
        { "stage": "Calculate", "if_case": {
          "comment": "PAT-HIGHEROF fallback: if recharge below trigger, evaluate C2C+SC individually (each needs other-param min), pay higher",
          "cases": [
            { "when": "recharge_achv_pct < @config.recharge_min_threshold_pct and c2c_achv_pct >= 100 and sc_achv_pct >= @config.other_param_min_pct",
              "then_var": "individual_amount", "then_expr": "c2c_recharge * base_rate" },
            { "when": "recharge_achv_pct < @config.recharge_min_threshold_pct and sc_achv_pct >= 100 and c2c_achv_pct >= @config.other_param_min_pct",
              "then_var": "individual_amount", "then_expr": "sc_recharge * base_rate" }
          ],
          "default": { "then_var": "individual_amount", "then": 0 }
        }},
        { "stage": "Calculate", "formulas": [
          { "as": "commission_amount", "expr": "max(cumulative_amount, individual_amount)" }
        ]}
      ]
    }
  ],
  "gates": [
    { "gate_id": "etsaf", "rule": "etsaf_accepted_pct >= 98 by 7th of next month", "on_fail": "exclude_distributor", "note": "GATE-ETSAF" }
  ],
  "final_mapping": {
    "channel_code": "distributor_code",
    "commission_amount": "commission_amount",
    "ait": { "applicable": true }
  }
}
```

### Example 3 — Retailer minimum-threshold flat (PAT-FLAT gated by min N HITs)
Real basis: Deno_Campaign `RET-01` — min N HITs from selected denos → flat BDT 50/retailer; selected-retailer list; AIT-excl SMS.

```json
{
  "report_id": "RETAILER_MIN_THRESHOLD_FLAT",
  "_meta": {
    "type": "Deno_Campaign RET-01",
    "recipient": "Retailer",
    "pattern": ["PAT-FLAT", "PAT-GATE(min-N)", "EXC-RYZE", "EXC-POSTPAID", "EXC-DATELOCK", "ROUND-MATH"],
    "config_refs": {
      "selected_retailers": "b2c_retailer_list_<campaign>.csv",
      "selected_denos": "SRF deno list e.g. [167,229,248]",
      "min_hits_threshold": 3,
      "flat_amount": 50,
      "agent_list": "agent_list_snapshot_<date>.csv"
    }
  },
  "achievement_blocks": [
    {
      "block_id": "retailer_hit_count",
      "stages": [
        { "stage": "Filter", "conditions": [
          { "column": "service_class", "op": "!=", "value": 141, "note": "EXC-RYZE" },
          { "column": "service_class_id", "op": "<", "value": 1000, "note": "EXC-POSTPAID" },
          { "column": "deno", "op": "in", "value": "@config.selected_denos" }
        ]},
        { "stage": "Combine", "joins": [
          { "with": "@config.selected_retailers", "on": "retailer_code", "type": "inner", "note": "only selected retailers eligible" },
          { "with": "@config.agent_list", "on": "retailer_code", "type": "inner", "note": "EXC-DATELOCK" }
        ]},
        { "stage": "Summarize", "group_by": ["retailer_code"], "aggregations": [
          { "fn": "Count", "of": "hit_id", "as": "qualifying_hits" }
        ]}
      ]
    }
  ],
  "incentive_blocks": [
    {
      "block_id": "retailer_flat_incentive",
      "depends_on": "retailer_hit_count",
      "stages": [
        { "stage": "Calculate", "if_case": {
          "comment": "min-N gate: pay flat only if threshold met (binary, no slab)",
          "cases": [
            { "when": "qualifying_hits >= @config.min_hits_threshold", "then_var": "commission_amount", "then_expr": "@config.flat_amount" }
          ],
          "default": { "then_var": "commission_amount", "then": 0 }
        }}
      ]
    }
  ],
  "final_mapping": {
    "channel_code": "retailer_code",
    "commission_amount": "commission_amount",
    "salescom_to_ev": true,
    "ait": { "applicable": true, "disburse": "excl_ait" },
    "sms_template": "Priyo Bikreta, Apni <date> Deno Drive er commission hishabe BDT #### (AIT katar por) peyechen. Dhonnobad."
  }
}
```

### Example 4 (bonus) — Weighted-gate variable pool (PAT-WEIGHTPOOL + per-gate threshold)
Real basis: BSP_CCR_KPI Jan26 — base BDT 4,000; GA 25%/93%, FVR 10%/95%, Stock 15%/90%, App 10%/81.5%, Daily Part 10%/95%, CSAT 30%/>97%; per-gate below-threshold = 0% that gate; outperformance bands.

```json
{
  "report_id": "BSP_CCR_WEIGHTED_GATE_JAN26",
  "_meta": {
    "type": "BSP_CCR_KPI",
    "recipient": "BSP CCR",
    "pattern": ["PAT-WEIGHTPOOL", "PAT-GATE(per-KPI)", "PAT-OUTPERF", "GATE-ETSAF", "ROUND-MATH"],
    "config_refs": {
      "base_pay": 4000,
      "kpi_config": "b2c_bsp_ccr_jan26_targets.xlsx",
      "weights": { "GA_target": 0.15, "GA_first_recharge": 0.10, "FVR": 0.10, "Stock": 0.15, "App": 0.10, "DailyParticipation": 0.10, "CSAT": 0.30 }
    }
  },
  "achievement_blocks": [
    { "block_id": "per_kpi_achievement", "stages": [
      { "stage": "Combine", "joins": [ { "with": "@config.kpi_config", "on": "ccr_code", "brings": ["ga_target","csat_score","fvr_pct","stock_pct","app_pct","daily_part_pct","first_recharge_pct"] } ]},
      { "stage": "Summarize", "group_by": ["ccr_code"], "aggregations": [ { "fn": "Count", "of": "ga_id", "as": "actual_ga" } ]},
      { "stage": "Calculate", "formulas": [ { "as": "ga_achv_pct", "expr": "round(actual_ga / ga_target * 100, 'math')" } ]}
    ]}
  ],
  "incentive_blocks": [
    { "block_id": "weighted_pool", "depends_on": "per_kpi_achievement", "stages": [
      { "stage": "Calculate", "if_case": { "comment": "GA Target payout% with outperformance (thr 93)", "cases": [
        { "when": "ga_achv_pct < 93", "then_var": "ga_payout", "then": 0 },
        { "when": "ga_achv_pct >= 93 and ga_achv_pct < 101", "then_var": "ga_payout", "then": 0.95 },
        { "when": "ga_achv_pct >= 101 and ga_achv_pct < 108", "then_var": "ga_payout", "then": 1.10 },
        { "when": "ga_achv_pct >= 108", "then_var": "ga_payout", "then": 1.20 } ], "default": { "then_var": "ga_payout", "then": 0 } }},
      { "stage": "Calculate", "if_case": { "comment": "App payout% with decimal breakpoints (thr 81.5)", "cases": [
        { "when": "app_pct < 81.5", "then_var": "app_payout", "then": 0 },
        { "when": "app_pct >= 81.5 and app_pct < 87.2", "then_var": "app_payout", "then": 0.95 },
        { "when": "app_pct >= 87.2 and app_pct < 91.6", "then_var": "app_payout", "then": 1.10 },
        { "when": "app_pct >= 91.6", "then_var": "app_payout", "then": 1.20 } ], "default": { "then_var": "app_payout", "then": 0 } }},
      { "stage": "Calculate", "if_case": { "comment": "CSAT Overall payout% (min 20 feedbacks)", "cases": [
        { "when": "csat_feedback_count < 20", "then_var": "csat_payout", "then": 0 },
        { "when": "csat_score < 97", "then_var": "csat_payout", "then": 0 },
        { "when": "csat_score >= 97 and csat_score <= 98", "then_var": "csat_payout", "then": 0.80 },
        { "when": "csat_score > 98 and csat_score <= 99.5", "then_var": "csat_payout", "then": 1.00 },
        { "when": "csat_score > 99.5", "then_var": "csat_payout", "then": 1.20 } ], "default": { "then_var": "csat_payout", "then": 0 } }},
      { "stage": "Calculate", "formulas": [
        { "comment": "FVR/Stock/DailyPart/FirstRecharge resolved by same band pattern -> *_payout vars",
          "as": "commission_amount",
          "expr": "@config.base_pay * ( ga_payout*0.15 + first_recharge_payout*0.10 + fvr_payout*0.10 + stock_payout*0.15 + app_payout*0.10 + daily_part_payout*0.10 + csat_payout*0.30 )" }
      ]}
    ]}
  ],
  "gates": [ { "gate_id": "etsaf", "rule": "etsaf_accepted_pct >= 98 by 7th of next month", "on_fail": "exclude_ccr" } ],
  "final_mapping": { "channel_code": "ccr_code", "commission_amount": "commission_amount", "ait": { "applicable": true } }
}
```

---

## 6. Implementation notes for the LLD

- **Config-first, not hard-coded.** The single most important architectural fact: slab tables, weights, targets, agent lists, categories, DRC/retailer/employee lists, and per-SRF cap flags live in **B2C-supplied external files**, not in the SRF text. The IR must reference them (`@config.*`) and the engine must ingest them. Many SRFs are "pictorial" (tables embedded as images) — those values arrive only via the external file.
- **The Deno_Campaign code library (`HIT/RCH/RSO/RET/EXC`) is the canonical taxonomy** — build the engine's slab-encoding and exclusion-stack to cover it first; the other 33 types are combinations/extensions of it plus the structural patterns in §2.1 (`PAT-WEIGHTPOOL`, `PAT-NESTED`, `PAT-RANK`, `PAT-VLRPENALTY`, `PAT-COHORT`, `PAT-PERIODRATE`, `PAT-CUMDEDUCT`, `PAT-PRIORITY`).
- **Gate vs penalty vs slab are three distinct mechanisms** — keep them separable: gate = binary zeroing (ETSAF/SCR/SAF/dual-48hr-VLR/center), penalty = multiplicative post-payout modifier (VLR 65/85% with AIT-on-net), slab = IF/CASE band. Do not collapse them.
- **Period-parameterization is pervasive.** Thresholds, rates, deposit tables, rounding policy, KPI composition, cap %, and AIT applicability all change by month/SRF-version/activation-date. Treat every such value as a dated parameter, and support **mid-month splits** (1-16 vs 17+ Oct; activation-date rate tables in Postpaid_MNP).
- **Aggregation depth matters.** Support nested group-by (SSO→BTS→DD) and two-level participation counts (install→retailer-participation→RSO), plus cross-period cumulative-deduction and cohort joins (M0→M2/M3, M1-M12). These are not expressible as a single flat Summarize.
- **AIT dual-output is a reporting requirement, not just a deduction** — several types (BL_Power, Digital, My_Bl) publish both incl-AIT and excl-AIT figures; disburse excl-AIT. Carry an `ait.applicable` flag per report (Sales-Team campaigns frequently exempt).