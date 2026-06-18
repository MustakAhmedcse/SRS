# SalesCom Automation — Server Layout ও Deployment Plan

এই ডকুমেন্টে তোমার ৪টা সার্ভারে কী কী থাকবে, কে কত resource পাবে, আর Docker-এ কী খেয়াল রাখতে হবে — সব এক জায়গায়।

---

## পুরো ছবিটা এক নজরে

```
┌─────────────────────┐   ┌─────────────────────┐
│   APP01 (Docker)    │   │   APP02 (Docker)    │
│  - Web (React)      │   │  - Web (React)      │   ← Load Balancer
│  - API (.NET)       │   │  - API (.NET)       │     এই দুটোর সামনে
│  - Hangfire         │   │  - Hangfire         │
└─────────────────────┘   └─────────────────────┘

┌─────────────────────┐   ┌─────────────────────┐
│   AI01 (Docker)     │   │   AI02 (Docker)     │
│  - Executor Service │   │  - RabbitMQ         │
│  - EV Disbursement  │   │  - SeaweedFS        │
│  - SQLGlot          │   │  - Loki (log)       │
└─────────────────────┘   └─────────────────────┘

┌─────────────────────┐   ┌─────────────────────┐
│  Airflow Server     │   │  Database Server    │
│  (Docker)           │   │  (Docker ছাড়া!)     │
│  - Airflow          │   │  - Percona PG 18    │
│  - ETL load tasks   │   │                     │
└─────────────────────┘   └─────────────────────┘
```

মোট ৬টা মেশিন। প্রতিটা ৮ core / ১৫GB RAM ধরে নিচ্ছি।
**Database বাদে বাকি সব Docker-এ।** এটাই সঠিক — DB সরাসরি মেশিনে চলবে, পুরো resource পাবে।

---

## কে কোথায়, কত Resource

### APP01 ও APP02 (দুটো একই — Load Balancer-এর পেছনে)

এখানে user যা দেখে আর যা ব্যবহার করে, সব।

| Component | কাজ | RAM limit | CPU limit |
|---|---|---|---|
| Web (React) | user যে UI দেখে | 1 GB | 1 |
| API (.NET) | সব business logic, request handle | 4 GB | 3 |
| Hangfire | schedule check করে RabbitMQ-তে message পাঠায় | 2 GB | 1 |

মোট ব্যবহার ~৭GB → ১৫GB-তে ~৮GB ফাঁকা (OS + buffer + spike-এর জন্য)।
৫০ user-এর জন্য এই দুটো সার্ভার আরাম করে বসে থাকবে।

### AI01 — ভারী কাজের সার্ভার

| Component | কাজ | RAM limit | CPU limit |
|---|---|---|---|
| Executor Service | RabbitMQ থেকে message নিয়ে report-এর SQL চালায় | 4 GB | 3 |
| EV Disbursement | টাকা disbursement (আলাদা process রাখো) | 2 GB | 2 |
| SQLGlot | JSONB config → SQL বানায় | (executor-এর সাথেই) | — |

মোট ~৬GB → ৯GB ফাঁকা। Executor বেশিরভাগ সময় DB-র উত্তর অপেক্ষা করে, তাই CPU কমই খায়।

### AI02 — Infrastructure সার্ভার

| Component | কাজ | RAM limit | CPU limit |
|---|---|---|---|
| RabbitMQ | message queue (Hangfire → Executor) | 1 GB | 1 |
| SeaweedFS | CSV ফাইল আর step detail জমা রাখে | 2 GB | 2 |
| Loki | সব log এক জায়গায় store | 1 GB | 1 |
| Grafana (পরে দরকারে) | log + telemetry দেখার dashboard | 512 MB | 1 |

মোট ~৪.৫GB → ১০GB ফাঁকা।

### Airflow Server

| Component | কাজ | RAM limit | CPU limit |
|---|---|---|---|
| Airflow (scheduler+web) | ETL orchestrate | 2 GB | 1 |
| Load tasks | CSV ফাইল PG-তে পাঠায় (byte pump মাত্র) | 3 GB (pool) | 2 |

ভারী parsing PG-তে হয়, Airflow শুধু ফাইল pump করে — তাই load খুব হালকা।
**একটা জরুরি কাজ:** SFTP-র যে ফোল্ডারে ফাইল আসে, সেটা Airflow container-এ bind mount করো (`/data/sftp:/data/sftp`), নাহলে container ফাইল দেখতে পাবে না।

### Database Server (Docker ছাড়া)

| Component | RAM | নোট |
|---|---|---|
| Percona PostgreSQL 18 | পুরো ১৫GB | shared_buffers 4GB, বাকি config আগের ডকুমেন্টে |

এখানে আর কিছু চলবে না — শুধুই DB।

---

## Docker-এ ৪টা জিনিস — না করলে সমস্যা

Docker = বাসা ভাড়া দেওয়ার মতো। সার্ভার একটা বিল্ডিং, প্রতিটা app একজন ভাড়াটে।

**১. প্রতিজনের ঘর ভাগ করো (memory limit)** — ভাগ না করলে এক app লোভী হয়ে সব RAM খেয়ে নেয়, অন্য app বন্ধ হয়ে যায়। .NET app বিশেষভাবে এটা করে — limit না দিলে ভাবে পুরো RAM তার। তাই প্রতিটা container-এ memory limit বাধ্যতামূলক (উপরের টেবিলের সংখ্যাগুলো)।

**২. জিনিস আলাদা গুদামে রাখো (volume)** — container মুছলে ভেতরের সব মুছে যায়। তাই RabbitMQ message, SeaweedFS ফাইল, Loki log — এগুলো volume-এ রাখো, নাহলে redeploy-তে সব হারাবে। যেগুলোতে volume লাগবেই: RabbitMQ, SeaweedFS, Loki, Grafana।

**৩. ময়লা জমতে দিও না (log rotation)** — প্রতিটা container নিজে নিজে log লেখে, জমে জমে disk ভরে যায়। প্রতি service-এ `max-size: 50m, max-file: 3` দাও।

**৪. পুরনো image মুছো** — প্রতি deploy-তে পুরনো image জমে। সপ্তাহে একবার: `docker system prune -af`

---

## আরও কিছু নিয়ম (সহজ ভাষায়)

**Image-এ version লেখো, `:latest` না** — `salescom/executor:1.4.2` লেখো। তাহলে সমস্যা হলে আগের version-এ ফিরে যাওয়া সহজ। `:latest` লিখলে কোনটা চলছে বোঝাই যায় না।

**`restart: unless-stopped` দাও** — app crash করলে বা সার্ভার reboot হলে নিজে নিজে আবার চালু হবে।

**সময় ঠিক রাখো** — প্রতি container-এ `TZ=Asia/Dhaka` দাও, নাহলে log-এ সময় UTC-তে দেখাবে, debug-এ গোলমাল হবে।

**Health check দাও** — Docker নিজে চেক করবে app বেঁচে আছে কিনা, মরে গেলে restart করবে।

**Container-রা কীভাবে কথা বলে:**
- একই সার্ভারের ভেতরে → service নাম দিয়ে (যেমন executor → `http://loki:3100`)
- আলাদা সার্ভারে → IP/hostname দিয়ে (যেমন executor → AI02-র RabbitMQ → `amqp://10.x.x.x:5672`)
- DB → DB server-এর IP (Docker-এর বাইরে বলে সরাসরি IP)

---

## শুরু কীভাবে করবে

তুমি Docker-এ নতুন, তাই:

- **Kubernetes, Swarm — এখন ভুলে যাও।** ওগুলো বড় system-এর জন্য, তোমার ৪ সার্ভারে দরকার নেই।
- **Docker Compose দিয়ে শুরু করো।** প্রতি সার্ভারে একটা `docker-compose.yml` ফাইল — সেখানে লেখা থাকবে কোন app চলবে, কত RAM, জিনিস কোথায়।
- চালু করতে: `docker compose up -d`
- বন্ধ করতে: `docker compose down`

প্রতি সার্ভারে আলাদা compose ফাইল:
```
APP01     → docker-compose.yml  (web, api, hangfire)
APP02     → docker-compose.yml  (web, api, hangfire — একই)
AI01      → docker-compose.yml  (executor, disbursement)
AI02      → docker-compose.yml  (rabbitmq, seaweedfs, loki)
Airflow   → docker-compose.yml  (airflow)
Database  → কোনো compose না, সরাসরি Percona PG
```

---

## এক মাস পরে

উপরের RAM/CPU সংখ্যাগুলো **starting point** — চূড়ান্ত না। Grafana-তে এক মাস ব্যবহার দেখে adjust কোরো। কোনো app যদি limit-এ বারবার ঠেকে যায়, তাকে বাড়িয়ে দাও; কোনোটা যদি অনেক কম খায়, কমিয়ে অন্যকে দাও।
