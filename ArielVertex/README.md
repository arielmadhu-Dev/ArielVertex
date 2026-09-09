# Ariel Vertex

**Operations, Review & Performance Platform** — an in-house portal for Ariel Software Solutions.
Projects, developer status updates, project/code reviews, structured quarterly feedback, resource
visibility, hiring requests, performance reporting, notifications and audit — in one secure,
role-based application.

> Ariel Vertex is **not** a Keka/HRMS replacement. Payroll, leave, attendance, salary and
> compliance stay in Keka. This portal owns Ariel-specific operational workflows.

---

## Tech stack

| Layer | Technology |
|---|---|
| Frontend | React + TypeScript + Vite, Tailwind, Framer Motion, TanStack Query, Recharts |
| Backend | ASP.NET Core 8 Web API, clean architecture (Domain → Application → Infrastructure → Api) |
| Database | EF Core — **SQLite** for dev, **PostgreSQL** for production (config switch) |
| Auth | App JWT + RBAC; **Microsoft Entra** login and Graph (sync/meetings/mail) drop in via config |
| Ops | In-process automation scheduler, rate limiting, health checks, audit trail, Docker + Compose |

> **Production deployment (Docker + Postgres + Entra activation): see [DEPLOYMENT.md](DEPLOYMENT.md).**

## Run it (zero setup, local dev)

Two terminals. The database auto-creates and seeds on first boot.

**1) Backend** (http://localhost:5041)
```
cd backend/src/ArielVertex.Api
dotnet run
```

**2) Frontend** (http://localhost:5173)
```
cd frontend
npm install
npm run dev
```

Open **http://localhost:5173** and sign in.

> Windows convenience: double-click `run-backend.cmd` then `run-frontend.cmd` in the project root.

### Or run the whole production stack with Docker
```
cp .env.example .env      # edit secrets
docker compose up -d --build
```
→ portal on **http://localhost:8080** (nginx SPA + API + PostgreSQL). Details in [DEPLOYMENT.md](DEPLOYMENT.md).

### Seeded accounts — password `Ariel@123`

| Email | Person / Role | Lands on |
|---|---|---|
| `arc@arielsoftwares.in` | Arc — CEO | Admin dashboard |
| `mareena@arielsoftwares.in` | Mareena — HR Manager | HR dashboard |
| `surbeen@arielsoftwares.in` | Surbeen — HR Director | HR dashboard |
| `shepherd@arielsoftwares.in` | Shepherd — Project Manager | Delivery dashboard |
| `maria@arielsoftwares.in` | Maria — Project Coordinator | Delivery dashboard |
| `arveen@arielsoftwares.in` | Arveen — Business Director | Business dashboard |
| `nikhil@arielsoftwares.in` | Nikhil — Team Lead | Employee dashboard |
| `rahul@arielsoftwares.in` | Rahul — Developer | Employee dashboard (has published performance) |
| `rajat@arielsoftwares.in` | Rajat — System Admin | Admin dashboard |
| `admin@arielsoftwares.in` | Super Admin | Admin dashboard |

A sample **MIB Portal** project is pre-loaded with team, status updates, a review request, feedback
(draft + published) and 3 months of performance history so every screen has real data.

## Security model (spec §9)

- **Capability-based authorization** — each role grants a set of capabilities (`Permissions`); every
  write endpoint is gated by an ASP.NET policy for the matching capability.
- **Project-scoped access** — `IProjectAccessService` is consulted by every project sub-resource, so a
  user can never reach another project by changing an id in the URL (privileged/HR/management excepted).
- Backend enforces all roles; the frontend guards are convenience only.
- Explicit DTOs (anti-overposting) + server-side validation + a consistent error envelope.
- **Audit trail** — login, role change, assignment, document actions, review scheduling, feedback
  approval/publish and sync are all recorded (Admin → Audit).
- Feedback & performance are **never employee-visible until HR publishes them**; internal notes are
  stored separately from the constructive summary.

## What's included

**Fully working:** Auth + RBAC, role dashboards, Projects + workspace (team, **document upload to
private storage with authorized download + visibility rules**, calls with Teams links, status
updates), Status Updates (submit + consolidated business summary + missing-update
detection), Reviews (HR request → PM/PC one-click schedule → reviewer outcome), structured Feedback
(competency scoring → HR approve → publish), **auto-generated performance reports** (HR generates a
monthly/quarterly score from status-update consistency + review outcomes + feedback via the weighted
model, reviews the transparent breakdown, then publishes), **print-ready PDF export** of any
performance report (brand-styled, browser-native), employee Performance dashboard (weighted
score, rating bands, trend), Resource visibility, Hiring requests, Employees directory + role
management, Notifications, **Business Comments** (team ↔ business thread per project), Admin
(integration status, employee-sync history, audit, manual job triggers), Reports (charts, CSV, **PDF**).

**Expense management (Front Desk):** internal expenses & payment requests with optional proof-of-
invoice upload, a **configurable** approval flow (HR Director / Accountant), and **daily + weekly
summaries** to configurable recipients (portal + Outlook email + optional Teams webhook).

**Performance Improvement Plans (PIP):** auto-initiated when an employee's approved performance /
feedback drops **below a configurable threshold** — sends a combined notification/email to HR and
the employee, and tracks the plan to outcome. HR can also create PIPs manually.

**Meetings & Minutes (Project Coordinator + HR):** capture rough meeting notes → the system
**auto-generates structured, grammar-corrected minutes** (Discussion / Decisions / Action Items) →
the organiser reviews a **preview** and edits if needed → **approve** → **send to all attendees**
(portal notification for internal attendees + Outlook email when live). Editing approved/sent minutes
returns them to preview for re-approval, so corrections are always re-sent cleanly. Minutes generation
runs **offline by default** (built-in structuring + deterministic grammar/formatting cleanup) and
plugs into **Claude for AI-grade minutes + grammar** as a config drop-in (`Ai:Enabled` + API key,
model configurable; admin can toggle AI polishing at runtime via `minutes.aiPolish`).

**Admin → Configuration (no-code):** an admin screen to edit **settings/values** (e.g. the PIP
threshold), **turn whole modules on/off** (feature flags that hide the nav *and* block the API), and
**edit email/notification templates** — all without code changes or redeploy.

**Operational automation (in-process scheduler):** missing-update reminders, quarter-end feedback
requests, monthly performance-report drafts, and the **daily/weekly expense summaries** — on a timer
and on demand from Admin.

**Microsoft 365 — fully coded, config-gated (drop-in):** Entra login (`/auth/microsoft` + MSAL button),
Graph directory sync, Graph Teams/Outlook meetings, and Outlook email notification delivery. All ship
**off**; supply your Entra app-registration values and flip the flags to activate — no code changes.
See [DEPLOYMENT.md](DEPLOYMENT.md) §3.

**Production-ready infrastructure:** PostgreSQL provider, Docker + Compose, rate limiting, security
headers, DB health checks, forwarded-headers/reverse-proxy support, secrets via environment variables.

**Parked (out of the new spec's scope):** Goals/KRA, L&D recommender, Promotion/Increment, PIP.

## Production hardening (built in)

- Config-switchable DB (`Database:Provider` = Sqlite/Postgres); secrets via `Section__Key` env vars.
- Rate limiting (global + `/auth`), security headers, `/health` + `/health/ready`, correlation ids.
- Private file storage behind authorized download endpoints (antivirus hook marked in `LocalFileStorage`).
- Full checklist and go-live steps in [DEPLOYMENT.md](DEPLOYMENT.md).
- Add rate limiting on sync/upload/report endpoints; enable structured logging sinks.

## Project layout

```
ArielVertex/
  backend/src/
    ArielVertex.Domain          # entities + enums (no dependencies)
    ArielVertex.Application      # DTOs, abstractions, permissions, performance model
    ArielVertex.Infrastructure   # EF Core, SQLite, seeder, JWT, audit/notify, Graph stubs
    ArielVertex.Api              # controllers, policies, middleware, Program.cs
  frontend/
    src/ui                       # Ariel Vertex design system
    src/components               # layout, nav, notification bell, command palette
    src/pages                    # one page per module
```
