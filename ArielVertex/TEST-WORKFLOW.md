# Ariel Vertex — Manual Test Workflow

A step-by-step script for a tester to validate the portal end to end. Follow the sections in order —
Section B is a single connected story (the MIB project), so earlier steps set up later ones.

## Setup

- Start the app (local dev): API `dotnet run` in `backend/src/ArielVertex.Api`, web `npm run dev` in `frontend`.
- Open **http://localhost:5173**.
- **Every account's password is `Ariel@123`.** On the login screen you can click a demo account to auto-fill.

### Primary personas (under test)

| Person | Role | Login email |
|---|---|---|
| **Maria** | Project Coordinator | `maria@arielsoftwares.in` |
| **Shepherd** | Project Manager | `shepherd@arielsoftwares.in` |
| **Mareena** | HR Manager | `mareena@arielsoftwares.in` |
| **Surbeen** | HR Director | `surbeen@arielsoftwares.in` |
| **Arc** | CEO | `arc@arielsoftwares.in` |
| **Arveen** | Business Director | `arveen@arielsoftwares.in` |

### Supporting accounts (needed for hand-offs in the workflow)

| Person | Role | Login email |
|---|---|---|
| Nikhil | Technical Lead | `nikhil@arielsoftwares.in` |
| Rahul | Developer | `rahul@arielsoftwares.in` |
| Priya | Developer | `priya@arielsoftwares.in` |
| Rajat | System Admin | `rajat@arielsoftwares.in` |

> Legend for the Result column: ✅ Pass · ❌ Fail · ✏️ note anything unexpected.

---

## Section A — Login & role landing (smoke test)

For each persona: log in, confirm the correct dashboard loads, then sign out (top-right avatar → Sign out).

| # | Steps | Expected result | Result |
|---|---|---|---|
| A1 | Log in as **Arc** (CEO) | Lands on **Admin/company dashboard**; sidebar shows Resource Visibility, Employees, Reports, Admin & Audit | |
| A2 | Log in as **Mareena** (HR Manager) | Lands on **HR dashboard**; shows "Feedback to approve", hiring, review requests | |
| A3 | Log in as **Surbeen** (HR Director) | Same HR dashboard & capabilities as Mareena | |
| A4 | Log in as **Shepherd** (PM) | Lands on **Delivery dashboard**; "My Projects", "Reviews to run", missing updates | |
| A5 | Log in as **Maria** (PC) | Lands on **Delivery dashboard** (coordinator view) | |
| A6 | Log in as **Arveen** (Business Director) | Lands on **Business dashboard**; sees assigned project summaries | |
| A7 | For every persona above, check the left sidebar | Only sections that role is allowed to use appear (e.g. Arveen has **no** Admin/Employees) | |

---

## Section B — End-to-end MIB project workflow (the main story)

This follows one project (**MIB Portal**) through the whole lifecycle across roles.

### B1 — Maria (Project Coordinator) sets up the workspace

| # | Steps | Expected result | Result |
|---|---|---|---|
| B1.1 | As **Maria**, open **Projects → MIB Portal** | Workspace opens with tabs: Overview, Team, Documents, Customer Calls, Status Updates, Reviews, Business Comments | |
| B1.2 | **Team** tab → **Add member** → pick an employee, set role & allocation → Add | Member appears in the team list; success toast | |
| B1.3 | **Documents** tab → **Add document** → choose a file (PDF/image) → Upload | File appears with its size; a **Download** button works and returns the file | |
| B1.4 | **Customer Calls** tab → **Schedule call** → title + date/time → Schedule | Call is created with a **Teams join link** (placeholder in local mode) | |
| B1.5 | **Business Comments** tab → type a message → Post | Message appears in the thread under Maria's name | |

### B2 — Developer activity (supporting: Rahul)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B2.1 | Log in as **Rahul** → **Status Updates** | Sees his own updates + a submit form limited to his assigned projects | |
| B2.2 | Submit an update for MIB (work done, hours, status) | Update saved; appears in "My recent updates" | |
| B2.3 | Try to submit for a project he's **not** on (not offered in the dropdown) | He can only pick assigned projects — no way to report on others | |

### B3 — Shepherd (Project Manager) runs delivery

| # | Steps | Expected result | Result |
|---|---|---|---|
| B3.1 | As **Shepherd**, open MIB → **Status Updates** tab | Sees the whole team's updates incl. Rahul's; missing-update people are visible | |
| B3.2 | Sidebar → **Hiring Requests** → **Raise request** (role, skills, count) | Request created; HR is notified | |
| B3.3 | Sidebar → **Status Updates** → the consolidated summary control (top-right) → pick MIB | Shows "X of Y reporting" and lists who is missing today | |

### B4 — Review cycle (Mareena → Shepherd/Maria → Nikhil)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B4.1 | As **Mareena** (HR) → **Reviews** → **Request review** → project MIB, subject **Rahul**, type **Code Review**, assign **Nikhil** | Request appears with status **Open**; Nikhil notified | |
| B4.2 | As **Shepherd** (or Maria) → **Reviews** → on that request click **Schedule** → set date/time → Schedule | Status → **Scheduled**; a **Teams link** is generated; subject + reviewer notified | |
| B4.3 | Log in as **Nikhil** → **Reviews** → open the assigned review → **Submit outcome** → set star ratings + notes | Status → **Completed**; "Outcome recorded" shows; requester notified | |

### B5 — Feedback approval (Shepherd → Mareena/Surbeen)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B5.1 | As **Shepherd** → **Feedback** → **New feedback** → subject **Rahul**, set competency sliders, summary, **internal notes**, tick "Submit for approval" | Feedback saved with status **Awaiting HR Approval** | |
| B5.2 | As **Mareena** (HR) → **Feedback** | Sees the submitted feedback incl. competency scores; **Approve** / **Request revision** buttons | |
| B5.3 | Click **Approve**, then **Publish to employee** | Status → **Published**; Rahul notified | |
| B5.4 | (Optional) As **Surbeen** (HR Director) → **Feedback** | Has the same approve/publish powers as Mareena | |

### B6 — Performance report (Mareena)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B6.1 | As **Mareena** → **Performance Reports** → select **Rahul**, period `2026-Q2` → **Generate report** | A **draft** score card appears with weighted category breakdown + data sources | |
| B6.2 | Click **Approve & publish** | Report becomes **Published**; Rahul notified | |
| B6.3 | On any report row → **PDF** | A print-ready, branded report opens; **Save as PDF** works | |

### B7 — Employee sees only approved data (supporting: Rahul)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B7.1 | Log in as **Rahul** → **My Performance** | Sees the published score ring, category breakdown, trend, and published feedback — **Save as PDF** available | |
| B7.2 | Confirm no internal/management notes are visible anywhere on his dashboard | Only constructive, approved content is shown | |

### B8 — Business visibility (Arveen)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B8.1 | As **Arveen** (Business Director) → **Projects → MIB → Business Comments** → post a reply | Message posts; she sees the existing thread | |
| B8.2 | MIB → **Status Updates** tab | She sees a **consolidated, client-safe** view — no internal notes or raw blockers | |
| B8.3 | MIB → **Documents** | She can see business/client-shareable docs but **not** internal-only files | |

### B9 — Executive view (Arc)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B9.1 | As **Arc** (CEO) → **Dashboard** | Company-wide stats: active projects, employees, open reviews, resource occupancy donut | |
| B9.2 | **Reports** → **PDF** | A print-ready **Management Report** (project register + resource availability) opens | |
| B9.3 | **Resource Visibility** | Allocation across the org: free / partially available / fully allocated / overloaded | |

### B10 — System administration (Rajat)

| # | Steps | Expected result | Result |
|---|---|---|---|
| B10.1 | As **Rajat** (System Admin) → **Admin & Audit** | Microsoft 365 integration panel (all "Stubbed" in local mode), sync history, audit trail | |
| B10.2 | **Employee Sync → Run sync** | A sync run is recorded (0 counts in local mode) with a clear message | |
| B10.3 | Confirm the **Audit Trail** lists earlier actions (logins, review scheduling, feedback publish, uploads) | Every sensitive action from this test appears | |

---

## Section C — Security & permission checks (should all be blocked)

| # | Steps | Expected result | Result |
|---|---|---|---|
| C1 | As **Rahul**, in the browser change the URL to a project he isn't on (e.g. `/projects/2`) | "You do not have access" / access denied — **not** the project | |
| C2 | As **Rahul**, open **My Performance** before HR publishes a new report | The new draft is **not** visible — only published reports | |
| C3 | As **Arveen** (business), confirm no **Admin**, **Employees**, or **Performance Reports** in her sidebar | Those sections are hidden and unreachable | |
| C4 | Enter a wrong password on login | Clear "Invalid email or password" message; no access | |
| C5 | As **Maria/Shepherd**, upload a document with a blocked type (e.g. rename a file to `.exe`) | Upload is rejected with a clear message | |
| C6 | Rapidly submit the login form ~15 times with a bad password | After ~12 attempts you get rate-limited (temporary block) | |

---

## Section D — UX & polish

| # | Steps | Expected result | Result |
|---|---|---|---|
| D1 | Top bar → sun/moon icon | Whole app switches between light and dark cleanly | |
| D2 | Press **Ctrl/⌘ + K** | Command palette opens; typing filters sections; Enter navigates | |
| D3 | Bell icon | Notifications dropdown lists alerts; "Mark all read" clears the badge | |
| D4 | Narrow the browser window / open on a phone | Sidebar collapses into a hamburger drawer; layout stays usable | |
| D5 | Any create/save action | A success toast appears; forms show validation on empty required fields | |

---

## Sign-off

| Field | |
|---|---|
| Tester name | |
| Build / date | |
| Environment (local / staging) | |
| Total pass / fail | |
| Blocking issues | |
