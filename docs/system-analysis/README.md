# DARECARE — System Analysis Package (Abdel Maqsoud)
**Prepared:** 19 November 2026 | **Owner shown in Notion screenshot:** عبد المقصود | **Intended delivery:** five source-backed, editable diagrams/documents for graduation-project analysis.

## Deliverables

| Notion task | Priority in screenshot | Document | Includes |
| --- | --- | --- | --- |
| ER Diagram | High | [01_ER_DIAGRAM.md](01_ER_DIAGRAM.md) | Three editable ERDs, full FK relationship inventory and cardinality/ownership notes |
| Logical & Physical Schema | High | [02_LOGICAL_PHYSICAL_SCHEMA.md](02_LOGICAL_PHYSICAL_SCHEMA.md) | Logical concepts, 25 SQL Server tables, full physical column data dictionary, 43 indexes/checks |
| DFD (Data Flow Diagrams) | Medium | [03_DATA_FLOW_DIAGRAM.md](03_DATA_FLOW_DIAGRAM.md) | Context (Level 0), Level 1, Level 2 booking and pharmacy data flows |
| Component Diagram | Medium | [04_COMPONENT_DIAGRAM.md](04_COMPONENT_DIAGRAM.md) | As-is .NET architecture, assembly dependencies and two sequence illustrations |
| State Diagram | Medium | [05_STATE_DIAGRAMS.md](05_STATE_DIAGRAMS.md) | Booking, pharmacy order and support ticket state machines from exact domain rules |

## Source and analysis methods
- GitHub repository: \`abuhaibra294-hub/project\`; current DARECARE work is on \`darecare-demo-free\`, **not** the unrelated default \`main\`.
- Physical database source: \`src/DareCare.Infrastructure/Persistence/Migrations/20261030184459_InitialSqlServer.cs\` and \`DareCareDbContext.cs\`.
- Runtime domain rules: \`src/DareCare.Domain/Entities/Booking.cs\`, \`PharmacyOrder.cs\`, \`SupportTicket.cs\`; respective enum files under \`src/DareCare.Domain/\`.
- Data flows: \`src/DareCare.Api/Controllers/\`, account email and private file services; external integration status is documented conditionally.
- Project references: \`DareCare.slnx\` and each \`src/DareCare.*/*.csproj\`.
- Data dictionary reflects **SQL Server migration source** only; the free demo uses disposable SQLite. These are documentation assets, **not** evidence that paid persistent staging exists.

## Accuracy and editorial policy
1. The ERDs use the **actual** physical FK graph. \`Bookings.PatientUserId\` references \`AspNetUsers\`; saved address ID is not persisted in \`Bookings\`.
2. Schema is derived from **25 SQL Server tables, 35 foreign keys, 43 indexes and 170 columns** in the migration.
3. DFD cylinders are **logical groups of stores**, not separate microservice databases.
4. Application contains **DTO contracts**; controllers still own orchestration in places. The Component Diagram never falsely claims full use-case Clean Architecture.
5. State machine edges are restricted to explicit Domain methods; \`IsApproved\` provider booleans are **not** their own enumerated workflow.
6. Prescription **file bytes** live in non-public storage outside SQL Server and should only contain fictional data for this demo.
7. No real clinical diagnosis, payments, pharmacy inventory, courier provider or medical claim was added to the drawings.

## Notion submission
See [06_NOTION_HANDOFF_AR.md](06_NOTION_HANDOFF_AR.md) for **ready-to-paste Arabic Description and Acceptance Criteria** for each of Abdel Maqsoud's five rows, plus GitHub links and review status. The Notion team-page link was **not accessible** through the connected integration, so **no existing task statuses were modified**. The screenshot shows all five as \`To Do\`.

## Acceptance and sign-off
- [x] All five technical topics documented using source material.
- [x] Diagram source is editable Mermaid embedded in GitHub Markdown.
- [x] SQL schema covers all physical tables, columns, constraints and FK relationships of the current migration.
- [x] Domain machine diagrams cover the explicit booking, pharmacy order and support transitions.
- [ ] Reviewer opens GitHub files and confirms diagram visual rendering/layout.
- [ ] Abdel Maqsoud validates academic conventions and identifies any requested notation changes.
- [ ] Backend/database reviewer checks current model revision; instructor signs off.
- [ ] Notion task rows updated after review; do not assert a status change from this PR.

**Status:** documentation prepared for review. This PR does not alter application behavior, SQL Server migrations or production deployment.
