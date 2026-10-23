# DareCare Sprint 5 - quality checklist

## Source-level checks performed during packaging

- JSON configs and XML project files parse.
- C# source delimiters balanced; required files/routes present.
- Source/archive excludes local credentials, *.db, private patient files and temporary outputs.
- PowerShell workflow and Windows launchers are included.

These source checks are NOT a substitute for compiling C# or running the server.

## Run on a Windows workstation with .NET 10 SDK

1. Unzip the whole project to an ordinary writable folder.
2. Run `START_DARECARE.cmd` to restore/build/test and launch fresh Sprint 5 SQLite demo database.
3. Run `SMOKE_DARECARE.cmd` (or `DEMO_SESSION.cmd` for synthetic demo credentials).
4. In `/dev/auth` verify tokens, roles and status changes. For newly added modules, inspect `DareCare.http`.
5. Run `TEST_DARECARE.cmd` for unit/API DB-backed tests.
6. If SQL Server LocalDB is available, run `START_SQLSERVER.cmd` on the isolated `DareCareSprint5Db`; verify migrations and CRUD there.

## Acceptance tests to confirm

- [ ] Patient A cannot GET address belonging to Patient B (404) or use B's addressId in a booking (400).
- [ ] First saved address becomes default; selecting another leaves exactly one default; deleting it chooses a surviving address.
- [ ] Same patient cannot favorite the same nurse twice; disapproved/inactive nurses are unavailable in public directory.
- [ ] A Nurse or Pharmacy user cannot create a Patient favorite or address (403).
- [ ] Patient A cannot GET Patient B's support ticket (403) or see it in `/mine`.
- [ ] Only Admin can list all support tickets and update ticket status.
- [ ] Reply to closed ticket fails with 409; double-clicked/racing edits cannot silently overwrite status.
- [ ] Search endpoints reject pageSize >50, unauthorized resources and invalid filter values.
- [ ] A booking made using addressId retains a snapshot when saved address changes.
- [ ] Timeline is accessible only to participants/Admin and contains no private actor details.
- [ ] Read-all notifications changes only the authenticated user's unread notifications.
- [ ] Old free-text bookings and unpaginated legacy endpoints still work.
- [ ] `dotnet test` and the synthetic E2E workflow pass on both SQLite and SQL Server LocalDB before any public deployment.

## Known limitations

- .NET SDK and SQL Server are not available in the artifact container. Build, EF migrations, xUnit and runtime HTTP checks **have not been executed here**.
- SQLite Development mode uses `EnsureCreated` and a new `darecare-sprint5-dev.db`; it does NOT migrate Sprint 4 SQLite files in-place.
- SQL Server LocalDB is for demo only. Back up existing databases before applying migrations; avoid mixing migration histories.
- Pagination and rate limiting are basic MVP implementations, not load-tested under concurrency.
- Sensitive medical data must not be entered into this development build.
