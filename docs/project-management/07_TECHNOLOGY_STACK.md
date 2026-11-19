# DareCare — Technology Stack | التقنيات المستخدمة

> Source-based inventory, 19 November 2026. This describes the current repository and differentiates implemented software from proposed team-facing clients.

## Backend (implemented in source)

| Layer / component | Technology | Evidence / purpose |
| --- | --- | --- |
| Runtime | ASP.NET Core Web API on .NET 10 (net10.0) | `Directory.Build.props`, `src/DareCare.Api` |
| Language | C# (nullable reference types on) | `Directory.Build.props` |
| Transport | Versioned REST routes `/api/v1`, JSON over HTTPS | API controllers and `Program.cs` |
| Authentication | ASP.NET Core Identity, JWT bearer, hashed rotating refresh tokens | Identity and API security |
| Additional login factors | Email OTP, TOTP authenticator via Otp.NET 1.4.1, Google identity token validation via Google.Apis.Auth 1.78.0 | Merged source + `DareCare.Api.csproj`; live activation NOT proven |
| Application contracts | C# request/response DTOs | `src/DareCare.Application/Contracts` |
| Domain model | C# entities and booking/order state enums | `src/DareCare.Domain` |
| Persistence | EF Core 10.0.13; SQL Server and SQLite providers | `DareCare.Infrastructure.csproj` |
| Relational DB (durable target) | SQL Server, versioned EF Core migrations | `deployment` and `docs/SPRINT8_SQLSERVER_AR.md` |
| Disposable development DB | SQLite on local Development and Render Free demo | `Dockerfile` / `render.yaml` |
| API documentation | ASP.NET Core OpenAPI, runtime `/openapi/v1.json`, generated Postman | `Program.cs`, `integration` |
| Tests | xUnit API/domain tests, SQL Server CI integration, Python scripted HTTP smoke | `tests` and `.github/workflows` |
| Packaging | Multi-stage .NET 10 Docker image, non-root runtime | `Dockerfile` |
| CI/CD | GitHub Actions build/test/contract drift and SQL Server migration HTTP tests | workflows under `.github/workflows` |

Source package versions verified:
- Microsoft.AspNetCore.Authentication.JwtBearer 10.0.13
- Microsoft.AspNetCore.OpenApi 10.0.13
- Microsoft.EntityFrameworkCore.SqlServer 10.0.13
- Microsoft.EntityFrameworkCore.Sqlite 10.0.13
- Microsoft.AspNetCore.Identity.EntityFrameworkCore 10.0.13
- Google.Apis.Auth 1.78.0; Otp.NET 1.4.1

## Frontend and integrations

| Client / integration | Status in examined source | Notes |
| --- | --- | --- |
| DareCare Lite web proof-of-concept | Implemented: RTL HTML/CSS/JavaScript served by API in Development/Staging | `src/DareCare.Api/wwwroot`, `docs/DARECARE_LITE_FRONTEND_AR.md` |
| Same-origin Admin portal | Implemented in source, privileged sign-in | `/admin` and `/admin/login`; actual deployed login unverified |
| Flutter patient mobile app | **Team integration deliverable, not proven implemented in this repo** | Dart sample in `integration/client-examples` is a starter, not a complete mobile app |
| Dedicated Nurse/Pharmacy Web clients | **Team integration deliverables** | Client framework not fixed by this repository; avoid asserting React or Angular |
| SMTP | Backend adapter/contract exists; real delivery needs deployment config | Use host credentials in secret manager |
| Google Sign-In | Backend verification exists; real OAuth client IDs/origins require setup | Patient role only; see `docs/DARECARE_AUTH_AR.md` |
| In-app notifications | Implemented as polled endpoints | Push / SignalR not represented as deployed |
| Hosting: Render Free | Existing demo blueprint uses ephemeral SQLite | Synthetic data only |
| Hosting: Render Starter with external SQL | Deployment blueprint exists, activation unverified | `deployment/render-sqlserver-staging.yaml` |
| Hosting: VPS option | Docker Compose + Caddy 2 blueprint exists | `deployment/compose.staging.yml`, `deployment/Caddyfile` |

## Why this stack?
A layered modular monolith keeps a graduation team's backend understandable and testable; EF Core mappings support relational data integrity; SQL Server migrations support a reproducible durable schema; ASP.NET Core Identity and ownership checks implement role isolation; OpenAPI contracts allow different Flutter and web clients to integrate without DB coupling.

## Architecture and tooling rules
1. Mobile/Web → HTTP API only; do not connect UI directly to SQL Server.
2. Deploy secrets, database credentials and OAuth settings outside Git.
3. Keep SQLite explicitly disposable and use SQL Server only after migrations and storage verification.
4. Do not describe code-only or CI-only features as live without target-environment evidence.
5. Do not process actual patient or prescription data in this graduation demo.

## References in repository
`README.md`; `docs/ARCHITECTURE.md`; `docs/DARECARE_AUTH_AR.md`; `docs/SPRINT8_SQLSERVER_AR.md`; `deployment`; `integration`.

Status: **documentation prepared for team review**; this document does not establish a new technology procurement decision.
