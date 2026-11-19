# DareCare — API Documentation & Contract Guide

## Contract provenance
**Source at 19-Nov-2026:** `integration/DareCare_API_Endpoints.json` declares **100** method/path operations. Earlier 16-Nov staging evidence documented **93**; no runtime export proving newer code is deployed is attached to this pack. The canonical API specification for a given environment is its running `GET /openapi/v1.json`, with commit SHA checked. Do not manually infer an endpoint's availability from this document.

- Base local: `http://127.0.0.1:5208`.
- Example staging: `https://project-kv53.onrender.com` (verify service first).
- Main REST namespace: `/api/v1`; `/health`, `/health/ready`, `/openapi/v1.json` and `/docs` are separate.
- Existing artifacts: `DareCare.http`, `integration/DareCare_Postman_Collection.json`, `integration/DareCare_Local.postman_environment.json`, `integration/DareCare_RenderStaging.postman_environment.json`, `integration/DareCare_OpenAPI_SourceSnapshot.json`.
- **Auth:** `Authorization: Bearer <accessToken>` for protected calls. Expired session: single refresh attempt; `403` is forbidden, not a refresh cue.

## Key endpoints (method/path and high-level authorization)
| Actor | Method and path | Purpose |
|---|---|---|
| Public | POST `/api/v1/auth/register/patient` | Register patient |
| Public | POST `/api/v1/auth/register/nurse` | Register unapproved nurse |
| Public | POST `/api/v1/auth/login` | Login; MFA users may receive HTTP 202 challenge |
| Public | POST `/api/v1/auth/refresh` | Rotate refresh session |
| Public | POST `/api/v1/auth/verify-email` | Email OTP verification |
| Public | POST `/api/v1/auth/reset-password` | Password reset with code |
| Public | GET `/api/v1/auth/google/config` | Configured public Google web client ID |
| Public | POST `/api/v1/auth/google` | Patient Google sign-in |
| Patient | POST `/api/v1/auth/google/link` | Link verified patient Google identity |
| Authenticated | POST `/api/v1/auth/authenticator/setup` | Begin MFA enrollment |
| Authenticated | POST `/api/v1/auth/authenticator/enable` | Enable MFA using code |
| Public (challenge) | POST `/api/v1/auth/authenticator/login` | Complete MFA challenge |
| Authenticated | GET `/api/v1/dashboard/me` | Role-scoped dashboard |
| Public | GET `/api/v1/services/search` | Find care services |
| Public | GET `/api/v1/nurses/search` | Find approved nurses |
| Public | GET `/api/v1/nurses/{nurseId}/slots` | Nurse available slots |
| Nurse | POST `/api/v1/nurses/me/slots` | Create available slot |
| Patient | POST `/api/v1/bookings` | Create booking |
| Patient/Nurse | GET `/api/v1/bookings/mine` | Own bookings |
| Nurse | POST `/api/v1/bookings/{id}/accept` | Accept booking |
| Nurse | POST `/api/v1/bookings/{id}/start` | Start visit |
| Nurse | POST `/api/v1/bookings/{id}/complete` | Complete visit |
| Patient | POST `/api/v1/prescriptions` | Private multipart upload |
| Patient | GET `/api/v1/prescriptions/mine` | List own prescriptions |
| Pharmacy | GET `/api/v1/prescriptions/inbox` | Assigned prescription requests |
| Pharmacy | POST `/api/v1/orders/prescriptions/{prescriptionId}/quote` | Quote |
| Patient | POST `/api/v1/orders/{id}/confirm` | Accept price quote |
| Pharmacy | POST `/api/v1/orders/{id}/prepare` | Begin preparation |
| Pharmacy | POST `/api/v1/orders/{id}/dispatch` | Mark out for delivery |
| Pharmacy | POST `/api/v1/orders/{id}/deliver` | Mark delivered |
| Admin | GET `/api/v1/admin/overview` | Admin summary |
| Admin | PATCH `/api/v1/admin/nurses/{nurseId}/approval` | Nurse approval |
| Admin | POST `/api/v1/admin/pharmacies` | Invitation |
| Authenticated | GET `/api/v1/notifications` | Own notifications |

The full manifest includes address book, reviews, favorites, support, audits, account settings and additional routes. **Exception to verify:** current manifest labels `GET /api/v1/pharmacies/available` as Pharmacy while its controller action has `[AllowAnonymous]`. Treat the actual action + runtime behavior as authoritative and track correction in issue #12.

## Request examples (fictional IDs and credentials)
**Login**
```http
POST /api/v1/auth/login
Content-Type: application/json

{"email":"demo.patient@example.test","password":"<test-password>"}
```
Normal success returns session tokens and roles; when authenticator MFA is enabled, HTTP 202 may instead return `{"twoFactorRequired":true,"challengeToken":"..."}`. Never store or print real tokens in team-shared logs.

**Booking** (patient bearer required)
```http
POST /api/v1/bookings
Authorization: Bearer <patient-access-token>
Content-Type: application/json

{"nurseProfileId":"<uuid>","availabilitySlotId":"<uuid>","careServiceId":"<uuid>","addressId":"<uuid>"}
```
Select actual IDs from nurse slots, service search and patient address book. Avoid hardcoded examples in production clients.

**Quote** (assigned approved pharmacy bearer required)
```http
POST /api/v1/orders/prescriptions/<prescription-uuid>/quote
Authorization: Bearer <pharmacy-access-token>
Content-Type: application/json

{"amount":150,"notes":"Synthetic test quote"}
```

## Response/error integration conventions
- `200/201/204`: operation succeeded according to specific contract; `202` may indicate email accepted or two-factor challenge.
- `400`: validation or invalid transition (inspect runtime response), `401`: no/expired session, `403`: unauthorized role, `404`: missing or concealed resource, `409`: resource/state conflict; `429`: rate-limited.
- Not every endpoint returns an identical error model. Suggested UI fallback order: `errors`, `detail`, `message`, `title`.
- Search endpoints use common pagination shape `{"page":1,"pageSize":20,"total":0,"items":[]}`, verify per-operation schema in OpenAPI.
- Use UTC timestamp storage and ISO-8601 offsets for client date/time exchange.
- Do not infer that all endpoints share the same request body, validation or success code; import runtime OpenAPI, then inspect actual DTOs where ambiguous.

## Contract freeze checklist
- [ ] Record branch commit + deployed commit and compare API operations.
- [ ] Export exact live OpenAPI from target environment.
- [ ] Resolve all method/path/role discrepancies, particularly pharmacy available.
- [ ] Refresh Postman collection/client examples from approved contract.
- [ ] Record auth-required flag, example payload, response schema, errors, ownership, pagination for each client-used operation.
- [ ] Add contract test in CI that fails on undocumented breaking changes.

## Appendix — complete source route list and client contract clarification

For every source operation (all **100**, grouped into 17 families), see [08_API_ROUTE_INVENTORY.md](08_API_ROUTE_INVENTORY.md). The source manifest's Pharmacy role label on `GET /api/v1/pharmacies/available` conflicts with the actual controller's `[AllowAnonymous]` annotation; the route inventory highlights that exception rather than silently trusting the manifest.

**Booking address:** The actual `CreateBookingRequest` DTO allows either a saved `addressId` or a free-text `serviceAddress`, but explicitly disallows sending both. An example using `addressId` alone is one valid option, not the complete contract.

**Release dependency:** Source inventory does not establish deployed availability; issue #12 must validate the selected environment's live OpenAPI and authentication semantics. Detailed execution gates are in [Integration Playbook](05_INTEGRATION_PLAYBOOK.md) and [Deployment Strategy](09_DEPLOYMENT_STRATEGY.md).
