# DareCare — Data Flow Diagrams (DFD)
**Owner:** Abdel Maqsoud | **Notion task:** DFD | **Priority:** Medium | **Version:** source analysis, 19 November 2026.

## Scope and notation
DFDs describe **business data moving between external actors, processes and logical stores**. They are *not* deployment/network diagrams, sequence diagrams or UML class diagrams. Solid arrows show data exchanges; cylinders denote logical data stores. Source basis is the existing \`src/DareCare.Api/Controllers\`, application DTOs and \`DareCareDbContext\`.

The project actors are **Patient, Nurse, Pharmacy, Admin**. SMTP and Google ID-token verification appear only as **configuration-dependent external services**; they are not guaranteed to be live on the current free demo. All sample prescription documents and accounts are fictional.

## DFD Level 0 — Context diagram
\`\`\`mermaid
flowchart LR
  PAT["Patient"] -->|Credentials, service/booking requests, synthetic prescription, quote decision| SYS(("0 - DareCare System"))
  SYS -->|Profile, available nurses/slots, booking/quote/order statuses, notifications| PAT
  NUR["Nurse"] -->|Registration, skills, availability and booking actions| SYS
  SYS -->|Approval status, bookings and notifications| NUR
  PHA["Pharmacy staff"] -->|Invitation login, quote and fulfillment actions| SYS
  SYS -->|Assigned prescriptions, accepted orders and notifications| PHA
  ADM["Administrator"] -->|Approval, invitations, status and support actions| SYS
  SYS -->|Pending providers, audit, user and dashboard data| ADM
  SYS -.->|OTP and invitation email when configured| SMTP["SMTP provider"]
  PAT -.->|Patient Google ID token| SYS
  SYS -.->|Validate configured Google sign-in token| GOOGLE["Google identity trust service"]
\`\`\`

**Balance:** The context's account, provider, booking, pharmacy and support exchanges are decomposed into Level 1 processes below. SMTP and Google are external trust/delivery services, not DFD stores or roles.

## DFD Level 1 — Main processes and stores
\`\`\`mermaid
flowchart TB
  PAT["E1 Patient"]
  NUR["E2 Nurse"]
  PHA["E3 Pharmacy staff"]
  ADM["E4 Admin"]
  P1(("1.0 Identity & profiles"))
  P2(("2.0 Provider discovery & approval"))
  P3(("3.0 Care booking"))
  P4(("4.0 Prescription & order"))
  P5(("5.0 Support & notification"))
  P6(("6.0 Admin reporting"))
  D1[("D1 Identity users, roles & sessions")]
  D2[("D2 Nurses, skills, services & slots")]
  D3[("D3 Patients, addresses, bookings & reviews")]
  D4[("D4 Pharmacies, prescriptions & orders")]
  D5[("D5 Private prescription file storage")]
  D6[("D6 Support tickets, messages & audit")]
  D7[("D7 In-app notifications")]

  PAT -->|Registration, login, profile/address changes| P1
  NUR -->|Registration and profile edits| P1
  PHA -->|Invitation login| P1
  P1 <-->|Users, roles, refresh sessions| D1
  P1 -->|Authentication result and profile| PAT
  P1 -->|Authentication result| NUR
  P1 -->|Authentication result| PHA

  NUR -->|Skills and available time slots| P2
  ADM -->|Provider approvals and invitations| P2
  P2 <-->|Approved providers, skills, slots| D2
  P2 <-->|Pharmacy approval state| D4
  P2 -->|Eligible nurse search results| PAT

  PAT -->|Service/slot selection and booking requests| P3
  NUR -->|Accept, reject, start, complete| P3
  P3 <-->|Slots, services and matching constraints| D2
  P3 <-->|Addresses, booking and review history| D3
  P3 -->|Booking outcome and status| PAT
  P3 -->|Assigned bookings and state| NUR
  P3 -->|Booking event| P5

  PAT -->|Synthetic file upload and quote decision| P4
  PHA -->|Quote, prepare, dispatch, deliver status| P4
  P4 <-->|Prescription metadata, quotes and orders| D4
  P4 <-->|Protected file store/read| D5
  P4 -->|Quote and order status| PAT
  P4 -->|Assigned inbox/order status| PHA
  P4 -->|Order event| P5

  PAT -->|Own support ticket and replies| P5
  ADM -->|Support status updates/replies| P5
  P5 <-->|Support and audit records| D6
  P5 <-->|Recipient-scoped inbox| D7
  P5 -->|Personal notifications| PAT
  P5 -->|Personal notifications| NUR
  P5 -->|Personal notifications| PHA
  P6 <-->|Operational aggregation| D2
  P6 <-->|Booking activity| D3
  P6 <-->|Order activity| D4
  P6 <-->|Support and audit| D6
  ADM -->|Dashboard/audit query| P6
  P6 -->|Role-protected overview/audit| ADM
\`\`\`

**Boundaries:** D1–D4 and D6–D7 are *logical data-store groups*, not seven separate physical databases; the current relational data uses a shared EF Core context, and prescription binaries use a **private filesystem outside** public web files. A provider approval updates source tables; this DFD does not imply a new approvals table.

## DFD Level 2 — Process 3.0 Care booking
\`\`\`mermaid
flowchart TB
  PAT["Patient"]
  NUR["Approved Nurse"]
  C31(("3.1 Search active service / approved nurse"))
  C32(("3.2 Select available slot & service address"))
  C33(("3.3 Validate ownership and atomically create booking"))
  C34(("3.4 Authorize transition and update state"))
  C35(("3.5 Emit audit / in-app notification"))
  SLOTS[("D2 Skills, nurses, slots & services")]
  ADDR[("D3 Patient addresses and bookings")]
  NOTIF[("D7 Notification inbox")]
  AUDIT[("D6 Audit events")]

  PAT -->|Service, city and slot filters| C31
  C31 <-->|Approved nurse/services catalog| SLOTS
  C31 -->|Candidate nurse/slots| PAT
  PAT -->|Chosen service, slot and address| C32
  C32 <-->|Saved address owned by patient| ADDR
  C32 -->|Selected IDs and address snapshot| C33
  C33 <-->|Recheck slot and nurse approval| SLOTS
  C33 <-->|Atomic booking write and active-slot constraint| ADDR
  C33 -->|Booking reference / conflict response| PAT
  C33 -->|New booking event| C35
  NUR -->|Accept/reject/start/complete request| C34
  PAT -->|Eligible cancel request| C34
  C34 <-->|Current booking owner and state| ADDR
  C34 -->|Authorized status response| NUR
  C34 -->|State-changed event| C35
  C35 -->|Recipient notification| NOTIF
  C35 -->|Auditable transition entry where implemented| AUDIT
\`\`\`

**Business controls:** One active/history-preserving booking per availability slot is enforced using filtered unique index and application concurrency controls; transitions follow \`Booking.TransitionTo\`. The user may submit a saved \`addressId\` **or** free-text \`serviceAddress\`, but the persisted Booking only retains the **ServiceAddress snapshot**. This flow does not include an online payment gateway.

## DFD Level 2 — Process 4.0 Prescription and pharmacy order
\`\`\`mermaid
flowchart TB
  PAT["Patient"]
  PHA["Approved assigned pharmacy"]
  Q41(("4.1 Validate patient, pharmacy and synthetic file"))
  Q42(("4.2 Store private file and metadata"))
  Q43(("4.3 Pharmacy reviews and issues quote"))
  Q44(("4.4 Patient confirms or declines"))
  Q45(("4.5 Pharmacy advances fulfillment status"))
  Q46(("4.6 Create notifications and audit"))
  R[("D4 Prescriptions and orders")]
  F[("D5 Protected file storage")]
  N[("D7 In-app notifications")]
  A[("D6 Audit records")]

  PAT -->|Assigned pharmacy, optional booking and upload| Q41
  Q41 -->|Validated form / ownership| Q42
  Q42 -->|Private file bytes| F
  Q42 -->|Storage key and prescription metadata| R
  Q42 -->|Upload reference| PAT
  Q42 -->|Prescription event| Q46
  PHA -->|Assigned inbox and proposed amount| Q43
  Q43 <-->|Assigned prescription, existing quote check| R
  Q43 -->|Quote record| R
  Q43 -->|Quote status| PHA
  Q43 -->|Quote event| Q46
  PAT -->|Confirm or decline quote| Q44
  Q44 <-->|Own quoted order and version| R
  Q44 -->|Consent decision| R
  Q44 -->|Decision status| PAT
  Q44 -->|Decision event| Q46
  PHA -->|Prepare / dispatch / mark delivered| Q45
  Q45 <-->|Authorized order and current state| R
  Q45 -->|State transition| R
  Q45 -->|Fulfillment status| PHA
  Q45 -->|State event| Q46
  Q46 -->|Recipient alert| N
  Q46 -->|Auditable event| A
\`\`\`

**Business controls:** At most one PharmacyOrder per Prescription; only the assigned pharmacy may quote/update and only the owning patient may accept/decline. \`Delivered\` is an internal status, **not** an actual courier integration; no live stock, medication substitution or payment processing is represented.

## Process-to-code traceability

| Process | Primary source/API surface | Main stores |
| --- | --- | --- |
| 1.0 Identity & profiles | \`AuthController\`, \`AuthenticatorController\`, \`PatientsController\`, Identity infrastructure | \`AspNetUsers\`, Identity tables, \`RefreshSessions\`, \`Patients\`, \`PatientAddresses\` |
| 2.0 Provider discovery | \`NursesController\`, \`MatchingController\`, \`ServicesController\`, \`AdminController\` | \`Nurses\`, \`Skills\`, \`NurseSkills\`, \`CareServices\`, \`AvailabilitySlots\`, \`Pharmacies\` |
| 3.0 Booking | \`BookingsController\`, \`ReviewsController\` | \`Bookings\`, \`NurseReviews\`, \`PatientAddresses\` |
| 4.0 Prescription & order | \`PrescriptionsController\`, \`PharmacyOrdersController\`, \`PrivatePrescriptionStorage\` | \`Prescriptions\`, \`PharmacyOrders\`, private file storage |
| 5.0 Support & alerts | \`SupportController\`, \`NotificationsController\` | \`SupportTickets\`, \`SupportMessages\`, \`Notifications\`, \`AuditEvents\` |
| 6.0 Operations | \`DashboardController\`, \`AdminController\` | aggregates from existing tables |

## Review checklist
- [x] Context diagram and Level 1 decomposition include all four user roles.
- [x] Level 2 booking and pharmacy flows show input, output, store access and authorization boundaries.
- [x] External SMTP/Google conditional services are clearly identified.
- [x] Each logical data store is mapped to actual tables; no invented payment, courier or diagnostic systems.
- [ ] Review DFD balance/notation with the course's exact Gane–Sarson/Yourdon conventions, if required.
- [ ] Inspect Mermaid visual rendering and approve with Abdel Maqsoud and backend reviewer.
