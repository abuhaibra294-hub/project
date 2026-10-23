# DareCare Authentication & Profiles — Sprint 2

## Account lifecycle

**Patient / Nurse:** `POST /api/v1/auth/register/patient` or `register/nurse` with a strong password. **Pharmacy:** must be invited by Admin using `POST /api/v1/admin/pharmacies` and the email setup code; it cannot register itself. **Admin:** created only via development bootstrap or separate secure operations provisioning, never public registration.

In **Development**, `Auth:RequireConfirmedEmail` defaults to `false`: registration returns credentials for rapid testing. With the flag set to `true` (production default), registration returns 202 and sends a verification code. The frontend then calls `POST /api/v1/auth/verify-email` with `{email,code}` before login. Account emails require SMTP; if SMTP isn't configured, verification-required registrations return 503 before creating an account.

Nurse accounts can sign in and manage profiles while awaiting Admin approval. However, only approved nurses are publicly searchable/bookable. Pharmacy accounts can sign in after accepting the invitation; their business cannot accept medicine orders until they are approved and the pharmacy-order module is built.

## Token behavior

- Short-lived **JWT access token**, default `15` minutes; issued with `sub`, `token_version`, roles, expiration, issuer, audience.
- Random **refresh token**, default `14` days, issued together with access token. Only a SHA-256 hash is persisted in `RefreshSessions`; each session also records the user token version to prevent old sessions from being resurrected by a refresh/logout race. Clients must store this confidentially (OS secure storage for Flutter, carefully designed secure session storage for web clients). No `localStorage` in the supplied demo.
- `POST /api/v1/auth/refresh` `{ "refreshToken": "..." }` rotates the token atomically. The previous refresh token cannot be used again. Reuse of a rotated token revokes the associated token family.
- `POST /api/v1/auth/logout` uses an authorized access token and the device's refresh token to revoke that family. **Note:** the current JWT remains valid until expiry (up to 15 minutes) unless the user calls `logout-all` or changes their password. Client must always discard local tokens on logout.
- `POST /api/v1/auth/logout-all` revokes all refresh sessions and increments `TokenVersion`, so **all access tokens are rejected on their very next request** (a lightweight database status/version query runs on every authenticated request).
- Changing or resetting password invalidates all active access and refresh tokens; the user signs in again.
- Admin can deactivate non-admin accounts, which instantly invalidates their JWT and all refresh sessions; activating an account does not restore revoked tokens.

**Security trade-off:** the access token includes role claims. Role changes by administrative provisioning should increment `TokenVersion`; the current APIs never change role after registration. Frontends should use `/api/v1/auth/me` to discover the latest account/approval state, not infer ownership from an ID submitted by the client.

## Email delivery, development setup

Use a local capture server such as Mailpit or MailHog when testing. Example Mailpit via Docker (optional):

```powershell
docker run --rm -d --name darecare-mail -p 1025:1025 -p 8025:8025 axllent/mailpit

dotnet user-secrets set "Smtp:Host" "localhost" --project src/DareCare.Api
dotnet user-secrets set "Smtp:Port" "1025" --project src/DareCare.Api
dotnet user-secrets set "Smtp:FromEmail" "no-reply@darecare.test" --project src/DareCare.Api
dotnet user-secrets set "Smtp:EnableSsl" "false" --project src/DareCare.Api
# Use this to test real email confirmation during development:
dotnet user-secrets set "Auth:RequireConfirmedEmail" "true" --project src/DareCare.Api
```

Mailpit inbox UI: `http://localhost:8025`. When using a real SMTP provider, configure TLS and credentials in a secret store; do **not** commit them to the repo. Built-in `SmtpClient` is sufficient for the MVP; consider a supported email API/provider with reliable delivery, bounces, retry and metrics before a production launch. Never write email codes or access tokens to logs.

For local smoke tests that expect immediate credentials, remove the last secret setting or set it to `false`; for production it defaults to `true` unless deliberately overridden.

## Endpoints (new in this sprint)

| Method | Endpoint | Role | Action |
| --- | --- | --- | --- |
| `POST` | `/api/v1/auth/refresh` | Public with refresh token | One-time rotation |
| `POST` | `/api/v1/auth/logout` | Any signed-in | Revoke this device/family |
| `POST` | `/api/v1/auth/logout-all` | Any signed-in | Invalidate all sessions |
| `POST` | `/api/v1/auth/change-password` | Any signed-in | Change + revoke all sessions |
| `PUT` | `/api/v1/auth/me` | Any signed-in | Update name & phone (not email) |
| `POST` | `/api/v1/auth/resend-verification` | Public | Email verification code |
| `POST` | `/api/v1/auth/verify-email` | Public | Confirm email with code |
| `POST` | `/api/v1/auth/forgot-password` | Public | Email password-reset code (generic response) |
| `POST` | `/api/v1/auth/reset-password` | Public | Reset or accept pharmacy invitation |
| `GET/PUT` | `/api/v1/patients/me` | Patient | Private address/contact profile |
| `GET/PUT` | `/api/v1/pharmacies/me` | Pharmacy | Private business profile |
| `POST` | `/api/v1/admin/pharmacies` | Admin | Invite unapproved pharmacy |
| `POST` | `/api/v1/admin/pharmacies/{id}/resend-invite` | Admin | Resend invitation |
| `GET` | `/api/v1/admin/pharmacies/pending` | Admin | Moderation queue |
| `GET` | `/api/v1/admin/pharmacies/{id}` | Admin | View specific pharmacy |
| `PATCH` | `/api/v1/admin/pharmacies/{id}/approval` | Admin | Manual pharmacy license review |
| `GET` | `/api/v1/admin/users` | Admin | User listing / roles / status |
| `PATCH` | `/api/v1/admin/users/{id}/status` | Admin | Activate/deactivate non-admin users |

Existing nurse and booking routes remain in place. Role response is derived server-side; arbitrary users cannot assign themselves `Pharmacy` or `Admin`.

## Quick payload examples

```http
POST /api/v1/auth/login
Content-Type: application/json

{"email":"demo@example.com","password":"CHANGE_ME"}
```

On success, response contains `userId`, `accessToken`, `expiresAtUtc`, `roles`, `refreshToken`, `refreshTokenExpiresAtUtc`.

```http
POST /api/v1/auth/refresh
Content-Type: application/json

{"refreshToken":"FROM_LAST_SUCCESSFUL_LOGIN_OR_REFRESH"}
```

```http
PUT /api/v1/patients/me
Authorization: Bearer ACCESS_TOKEN
Content-Type: application/json

{"city":"Mansoura","defaultAddress":"Test address","emergencyContactName":"Test contact","emergencyContactPhone":"+201012345678"}
```

```http
POST /api/v1/admin/pharmacies
Authorization: Bearer ADMIN_ACCESS_TOKEN
Content-Type: application/json

{"email":"pharmacy@example.com","contactName":"Demo Pharmacist","phoneNumber":"+201012345678","businessName":"Demo Pharmacy","licenseNumber":"DEMO-123","city":"Mansoura","address":"Test address"}
```

Admin-created pharmacy gets a password-setup code by email, then submits `{email,code,newPassword}` to `POST /api/v1/auth/reset-password`. Only after accepting the invitation and undergoing manual approval will it be eligible for future order fulfillment.

## Manual verification

1. Set up DB/migrations, start API locally; `http://localhost:5208/dev/auth` opens the **development-only** login/account testing page.
2. In default dev mode register a Patient, `GET /auth/me`, update account + Patient profile, refresh, logout. Test patient attempting `GET /api/v1/nurses/me`: **403**.
3. Wrong password must return 401; five bad attempts trigger Identity's 15-minute lockout for that account.
4. After `logout-all` or a password change, prior access token must fail with 401 **immediately** (version check).
5. With SMTP configured, register nurse, confirm email, choose skills, seek Admin approval; pharmacy is invited rather than self-registered.
6. From Windows PowerShell, with email confirmation disabled locally, run `./scripts/Smoke-Auth.ps1` for automated HTTP assertions.

**Needs follow-up before real use:** actual database-backed integration tests (especially simultaneous refresh/revoke requests), secure email delivery and its failure queue, patient consent/privacy policy, audit/monitoring, API deployment hardening, backups, UI production flows. All data should be synthetic until these are done.
