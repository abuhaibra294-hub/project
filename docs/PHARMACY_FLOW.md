# DareCare Sprint 3 — Pharmacy + matching + notifications

## 1. Prescription workflow

1. Approved pharmacy must already exist (Admin invite, password setup, approval).
2. Patient uploads PDF/PNG/JPEG (up to 8 MiB) to an approved pharmacy. An optional linked booking must belong to the patient and be `Completed`.
3. File bytes are stored in a non-public `App_Data/prescriptions/` folder, with a generated opaque server key. Metadata is stored in `Prescriptions`.
4. Only the owning patient, the specific approved pharmacy, or Admin can download via `GET /api/v1/prescriptions/{id}/file`.
5. Assigned pharmacy staff review the image or PDF and issue *one quote* per prescription, using `POST /api/v1/orders/prescriptions/{id}/quote`.
6. Patient confirms or declines; pharmacy can start preparing only after confirmation.
7. Allowed transitions:
   - Quoted → Confirmed → Preparing → OutForDelivery → Delivered
   - Quoted → Declined
8. Every state change is written to `AuditEvents` and produces an in-app `UserNotification` for the other party. These are *not* push notifications.

## 2. Identity and access checks

| Operation | Role / ownership |
|---|---|
| Choose/list approved pharmacies | Any public user |
| Upload prescription | Logged-in Patient; pharmacy verified and active |
| Read own prescriptions | Owning Patient |
| Pharmacy inbox / quote | Pharmacy account, approved and assigned |
| Read prescription file | Owning Patient, approved assigned Pharmacy, or Admin |
| Confirm/decline quote | Owning Patient |
| Prepare/dispatch/deliver | Approved assigned Pharmacy |
| Read/mark notification | Recipient user only |
| View admin overview | Admin only |
| View mock email mailbox | Admin + loopback request + Development environment only |

Authorization is checked against the **database object** on every sensitive request (not just the caller's role). Row/ownership permission is required even for a valid JWT.

## 3. Explainable nurse selection

`GET /api/v1/matching/nurses?serviceId=GUID&city=Mansoura` returns approved, active nurses who have the required skill and an unbooked future slot. Optional `startsAt` asks for an *exact slot start* (ISO timestamp). Ranking scores only:

- Skill (mandatory): 50 points
- Free availability (mandatory): 20 points
- Exact city/service-area match: 20 points
- Years of experience (max 10): 0–10 points

Results include explanations, a slot ID and full start/end timestamps. No machine-learning model, distance calculations, or nurse ratings exist yet. An AI-assisted matching service can later replace this baseline after collecting appropriate verified data and evaluation metrics.

## 4. Important MVP limitations

- The PDF/JPEG/PNG **file signature** is validated, but this is not antivirus scanning and cannot confirm the medical content is authentic. Use only fictional demo prescriptions.
- Storage is local; production should use encrypted private object storage, malware scanning, retention/deletion controls, signed access, backups, privacy/legal review, and audit alerting.
- No medication substitution algorithm, inventory, licensed dispensing/payment provider, courier API, real pharmacy stock checks, real clinical advice, or medication-safety claims.
- User notifications are polled using `GET /api/v1/notifications`; push/SignalR is a later stage.
- `EnsureCreated` is used only in development SQLite quick start. SQL Server uses EF Core migrations; schema migrations must be reviewed and committed by the team.
- SQL Server/SQLite project files have not been compiled here due to no local .NET SDK. Run `TEST_DARECARE.cmd` and `SMOKE_DARECARE.cmd` on a Windows development machine.

## 5. API index (Sprint 3 additions)

```text
GET    /api/v1/pharmacies/available
POST   /api/v1/prescriptions            multipart/form-data
GET    /api/v1/prescriptions/mine
GET    /api/v1/prescriptions/inbox
GET    /api/v1/prescriptions/{id}
GET    /api/v1/prescriptions/{id}/file
POST   /api/v1/orders/prescriptions/{prescriptionId}/quote
GET    /api/v1/orders/mine
GET    /api/v1/orders/inbox
GET    /api/v1/orders/{id}
POST   /api/v1/orders/{id}/confirm
POST   /api/v1/orders/{id}/decline
POST   /api/v1/orders/{id}/prepare
POST   /api/v1/orders/{id}/dispatch
POST   /api/v1/orders/{id}/deliver
GET    /api/v1/notifications
PATCH  /api/v1/notifications/{id}/read
GET    /api/v1/matching/nurses?serviceId=...&city=...
GET    /api/v1/admin/overview
GET    /api/v1/dev/mailbox                (development admin, local only)
```
