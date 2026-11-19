# DareCare — Complete Source API Route Inventory (100 Operations)

**Baseline:** `darecare-demo-free`, 19 November 2026; generated from committed `integration/DareCare_API_Endpoints.json`. This is a **source-derived method/path and declared-role inventory**, not runtime OpenAPI or evidence of deployment.

## How to use this inventory
- Base URI: `/api/v1`; entries below include the full prefix.
- Client auth for protected endpoints: `Authorization: Bearer <accessToken>`. Authentication and ownership checks occur on the server.
- This manifest enumerates **100** operations across **17** route families. Infrastructure routes such as `/health`, `/health/ready`, `/docs` and `/openapi/v1.json` are *not* counted.
- Earlier documented public staging verification counted **93** operations on 16 November 2026. The runtime contract and deployed SHA remain unverified for this newer source baseline. Do not assume all 100 have reached Render.
- Read `04_API_DOCUMENTATION.md` for DTO examples, error and auth conventions, `../FRONTEND_START_HERE_AR.md` for client onboarding, and the **running** `/openapi/v1.json` for exact framework-generated schemas and status codes.
- **Known manifest bug:** `GET /api/v1/pharmacies/available` has method-level `[AllowAnonymous]` preceding its `[HttpGet]` annotation, overriding the controller's Pharmacy role. The source-derived manifest incorrectly lists `Pharmacy`. This table displays `Public*` for that route, and issue #12 must fix the generator/contract output and verify the server. No other role corrections are inferred here.

## Group breakdown

| Route family | Source operations |
| --- | ---: |
| admin | 11 |
| auth | 20 |
| bookings | 10 |
| dashboard | 1 |
| dev | 1 |
| favorites | 4 |
| matching | 1 |
| notifications | 4 |
| nurses | 10 |
| orders | 9 |
| patients | 8 |
| pharmacies | 3 |
| prescriptions | 5 |
| reviews | 4 |
| services | 2 |
| skills | 1 |
| support | 6 |
| **Total** | **100** |

## Full API list (source; not live verification)

### Administration and approvals (11)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/admin/audit` | Admin |
| GET | `/api/v1/admin/nurses/pending` | Admin |
| PATCH | `/api/v1/admin/nurses/{nurseId}/approval` | Admin |
| GET | `/api/v1/admin/overview` | Admin |
| POST | `/api/v1/admin/pharmacies` | Admin |
| GET | `/api/v1/admin/pharmacies/pending` | Admin |
| GET | `/api/v1/admin/pharmacies/{pharmacyId}` | Admin |
| PATCH | `/api/v1/admin/pharmacies/{pharmacyId}/approval` | Admin |
| POST | `/api/v1/admin/pharmacies/{pharmacyId}/resend-invite` | Admin |
| GET | `/api/v1/admin/users` | Admin |
| PATCH | `/api/v1/admin/users/{userId}/status` | Admin |

### Authentication, OTP, MFA and Google (20)

| Method | Route | Declared roles |
| --- | --- | --- |
| POST | `/api/v1/auth/authenticator/disable` | Authenticated |
| POST | `/api/v1/auth/authenticator/enable` | Authenticated |
| POST | `/api/v1/auth/authenticator/login` | Public |
| POST | `/api/v1/auth/authenticator/setup` | Authenticated |
| POST | `/api/v1/auth/change-password` | Authenticated |
| POST | `/api/v1/auth/forgot-password` | Public |
| POST | `/api/v1/auth/google` | Public |
| GET | `/api/v1/auth/google/config` | Public |
| POST | `/api/v1/auth/google/link` | Patient |
| POST | `/api/v1/auth/login` | Public |
| POST | `/api/v1/auth/logout` | Authenticated |
| POST | `/api/v1/auth/logout-all` | Authenticated |
| GET | `/api/v1/auth/me` | Authenticated |
| PUT | `/api/v1/auth/me` | Authenticated |
| POST | `/api/v1/auth/refresh` | Public |
| POST | `/api/v1/auth/register/nurse` | Public |
| POST | `/api/v1/auth/register/patient` | Public |
| POST | `/api/v1/auth/resend-verification` | Public |
| POST | `/api/v1/auth/reset-password` | Public |
| POST | `/api/v1/auth/verify-email` | Public |

### Care bookings (10)

| Method | Route | Declared roles |
| --- | --- | --- |
| POST | `/api/v1/bookings` | Patient |
| GET | `/api/v1/bookings/mine` | Nurse, Patient |
| GET | `/api/v1/bookings/mine/search` | Nurse, Patient |
| GET | `/api/v1/bookings/{id}` | Admin, Nurse, Patient |
| POST | `/api/v1/bookings/{id}/accept` | Nurse |
| POST | `/api/v1/bookings/{id}/cancel` | Patient |
| POST | `/api/v1/bookings/{id}/complete` | Nurse |
| POST | `/api/v1/bookings/{id}/reject` | Nurse |
| POST | `/api/v1/bookings/{id}/start` | Nurse |
| GET | `/api/v1/bookings/{id}/timeline` | Admin, Nurse, Patient |

### Role dashboard (1)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/dashboard/me` | Authenticated |

### Development-only test mailbox (1)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/dev/mailbox` | Admin |

### Saved nurse favorites (4)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/favorites/nurses` | Patient |
| DELETE | `/api/v1/favorites/nurses/{nurseId}` | Patient |
| GET | `/api/v1/favorites/nurses/{nurseId}` | Patient |
| POST | `/api/v1/favorites/nurses/{nurseId}` | Patient |

### Explainable nurse matching (1)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/matching/nurses` | Public |

### User notifications (4)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/notifications` | Authenticated |
| PATCH | `/api/v1/notifications/read-all` | Authenticated |
| GET | `/api/v1/notifications/unread-count` | Authenticated |
| PATCH | `/api/v1/notifications/{id}/read` | Authenticated |

### Nurse discovery and profiles (10)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/nurses` | Public |
| GET | `/api/v1/nurses/me` | Nurse |
| PUT | `/api/v1/nurses/me` | Nurse |
| PUT | `/api/v1/nurses/me/skills` | Nurse |
| GET | `/api/v1/nurses/me/slots` | Nurse |
| POST | `/api/v1/nurses/me/slots` | Nurse |
| DELETE | `/api/v1/nurses/me/slots/{slotId}` | Nurse |
| GET | `/api/v1/nurses/search` | Public |
| GET | `/api/v1/nurses/{nurseId}` | Public |
| GET | `/api/v1/nurses/{nurseId}/slots` | Public |

### Pharmacy medicine orders (9)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/orders/inbox` | Pharmacy |
| GET | `/api/v1/orders/mine` | Patient |
| POST | `/api/v1/orders/prescriptions/{prescriptionId}/quote` | Pharmacy |
| GET | `/api/v1/orders/{id}` | Authenticated |
| POST | `/api/v1/orders/{id}/confirm` | Patient |
| POST | `/api/v1/orders/{id}/decline` | Patient |
| POST | `/api/v1/orders/{id}/deliver` | Pharmacy |
| POST | `/api/v1/orders/{id}/dispatch` | Pharmacy |
| POST | `/api/v1/orders/{id}/prepare` | Pharmacy |

### Patient profiles and saved addresses (8)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/patients/me` | Patient |
| PUT | `/api/v1/patients/me` | Patient |
| GET | `/api/v1/patients/me/addresses` | Patient |
| POST | `/api/v1/patients/me/addresses` | Patient |
| DELETE | `/api/v1/patients/me/addresses/{addressId}` | Patient |
| GET | `/api/v1/patients/me/addresses/{addressId}` | Patient |
| PUT | `/api/v1/patients/me/addresses/{addressId}` | Patient |
| PUT | `/api/v1/patients/me/addresses/{addressId}/default` | Patient |

### Pharmacy discovery and profiles (3)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/pharmacies/available` | Public* (manifest currently: Pharmacy) |
| GET | `/api/v1/pharmacies/me` | Pharmacy |
| PUT | `/api/v1/pharmacies/me` | Pharmacy |

### Private prescriptions (5)

| Method | Route | Declared roles |
| --- | --- | --- |
| POST | `/api/v1/prescriptions` | Patient |
| GET | `/api/v1/prescriptions/inbox` | Pharmacy |
| GET | `/api/v1/prescriptions/mine` | Patient |
| GET | `/api/v1/prescriptions/{id}` | Authenticated |
| GET | `/api/v1/prescriptions/{id}/file` | Authenticated |

### Completed-visit reviews (4)

| Method | Route | Declared roles |
| --- | --- | --- |
| POST | `/api/v1/reviews/bookings/{bookingId}` | Patient |
| GET | `/api/v1/reviews/mine` | Patient |
| GET | `/api/v1/reviews/nurses/{nurseProfileId}/summary` | Public |
| GET | `/api/v1/reviews/{id}` | Patient |

### Care services (2)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/services` | Public |
| GET | `/api/v1/services/search` | Public |

### Care skills (1)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/skills` | Public |

### Support tickets (6)

| Method | Route | Declared roles |
| --- | --- | --- |
| GET | `/api/v1/support/admin/tickets` | Admin |
| PATCH | `/api/v1/support/admin/tickets/{id}/status` | Admin |
| POST | `/api/v1/support/tickets` | Authenticated |
| GET | `/api/v1/support/tickets/mine` | Authenticated |
| GET | `/api/v1/support/tickets/{id}` | Authenticated |
| POST | `/api/v1/support/tickets/{id}/messages` | Authenticated |

## Critical client contract examples (fictional data only)

### Authentication
Request: `POST /api/v1/auth/login` with JSON `{"email":"test@example.test","password":"<test-password>"}`.
- Normal success: session object includes `userId`, `accessToken`, `expiresAtUtc`, `roles`, `refreshToken`, `refreshTokenExpiresAtUtc`. See `AuthResponse` in `src/DareCare.Application/Contracts/AuthContracts.cs`.
- Accounts with authenticator may receive HTTP **202** containing `twoFactorRequired`, `challengeToken` and `expiresAtUtc`; complete with `POST /api/v1/auth/authenticator/login`.
- Email OTP is optional per registration's `useEmailOtp` and requires configured email delivery in staging.
- Google sign-in accepts a Google `idToken` for eligible patient accounts; never expose a privileged role solely through Google registration.

### Booking
Request: `POST /api/v1/bookings` with `nurseProfileId`, `availabilitySlotId`, `careServiceId` plus **either** `addressId` **or** `serviceAddress` (never both). DTO reference: `src/DareCare.Application/Contracts/BookingContracts.cs`.
Expected lifecycle: `Pending → Accepted → InProgress → Completed` with restricted cancel/reject transitions. Exact error status depends on the branch and concurrent state. Test stale-slot conflicts explicitly.

### Prescription and quote
- `POST /api/v1/prescriptions`: `multipart/form-data`, synthetic PNG/JPEG/PDF only; endpoint source validates file size/type signatures but **does not** constitute malware scanning.
- `POST /api/v1/orders/prescriptions/{prescriptionId}/quote`: JSON `{"amount":150,"notes":"Synthetic quote"}`. Amount validation: 0.01 to 1,000,000 per `QuotePrescriptionRequest`.
- Owner/pharmacy access checks must be verified against actual **resource records**, not only caller role.

### Common HTTP semantics
| Response | Client reaction |
| --- | --- |
| 200/201/204 | Handle success using the **specific operation's** response schema |
| 202 | Email accepted or authenticator second factor required, by endpoint |
| 400 | Invalid input or disallowed transition; show validated field/action message |
| 401 | Missing/expired access token; refresh **once** and retry **once** |
| 403 | Insufficient role; never force refresh loop |
| 404 | Object absent or deliberately concealed by ownership check |
| 409 | Conflict; refetch current state and ask for explicit user action |
| 429 | Throttled; honor retry policy without aggressive loops |

## Release gate — before declaring API documentation complete

- [ ] Retrieve the deployed SHA on the actual target deployment.
- [ ] Save `GET /openapi/v1.json` from that exact deployment revision.
- [ ] Diff **method + path + auth policy** from runtime vs generated manifest; close source role bug, particularly pharmacy availability.
- [ ] Confirm request/response DTOs, filters, pagination, status codes and ownership semantics for **each client-used route**.
- [ ] Check CI `python3 scripts/generate_integration.py --check`; regenerate checked-in artifacts when source changes.
- [ ] Repeat authenticated patient/nurse/pharmacy/admin and forbidden-resource E2E tests.
- [ ] Link CI/run details and non-sensitive proof in issue #12.

This inventory is a **prepared documentation deliverable**. Its 100 rows are not a substitute for running API verification.
