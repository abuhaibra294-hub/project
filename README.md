> **GitHub:** To create a new private DareCare.Backend repository without touching unrelated repos, run `PUBLISH_TO_GITHUB.cmd` on Windows. See `docs/GITHUB_PUBLISH_AR.md`.

# DareCare — ASP.NET Core .NET 10 Backend (Sprint 6 / Team Handoff)

A modular-monolith REST API for a **home-care graduation project** with four roles: `Patient`, `Nurse`, `Pharmacy`, and `Admin`. Uses ASP.NET Core Identity, JWT/refresh sessions, EF Core, and an SQL Server production schema. For fast local development, an **opt-in Development-only SQLite** provider creates an isolated demo DB automatically.

**Windows quick start: extract ZIP and double-click `START_DARECARE.cmd`.**

The script checks .NET 10 SDK, restores dependencies, configures a random local signing key and admin login, builds, runs unit tests, starts the API, and opens a local Arabic API lab. See **`START_HERE_AR.md`** for step-by-step Arabic instructions. `START_SQLSERVER.cmd` uses SQL Server LocalDB with EF migrations. `SMOKE_DARECARE.cmd` tests the synthetic flow after the API is running. `DEMO_SESSION.cmd` also opens the Arabic lab and session agenda, and writes synthetic logins locally.

## Modules

- Identity: Patient/Nurse self-registration, Admin-only Pharmacy invitation, JWT, refresh rotation/revocation, password reset, optional email confirmation, account approval and role checks
- Care: Nurse profiles, skills, availability slots, booking state machine, optimistic concurrency and audit
- Matching: Explainable rule-based ranking (skill/availability/city/experience); **not ML or medical advice**
- Pharmacy: Private prescription upload/download, pharmacy quotes, patient consent, delivery state machine, in-app inbox notifications
- Reviews: one patient rating (1-5) per completed booking, private comments, public aggregates feed matching
- Dashboard: role-scoped counts for Patient / Nurse / Pharmacy / Admin
- Admin: Approvals, user account status, audit log and aggregated overview
- Development lab: Arabic authentication/workflow UI, local test mailbox (admin/loopback/Development only)
- Patient address book: private address CRUD, exactly one default, snapshot into new bookings
- Favorites: patients save approved nurses and view favorites without exposing private details
- Support: owner/admin tickets, private threaded replies, triage statuses, audit and in-app alerts
- Discovery and operations: paginated nurse/service search, nurse calendar, booking search/timeline, bulk notification read and support dashboard totals

## Project layout

```text
DareCare.slnx
START_DARECARE.cmd / START_SQLSERVER.cmd / TEST_DARECARE.cmd
SMOKE_DARECARE.cmd / STOP_DARECARE.cmd / SHOW_APIS.cmd
src/
  DareCare.Domain/
  DareCare.Application/
  DareCare.Infrastructure/
  DareCare.Api/
tests/
  DareCare.Domain.Tests/
  DareCare.Api.Tests/
.env.example
.config/dotnet-tools.json
scripts/
docs/
```

### Security and operating constraints

This is **MVP/dev code, not production-ready**. Do not use live prescriptions or patient information. Real healthcare use requires compliance/privacy review, professional approval processes, encrypted object storage, file malware scanning, secrets management, backups, hardening and real integration tests. The demo e-mail mailbox retains codes **in memory only** and is accessible only in local Development for an authenticated administrator. The development admin password stays outside source control. Prescriptions are stored outside `wwwroot`, and every download checks object ownership.

## Local developer commands

```powershell
dotnet restore DareCare.slnx
dotnet build DareCare.slnx --no-restore
dotnet test DareCare.slnx --no-build
dotnet run --project src/DareCare.Api --launch-profile http
```

For SQL Server select provider `SqlServer`, set `ConnectionStrings:DefaultConnection` and `Jwt:Key` in dotnet user-secrets, restore EF CLI tool, create an initial migration if one is not already committed, then update the DB. The SQL Server CMD automates that if LocalDB is present.

**Build limitation:** .NET SDK and SQL Server were unavailable in the artifact-generation environment, so C# compilation, local Windows CMD execution and DB-backed tests have **not** been verified. Use the bundled build/unit/smoke scripts on Windows; `docs/SPRINT5_QA.md` has the new checks. The one-click SQLite demo uses a fresh Sprint 5 database filename and does not overwrite Sprint 4 data.

## Team session on 23 October 2026

Read `docs/SESSION_TODAY_AR.md` for a suggested 8-10 minute demo, an honest done-vs-not-verified checklist, endpoint contracts and task split. After starting the API using `START_DARECARE.cmd`, double-click `DEMO_SESSION.cmd` to exercise a fully synthetic flow and open the Arabic lab. Do not show the `.localdata` credential files on shared screens.

## Sprint 5 REST API reference

- `docs/SPRINT5_API_CONTRACTS.md`: 24 new endpoints, auth roles, filters, request JSON examples, status semantics, legacy compatibility.
- `docs/SPRINT5_QA.md`: run-and-verify checklist, including cross-account 403/404 cases and status state machines.
- `DareCare.http`: sample requests for VS Code REST Client / Visual Studio HTTP files.
- `SHOW_APIS.cmd`: open the complete API reference, HTTP examples and OpenAPI JSON locally.
- Run `SMOKE_DARECARE.cmd` after `START_DARECARE.cmd` to exercise the expanded synthetic workflow.

The quick-start SQLite DB is now `darecare-sprint5-dev.db`; SQL Server LocalDB uses a separate `DareCareSprint5Db`. Existing Sprint 4 demo data remains on disk, but is **not automatically migrated**. Never apply a new initial migration over a non-empty database with unknown migration history.

## Sprint 6 — Flutter/Web Integration Handoff and Render Staging Blueprint

- `integration/DareCare_Postman_Collection.json`, `integration/DareCare_Local.postman_environment.json`: source-derived client requests with role-separated auth variables, no shared secrets.
- `integration/DareCare_OpenAPI_SourceSnapshot.json`: **static review snapshot**; `/openapi/v1.json` is authoritative after the server actually starts.
- `integration/DareCare_API_Endpoints.json`: machine-readable route and roles manifest.
- `integration/client-examples/`: Flutter Dart and browser JavaScript starters for consuming the API (not production auth-storage designs).
- `docs/TEAM_HANDOFF_AR.md`: detailed handoff to Flutter and web developers.
- `OPEN_TEAM_PACKAGE.cmd`, `VERIFY_TEAM_API.cmd`, `EXPORT_LIVE_OPENAPI.cmd`: open package, verify live endpoints and export runtime OpenAPI.
- `/docs`: a minimal read-only API Explorer in Development and optionally Staging. It never exposes tokens or the Development test mailbox.
- `render.yaml`, `docs/RENDER_DEPLOY_AR.md`: **not yet deployed**, paid Render staging blueprint with external SQL Server, SMTP, persistent disk and readiness checks.

**Do not claim production readiness or a public URL until Windows .NET build/tests, EF migrations, HTTPS deploy and runtime smoke testing have passed.**

## Sprint 7 — availability transaction safety and privacy regression tests

- Creating a nurse availability slot now checks overlaps **inside a serializable transaction**, so simultaneous API requests cannot both commit overlapping windows. Known SQL Server/SQLite write collisions produce HTTP 409 (reload and retry); unexpected DB errors are not suppressed.
- Added SQLite-backed controller and persistence tests for slot overlaps/adjacency, active-booking uniqueness, patient/pharmacy resource ownership, and notification isolation. No existing API routes or response shapes were changed.
- CI builds/tests and starts the Docker demo, but Render Free uses **disposable SQLite data**. SQL Server multi-instance race testing and real HTTP integration testing remain release gates.
- QA instructions: `docs/SPRINT7_BACKEND_STABILIZATION_AR.md`.
