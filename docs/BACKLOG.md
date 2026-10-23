# DareCare .NET Backlog

## First delivery — implemented as source in this archive

- [x] .NET 10 solution with 4 project layers, xUnit test project, GitHub CI.
- [x] Identity, password policy, JWT signing/validation, role-based authorization.
- [x] Patient and Nurse self-registration; nurse awaiting Admin approval.
- [x] SQL Server EF Core DbContext, relationships, slot uniqueness & concurrency protections.
- [x] Admin bootstrap (local-only) and basic audit, seeded demo services and skills.
- [x] Nurse skill selection, availability slots, approved nurse discovery.
- [x] Patient booking and Nurse status transition endpoints.
- [x] Booking unit tests added (NOT executed in authoring environment).
- [ ] Verify .NET restore/build/test locally and generate / apply first EF migration (SDK not available here).
- [ ] Exercise the end-to-end API requests with Postman/REST Client.
- [ ] Write SQL Server-backed integration tests for authorization, concurrent bookings, and role bootstrap.

## Sprint 2 — source implemented (not compiled in this environment)

- [x] JWT access + hashed rotating refresh sessions, logout and logout-all.
- [x] Token-version validation prevents use of old JWTs after password changes/deactivation.
- [x] Change password, forgot/reset password, verify email/resend verification (SMTP-dependent).
- [x] Patient private address and emergency-contact profile, account name/phone editing.
- [x] Nurse city/service area and reapproval for material profile changes.
- [x] Admin-only pharmacy invitation, pharmacy profile, manual approval and account activation controls.
- [x] Development-only HTML login/test page and PowerShell auth smoke test.
- [ ] Compile, restore, run database migration, and execute local/integration tests with .NET 10 SDK and SQL Server.
- [ ] Finish the pharmacy medicine-order and prescription workflows.

## Next sprint — Backend A

1. Expand patient address book and emergency-contact validation (basic profile fields completed).
2. Nurse verification documents **secure** upload/review and consent.
3. Appointment reminders and cancellation/no-show policies.
4. Explainable recommendation endpoint: skill, time, location, approval, score; no diagnosis AI.
5. Availability recurring windows with cross-instance concurrency safety.
6. Ratings with eligibility checks after completed visits.
7. Move controller application logic into testable use-case services and validators.

## Next sprint — Backend B

1. Expand existing Pharmacy profiles and invitation/approval with branches, opening hours, pharmacy-staff teams; never allow anonymous self-registration into the Pharmacy role.
2. Prescription uploads with size/type/content validation, private storage and authorized download.
3. Pharmacist review: requests for prescription, acceptance/rejection/quotation.
4. Medicine orders: quote, patient confirmation, preparation, delivery-state tracking.
5. Notification outbox and audit extension.
6. Integration tests with role isolation/IDOR protections.

## Completion gates

- `dotnet restore`, `dotnet build`, `dotnet test` pass in CI.
- Fresh SQL Server can apply migrations on an empty DB.
- At least one happy-path and one forbidden-path test for each sensitive API.
- Document request/response examples; no leaked secrets / sample real patient data.
- Multi-developer PR review before merging.

## Sprint 5 - API expansion (source added, runtime testing pending)

- [x] Saved patient addresses with default selection, owner-only CRUD and immutable booking snapshot.
- [x] Patient nurse favorites with approved/active visibility and composite uniqueness.
- [x] Private support tickets and replies, owner/Admin authorization, status transitions, throttling, notifications and audit.
- [x] Paginated service/nurse/booking search, nurse calendar, booking timeline.
- [x] Notification unread count, unread-only filtering and mark-all-read.
- [x] New contract examples in `DareCare.http` and `docs/SPRINT5_API_CONTRACTS.md`.
- [x] Expanded synthetic PowerShell smoke workflow and added xUnit tests for schema, privacy and ticket logic.
- [ ] Compile and run all xUnit tests on a .NET 10 workstation.
- [ ] Run SQL Server migration and end-to-end tests on an isolated database; review generated migration SQL.
- [ ] Integrate mobile and web UI with the documented request schemas.
