# DareCare — Integration & QA Playbook

## Prerequisites
1. Backend coordinator identifies the commit actually deployed and accessible HTTPS base URL. The source branch is `darecare-demo-free`; the default repository `main` is unrelated to this handoff.
2. `GET /health/ready` returns HTTP 200 on **that URL**. Download the runtime `/openapi/v1.json` and reconcile operations with the approved manifest.
3. Import `integration/DareCare_Postman_Collection.json` and the environment appropriate for local/staging. Keep passwords/tokens in local secret variables, never PR diffs.
4. Create four separate fictional test identities: Patient/Nurse/Pharmacy/Admin through authorized methods. Nurse requires Admin approval; Pharmacy via Admin-only invitation. No default public Admin registration.
5. Configure SMTP and Google OAuth (web origin and Flutter server client ID), exact CORS origin for external browser clients; avoid HTTP on public networks.
6. Client code must not query SQL Server directly.

## Client SDK and auth agreement
- Flutter: central HTTP client, models from current OpenAPI/DTOs, platform-protected secure token storage, refresh synchronization (one refresh request across concurrent 401s), new login on refresh failure, robust file multipart upload.
- Web: central typed API client with safe session handling; the existing DareCare Lite proof-of-concept keeps JWTs **in memory** and loses session on refresh by design. Implement a reviewed production session strategy separately.
- HTTP 401: refresh once then replay original request once. HTTP 403: show insufficient privilege; do **not** refresh indefinitely. HTTP 409: re-fetch state and offer user an explicit retry.
- HTTP 202 for MFA: open challenge screen and call `POST /auth/authenticator/login` with challenge and current OTP; code replay is denied.
- Use role-specific navigation; front-end hiding buttons is not a security control.

## Integration acceptance flows
**A. Patient + Nurse care**
1. Register fictional Nurse; complete profile and skill; verify unapproved nurse is not discoverable.
2. Admin approves Nurse using authorized portal.
3. Nurse publishes available slot.
4. Register/login fictional Patient; choose address and service; discover nurse and choose exact available slot.
5. Patient creates booking; Nurse accepts, starts and completes; Patient sees timeline and may leave eligible review.
6. Negative: second Patient cannot access first Patient's booking; concurrent double-booking fails.

**B. Patient + Pharmacy prescription**
1. Admin invites and approves Pharmacy.
2. Patient selects approved pharmacy, uploads **fictional** PNG/JPEG/PDF within current limits.
3. Assigned Pharmacy reads inbox and issues quote.
4. Patient confirms or declines quote; Pharmacy may prepare/dispatch/mark delivered only after confirmation.
5. Negative: unrelated account cannot download file or quote another pharmacy's assigned request.

**C. Authentication**
1. Validate password login and rotation; email OTP verify/reset with expiration and retry limits.
2. Validate MFA setup, 202 challenge, one-time recovery codes and session invalidation.
3. Validate Google ID token from configured approved client for Patient; existing-account linking explicit; privileged roles denied.
4. Validate 401/403/429/409 UX and logging without tokens.

**D. Browser and deployment**
1. Test browser preflight and authenticated fetch from actual allowed origin; Postman cannot prove CORS.
2. Test Admin role gates and `/admin` portal on 390px and desktop; patient cannot access restricted Admin API.
3. Restart persistent SQL Server staging and verify fictional booking/order and prescription file still exist.
4. Record CI job URL, runtime build SHA, staging request IDs and redacted screenshots/recordings.

## Environment / release gates
| Environment | Allowed | Not allowed / assumptions |
|---|---|---|
| Local Development | Disposable fictional test accounts; SQLite and local tools | Real clinical data; publicly exposed local test mailbox |
| Render Free demo | Disposable synthetic flow and UI review | Persistent patient/medicine records; claim of durable staging |
| Durable staging (to implement/verify) | SQL Server migrations, private durable files, HTTPS, CI/integration signoff | Real patient data without full privacy/security assessment |
| Production | Out of MVP scope until security, legal/compliance, licensed provider, backups and operations checks pass | Automatic graduation-demo promotion to healthcare operation |

## Definition of integration done
Actual application on specified device/origin completes positive and negative paths on the same verified backend revision, with no secrets/real health data, and tested behavior is recorded in issue and reviewed PR. A Postman screenshot, fabricated mocked API response, or local-only CI pass does **not** count as proof of deployed functionality.
