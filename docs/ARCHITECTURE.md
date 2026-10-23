# DareCare backend architecture (MVP / Phase 2)

## Modules and boundaries

```
Flutter Patient App          Nurse Web Portal          Pharmacy Web (profiles only)
               \                 |                     /
                        HTTPS / REST
                              |
                   DareCare.Api (Controllers)
                              |
               DareCare.Application (DTO contracts)
                              |
                  DareCare.Domain (entities)
                              ^
                     DareCare.Infrastructure
                     (SQL Server / EF / Identity)
```

- Domain contains business entities and booking state transitions; it does not depend on EF or ASP.NET.
- Application contains versioned endpoint request/response contracts and will later hold use-case services/interfaces.
- Infrastructure owns Identity entities, database mappings and seed data.
- Api wires DI, security and controllers. Future work can extract controllers' business logic into Application use cases as complexity grows.
- REST paths carry `/api/v1`; dates are stored in UTC; request times accept ISO-8601 offsets.

## Entity relationships

```
ApplicationUser (Identity) <--1:1-- PatientProfile
ApplicationUser (Identity) <--1:1-- NurseProfile
ApplicationUser (Identity) <--1:1-- PharmacyProfile
ApplicationUser (Identity) --1:many--> RefreshSession (token families)
NurseProfile 1--many NurseSkill many--1 Skill
NurseProfile 1--many AvailabilitySlot
Skill 1--many CareService
Patient (ApplicationUser) 1--many Booking
NurseProfile 1--many Booking
AvailabilitySlot 1--many Booking (one non-cancelled/non-rejected)
CareService 1--many Booking
ApplicationUser 1--many AuditEvent
```

### Security invariants

1. Anonymous registration grants only `Patient` or `Nurse`, not `Admin` / `Pharmacy`. Pharmacy staff use an admin-only email invitation with passwordless account creation.
2. New nurses are never listed until approved by the Admin.
3. Changing nurse skills resets approval.
4. Patients cannot read or change other patients' bookings; nurses can only change their own assigned bookings.
5. The server validates allowed booking state transitions and updates a rowversion for concurrency.
6. SQL Server filtered unique index enforces at most one active/history-preserving booking per slot.
7. JWT private key, connection secrets, admin bootstrap password are never in committed settings.
8. Authentication endpoints are rate-limited and password failures lock out users.
9. Passwords are handled only by ASP.NET Core Identity; refresh tokens are random, hashed, one-time and rotated atomically.
10. Disabled accounts, password resets, and logout-all invalidate existing JWTs using an indexed user status/version lookup on each request.
11. Profile endpoints derive user ownership only from the authenticated `sub` claim; clients cannot supply arbitrary user IDs.
12. Pharmacy and nurse verification require Admin approval; license presence is NOT equivalent to official government verification.

## Public API evolution

- Phase 1: identity, nurses, skills, availability, services, bookings, Admin approval.
- Phase 2 (current): session management, email codes, private Patient/Nurse/Pharmacy profiles, pharmacy invitation and Admin approvals.
- Phase 3: prescriptions + medicine orders + secure file storage + delivery flow + audit expansion.
- Phase 4: explainable nurse matching, notifications, reviews, appointment OTP, integration tests, optional payment sandbox.

### API compatibility

Flutter and web developers should use DTO contracts, not read database models. Use route versioning and add optional JSON properties for backward compatibility; coordinate breaking changes via PR review.

## Deployment cautions (future production work)

- Persist ASP.NET Data Protection keys across restarts/instances so confirmation/reset email codes do not become invalid unexpectedly.
- Run SQL Server migrations through a deployment procedure; do not rely on automatic `EnsureCreated()` for Identity.
- Use HTTPS/TLS (with correct reverse-proxy forwarding) and a trusted email provider; do not use real patient records with this MVP.
- Scheduled cleanup for old refresh-session rows and durable email retries/queues are still outstanding.
- See `AUTH_AND_PROFILES.md` for role-specific account flows and token semantics.
