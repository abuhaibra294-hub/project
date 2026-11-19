# DareCare — Deployment Strategy | استراتيجية النشر والتشغيل

**Goal:** reproducible synthetic-data staging for the graduation team, with a separately gated path to durable staging. Do not mistake a green CI job for a successfully deployed healthcare service.

## 1. Environment decisions

| Environment | Infrastructure | Data | Intended use |
| --- | --- | --- | --- |
| Local Development | `dotnet run` or local Docker, loopback API | Disposable SQLite or isolated SQL Server LocalDB | Unit tests and synthetic feature work |
| Current free demo | Render Free Docker service from `render.yaml` | SQLite and private uploads under temporary paths | Synthetic demos; restarts/redeploys can lose everything |
| Proposed durable staging | Render Starter + external SQL Server + persistent disk from `deployment/render-sqlserver-staging.yaml` | Private DB, prescription files and backup plan | Realistic *fictional* end-to-end tests; not certified healthcare production |
| Alternative staging | VPS Docker Compose and Caddy 2 from `deployment/compose.staging.yml` | External SQL Server + named volume | Only after DNS, TLS, storage, credentials, and operations approval |
| Real production | Out of current graduation-project scope | Requires additional controls | No personal medical records until legal, security and clinical due diligence |

**Status at documentation time:** The repository contains both blueprints. No evidence was obtained of a paid durable staging installation or a permanently persisted data store on Render.

## 2. Deployment architecture principles
- API is a single ASP.NET Core modular monolith behind HTTPS. Flutter and external web clients communicate with `/api/v1`.
- The current DareCare Lite and Admin UI may be served on the same API origin in Staging. Separate web origins require exact `Cors:AllowedOrigins` entries and browser preflight validation; Flutter mobile is not subject to browser CORS.
- SQL Server owns relational records; prescription binaries stay in *non-public* private storage. Files and DB must be backed up together.
- SMTP handles delivery of account email codes/invitations. Google OAuth verifies patient identity, but it is not a general Admin login.
- Render Free uses `DOTNET_ENVIRONMENT=Staging` with `Database__Provider=Sqlite` and `Demo__AllowStagingSqlite=true`. Do **not** reuse these settings for durable staging.

## 3. Prerequisites for durable staging (implementation checklist)
1. Approve candidate commit and API schema; tag the tested source revision and record target URL. Branch of record is `darecare-demo-free`, not the unrelated repository default `main`.
2. Provision a **new, empty** SQL Server database reachable via encrypted TLS. Runtime credentials must be least-privileged and distinct from migration credentials.
3. Review the committed migration script and backups; produce an idempotent SQL migration script. A schema initially created with SQLite or `EnsureCreated` is not interchangeable with SQL Server migrations.
4. Provision writable persistent *private* prescription storage (e.g., `/data/prescriptions` on supported Render disk); enforce filesystem permissions. Render disk alone is not an automated backup.
5. Configure an **absolute, persistent, access-restricted** `Auth__DataProtectionKeyDirectory`. The existing staging blueprint does not declare this variable; add it to deployment configuration before enabling authenticator factors across restarts, and ensure keys are backed up/restored as required.
6. Set environment-only `ConnectionStrings__DefaultConnection`, `Jwt__Key`, OAuth client IDs, and SMTP credentials; never commit them. Set `Auth__RequireConfirmedEmail=true` only when SMTP verification is working.
7. Specify exact HTTPS frontend origin(s) via `Cors__AllowedOrigins__0` (additional indexed entries as needed). Validate that unauthorized origins are denied.
8. Plan Admin bootstrap as a one-time controlled action. Disable it and remove bootstrap credentials immediately afterward; no anonymous Admin registration.
9. Set `Database__Provider=SqlServer` and `Demo__AllowStagingSqlite=false`. The Dockerfile's demo defaults must be overridden by the deployment environment.
10. Configure backups of SQL data, prescription objects/volume and key material, encrypted at rest and with a tested restore procedure. Do not use real patient records.

## 4. Build, review and database migration
From a checked-out tagged revision on an authorized workstation with .NET 10 SDK and SQL Server access:

```bash
dotnet restore DareCare.slnx
dotnet tool restore
dotnet build DareCare.slnx --configuration Release
dotnet test DareCare.slnx --configuration Release
python3 scripts/generate_integration.py --check
dotnet ef migrations has-pending-model-changes --project src/DareCare.Infrastructure --startup-project src/DareCare.Api
dotnet ef migrations script --idempotent --project src/DareCare.Infrastructure --startup-project src/DareCare.Api --output sqlserver-migration-review.sql
```

A reviewer must inspect `sqlserver-migration-review.sql` and approve the target DB **before** executing the reviewed migration:

```bash
dotnet ef database update --project src/DareCare.Infrastructure --startup-project src/DareCare.Api
```

Do not run migrations automatically on each web-instance startup. For existing SQL databases inspect `__EFMigrationsHistory` and take a verified backup before migration.

## 5. Two supported staging options

### Option A — Render Starter (proposed)
- Start with `deployment/render-sqlserver-staging.yaml`; connect the intended DareCare branch, confirm the revision to deploy, and provision the external SQL Server independently.
- Set DB/SMTP/JWT/CORS/Google/DataProtection settings in the provider's secret UI; never in the blueprint file.
- Attach the persistent disk at `/data`, configure private storage at `/data/prescriptions` and keys at a separate protected directory such as `/data/dataprotection`.
- Apply migrations from a trusted operator/CI release step **before** deploying a schema-dependent API build.
- Verify disk ownership/writes for the non-root container user and SQL migration access outside the app.
- Render's `autoDeployTrigger: checksPass` does not substitute for a deployment-time smoke test or a schema compatibility decision.

### Option B — VPS + Docker Compose (alternative)
- Provision Linux VPS, firewall/DNS, external SQL Server, certificate-ready public domain, private volume and monitored backups.
- Copy the example environment into **untracked**, permission-restricted `.env.staging` on the server. Never upload it to GitHub.
- With the prerequisites and manual migration completed, deploy using:

```bash
docker compose --env-file .env.staging -f deployment/compose.staging.yml up -d --build
```

- Caddy handles HTTPS at the configured host, reverse-proxying to the container; do not expose the SQL or internal API port directly to public clients.
- Ensure `Auth__DataProtectionKeyDirectory` and volume mount are added/configured for the VPS path before MFA is relied upon. This is a **configuration work item**, not already present in the Compose example.

## 6. Deployment acceptance / verification (all synthetic)
- [ ] CI build/unit/API drift checks pass for the candidate commit.
- [ ] SQL Server migration CI passes; migration apply and restart persistence pass on the *chosen* staging database.
- [ ] Record deployed commit SHA; GET `/health/ready` returns 200 with `status=ready`.
- [ ] Fetch *live* `/openapi/v1.json` and compare operations and signatures with the approved manifest (known source=100 vs prior live=93 discrepancy).
- [ ] Real HTTPS Flutter client logs in and books a visit; nurse accepts, starts and completes it.
- [ ] Fictional prescription upload → pharmacy quote → patient confirmation → preparation → dispatch → delivered status.
- [ ] Actual web browser preflight and authenticated fetch succeed from approved origin; invalid origin does not receive an allowed CORS response.
- [ ] Admin role gates and cross-account object ownership pass negative tests (401/403/404 as applicable).
- [ ] Restart service and verify test DB rows, prescription file and authenticator protected data survive.
- [ ] Check SMTP/Google if those features are in the acceptance scope; no unverified social-login or email claims.
- [ ] Backups and **restore dry-run** verified by an operator; observed outcomes attached to Issue #13 and #20.

## 7. Rollback and incident response
- Preserve the last known-good image/commit, schema review, backups, migration plan and timestamps.
- If application-only change fails and DB schema remains backward compatible: redeploy previous tested image, confirm readiness and core flows.
- If migration is incompatible: stop writes and use an **approved, rehearsed database restore/recovery plan**; do not blindly reverse a committed migration or delete patient/test rows.
- If prescription volume is lost: restore files and DB to a consistent snapshot; document what is recoverable and suspend relevant flows until consistency is checked.
- If JWT or OAuth secrets leak: rotate/revoke safely; assess active session impact. Do not paste secrets in incident GitHub Issues.
- Record incident owner, severity, affected revision, start/end, root cause and follow-up actions.

## 8. Release sign-off and current blockers
**Pending evidence:** exact public deployed commit, live 100-operation API contract parity, durable SQL Server setup, persistent prescription files and Data Protection keys, HTTPS browser CORS, authenticated multi-role flow, SMTP/Google activation and recovery exercise.

This document is a **deployment strategy and runbook draft**, not evidence that deployment was executed. Related open tasks: #12 (contracts), #13 (durable staging), #14 (identity), #18 (security), #20 (release acceptance).
