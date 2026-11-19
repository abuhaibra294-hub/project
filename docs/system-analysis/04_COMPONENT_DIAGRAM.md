# DareCare — Software Component Diagram
**Owner:** Abdel Maqsoud | **Notion task:** Component Diagram | **Priority:** Medium | **Source:** \`DareCare.slnx\`, \`src/\`, \`Program.cs\`, runtime controller/DTO references, 19 Nov 2026.

## Component structure — what exists now
DARECARE is a **layered modular monolith**, not an implemented microservices system. \`DareCare.Application\` **currently contains API contract DTOs**, not a complete use-case/service bus. Several controllers directly coordinate EF Core persistence and domain methods. The diagram intentionally shows that practical as-is coupling.

\`\`\`mermaid
flowchart TB
  subgraph USERS["Presentation / clients"]
    FL["Flutter Patient App (team client deliverable)"]
    WEB["External Nurse / Pharmacy Web (team client deliverable)"]
    LITE["DareCare Lite / Admin UI (same-origin demo source)"]
  end

  subgraph API["DareCare.Api / ASP.NET Core .NET 10"]
    ENTRY["HTTPS Routing / Controller endpoints / OpenAPI"]
    GUARD["JWT bearer, role & owner checks, rate limits, CORS"]
    AUTH["Auth + Authenticator controllers"]
    CARE["Nurses / Services / Bookings / Matching controllers"]
    PHARMA["Prescriptions / Orders controllers"]
    ADMIN["Admin / Dashboard / Support / Notifications controllers"]
    MAIL["AccountEmailService"]
    STORE["PrivatePrescriptionStorage"]
  end

  subgraph APP["DareCare.Application"]
    DTO["Request / Response DTO contracts"]
  end

  subgraph DOMAIN["DareCare.Domain"]
    MODEL["Patient, provider, booking, prescription and support entities"]
    RULES["Booking / order / support state transition rules"]
  end

  subgraph INFRA["DareCare.Infrastructure"]
    IDENTITY["ASP.NET Core Identity models / refresh sessions"]
    DBCTX["DareCareDbContext / EF Core mappings & migrations"]
  end

  DATABASE[("SQL Server durable target / SQLite demo provider")]
  FILES[("Private prescription files outside wwwroot")]
  SMTP["SMTP provider (configured environments)"]
  GOOGLE["Google ID token trust (configured patient sign-in)"]

  FL -->|HTTPS /api/v1| ENTRY
  WEB -->|HTTPS /api/v1| ENTRY
  LITE -->|Same-origin /api/v1| ENTRY
  ENTRY --> GUARD
  GUARD --> AUTH
  GUARD --> CARE
  GUARD --> PHARMA
  GUARD --> ADMIN
  AUTH --> DTO
  CARE --> DTO
  PHARMA --> DTO
  ADMIN --> DTO
  CARE --> MODEL
  PHARMA --> RULES
  ADMIN --> MODEL
  AUTH --> IDENTITY
  AUTH --> DBCTX
  CARE --> DBCTX
  PHARMA --> DBCTX
  ADMIN --> DBCTX
  DBCTX --> MODEL
  DBCTX --> IDENTITY
  DBCTX --> DATABASE
  AUTH --> MAIL
  AUTH -.-> GOOGLE
  MAIL -.-> SMTP
  PHARMA --> STORE
  STORE --> FILES
\`\`\`

**Clarifying boundaries**
- \`DareCare.Api\` references Application and Infrastructure project assemblies; Application and Infrastructure depend on Domain. Controllers may call the domain entities/DbContext directly, so this is **not** falsely presented as fully implemented Clean Architecture.
- API security uses ASP.NET Identity, JWT access/hashed rotating refresh sessions, role guards and **resource ownership**. UI controls are not authority to bypass backend checks.
- \`DareCare.Api/Services\` currently includes account email and private file-storage implementations; they are **not** independently deployed services.
- \`DareCare.Infrastructure/Persistence\` contains the DbContext, migration and seeder; it is the single relational model. Provider uses SQL Server for persistent target vs temporary SQLite demo.
- SMS, Stripe/payment, inventory, third-party courier and AI clinical inference components do **not** exist in the current MVP; none are drawn.
- Flutter and external web clients are integration deliverables, not proven live applications at the documented commit; DareCare Lite/Admin web assets exist in the API source.

## Project and assembly dependencies (source-level)
\`\`\`mermaid
flowchart LR
  API["DareCare.Api.csproj"] --> APP["DareCare.Application.csproj"]
  API --> INF["DareCare.Infrastructure.csproj"]
  APP --> DOM["DareCare.Domain.csproj"]
  INF --> DOM
  INF --> EF["EF Core / SQL Server / SQLite / Identity packages"]
  API --> JWT["JWT Bearer / OpenAPI / Google.Apis.Auth / Otp.NET"]
\`\`\`

This is the project-reference/package dependency graph; direct namespaces inside controllers can additionally use Domain types via referenced assemblies. Do not infer that Application hosts the booking logic as a separate service.

## Integration sequence — core booking (logical)
\`\`\`mermaid
sequenceDiagram
  actor Patient
  participant Client as Flutter / Web client
  participant API as BookingsController
  participant DB as DareCareDbContext
  participant Rules as Booking entity
  Patient->>Client: Select approved nurse, slot and address
  Client->>API: POST /api/v1/bookings (Bearer Patient)
  API->>DB: Validate service, ownership, approval, slot and conflicts
  DB-->>API: Current relational state
  API->>DB: Insert Booking + notice/audit data as applicable
  DB-->>API: Save / concurrency result
  API-->>Client: Booking response or validation/conflict
  Note over Client,API: Nurse later invokes accept/start/complete transitions
  Client->>API: GET /api/v1/bookings/mine
  API->>DB: Query only current patient's bookings
  DB-->>API: Owner-filtered rows
  API-->>Client: Current booking states
\`\`\`

\`Booking.TransitionTo\` controls later state changes; first creation initializes \`Pending\`. The database filtered unique index prevents a second active booking of the same slot. The code's actual HTTP error type depends on the validation and conflict branch.

## Integration sequence — fictional prescription and pharmacy
\`\`\`mermaid
sequenceDiagram
  actor Patient
  actor Pharmacy
  participant API as Prescription / Order controllers
  participant Files as PrivatePrescriptionStorage
  participant DB as DareCareDbContext
  Patient->>API: POST /api/v1/prescriptions (multipart, Patient token)
  API->>Files: Store validated fictional file privately
  Files-->>API: Generated storage key
  API->>DB: Persist metadata assigned to approved pharmacy
  DB-->>API: Prescription ID
  API-->>Patient: Upload result
  Pharmacy->>API: POST /api/v1/orders/prescriptions/{id}/quote
  API->>DB: Verify assigned pharmacy + unique quote rule
  DB-->>API: Quote/order created
  API-->>Pharmacy: Quoted
  Patient->>API: POST /api/v1/orders/{id}/confirm
  API->>DB: Verify patient ownership + transition
  DB-->>API: Confirmed
  API-->>Patient: Confirmation
  Pharmacy->>API: prepare / dispatch / deliver
  API->>DB: Enforce state sequence + updated version
  DB-->>API: State persisted
  API-->>Pharmacy: Latest status
\`\`\`

The flow is an **application-managed delivery status**, not third-party real-world medicine dispensing or shipping integration.

## Test / acceptance checklist
- [x] Source-oriented component diagram with four assemblies, API modules, auth and persistence boundaries.
- [x] Separate project-reference diagram and two flow/interaction examples.
- [x] Distinguishes present implementation from proposed independent mobile/web clients and conditional SMTP/Google.
- [x] Does not invent a microservices architecture, payment or courier service.
- [ ] Backend reviewers check against future DI/service extraction changes.
- [ ] Confirm Mermaid renders on GitHub and obtain Abdel Maqsoud/course reviewer approval.
