# DareCare — Delivery Plan & Ownership

## Planning assumptions
Planning baseline: **19 November 2026**. Below is a **proposed 5-week stabilization and integration schedule**, not an agreed university deadline. Existing backend and DareCare Lite source are reused. Validate deadlines and personnel at kickoff.

## Team responsibility matrix
| Role | Accountable output | Supporting dependencies |
|---|---|---|
| Project Lead / Integration (member 1) | Scope, weekly priorities, issue triage, API freeze, integration sign-off and demo | All roles |
| Backend A (.NET) (member 2) | Identity, OTP/MFA/Google staging activation, roles/approval, auth contracts | Cyber, Flutter, Web |
| Backend B (.NET) (member 3) | SQL Server persistence, booking/order lifecycle, prescription security, staging | Backend A, Cyber |
| Flutter (member 4) | Patient app auth, nurse search/booking, medicine requests, error/refresh flow | API contract, UI/UX |
| Frontend A (member 5) | Nurse web portal, booking lifecycle, notifications and responsive RTL states | Backend A/B, UI/UX |
| Frontend B (member 6) | Pharmacy and Admin portal UX/integration; coordinate privileged review | Backend A/B, Cyber, UI/UX |
| UI/UX (member 7) | Journey mapping, component specs, accessibility, UX QA | Flutter, Web |
| Cybersecurity (member 8) | Threat model, IDOR/auth tests, upload security, staging hardening checklist | Backend A/B |

The lead is an **accountability role**, not permission to bypass code review. No identities or GitHub assignees were guessed.

## Delivery roadmap
| Week (proposed) | Focus | Exit criteria |
|---|---|---|
| 1 — 19–25 Nov | Kickoff, route/source vs live contract reconciliation, demo architecture freeze | Exact deployed SHA + current OpenAPI captured; issue #12 closed with evidence |
| 2 — 26 Nov–2 Dec | Auth activation and persistent staging design, UI screens ready | Endpoints & error contracts approved, SMTP/Google staging tests, SQL migration plan |
| 3 — 3–9 Dec | Flutter and nurse/pharmacy/admin web functional integration | Happy paths work on local/test staging; negative paths logged |
| 4 — 10–16 Dec | Cross-role E2E, security, responsive UX, persistence and backups | Staging scenario evidence, IDOR tests, browser CORS, DB restart persistence |
| 5 — 17–23 Dec | Regression fixes, release-candidate rehearsal, supervisor package | All P0 issues closed, demo rehearsal signed off, blockers documented |

Dates are proposals and may shift with academic schedule and hosting availability.

## Workstream dependencies
1. **API reconciliation (#12)** blocks declaring deployed contract ready for team use.
2. **Durable staging (#13)** blocks proof of persistent operational workflows and safe file durability.
3. **OTP/Google/MFA staging (#14)** blocks declaring real auth integrations complete.
4. **Flutter (#15), Web (#16), Admin (#17)** may develop against reviewed API contracts, but live integration sign-off waits for backend checks.
5. **Security (#18)** runs throughout development, not only before the demo.
6. **UX (#19)** starts immediately and informs Flutter/Web acceptance tests.
7. **Final release/demo (#20)** depends on all P0 acceptance evidence.

## Workflow
- GitHub Issues: every task has owner, priority, due date, dependencies, acceptance criteria and evidence.
- Branch from **`darecare-demo-free`**, PR into **`darecare-demo-free`**; never into unrelated `main`.
- Require peer review, CI build/tests, security checks for sensitive APIs, and screenshots/video for UI changes.
- Escalate blockers unresolved for >1 workday to lead; disclose environment, minimal repro, affected endpoint, exact status, and sanitized request ID.
- No one pastes secrets or personal patient data in Issues, PRs or Postman exports.
- Definition of Done: merged reviewed PR, updated docs/contracts, passing tests, target-environment verification when needed, and traceable evidence.

## Deliverables and issue references
- #12 API contract reconciliation; #13 persistent SQL + private storage; #14 identity integration; #15 Flutter; #16 Web nurse/pharmacy; #17 Admin; #18 cybersecurity; #19 UX design; #20 demo release.
- Priority: P0 before candidate release; P1 UX improvements selected by supervisor feedback; P2 feature expansion only after MVP is stable.
