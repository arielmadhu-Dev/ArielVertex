# Ariel Vertex — Deployment Guide

Two ways to run: **local dev** (zero setup, SQLite) and **production** (Docker + PostgreSQL).

---

## 1. Local development

```
# API — http://localhost:5041 (SQLite auto-creates + seeds)
cd backend/src/ArielVertex.Api && dotnet run

# Web — http://localhost:5173
cd frontend && npm install && npm run dev
```
Sign in with any seeded account (password `Ariel@123`) — e.g. `amit@arielsoftwares.in`.

---

## 2. Production (Docker Compose)

Requirements: Docker + Docker Compose.

```
cp .env.example .env          # then edit .env (see below)
docker compose up -d --build  # builds web + api, starts Postgres
```
Open **http://localhost:8080** (put your TLS-terminating reverse proxy / load balancer in front for HTTPS).

The stack:
- **db** — PostgreSQL 16 (named volume `db-data`)
- **api** — ASP.NET Core 8 (schema auto-creates + seeds on first boot; uploads on volume `uploads`)
- **web** — nginx serving the built SPA, proxying `/api` → api

### Required `.env` values
| Var | Purpose |
|---|---|
| `DB_PASSWORD` | Postgres password |
| `JWT_SECRET` | Long random string — signs app tokens. **Rotate.** |
| `PUBLIC_ORIGIN` | Public URL of the portal (CORS allow-list) |
| `SEED_PASSWORD` | Password for seeded demo accounts (Local mode) |
| `ALLOWED_DOMAIN` | Email domain allowed to sign in |
| `AUTH_MODE` | `Local` or `Entra` |

> All API config can be overridden by environment variables using the `Section__Key` convention
> (e.g. `Jwt__Secret`, `ConnectionStrings__Default`). Never bake secrets into image or source.

---

## 3. Enabling Microsoft 365 (Entra + Graph)

The integration is fully coded and ships **off**. To go live:

1. **Create the Entra app registration** (see `Microsoft_365_Graph_API_App_Registration_Setup_Guide`).
   - Single-tenant. Add a **SPA redirect URI** = your portal origin (e.g. `https://portal.arielsoftwares.in`).
   - Add a **client secret**.
   - **API permissions** (application, admin-consented): `User.Read.All` (sync), `Calendars.ReadWrite`
     (Outlook calendar invites with Teams links), and `Mail.Send` (Outlook delivery). Delegated
     `openid profile email User.Read` for login.
2. **Backend `.env`:**
   ```
   AUTH_MODE=Entra
   AZURE_TENANT_ID=<tenant id>
   AZURE_CLIENT_ID=<client id>
   AZURE_CLIENT_SECRET=<secret>            # keep in a secret store
   AZURE_SERVICE_USER=operations@arielsoftwares.in   # mailbox/calendar used for meetings & mail
   DIRECTORY_SYNC_LIVE=true
   GRAPH_MEETINGS_LIVE=true
   OUTLOOK_NOTIFICATIONS_LIVE=true
   ```
3. **Frontend** — provide MSAL config at build time so the "Sign in with Microsoft" button appears:
   ```
   VITE_MSAL_CLIENT_ID=<client id>
   VITE_MSAL_TENANT_ID=<tenant id>
   ```
4. Verify on the **Admin → Integration** panel: each flag should read **Live**. Run an employee sync
   from **Admin → Employee Sync → Run sync**.

What each flag does when live:
- **Microsoft login** — SPA gets an Entra token (MSAL) → `POST /auth/microsoft` validates it against the
  tenant keys, enforces the allowed domain, provisions/maps the user, and issues the app JWT (RBAC unchanged).
- **Directory sync** — pulls Entra users, departments, and manager relationships via Graph and upserts them (keyed on email); disabled users → Inactive. HR can keep selected profile or manager fields locally managed from the Employees page.
- **Meetings** — reviews/calls create real Outlook events with Teams join links.
- **Outlook delivery** — high-signal notifications are also emailed via Graph.

---

## 4. Database

- Dev: SQLite file `arielvertex.db` (auto-created). Delete it to reset.
- Prod: PostgreSQL. Schema is created on first boot. **Back up the `db-data` volume** and the `uploads`
  volume regularly. For controlled schema evolution, adopt EF Core migrations before your first release
  (the app currently uses `EnsureCreated`, which creates missing tables but does not alter existing ones).

## 5. Operations

- **Health**: `GET /health` (liveness), `GET /health/ready` (DB readiness).
- **Automation** runs in-process on a timer (`Automation__IntervalMinutes`, default 6h): missing-update
  reminders, quarter-end feedback requests, monthly report drafts. Trigger on demand from **Admin → jobs**.
- **Rate limiting**: 240 req/min per IP globally; 12/min on `/auth`.
- **Audit**: every sensitive action is recorded (Admin → Audit).
- Scale the `api` service horizontally behind the load balancer; it is stateless apart from the DB and the
  uploads volume (move uploads to Azure Blob/S3 by implementing `IFileStorage` for multi-node).

## 6. Security checklist before go-live
- [ ] Set a strong `JWT_SECRET` and `DB_PASSWORD`; store secrets in a vault, not `.env` on disk.
- [ ] Terminate TLS at the proxy; set `EnforceHttps=true` if the API is directly exposed.
- [ ] Restrict `Cors__Origins` to your real portal origin(s).
- [ ] Grant only the minimum Graph permissions; document admin consent.
- [ ] Add antivirus scanning at the marked hook in `LocalFileStorage.SaveAsync` if required.
