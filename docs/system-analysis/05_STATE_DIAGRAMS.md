# DareCare — State Diagrams | مخططات الحالات
**Owner:** Abdel Maqsoud | **Notion task:** State Diagram | **Priority:** Medium.

**Authoritative source for allowed edges:** \`src/DareCare.Domain/Entities/Booking.cs\` (\`Booking.TransitionTo\`), \`PharmacyOrder.cs\` (\`PharmacyOrder.TransitionTo\`) and \`SupportTicket.cs\` (\`SupportTicket.ChangeStatus\`). The diagrams show **only** transitions explicitly allowed in the source—not imagined business states.

## 1. Booking lifecycle
\`\`\`mermaid
stateDiagram-v2
  [*] --> Pending : patient creates booking
  Pending --> Accepted : nurse accepts
  Pending --> Rejected : nurse rejects
  Pending --> Cancelled : patient cancels
  Accepted --> InProgress : nurse starts visit
  Accepted --> Cancelled : patient cancels
  InProgress --> Completed : nurse completes visit
  Completed --> [*]
  Cancelled --> [*]
  Rejected --> [*]
\`\`\`

| From | Allowed next | Actor / endpoint |
| --- | --- | --- |
| \`Pending\` | \`Accepted\` | Nurse — \`POST /api/v1/bookings/{id}/accept\` |
| \`Pending\` | \`Rejected\` | Nurse — \`POST /api/v1/bookings/{id}/reject\` |
| \`Pending\` | \`Cancelled\` | Patient — \`POST /api/v1/bookings/{id}/cancel\` |
| \`Accepted\` | \`InProgress\` | Nurse — \`POST /api/v1/bookings/{id}/start\` |
| \`Accepted\` | \`Cancelled\` | Patient — \`POST /api/v1/bookings/{id}/cancel\` |
| \`InProgress\` | \`Completed\` | Nurse — \`POST /api/v1/bookings/{id}/complete\` |

The server also validates the authenticated actor owns/controls the booking. A second active booking of the same slot is rejected by the database's filtered unique index. **Disallowed examples:** \`Pending → Completed\`, \`InProgress → Cancelled\`, \`Cancelled → Accepted\`. Enum values: Pending=0, Accepted=1, InProgress=2, Completed=3, Cancelled=4, Rejected=5.

## 2. Pharmacy order lifecycle
\`\`\`mermaid
stateDiagram-v2
  [*] --> Quoted : pharmacy creates first quote
  Quoted --> Confirmed : owning patient confirms
  Quoted --> Declined : owning patient declines
  Confirmed --> Preparing : assigned pharmacy prepares
  Preparing --> OutForDelivery : assigned pharmacy dispatches
  OutForDelivery --> Delivered : assigned pharmacy marks delivered
  Declined --> [*]
  Delivered --> [*]
\`\`\`

| From | Allowed next | Actor / endpoint |
| --- | --- | --- |
| \`Quoted\` | \`Confirmed\` | Patient — \`POST /api/v1/orders/{id}/confirm\` |
| \`Quoted\` | \`Declined\` | Patient — \`POST /api/v1/orders/{id}/decline\` |
| \`Confirmed\` | \`Preparing\` | Pharmacy — \`POST /api/v1/orders/{id}/prepare\` |
| \`Preparing\` | \`OutForDelivery\` | Pharmacy — \`POST /api/v1/orders/{id}/dispatch\` |
| \`OutForDelivery\` | \`Delivered\` | Pharmacy — \`POST /api/v1/orders/{id}/deliver\` |

Initial \`Quoted\` state is created by an approved assigned pharmacy's \`POST /api/v1/orders/prescriptions/{prescriptionId}/quote\`. There is a unique index on \`PharmacyOrders.PrescriptionId\`, so **at most one order per prescription**. The owning patient cannot prepare or dispatch, and unrelated pharmacy users cannot mutate the order. \`Delivered\` is an internal workflow flag; **no external courier** is integrated. Enum values: Quoted=0, Confirmed=1, Preparing=2, OutForDelivery=3, Delivered=4, Declined=5.

## 3. Support ticket lifecycle
\`\`\`mermaid
stateDiagram-v2
  [*] --> Open : ticket is created
  Open --> InProgress : admin triages
  Open --> Resolved : admin resolves directly
  Open --> Closed : admin closes
  InProgress --> Resolved : admin resolves
  InProgress --> Closed : admin closes
  Resolved --> InProgress : admin reopens
  Resolved --> Closed : admin closes
  Closed --> [*]
\`\`\`

| From | Allowed next | Privileged action |
| --- | --- | --- |
| \`Open\` | \`InProgress\`, \`Resolved\`, \`Closed\` | Admin — \`PATCH /api/v1/support/admin/tickets/{id}/status\` |
| \`InProgress\` | \`Resolved\`, \`Closed\` | Admin — same endpoint |
| \`Resolved\` | \`InProgress\`, \`Closed\` | Admin — same endpoint |
| \`Closed\` | No different status | Terminal in the current domain transition policy |

\`SupportTicket.ChangeStatus\` also accepts a request to keep the **same status** as a no-op (no transition). It does **not** allow \`Resolved → Open\`, \`Closed → Open\` or \`Closed → InProgress\`. The owner/Admin can read and reply under the authorization and business rules enforced by \`SupportController\`; posting a message is not, by itself, a status transition.

## 4. Provider approval is a flag, not a domain state-machine enum
\`NurseProfile.IsApproved\` and \`PharmacyProfile.IsApproved\` are database **booleans** controlled by Admin endpoints. Do **not** present fictional \`Submitted → Verified → Licensed\` transitions as implemented. Changing nurse skills triggers reapproval as documented by the API; actual approval behavior is enforced at controller/database level rather than these three domain machines.

## 5. Transition rules common to these diagrams
- All critical mutations pass through **server-side role and resource-ownership checks**. Client buttons do not govern authorization.
- Booking and order changes update their \`Version\` GUID optimistic-concurrency token; Bookings also use SQL Server \`RowVersion\`. SupportTicket changes touch their \`Version\`.
- Domain rejects illegal transitions via \`InvalidOperationException\`; controller may map to validation/conflict HTTP response depending on the situation. Do not assert a single HTTP code universally.
- Domain states and transitions shown above are source-derived; SMTP or notification delivery isn't a prerequisite transition in these domain methods.
- Automated unit/integration tests and a reviewed staging execution are separate acceptance requirements. Mermaid diagrams alone are not runtime evidence.

## Acceptance / verification checklist
- [x] Booking, pharmacy-order and support-ticket state diagrams contain all explicit allowed transitions and no extra edges.
- [x] Actors, endpoints, terminal states and illegal transition examples documented.
- [x] Reapproval flags distinguished from enum state machines.
- [ ] Re-review when domain rules change and confirm actual rendered diagrams.
- [ ] Abdel Maqsoud and the technical lead approve notation and actor mapping before final Notion \`Done\`.
