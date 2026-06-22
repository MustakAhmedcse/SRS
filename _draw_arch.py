# -*- coding: utf-8 -*-
from PIL import Image, ImageDraw, ImageFont

W, H = 1180, 760
SCALE = 2  # supersample for crispness
img = Image.new("RGB", (W*SCALE, H*SCALE), "white")
d = ImageDraw.Draw(img)

def font(size, bold=False):
    path = r"C:\Windows\Fonts\arialbd.ttf" if bold else r"C:\Windows\Fonts\arial.ttf"
    return ImageFont.truetype(path, size*SCALE)

F_TITLE = font(15, True)
F_BODY  = font(11)
F_SMALL = font(10)
F_NOTE  = font(10, True)

def rrect(x, y, w, h, radius, fill, outline=None, width=1):
    d.rounded_rectangle([x*SCALE, y*SCALE, (x+w)*SCALE, (y+h)*SCALE],
                        radius=radius*SCALE, fill=fill, outline=outline, width=width*SCALE)

def text(x, y, s, fnt, fill, center_w=None):
    if center_w:
        tw = d.textlength(s, font=fnt)
        x = x*SCALE + (center_w*SCALE - tw)/2
        d.text((x, y*SCALE), s, font=fnt, fill=fill)
    else:
        d.text((x*SCALE, y*SCALE), s, font=fnt, fill=fill)

def box(x, y, w, h, title, lines, color, title_color="white", body_color="#222222"):
    # shadow
    rrect(x+2, y+3, w, h, 10, "#E2E2E2")
    # body
    rrect(x, y, w, h, 10, "white", outline=color, width=2)
    # header strip
    d.rounded_rectangle([x*SCALE, y*SCALE, (x+w)*SCALE, (y+30)*SCALE], radius=10*SCALE, fill=color)
    d.rectangle([x*SCALE, (y+18)*SCALE, (x+w)*SCALE, (y+30)*SCALE], fill=color)
    text(x, y+7, title, F_TITLE, title_color, center_w=w)
    yy = y + 38
    for ln in lines:
        text(x+12, yy, ln, F_BODY, body_color)
        yy += 17

def arrow(x1, y1, x2, y2, label=None, color="#555555"):
    d.line([x1*SCALE, y1*SCALE, x2*SCALE, y2*SCALE], fill=color, width=2*SCALE)
    # arrowhead (pointing down assumed)
    a = 6
    d.polygon([(x2*SCALE, y2*SCALE), ((x2-a)*SCALE, (y2-2*a)*SCALE), ((x2+a)*SCALE, (y2-2*a)*SCALE)], fill=color)
    if label:
        d.text(((x1+8)*SCALE, ((y1+y2)/2-7)*SCALE), label, font=F_SMALL, fill="#666666")

# ---- palette ----
C_PRES = "#438DD5"   # presentation - light blue
C_API  = "#08427B"   # application - dark blue
C_PG   = "#336791"   # postgres blue
C_MQ   = "#F26E21"   # BL orange - message bus
C_S3   = "#2E8B57"   # sea green - storage
C_CALC = "#5B4B9E"   # calc - purple
C_EXT  = "#8A8A8A"   # external - gray

# 1. Presentation
box(30, 26, 1120, 70, "PRESENTATION — Next.js / React / TypeScript",
    ["Talks to the backend only over HTTPS /api carrying the SalesCom JWT.",
     "No business logic: renders the wizard, lists, approval, dashboard."], C_PRES)

arrow(590, 96, 590, 122, "HTTPS (JWT per request)")

# 2. Application / API
box(30, 124, 1120, 132, "APPLICATION / API — .NET ASP.NET Core (4 layers)",
    ["Api — controllers, DTOs, JWT + RBAC middleware, DI, Hangfire",
     "Application — use-case handlers, validators, ports     Domain — entities, int enums, business rules",
     "Infrastructure — Dapper + EF Core, SeaweedFS, RabbitMQ, Central Login, SMS/SMTP, Hangfire jobs",
     "Owns: auth, wizard save, the IR, trusted-path money writes, approval state machine, notifications."],
    C_API)

# arrows to 3 stores
arrow(180, 256, 180, 300, "Dapper / EF Core")
arrow(590, 256, 590, 300, "publish / consume")
arrow(1000, 256, 1000, 300, "S3 API")

# 3. Stores row
box(30, 302, 320, 120, "PostgreSQL (Percona 18)",
    ["schema salescomdbtst", "21 tables + per-run temp tables", "JSONB IR · NUMERIC money"], C_PG)
box(380, 302, 380, 120, "RabbitMQ — event bus",
    ["q.sql-generate", "q.run.high / mid / low", "q.ev-disburse  (+ DLQs)"], C_MQ)
box(790, 302, 360, 120, "SeaweedFS (S3-compatible)",
    ["uploads · per-stage outputs", "POS dump files", "one server · TTL auto-expire"], C_S3)

# 4. calc services under RabbitMQ
arrow(470, 422, 470, 452)
arrow(660, 422, 660, 452)
box(380, 454, 180, 86, "SQL Generator",
    ["IR -> SQL (Python,", "SQLGlot) -> section_", "wise_report_sqls"], C_CALC)
box(580, 454, 180, 86, "SQL Executor",
    [".NET — run stages ->", "final_commissions", "(trusted path)"], C_CALC)

# side note box (workers)
box(790, 454, 360, 86, "Background jobs", [], "#6B7280")
d.text((804*SCALE, 492*SCALE), "Hangfire (.NET): Scheduler · EV Disbursement ·", font=F_SMALL, fill="#333")
d.text((804*SCALE, 508*SCALE), "Notification · stale-run · object-purge", font=F_SMALL, fill="#333")
d.text((804*SCALE, 524*SCALE), "Airflow: POS dump · hourly user-sync · ETL", font=F_SMALL, fill="#333")

# 5. External systems
box(30, 568, 1120, 110, "EXTERNAL SYSTEMS (7 integration points — contracts-first)", [], C_EXT)
ext = [
 "Central Login (SSO + OTP)        EV API (10.13.2.7:9898)        SMS gateway (172.16.7.210:13082)",
 "Email / SMTP        Source systems via Airflow ETL (DWH / In-house / vPeople / POSDMSDB)",
 "POS (CSV handoff)        RSO App (consumes the SalesCom public API)",
]
yy = 606
for ln in ext:
    d.text((48*SCALE, yy*SCALE), ln, font=F_BODY, fill="white")
    yy += 21

# footer note
d.text((30*SCALE, 700*SCALE), "Everything is deployed in Docker except the production database (bare-metal Percona PostgreSQL 18).",
       font=F_SMALL, fill="#666666")

img = img.resize((W, H), Image.LANCZOS)
img.save("architecture.png")
print("architecture.png written", img.size)
