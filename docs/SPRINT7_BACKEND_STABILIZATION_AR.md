# DareCare — Sprint 7: Backend Stabilization

> **Scope:** API stability, availability-slot concurrency, privacy regression checks. No new API routes or DTO changes; Flutter/Web integrations should continue to work without changes.

## What changed

1. `POST /api/v1/nurses/me/slots` runs the overlap check and insert in a **single serializable transaction**. A request no longer relies on a check outside the write transaction; concurrent overlapping inserts should be blocked/aborted instead of creating conflicting availability.
2. Known SQL Server deadlocks/lock timeouts and SQLite SQLITE_BUSY / SQLITE_LOCKED conflicts return `409 Conflict` with a safe message asking the client to reload and retry. Unexpected DB errors are not hidden.
3. Regression tests now check overlapping vs adjacent windows, filtered active-booking uniqueness, patient/pharmacy ownership for orders and prescriptions, and per-user notification updates. Tests use **synthetic data** and an in-memory SQLite database.
4. The PR now runs the Render Docker container and a **real HTTP JWT/authorization smoke test**: signup/login, refresh-token rotation, owner isolation for saved addresses, admin-role denial, and logout-all invalidation. This exercises ASP.NET middleware, not only controller methods.
5. Pinned `System.Security.Cryptography.Pkcs` to patched 10.0.13 to remove two Oct 2026 advisories surfaced by NuGet restore (`GHSA-846f-f764-76mx` and `GHSA-2fmv-rv2c-59x7`).

## How to verify

- On the PR, ensure `build-test-contracts` and `render-free-container-smoke` pass.
- With a test nurse token, post `StartsAt` and `EndsAt` to `POST /api/v1/nurses/me/slots`. A first future window must return `201`; an overlapping future window must return `409`; a window starting exactly at the previous end must return `201`.
- In a SQL Server staging environment, send simultaneous overlapping requests for the *same nurse* using separate connections. Confirm there is **at most one overlapping committed slot** and the losing request returns `409` (retryable) or is otherwise safely rejected.
- As Patient B, fetch Patient A's order and prescription: `403`; another Pharmacy cannot access the prescription's order/attachment either. As the owner, reads should work.
- Mark notifications read for Patient A; Patient B's notifications must remain unread.
- For API consumers, a `409` after slot creation means **refresh slots before retrying**. Do not blindly re-submit identical requests.

## Still out of scope

- Full SQL Server-backed HTTP integration testing: the new HTTP smoke uses disposable SQLite, not production SQL Server.
- Real SQL Server multi-instance stress tests, persistent database migrations and reliable media storage.
- Render Free currently runs synthetic-data-only SQLite on ephemeral storage; do not upload real patient information.
- This sprint doesn't implement payments, appointment reminders, pharmacy inventory, malware scanning, or production healthcare compliance.

## Files

- `src/DareCare.Api/Controllers/NursesController.cs`
- `src/DareCare.Api/Security/DbConflict.cs`
- `tests/DareCare.Api.Tests/Sprint7AvailabilityTests.cs`
- `tests/DareCare.Api.Tests/Sprint7PrivacyTests.cs`
- `src/DareCare.Infrastructure/DareCare.Infrastructure.csproj`
- `.github/workflows/dotnet-ci.yml`
- `scripts/ci_http_smoke.py`
