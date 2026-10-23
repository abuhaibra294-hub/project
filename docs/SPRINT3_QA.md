# DareCare Sprint 3 — Verification / QA checklist

This checklist separates **static checks performed during generation** from **runtime checks that must be performed on a developer machine**.

## Offline checks performed

- XML parsing of all `.csproj` and solution manifests
- JSON parsing of appsettings and tool manifest
- Balanced C# bracket/brace structural inspection
- Node.js JavaScript syntax validation of `wwwroot/auth.html`
- ZIP manifest check; all CMD and PowerShell scripts included

These checks are **not a substitute for the C# compiler, NuGet restore, or integration tests**.

## Runtime checks to complete on Windows

- [ ] START_DARECARE.cmd successfully installs/finds .NET SDK 10 and restores dependencies
- [ ] `dotnet build DareCare.slnx` succeeds
- [ ] `dotnet test DareCare.slnx` passes the Domain/API tests
- [ ] `http://127.0.0.1:5208/health` returns the expected DB provider
- [ ] Development auth page loads and local demo admin sign-in succeeds
- [ ] SMOKE_DARECARE.cmd passes the synthetic end-to-end workflow
- [ ] Patient can download own Rx, unrelated Nurse cannot (403)
- [ ] Pharmacy cannot prepare before Patient confirms (409)
- [ ] Changing a refresh token and logging out invalidates the proper sessions
- [ ] New SQL Server LocalDB schema can be created with START_SQLSERVER.cmd
- [ ] Review generated migrations before sharing with team or deploying
- [ ] Dev mailbox is denied off-loopback or outside Development
- [ ] No demo `.localdata`, private `App_Data`, JWT keys or patient files are committed to GitHub

## Next tasks

1. Run and fix SDK/compiler/runtime results.
2. SQL Server schema migration checked in CI and explicit schema upgrade strategy.
3. Integration tests using WebApplicationFactory and a real disposable test database.
4. Rich pharmacy order items, stock availability, patient confirmation of substitutions.
5. Production-ready object storage + virus scan + retention & privacy rules.
6. Notification delivery pipeline, not just database inbox.
7. Ratings, distance, matching dataset and explainability/evaluation metrics.
