# Sprint 4 — Reviews, role dashboards and demo automation

## Scope

- `NurseReview` stored only for a completed, owner-authorized booking, unique per booking (database constraint).
- Review comments are **private**: only the author can GET them. Public matching and nurse summary show aggregates only.
- Review score is included in the explainable nurse-matching rules-v2 score (capped to 100).
- Approved nurses with disabled accounts no longer appear on public list/free slots or new booking creation.
- `GET /api/v1/dashboard/me` returns role-scoped totals for Patient/Nurse/Pharmacy/Admin.
- New demo script creates ONLY synthetic data and writes generated login information to ignored `.localdata` directory.
- SQLite dev file changed to `darecare-sprint4-dev.db` to avoid silently applying a new schema to a Sprint 3 `EnsureCreated` DB. Older local databases are untouched.

## Acceptance scenarios (run on Windows with .NET 10)

1. Double-click `START_DARECARE.cmd` → build succeeds, unit tests succeed, `/health` responds.
2. Double-click `DEMO_SESSION.cmd` while API runs → all synthetic smoke test requests PASS, `demo-accounts.txt` created.
3. Patient reviews completed visit → 201; repeat → 409; rating 0 or 6 → 400; nurse unauthorized to view patient's comment → 403.
4. `GET /api/v1/reviews/nurses/{nurseId}/summary` reports `count=1`, `averageRating=5` after smoke.
5. Matching returns same nurse with `reviewCount=1`, `averageRating=5`, after second free slot is added.
6. `/api/v1/dashboard/me` returns only role-specific counts for all four roles.
7. Unapproved or disabled nurse cannot be booked or listed publicly.
8. Real patients, real prescriptions, production secrets, and public network exposure are NOT used.

## SQL Server LocalDB

Use `START_SQLSERVER.cmd` for an isolated Sprint 4 DB `DareCareSprint4Db` by default; the original Sprint 3 DB is not modified. The script creates EF migrations as required. When upgrading an **existing** SQL Server database, first take a backup and review generated EF migrations manually; don't rely on `EnsureCreated` for migration. An existing stored `ConnectionStrings:DefaultConnection` secret takes precedence over the default.

## Verified in artifact generation (offline)

- ZIP file listings and archive CRC integrity.
- JSON and project/solution XML parse successfully.
- C# source braces/parentheses pass a lightweight offline structural check; **not compilation**.
- JavaScript parses with `node --check`.

## Not verified in artifact generation

- Compiling C# with .NET 10, running xUnit, applying database migrations, or executing `.cmd`/PowerShell on Windows. SDK could not be downloaded from this container. Treat all application behavior as **implemented, pending runtime proof**.
