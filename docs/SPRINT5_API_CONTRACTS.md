# DareCare Sprint 5 - API contracts for Flutter and Web

> All examples are synthetic. Backend returns JSON string enums. JWT is required except for public nurse/service discovery.
> Use `Authorization: Bearer <accessToken>` for protected endpoints. Do not embed secrets in front-end code.

## New features

| Method | Endpoint | Who | Purpose |
|---|---|---|---|
| GET | `/api/v1/patients/me/addresses` | Patient | My saved addresses (default first) |
| GET | `/api/v1/patients/me/addresses/{addressId}` | Patient | Read one of my addresses |
| POST | `/api/v1/patients/me/addresses` | Patient | Save up to 10 addresses |
| PUT | `/api/v1/patients/me/addresses/{addressId}` | Patient | Update address; `isDefault:true` also selects it |
| PUT | `/api/v1/patients/me/addresses/{addressId}/default` | Patient | Set default address |
| DELETE | `/api/v1/patients/me/addresses/{addressId}` | Patient | Delete; default automatically falls back if possible |
| GET | `/api/v1/favorites/nurses` | Patient | Favorite approved nurses |
| GET | `/api/v1/favorites/nurses/{nurseId}` | Patient | Is this nurse a favorite? |
| POST | `/api/v1/favorites/nurses/{nurseId}` | Patient | Favorite approved nurse (idempotent) |
| DELETE | `/api/v1/favorites/nurses/{nurseId}` | Patient | Remove favorite (idempotent) |
| POST | `/api/v1/support/tickets` | Any logged-in account | Create private ticket and initial message |
| GET | `/api/v1/support/tickets/mine` | Any logged-in account | My ticket list, paginated/status-filtered |
| GET | `/api/v1/support/tickets/{id}` | Owner/Admin | One ticket and paginated messages |
| POST | `/api/v1/support/tickets/{id}/messages` | Owner/Admin | Add reply (closed tickets reject replies) |
| GET | `/api/v1/support/admin/tickets` | Admin | Paginated tickets, status/category filters |
| PATCH | `/api/v1/support/admin/tickets/{id}/status` | Admin | Change ticket workflow status |
| GET | `/api/v1/nurses/search` | Public | Filter approved active nurses, paginated |
| GET | `/api/v1/nurses/{nurseId}` | Public | Public profile and aggregate rating |
| GET | `/api/v1/nurses/me/slots` | Nurse | My calendar, including booked flag |
| GET | `/api/v1/services/search` | Public | Search service catalog, paginated |
| GET | `/api/v1/bookings/mine/search` | Patient/Nurse | Filter my bookings, paginated |
| GET | `/api/v1/bookings/{id}/timeline` | Patient/Nurse participant/Admin | Safe status history (no private actors/comments) |
| GET | `/api/v1/notifications/unread-count` | Logged-in | Unread notifications count |
| PATCH | `/api/v1/notifications/read-all` | Logged-in | Mark all my notifications read |

Existing routes remain valid. Two previously existing routes have backwards-compatible extensions:
- `POST /api/v1/bookings` supports either `serviceAddress` (old behavior) **or** `addressId` (new behavior). Using both or neither is a 400.
- `GET /api/v1/notifications?unreadOnly=true` returns only unread notifications. Default continues to return the existing JSON array.
- `GET /api/v1/dashboard/me` adds `openSupportTickets` and, for Admin, `pendingSupportTickets`.

## Request examples

**Add address**: `POST /api/v1/patients/me/addresses`
```json
{ "label": "Home", "city": "Mansoura", "fullAddress": "Demo street 10, building 2", "notes": "Synthetic test address", "isDefault": true }
```
First saved address becomes default. Database index enforces at most one default per patient.

**Book using address**: `POST /api/v1/bookings`
```json
{
  "careServiceId": "SERVICE-GUID",
  "nurseProfileId": "NURSE-GUID",
  "availabilitySlotId": "SLOT-GUID",
  "addressId": "ADDRESS-GUID"
}
```
The system copies the current address into the booking. If saved address is later edited/deleted, historical booking keeps the original snapshot.

**Support ticket**: `POST /api/v1/support/tickets`
```json
{
  "subject": "Question about a home-care booking",
  "category": "Booking",
  "message": "Synthetic test: need to confirm my future appointment."
}
```
Valid categories: `Account`, `Booking`, `Pharmacy`, `Other`.

**Support reply**: `POST /api/v1/support/tickets/{id}/messages`
```json
{ "message": "The issue has been reviewed and a follow-up was sent." }
```

**Admin status change**: `PATCH /api/v1/support/admin/tickets/{id}/status`
```json
{ "status": "InProgress" }
```
Statuses: `Open` -> `InProgress` -> `Resolved` -> `Closed`. Admin may resolve/close an open ticket directly; resolved can be reopened to `InProgress`. Closed is terminal.

## Filtering and pagination

`GET /api/v1/nurses/search?city=Mansoura&minYears=2&maxHourlyRate=400&skillId=SKILL-GUID&page=1&pageSize=20`

`GET /api/v1/services/search?query=home&maxPrice=500&skillId=SKILL-GUID&page=1&pageSize=20`

`GET /api/v1/bookings/mine/search?status=Completed&from=2026-10-01T00:00:00Z&to=2026-12-01T00:00:00Z&page=1&pageSize=20`

`GET /api/v1/support/tickets/mine?status=Open&page=1&pageSize=20`

`GET /api/v1/support/admin/tickets?category=Booking&status=Open&page=1&pageSize=20`

Paginated responses use:
```json
{ "page": 1, "pageSize": 20, "total": 0, "items": [] }
```
Pagination max 50 items/page (100,000 pages). Existing unpaginated APIs are unchanged.

## Protection and data ownership

- Every address and favorite is scoped to the authenticated Patient.
- Only approved/active nurses are listed in public search and favorites. License numbers are not public.
- Support ticket subject and messages can be read only by the ticket owner and an Admin; all other users get `403`.
- Support creation/replies are throttled (6 requests per minute, per authenticated user).
- Booking search and timeline are scoped to the patient/nurse involved or Admin.
- Only the authenticated account can mark its own notifications as read.
- `409 Conflict` is used for duplicate/default-address races and invalid support status transitions.

## What is NOT part of this sprint

No push notifications, payment integration, pharmacy inventory, nursing document verification, OTP visits, live GPS, ML model, encrypted clinical document vault, or production compliance approval. Use synthetic test data only.
