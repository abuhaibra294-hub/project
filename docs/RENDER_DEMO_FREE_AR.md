# DareCare - Render Free demo without an external SQL Server

**Synthetic test data only. Never upload real patient records or prescriptions.**

## Configure Web Service
- Public Git repository: `https://github.com/abuhaibra294-hub/project`
- Branch: `darecare-demo-free` (never `main`)
- Name: `darecare-api-demo`; Language: Docker; Root Directory: blank
- Dockerfile: `./Dockerfile`; Region: Frankfurt; Compute Plan: Free
- No Build/Start command, persistent disk, SQL Server connection string, or SMTP required for the first trial
- Health check: `/health/ready`

## Environment variables
| Key | Value |
| --- | --- |
| DOTNET_ENVIRONMENT | Staging |
| ASPNETCORE_ENVIRONMENT | Staging |
| ASPNETCORE_HTTP_PORTS | 8080 |
| PORT | 8080 |
| ASPNETCORE_FORWARDEDHEADERS_ENABLED | true |
| Database__Provider | Sqlite |
| Demo__AllowStagingSqlite | true |
| Storage__PrivateDirectory | /tmp/darecare-demo/prescriptions |
| Auth__RequireConfirmedEmail | false |
| ApiDocs__EnableInStaging | true |
| Jwt__Issuer | DareCare.Api |
| Jwt__Audience | DareCare.Clients |
| Jwt__Key | Secure random value: 32 UTF-8 bytes minimum; use Render's Generate function |

No CORS entry is needed until a real HTTPS browser frontend exists. Add only its exact URL in `Cors__AllowedOrigins__0`.

## Verify
- `https://YOUR-SERVICE.onrender.com/health/ready` should return HTTP 200.
- `https://YOUR-SERVICE.onrender.com/docs` documents the endpoints.
- Register a fake patient via `POST /api/v1/auth/register/patient`, then login and use `GET /api/v1/auth/me` with Bearer token.
- Email verification and SMTP-required pharmacy invitations are not part of this demo; the Development-only `/dev/auth` page remains disabled publicly.

## Critical limitation
Free Render has an **ephemeral filesystem**. The SQLite database, user accounts, bookings and fake uploads can disappear after a restart or redeployment. Do not rely on it for durable team integration. Use it only to prove the .NET API can build and start.

The original SQL Server staging blueprint remains in `deployment/render-sqlserver-staging.yaml`. A reliable team staging environment needs a persistent external database or paid disk and proper mail configuration. Production rejects SQLite.

## If Render shows Production + SQL Server in startup logs
Ensure the latest GitHub commit is deployed. The demo Dockerfile now sets
`DOTNET_ENVIRONMENT=Staging` and `Database__Provider=Sqlite` by default.
In Render Environment, delete any contradictory `DOTNET_ENVIRONMENT=Production`
or `Database__Provider=SqlServer` entry. Do not change secrets or switch to
Development. Rebuild using **Manual Deploy > Deploy latest commit**.
