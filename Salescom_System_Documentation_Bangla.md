# Sales Commission Automation Platform (SalesCom)
## সম্পূর্ণ সিস্টেম ডকুমেন্টেশন (Business + Technical) — বাংলা

> এই ডকুমেন্টটি SalesCom প্ল্যাটফর্মের পুরো সিস্টেমের একটি বিস্তারিত বর্ণনা। এটি SRS (ব্যবসায়িক চাহিদা ও ফিচার) এবং LLD (টেকনিক্যাল ডিজাইন) — দুইটি ডকুমেন্ট মিলিয়ে তৈরি করা হয়েছে। উদ্দেশ্য: টিম, ম্যানেজমেন্ট এবং ডেভেলপার — সবাই যেন পুরো সিস্টেমটা এক জায়গায় বুঝতে পারে।

---

## ১. পরিচিতি (Introduction)

### ১.১ সিস্টেমটি কী
SalesCom হলো একটি **Sales Commission Automation Platform** — অর্থাৎ বিক্রয় কমিশন স্বয়ংক্রিয়ভাবে হিসাব ও বিতরণের একটি ওয়েব অ্যাপ্লিকেশন। আগে এই কমিশন হিসাব হতো **ম্যানুয়াল SQL/SRF (Service Request Form)** প্রক্রিয়ায় — অর্থাৎ প্রতিবার হাতে করে SQL লিখে রিপোর্ট বানাতে হতো। SalesCom এই পুরনো ম্যানুয়াল পদ্ধতিকে **replace** করে একটি **self-service, configuration-driven** সিস্টেম দিয়ে, যেখানে Business User নিজে কোনো কোড না লিখেই একটি গাইডেড উইজার্ডের মাধ্যমে কমিশন রিপোর্ট তৈরি করতে পারে।

### ১.২ মূল উদ্দেশ্য
- Business User নিজে রিপোর্ট সেটআপ করবে (SQL জানার দরকার নেই)।
- Achievement (অর্জন) ও Incentive (প্রণোদনা/পেমেন্ট) — দুইটাই UI থেকে কনফিগার করা যাবে।
- রিপোর্ট on-demand (এখনই) অথবা schedule (নির্ধারিত সময়ে) চালানো যাবে।
- Maker-Checker অনুমোদন (approval) ওয়ার্কফ্লো থাকবে।
- অনুমোদনের পর কমিশন **EV** ও **POS** চ্যানেলে বিতরণ (disbursement) হবে।
- প্রতিটি ধাপে SMS/Email নোটিফিকেশন যাবে।

### ১.৩ কারা ব্যবহার করবে (User Roles)
সিস্টেমে প্রতিটি ব্যবহারকারীর **ঠিক একটি role** থাকবে:

| Role | কাজ |
|---|---|
| **Business User (Maker)** | রিপোর্ট তৈরি, clone, সেটআপ কনফিগার, run-এর জন্য জমা দেওয়া। Data Source শুধু দেখতে পারে, কনফিগার করতে পারে না। ETL টেবিল স্ট্যাটাস, row count ও abnormality alert দেখতে পারে। |
| **Approver (Checker)** | রিপোর্ট detail দেখে এক বা একাধিক লেভেলে Approve/Reject করে। Reject করতে হলে অবশ্যই comment দিতে হবে। |
| **Administrator** | Data Source, Approval Flow/Level/User কনফিগার করে এবং সিস্টেম-ওয়াইড সেটআপ পরিচালনা করে। Maker ও Checker-এর কাজও করতে পারে। |

---

## ২. সিস্টেম আর্কিটেকচার (System Architecture)

### ২.১ আর্কিটেকচার ওভারভিউ
SalesCom একটি **layered (স্তরভিত্তিক) architecture** অনুসরণ করে। সহজ ভাষায় ডেটার প্রবাহ:

```
Next.js UI  ⇄ (HTTPS) ⇄  .NET API  ⇄  PostgreSQL (ডেটাবেস)
                              │
                              ├──→ SeaweedFS (ফাইল/অবজেক্ট স্টোরেজ)
                              │
                              └──→ Hangfire (ব্যাকগ্রাউন্ড জব) → RabbitMQ (ইভেন্ট)
                                        ├──→ Calc-engine (Python): রিপোর্ট সংজ্ঞা → SQL
                                        └──→ Notification সার্ভিস (SMS/Email)
```

- **Frontend:** React/Next.js — ব্যবহারকারী যা দেখে ও ক্লিক করে।
- **Backend:** C#/.NET (ASP.NET Core) — মূল লজিক ও নিয়ম এখানে।
- **Database:** PostgreSQL — রিপোর্ট সংজ্ঞা, run তথ্য, সব ডেটা।
- **File storage:** SeaweedFS (S3-compatible object storage) — আপলোড করা ফাইল ও প্রতিটি ধাপের আউটপুট CSV।
- **Background jobs:** Hangfire — schedule করা run, ভারী কাজ ব্যাকগ্রাউন্ডে চলে।
- **Message queue:** RabbitMQ — সার্ভিসগুলোর মধ্যে ইভেন্ট আদান-প্রদান।
- **Calc-engine:** Python — প্রতিটি রিপোর্টের JSON সংজ্ঞা (IR) থেকে SQL তৈরি করে PostgreSQL-এ চালায়।

### ২.২ সাধারণ কনভেনশন (Common Conventions)
- সব API endpoint `/api/v1`-এর অধীনে, JSON ফেরত দেয়।
- সব identifier **UUID**।
- সব timestamp **UTC (TIMESTAMPTZ)**, ISO 8601 ফরম্যাটে।
- সব টাকার মান **NUMERIC**, মুদ্রা **BDT** (implicit)।
- প্রতিটি রিকোয়েস্টে **JWT** থাকে; backend সার্ভার-সাইডে user ও role যাচাই করে।
- **Soft-delete** ডিফল্ট (`is_active` flag) — Data Source ও history কখনো hard-delete হয় না।

### ২.৩ ডেটাবেস ওভারভিউ
পুরো প্ল্যাটফর্ম **২১টি PostgreSQL টেবিলে** সবকিছু সংরক্ষণ করে, যা ERD (commission_system_erd_2) অনুযায়ী মডিউল-ভিত্তিক ভাগ করা: Authentication, Data Source, Report, Approval, Notification, Disbursement, এবং cross-cutting (audit ইত্যাদি)।

---

## ৩. Authentication ও Session (লগইন)

### ৩.১ ওভারভিউ
SalesCom একটি **internal-only** অ্যাপ — অর্থাৎ শুধু প্রতিষ্ঠানের অভ্যন্তরীণ ব্যবহারকারীরা ব্যবহার করবে। সবাই **Central Login (SSO) + OTP** ফ্লো দিয়ে লগইন করে। দুই-ধাপ (two-factor) নিরাপত্তা: প্রথমে username/password, তারপর OTP।

### ৩.২ লগইন কীভাবে কাজ করে (ধাপে ধাপে)
1. ব্যবহারকারী SalesCom-এর **Login screen**-এ Username, Password (show/hide সহ) ও Remember-me দেয়। ফর্মটি সরাসরি Central Login-এ নয়, **SalesCom backend-এ** পাঠায়।
2. Backend গোপন `applicationName` + `applicationKey` যোগ করে Central Login-কে কল করে।
3. সব ব্যবহারকারী internal হওয়ায় Central Login **SSO** ফ্লো ফেরত দেয় — ব্রাউজারকে Central Login-এর OTP পেজে redirect করা হয়। **OTP-এর মালিক Central Login** (challenge, countdown, resend সব ওখানে); SalesCom নিজে OTP স্ক্রিন দেখায় না।
4. OTP সফল হলে Central Login একটি **single-use authToken** সহ SalesCom-এর callback-এ ফেরত পাঠায়।
5. Backend সেই authToken যাচাই করে এবং ব্যবহারকারীর `userInfo` পড়ে।
6. এরপর backend তার **নিজের SalesCom JWT** তৈরি করে (claims: userId, userName, issued-at, expiry) এবং frontend-কে দেয়। **Central Login-এর কোনো token ব্রাউজারে যায় না।**
7. Frontend প্রতিটি API কল-এ এই JWT পাঠায়; backend signature ও expiry যাচাই করে user + role লোড করে।
8. JWT expire হলে ব্যবহারকারীকে আবার পুরো লগইন প্রক্রিয়ায় যেতে হয়।

### ৩.৩ গুরুত্বপূর্ণ নিরাপত্তা নিয়ম
- Access সম্পূর্ণভাবে assigned role/rights দিয়ে নিয়ন্ত্রিত (**BR1**); role আসে Central Login-এর `userGroupId` থেকে।
- `applicationName`/`applicationKey` শুধু backend-এর গোপন তথ্য; frontend কখনো পায় না।
- OTP, failed-attempt lockout (পরপর ৩ বার ভুল হলে লক), OTP expiry (২ মিনিট) — সব Central Login enforce করে।
- `isLocked = Y` বা `userStatus != Y` হলে লগইন প্রত্যাখ্যান।
- **User provisioning:** একটি সার্ভিস প্রতি ঘণ্টায় POS সিস্টেম থেকে user তথ্য ও rights sync করে — অর্থাৎ কোনো অধিকার পরিবর্তন ১ ঘণ্টার মধ্যে কার্যকর হয়।

### ৩.৪ RBAC (Role-Based Access Control) — পেজ ও অ্যাকশন অনুমতি

| Page / Action | Business User (Maker) | Approver (Checker) | Administrator |
|---|---|---|---|
| Login, OTP, Dashboard | Yes | Yes | Yes |
| Data Source Setup (create/edit/deactivate) | View only | View only | Full access |
| Report Create, Edit, Clone | Yes | No | Yes |
| Report List & Detail View | Yes | Yes | Yes |
| Approve / Reject Report | Yes (assigned levels) | Yes (assigned levels) | Yes (assigned levels) |
| Approval Level & User configuration | No | No | Yes |

**Access Governance:**
- প্রতিটি পেজ ও অ্যাকশন assigned role দিয়ে নিয়ন্ত্রিত (action-based access control)।
- শুধু বৈধ Active Directory credential ও assigned role থাকা internal ব্যবহারকারী ঢুকতে পারে।
- Role rights প্রতি ঘণ্টায় POS থেকে sync হয়; পরিবর্তন ১ ঘণ্টায় কার্যকর।
- **Maker-Checker পৃথকীকরণ (BR5):** একই ব্যবহারকারী একই run-এ Maker ও Checker দুইটাই হতে পারবে না।

---

## ৪. Data Source Management (ডেটা সোর্স ব্যবস্থাপনা)

### ৪.১ ওভারভিউ
এটি একটি **শুধু Administrator-এর** এলাকা, যেখানে কোন কোন ডেটা সোর্স (টেবিল) Business User রিপোর্ট বানাতে ব্যবহার করতে পারবে তা রেজিস্টার ও রক্ষণাবেক্ষণ করা হয়। Business User শুধু **active** ডেটা সোর্স থেকে বেছে নিতে পারে।

### ৪.২ ব্যবহারকারী যা দেখে (UI)
- **Listing টেবিল:** SL, Table Name, Description, Status, Action (Edit)। ডিফল্ট ১০ row, "Showing N of M" indicator, Name দিয়ে search।
- উপরে-ডানে **Add a New Data Source**।
- **Add/Edit ফর্ম:** Source Table (শুধু এখনো রেজিস্টার হয়নি এমন টেবিল বাছাই করা যায়, এবং সেই টেবিলের column গুলো দেখায়), Description, Status (ডিফল্ট Inactive)।

### ৪.৩ আচরণ ও নিয়ম (Business Rules)
- Source table নির্বাচন করলে তার column তালিকা দেখায়; admin Description ও Status সেট করে।
- **Deactivate** করলে নতুন রিপোর্টে আর দেখা যাবে না, কিন্তু পুরনো রিপোর্টের রেফারেন্স অক্ষত থাকে।
- কোনো রিপোর্টে **ব্যবহৃত** ডেটা সোর্স deactivate করা যাবে না।
- ডেটা সোর্স **কখনো delete হয় না** (soft state only) — **BR2**।
- শুধু Administrator create/edit/deactivate করতে পারে; Business User view-only।

---

## ৫. Report Management (রিপোর্ট ব্যবস্থাপনা) — মূল মডিউল

এটি সিস্টেমের হৃদয়। Business User এখানে একটি **পাঁচ ধাপের উইজার্ড** দিয়ে কমিশন রিপোর্ট কনফিগার করে, on-demand বা schedule-এ চালায়, ট্যাব-ভিত্তিক Detail View-তে ফলাফল দেখে, এবং রিপোর্ট clone করে। রিপোর্ট চালানোর ফলেই চূড়ান্ত কমিশন তৈরি হয়, যা পরে approval ও disbursement-এ যায়।

### ৫.১ Report Listing (রিপোর্ট তালিকা)
প্রতিটি রিপোর্টে ঢোকার মূল পেজ।
- **কলাম:** SL, Report Name, Channel, Start Date, End Date, Recurrency, EV, POS, Status, Action।
- **Status উদাহরণ:** Save as Draft, Waiting for Running, Waiting for RA L1, Approved by All, Rejected by RA।
- **Filter:** Start Date, End Date, Status, Channel, Report Name। Report Name-এ **prefix-based search** (যে অক্ষর দিয়ে শুরু সেই অনুযায়ী)।
- **Pagination:** ডিফল্ট ১০, "Showing N of M" indicator।
- উপরে-ডানে **Create a New Report**।
- প্রতিটি row-তে **Action menu:** View Report, Edit Report, Clone Report, Demo Run, Approval History, Report Stop।

**গুরুত্বপূর্ণ আচরণ:**
- **Edit Report** শুধুমাত্র **Final Approval** হওয়ার পর লুকানো/বন্ধ থাকবে। তার আগ পর্যন্ত (Draft, Final Saved, এবং Approval Pending — সব অবস্থায়) Edit করা যাবে। বিস্তারিত §৫.৮ দেখুন।
- **Report Stop** শুধু তখনই দেখাবে যখন রিপোর্ট approved এবং একটি নির্দিষ্ট তারিখে schedule করা আছে।
- **Run Report Now** শুধু তখনই আসবে যখন রিপোর্ট সব লেভেলে approved। ক্লিক করলে — অন্য কোনো রিপোর্ট না চললে সঙ্গে সঙ্গে চলা শুরু হয়; অন্য রিপোর্ট চললে priority অনুযায়ী queue-তে যায়।
- **Demo Run** সর্বোচ্চ **৫ বার** প্রতি রিপোর্ট; demo run-এ শুধু detail তৈরি হয় (disbursement/approval হয় না)।
- **Run priority:** Schedule run = Low, Demo Run = Medium, Run Report Now = High।

### ৫.২ পাঁচ ধাপের Report Setup উইজার্ড

#### ধাপ ১ — Basic Input
- **Report Name** (আবশ্যক, সিস্টেম-ব্যাপী unique — **BR3**)।
- **Commission Cycle** (যেমন "July 2025") ও **Channel** (Distributor, Retailer, RSO)।
- **Start Date** ও **End Date** (Start ≤ End — **BR4**)।
- **Recurrent Report toggle:** চালু করলে Recurrency Frequency বাছতে হয় (Daily/Weekly/Monthly) — সিস্টেম সেই অনুযায়ী স্বয়ংক্রিয়ভাবে চালায়।
- **EV Disbursement toggle:** চালু করলে Disbursement Time ও একটি SMS text দিতে হয়; অনুমোদনের পর ঐ সময়ে EV disbursement হয়।
- ঐচ্ছিক **Remarks**।
- **Save as Draft** — খসড়া হিসেবে সংরক্ষণ (status = draft), একটি report id তৈরি হয় যা পুরো উইজার্ডে ব্যবহৃত হয়।
- **Cancel** — কিছু সংরক্ষণ না করে তালিকায় ফেরত।

#### ধাপ ২ — Supporting Upload (সহায়ক ফাইল আপলোড)
- **Drag-drop** বা **Browse CSV**; প্রথম row-তে অবশ্যই column নাম থাকতে হবে।
- একাধিক CSV আপলোড করা যায় (SL, Original/Source File Name, Remove — সংরক্ষণের আগে)।
- **Preview:** প্রথম কয়েকটি row দেখায়, প্রতিটি column-এর type স্বয়ংক্রিয়ভাবে শনাক্ত (String/Number/Date), এবং একটি checkbox যা বলে column-টি null/empty মান নিতে পারবে কিনা। ব্যবহারকারী type পরিবর্তন করতে পারে।
- type না মিললে inline **Warning** দেখায় (যেমন "Target: Invalid number found...")।
- নিশ্চিত করলে — কাঁচা ফাইল object storage-এ যায় এবং parsed row থেকে একটি ডেটাবেস টেবিল তৈরি হয়, যা পরের ধাপে datasource হিসেবে ব্যবহার করা যায়।
- **সীমা:** প্রতি ফাইলে সর্বোচ্চ **৩০ column**, সর্বোচ্চ **৫০০ MB**। File name < 40 অক্ষর, column name < 30 অক্ষর; টেবিল/column নামে space বা special character থাকবে না (যেমন `channel_code`, `ga_target`)।

#### ধাপ ৩ — Achievement Calculation (অর্জন হিসাব)
- এক বা একাধিক **Achievement block** তৈরি (ACH#1, ACH#2...); block-এর নাম পরিবর্তনযোগ্য।
- প্রতিটি block-এর জন্য datasource বাছাই (রেজিস্টার্ড সোর্স বা ধাপ-২ এর CSV)।
- প্রতিটি block-এ **পাইপলাইন স্টেজ** (প্রতিটি collapsible card):
  - **Filter:** Column/Value, Condition (Is one of, Equals, Greater than...), Compare-to value।
  - **Combine Data:** Get-Data-From source, join key (যেমন `recharge.RETAILER_MSISDN = agent_map.RET_MSISDN`), কোন column আনবে, Operations (None/Aggregate), কীভাবে মেলাবে (শুধু matched rows / সব rows)।
  - **Summarize:** Get-Data-From, Result Column (যেমন HIT), Calculation (Count/Sum/Avg/Min/Max), ঐচ্ছিক Form Column ও Filter।
  - **Calculate:** Result Column ও একটি Math Formula (যেমন `(Hit ÷ Hit_Target) ÷ 100`), Add-Operation/Add-Group সহ।
  - **Modify:** পরের block-এ পাঠানোর আগে column মান cast/পরিবর্তন।
- প্রতিটি block-এ Action: Add a new section, Add another Block, Remove (শুধু **শেষের** block মোছা যায়, মাঝেরটা নয়)।
- **Outputs panel:** active block-এর সম্ভাব্য আউটপুট column (Name, Type, Source) আগেই দেখায়।
- একটি বিদ্যমান Achievement আরেকটি Achievement-এর input হিসেবে ব্যবহার করা যায়।
- **Next-এর আগে** প্রতিটি পাইপলাইন pre-validate হয় (রেফার করা column/calc field আছে কিনা)।

#### ধাপ ৪ — Incentive Calculation (প্রণোদনা/পেমেন্ট হিসাব)
- এক বা একাধিক **Incentive block** (INC#1...); নাম পরিবর্তনযোগ্য। ধাপ-৩ এর মতোই স্টেজ।
- **Calculate স্টেজে IF/CASE (slab-based):** এক বা একাধিক Case — `When <col> <op> <val> And <col> <op> <val> Then <val>`, Add Case দিয়ে slab টেবিল বড় করা যায়, প্রতিটি Case Expand/Collapse।
- Incentive block এক বা একাধিক Achievement block (বা system source) input হিসেবে পড়ে; বিদ্যমান Incentive আরেকটির input হতে পারে।
- **Final mapping (গুরুত্বপূর্ণ):** শেষে ব্যবহারকারী আউটপুট column-গুলোকে চূড়ান্ত কমিশনে map করে — কোনটি **Channel Code** আর কোনটি **Commission Amount**। সিস্টেম এটি **per channel** গ্রুপ করে চূড়ান্ত কমিশন (`final_commission`) তৈরি করে। এই ধাপ ছাড়া পেমেন্ট তৈরি হয় না।
- Submit-এর আগে প্রতিটি Incentive পাইপলাইন pre-validate হয়।

#### ধাপ ৫ — Run Now ও Schedule
- Submit-এর পর **"Report Submitted Successfully!"** স্ক্রিন (Report Name, Commission Cycle, Date Range দেখায়), তিনটি অ্যাকশন: Back to Reports, Schedule for Later, Run Report Now।
- **Run Now** শুধু সব লেভেলে approved হওয়ার পর; একসাথে একটি run চলে, বাকিগুলো priority queue-তে।
- **Schedule modal:** date + time (যেমন 08 Jul 2025, 2:00 AM)। Recurrent রিপোর্ট run_start_date/run_end_date অনুযায়ী cadence-এ চলে; non-recurrent একটি তারিখে।
- Schedule **pause/edit/cancel শুধু Maker** করতে পারে; প্রতিটি পরিবর্তন audit হয় (**BR9**); কে run trigger করল তা রেকর্ড হয়।

### ৫.৩ Run Execution Lifecycle (run চালানোর সম্পূর্ণ প্রক্রিয়া)
trigger থেকে disbursement পর্যন্ত canonical ধাপ:
1. **Trigger:** Run Now (full approval-এর পর) বা scheduler; type = Final বা Demo; triggered_by = User বা System।
2. **Queue:** একসাথে একটি run; বাকিরা priority queue-তে অপেক্ষা করে।
3. **Create run:** report_run তৈরি (pending → running)।
4. **Snapshot stages:** সব stage কপি করে freeze করা হয় (যাতে পরে কনফিগ বদলালেও এই run অপরিবর্তিত থাকে)।
5. **Execute in order:** প্রতিটি stage ক্রমান্বয়ে চলে; প্রতিটি একটি temp টেবিল তৈরি করে; status: not run → running → succeeded/failed।
6. **Export outputs:** প্রতিটি সফল stage-এর আউটপুট CSV হিসেবে object storage-এ যায় (Run Log থেকে download করা যায়)।
7. **Final commission:** শেষ stage প্রতি channel-এ একটি করে row লেখে (channel_code, commission_amount)।
8. **Cleanup:** temp টেবিল মুছে ফেলা হয়; run = completed (বা ব্যর্থ হলে failed + error)।
9. **Disbursement (Final + approved হলে):** EV হলে ev_disburse row লেখে ও SMS পাঠায়; POS হলে final_commission থেকে CSV তৈরি করে।

### ৫.৪ Report Detail View (রিপোর্ট বিস্তারিত)
View Report-এ ক্লিক করলে খোলে। হেডারে Report Name, Commission Cycle, Date Range; উপরে-ডানে global **Export**। body-তে read-only ট্যাব:
- **Basic Tab:** Channel, Approval Flow, Commission Cycle, Start/End Date, Recurrent (Active/Inactive), Frequency, EV Disbursement, Disbursement Time, Remarks — সব read-only।
- **Supporting Uploads Tab:** আপলোড করা CSV তালিকা (SL, Original/Source File Name, Details), per-row **Download**। এখান থেকে নতুন ফাইল আপলোড করা যায় না।
- **Achievements Tab:** প্রতিটি block ও তার স্টেজ (Filter, Combine, Summarize, Modify, Calculate) collapsible card হিসেবে read-only; expand state ট্যাব পরিবর্তনেও মনে রাখে।
- **Incentives Tab:** প্রতিটি block-এর Calculate card (Result Column + IF/CASE), প্রতিটি Case-এর When/And/Then, **MAP EACH INPUT** card (Value ও Decimals), এবং বাকি স্টেজ।
- **Run Log Tab:** প্রতিটি run (Final/Demo) নতুন থেকে পুরনো ক্রমে; প্রতিটিতে run kind + ordinal ("Final Run 1", "Demo Run 2") ও timestamp; per-detail **Download** ও **Download All**।
- **Disbursement Tab:** সর্বোচ্চ একটি ট্যাব — EV Active হলে "EV Disbursement", POS Active হলে "POS Disbursement", কোনোটি না থাকলে ট্যাব লুকানো। প্রতিটি batch: SL, Disbursement Time, Record Count, **Download CSV**।
  - **EV CSV:** Channel Type, Channel Code, Amount, Disbursement Time, Status, Message।
  - **POS CSV:** Channel Code, Amount।

### ৫.৫ Demo Run (হালনাগাদ)
disbursement/approval ছাড়াই রিপোর্ট চালিয়ে আউটপুট আগে দেখার সুযোগ — calculation ঠিক আছে কিনা যাচাই করতে। শুধু detail তৈরি হয়, Run Log-এ "Demo Run N" হিসেবে দেখায়, সর্বোচ্চ ৫ বার।

**Demo Run কখন allowed:**
- শুধুমাত্র **Final Saved** রিপোর্ট, অথবা **Approval Pending** রিপোর্টে Demo Run করা যাবে।
- **Draft** রিপোর্টে Demo Run করা যাবে **না** — কারণ Draft অবস্থায় SQL generate হয় না।
- অর্থাৎ Demo Run করতে হলে রিপোর্ট অবশ্যই আগে **Final Save** হতে হবে।

### ৫.৬ Report Cloning (রিপোর্ট কপি)
- Report Listing ও Detail View থেকে **Clone** অ্যাকশন।
- নতুন **unique Report Name** দিতে হয় (duplicate হলে error)।
- কপি হয়: সম্পূর্ণ কনফিগারেশন (achievement/incentive block, calc-field ও datasource রেফারেন্স, assigned approval level)।
- **কপি হয় না:** supporting আপলোড, পুরনো run, approval decision, disbursement record — clone একটি **fresh draft** হিসেবে শুরু হয়।

### ৫.৭ Report Management — মূল Business Rules
- Report name unique (**BR3**); period start ≤ end (**BR4**)।
- **Final Approval** হলে রিপোর্ট locked — Edit নিষ্ক্রিয়; তার আগ পর্যন্ত Edit করা যায় (§৫.৮)। Run Now শুধু full approval-এর পর।
- Supporting file ≤ 30 column, ≤ 500 MB; advance-এর আগে পাইপলাইন pre-validate।
- শুধু Maker schedule manage করে; প্রতিটি পরিবর্তন audit (**BR9**)।

### ৫.৮ Report Save, Edit ও State লজিক (হালনাগাদ Requirement)

> **পরিবর্তন:** আগের ধারণা ছিল "Report Approved না হওয়া পর্যন্ত Edit করা যাবে"। নতুন requirement অনুযায়ী Edit-এর নিয়ম নিচে বিস্তারিত করা হলো। এই সেকশন আগের ধারণাকে replace করে।

#### রিপোর্টের State (অবস্থা) ও জীবনচক্র
```text
Create → Draft Save → Final Save → (Edit করা যায়) → Send to Approval
       → Approval Pending (Approve না হওয়া পর্যন্ত Edit করা যায়)
       → Final Approval → LOCKED (আর Edit করা যাবে না)
```

#### Edit লজিক (কখন Edit করা যাবে)
- Report **Create** করার পর User **Draft Save** করতে পারবে।
- Draft থেকে **Final Save** করতে পারবে।
- **Final Save হওয়ার পরও Edit** করা যাবে।
- **Approval-এ পাঠানোর পরও**, Approver **Approve না করা পর্যন্ত** Edit করা যাবে।
- **Final Approval** হয়ে গেলে রিপোর্ট **Locked** — এরপর আর Edit করা যাবে না।

#### Draft Save অবস্থা
- রিপোর্ট **অসম্পূর্ণ** থাকতে পারবে।
- **SQL generate হবে না।**
- **Demo Run করা যাবে না।**
- **Approval-এ পাঠানো যাবে না।**

#### Final Save অবস্থা
- সকল **Validation execute** হবে।
- **SQL generate** হবে।
- generated SQL **ডেটাবেসে store** হবে।
- এরপর রিপোর্ট Demo Run ও Approval-এ পাঠানোর যোগ্য হয়।

#### SQL Regeneration লজিক
- একটি **Final Saved** রিপোর্ট **Edit** করে আবার **Save** করলে — Validation আবার চলবে এবং **SQL আবার generate ও store** হবে (পুরনো SQL আপডেট হবে)।

```text
Final Saved → Edit → Save → (Validation + SQL আবার generate ও store)
```

> **নিশ্চিত করার বিষয়:** আপনার মূল মেসেজে SQL Regeneration Rule-এর শেষ অংশ কেটে গিয়েছিল; উপরের লজিকটি যৌক্তিকভাবে ধরে নেওয়া হয়েছে। ভিন্ন কিছু চাইলে জানান।

---

## ৬. Approval (Maker-Checker অনুমোদন)

### ৬.১ ওভারভিউ
একটি **কনফিগারযোগ্য maker-checker** ওয়ার্কফ্লো। Administrator পুনঃব্যবহারযোগ্য **Approval Flow**, তার ভেতরের ক্রমিক **Level**, এবং প্রতিটি লেভেলে কারা কাজ করবে সেই **User** সংজ্ঞায়িত করে। জমা দেওয়া run ক্রম অনুযায়ী লেভেলগুলোতে যায়।

### ৬.২ তিনটি কনফিগারেশন পেজ
- **Approval Flow:** SL, Name, Type, Action; Add a New Flow (Flow Name unique, Flow Type)।
- **Approval Level:** SL, Flow, Level, Order, Action; Add Level (Flow Name, Level Name, Order — প্রতি flow-এ unique positive integer)।
- **Approval Level User:** SL, Flow, Level, user identity (Name, Username, Mail), Action; assign করা user central directory থেকে যাচাই হয় (যাতে সবসময় বাস্তব, বর্তমান কর্মী হয়)। একই user একাধিক level ও flow-তে থাকতে পারে।

### ৬.৩ অনুমোদন প্রক্রিয়া
- জমা দেওয়া run সবচেয়ে নিচের level-এ একটি **approval_request** খোলে; Approve করলে ক্রমবর্ধমান (ascending) order-এ পরের level-এ যায় (**BR6**)।
- শেষ level-এ Approve হলে overall_status = **Approved**, এবং disbursement সক্রিয় হয় (**BR8**)।
- **Reject** করতে হলে অবশ্যই comment দিতে হবে (**BR7**); run ফেরত যায় এবং Maker notified হয়।
- প্রতিটি flow/level/user পরিবর্তন actor ও timestamp সহ audit log-এ লেখা হয়।

> **নোট (পরিষ্কার করা দরকার):** SRS-এ লেখা আছে Reject হলে "previous level"-এ ফেরত যায়, কিন্তু LLD বলছে "Maker"-এর কাছে ফেরত যায়। বাস্তবায়নের আগে এই পয়েন্টটি চূড়ান্ত করা উচিত।

### ৬.৪ মূল নিয়ম
- ক্রমিক (sequential) approval বাধ্যতামূলক (**BR6**); Reject-এ comment বাধ্যতামূলক (**BR7**)।
- Maker-Checker পৃথকীকরণ (**BR5**); Flow Name ও প্রতি-flow Level Order unique।
- শুধু Administrator flow/level/user কনফিগার করে।

---

## ৭. Dashboard (ড্যাশবোর্ড)

লগইনের পর ডিফল্ট পেজ — চলতি মাসের কমিশন KPI, login audit, এবং সাম্প্রতিক trend দেখায়, সবই role অনুযায়ী scoped।
- **KPI card:** চলতি মাসের মোট Commission Amount এবং মোট Report Count — প্রতিটিতে আগের মাসের তুলনায় delta (যেমন "38% higher")।
- **Login Attempts card:** ব্যবহারকারীর সর্বশেষ সফল ও সর্বশেষ ব্যর্থ লগইন সময়।
- **Trend chart:** সাপ্তাহিক Commission Amount ও মাসিক Report Count (চলতি ও আগের মাস)।
- চলতি মাসের retailer/RSO/distributor অনুযায়ী report count ও commission।
- প্রতিটি menu, page, chart user claim authorization দিয়ে scoped (**BR1**)।
- (টেকনিক্যাল: ড্যাশবোর্ডের নিজস্ব টেবিল নেই; report_run, final_commission, audit_log থেকে aggregate পড়ে।)

---

## ৮. ETL ও Data Trend & Prediction

### ৮.১ ETL Process (ব্যাকগ্রাউন্ড)
পুরো সিস্টেম চলার জন্য যে ডেটা লাগে তা একটি **backend ETL প্রক্রিয়ার** মাধ্যমে আসে। এটি ব্যাকগ্রাউন্ডে চলে, ব্যবহারকারীর কাছে দৃশ্যমানতা কম, কিন্তু রিপোর্ট তৈরির জন্য অপরিহার্য — কারণ ডেটা ছাড়া রিপোর্ট চলে না।
- উৎস সিস্টেম থেকে ডেটা dump ও load করে: **DWH, In-house, POS, DMS, vPeople**।
- কাঁচা ডেটা process করে **finished (processed)** ডেটায় রূপান্তর করে, যার উপর Business User achievement ও incentive হিসাব করে।
- **প্রতিটি run-এ:** রিপোর্ট যে যে ডেটা সোর্স ব্যবহার করে, সেগুলোর ডেটা প্রয়োজনীয় তারিখ পর্যন্ত এসেছে কিনা সিস্টেম যাচাই করে।
- **সব** প্রয়োজনীয় সোর্সে campaign End Date পর্যন্ত ডেটা থাকলে তবেই run শুরু হয়; না থাকলে run শুরু হয় না।
- প্রয়োজনীয় ডেটা না এলে সংশ্লিষ্ট ব্যবহারকারীদের email/SMS alert দেওয়া হয়, যাতে তারা জানে কেন run চলেনি।

### ৮.২ Data Trend & Prediction পেজ
incoming ডেটার পরিমাণ পর্যবেক্ষণ ও ঐতিহাসিক প্যাটার্ন দেখার পেজ:
- শেষ **৬ মাসের** ডেটা-ভলিউম trend।
- ঐতিহাসিক ডেটার ভিত্তিতে **projected expected daily volume**।
- কোনো দিনের ডেটা count স্বাভাবিকের চেয়ে অস্বাভাবিক হলে **alert**।

---

## ৯. Notification (নোটিফিকেশন)

প্ল্যাটফর্মের ঘটনাভিত্তিক SMS ও Email পাঠায় এবং প্রতিটি send audit-এর জন্য একটি log-এ রাখে। নিজস্ব কোনো স্ক্রিন নেই — ঘটনাভিত্তিকভাবে trigger হয়।
- **SMS:** প্রতিটি সফল EV disbursement-এর পর প্রতিটি কমিশন প্রাপককে (শুধু full approval-এর পর — **BR8**)।
- **Email:** approval requested, approval rejected, disbursement complete, run failed — একাধিক প্রাপক (To ও CC)। (ইমেইল approval-level user থেকে সংগ্রহ করা হয়।)
- প্রতিটি attempt রেকর্ড করে: channel, status (PENDING/SENT/FAILED), attempt_count, scheduled_at, sent_at, error_message।

> **নোট:** SRS V1.6-এ Email শুধু "approval request" ও "approval rejected"-এ সীমাবদ্ধ করা হয়েছে। LLD-তে "disbursement complete" ও "run failed"-ও আছে — Business user-এর জন্য এই দুইটি ফেরত আনার সুপারিশ করা হয়েছে।

---

## ১০. Disbursement (কমিশন বিতরণ)

run পূর্ণ approved হওয়ার পর কমিশন **EV এবং/অথবা POS** চ্যানেলে বিতরণ হয়।

### ১০.১ EV বনাম POS — মূল পার্থক্য
- **EV Disbursement (স্বয়ংক্রিয়/সরাসরি):** নির্ধারিত Disbursement Time-এ সিস্টেম final_commission পড়ে প্রতি channel-এ একটি করে ev_disburse row লেখে, এবং প্রাপকদের **SMS** পাঠায়।
- **POS Disbursement (ফাইল-ভিত্তিক):** সিস্টেম final_commission থেকে একটি **CSV dump ফাইল** (Channel Code, Amount) তৈরি করে object storage-এ রাখে; **POS সিস্টেম সেই ফাইল থেকে আসল পেমেন্ট** process করে। অর্থাৎ এখানে সিস্টেম নিজে টাকা দেয় না, ফাইল হস্তান্তর করে।

### ১০.২ UI ও নিয়ম
- Detail View-তে সর্বোচ্চ একটি disbursement ট্যাব (EV বা POS) দেখায়, রিপোর্টের toggle অনুযায়ী।
- প্রতিটি batch row: SL, Disbursement Time, Record Count, Download CSV।
- বিতরণ শুধু **সব approval level approve করার পর** (**BR8**)।
- **Reconciliation:** সিস্টেম disbursed total ও approved run total মিলিয়ে দেখে; অমিল থাকলে flag করে।

---

## ১১. Cross-cutting Concerns (সর্বব্যাপী বিষয়)

### ১১.১ Audit Trail
প্রতিটি create/update/delete এবং প্রতিটি approval, schedule ও disbursement অ্যাকশন audit log-এ যুক্ত হয় (actor + timestamp সহ)।

### ১১.২ Data Retention (ডেটা সংরক্ষণ নীতি)
- Source/finished (processed) ডেটা টেবিল active database-এ **৩ মাস** থাকে; এরপর **archive টেবিলে** সরানো হয় (delete নয়)।
- dump ও detail ফাইল object storage-এ প্রথমে **৩০ দিন** থাকে, এরপর purge হয় (আনুমানিক প্রাথমিক ক্ষমতা ~১০০ GB)।
- ESI রিপোর্টের জন্য আলাদা retention নীতি (পরে নির্ধারিত হবে)।

### ১১.৩ Pagination, Filter, Search
- Query: `?page=&size=&sort=&q=<search>&<filter>=<value>`; response: `{ items, page, size, total }`; ডিফল্ট size ১০।
- List endpoint-এ name-এর prefix search ও অন্তত একটি filter।

### ১১.৪ নিরাপত্তা ও ইন্টারফেস
- Action-based access control; সব ডেটা ট্রান্সমিশন **HTTPS**।
- ওয়েব-ভিত্তিক UI, সব major browser সমর্থিত; বিদ্যমান ETL সিস্টেম ও third-party API-এর সাথে ইন্টিগ্রেশন।

---

## ১২. মূল Business Rules সারসংক্ষেপ

| ID | নিয়ম |
|---|---|
| BR1 | Access সম্পূর্ণভাবে assigned role/right দিয়ে নিয়ন্ত্রিত। |
| BR2 | শুধু Admin Data Source create/edit/deactivate; কখনো delete নয়। |
| BR3 | Report Name সিস্টেম-ব্যাপী unique। |
| BR4 | Report-এর Start Date ≤ End Date। |
| BR5 | একই user একই run-এ Maker ও Checker হতে পারবে না। |
| BR6 | Approval ক্রমিক (ascending order) — এক লেভেলের পর পরের লেভেল। |
| BR7 | Reject করতে comment বাধ্যতামূলক। |
| BR8 | Disbursement শুধু সব level approve করার পর। |
| BR9 | Schedule শুধু Maker manage করে; প্রতিটি পরিবর্তন audited। |

---

## ১৩. পরিভাষা (Glossary)

| সংক্ষেপ | পূর্ণরূপ / অর্থ |
|---|---|
| KPI | Key Performance Indicator |
| SRF | Service Request Form (পুরনো প্রক্রিয়া, যা replace হচ্ছে) |
| IR | Intermediate Representation (রিপোর্ট সংজ্ঞার JSON রূপ) |
| ETL | Extract, Transform, Load (ডেটা আনা-প্রসেস করার প্রক্রিয়া) |
| DWH | Data Warehouse |
| DMS | (উৎস সিস্টেম) |
| POS | Point of Sale |
| EV | Electronic Value (পেমেন্ট চ্যানেল) |
| RA | Report Approver (যেমন RA L1 = Approver Level 1) |
| RSO | (চ্যানেল টাইপ) |
| Achievement block | KPI/অর্জন হিসাব করার পাইপলাইন |
| Incentive block | Achievement থেকে payout হিসাব (সাধারণত IF/CASE slab) |
| Final commission | run-এর শেষ ধাপে তৈরি per-channel commission_amount |
| Maker / Checker | রিপোর্ট তৈরিকারী / অনুমোদনকারী |

---

> **মনে রাখার জন্য (open items):**
> ১. Reject হলে previous level না Maker — চূড়ান্ত করতে হবে।
> ২. Recurrency frequency-তে Monthly থাকবে কিনা (SRS: Daily/Weekly; LLD: Daily/Weekly/Monthly)।
> ৩. "disbursement complete" ও "run failed" email notification রাখা হবে কিনা।
> ৪. ESI report-এর retention নীতি নির্ধারণ।
> ৫. POS Disbursement toggle Basic Input-এ যোগ করা; Report-এ Approval Flow নির্বাচন; Approval History, Report Stop ও Export-এর আচরণ পরিষ্কার করা।


