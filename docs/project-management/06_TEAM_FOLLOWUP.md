# DareCare — Team Follow-Up, Risks & Release Readiness

## Operating cadence (proposal)
- **Daily asynchronous update** by each member: Yesterday / Today / Blocker / PR or evidence URL (5 lines maximum).
- **Monday 30-minute planning:** review previous exit criteria, prioritize P0, confirm API changes and owner/dependencies.
- **Wednesday 15-minute integration checkpoint:** Flutter↔backend and Web↔backend verify one real flow with a redacted trace.
- **Friday 30-minute review/demo:** each owner demonstrates completed acceptance on staging/device, records bugs and next-week carry-over.
- Lead checks blocked tasks each workday; blockers older than one day require explicit decision (help, re-scope, escalate).

## Live issue board
| Priority | Issue | Lead role | Evidence required |
|---|---|---|---|
| P0 | [#12 API contract reconciliation](https://github.com/abuhaibra294-hub/project/issues/12) | Backend A + Lead | deployed SHA, source vs live diff |
| P0 | [#13 persistent staging](https://github.com/abuhaibra294-hub/project/issues/13) | Backend B | SQL migration, restore/restart test |
| P0 | [#14 SMTP/Google/MFA](https://github.com/abuhaibra294-hub/project/issues/14) | Backend A + Cyber | role-secure staging auth tests |
| P0 | [#15 Flutter integration](https://github.com/abuhaibra294-hub/project/issues/15) | Flutter | real Android/client workflows |
| P0 | [#16 Nurse/Pharmacy web](https://github.com/abuhaibra294-hub/project/issues/16) | Web A + B | browser E2E, allowed CORS origin |
| P0 | [#17 Admin privileged QA](https://github.com/abuhaibra294-hub/project/issues/17) | Lead + Web B | test admin portal, forbidden-role checks |
| P0 | [#18 security regression](https://github.com/abuhaibra294-hub/project/issues/18) | Cyber | IDOR and upload/session evidence |
| P1 | [#19 RTL UX handoff](https://github.com/abuhaibra294-hub/project/issues/19) | UI/UX | flows, component specs, QA |
| P0 | [#20 demo release candidate](https://github.com/abuhaibra294-hub/project/issues/20) | Lead, all reviewing | E2E rehearsal, release notes, CI links |

## Weekly status template
```markdown
Week ending:
Overall: GREEN / AMBER / RED (with reason)
Completed with proof: #issue — PR/CI/recording
In progress: #issue — owner role — expected next checkpoint
Blocked: #issue — dependency — support requested — decision owner
Changes to contract: endpoint/DTO/version, compatibility impact
Bugs discovered: reproducible steps, environment, expected/actual
Next demo scenario: actor + start/end state + staging revision
Decision log: decision / rationale / approver / date
```

## Risk register (living document)
| Risk | Impact | Likelihood (initial estimate) | Mitigation/owner |
|---|---|---|---|
| Runtime API behind source (100 vs prior 93) | Flutter/Web integration failures | High | #12 Backend A, freeze verified live spec |
| Render Free SQLite data loss | Demo instability, invalid persistence claims | High | #13 Backend B, durable isolated staging |
| OAuth/SMTP not configured on environment | OTP/Google flows fail live | High | #14 Backend A + Cyber, secret config and E2E |
| Cross-user IDOR or role mistakes | Privacy/security breach | High consequence | #18 Cyber and backend ownership tests |
| Prescription files not backed up/scanned | File loss/malware exposure | High consequence | #13/#18; fictional data only |
| Team members integrating against different API revisions | Feature mismatch and wasted effort | Medium | Versioned docs/issue #12 and release tagging |
| Frontend only mocked or Postman-only tested | False demo readiness | Medium | #15/#16/#20 actual browser/device checks |
| Missing time for academic presentation | Incomplete demonstration | Medium | Weekly rehearsal, stable fallback scenario |

Risk likelihood is **planning judgment**, not measured incident frequency.

## Decision log (initial)
- 2026-11-19: Proposed to keep REST modular monolith and avoid mid-project microservices migration. Pending team review.
- 2026-11-19: Documentation baseline taken from source `94ebc1e...`; **not** a certification that staging deployed that SHA.
- 2026-11-19: Prioritize integration/security/staging verification over adding new patient-facing features.
- 2026-11-19: Use synthetic data for all demonstrations and reject external clinical launch until separate requirements met.

## Demo rehearsal (12–15 minutes proposed)
1. Project problem, personas, boundaries (1–2 min).
2. Patient signup, service discovery, Nurse approval and booking (4 min).
3. Nurse accepts/completes; Patient review (2 min).
4. Pharmacy quote, Patient confirmation, fulfillment status (3 min).
5. Admin audit, denial of unauthorized requests, architecture & next steps (2–4 min).
6. Fallback: pre-recorded **real staging run** clearly labeled by environment/date if live internet fails, not a mock passed off as live.

## Release candidate checklist
- [ ] Team scope and academic date confirmed
- [ ] API source/live contract reconciled and deployed SHA recorded
- [ ] CI, integration and security suites pass on release candidate
- [ ] Role-specific Flutter/Web/Admin flows verified with fictional accounts
- [ ] SQL Server persistence and private-file backup/restore tested
- [ ] HTTPS, approved origins, secret storage and logging safeguards reviewed
- [ ] All P0 GitHub issues closed with evidence, all blocking bugs fixed or explicitly accepted
- [ ] Demo script rehearsed, fallback prepared and final signoff recorded

## Explicit Notion-to-GitHub progress mapping for Mahmoud Gamal

The screenshot of **DarCare Team Tasks** shows all 10 Mahmoud Gamal tasks as `To Do`. Documentation coverage and formal task completion are **not** the same thing. Use [12_MAHMOUD_TASK_TRACEABILITY.md](12_MAHMOUD_TASK_TRACEABILITY.md) for each title, priority, exact deliverable link and reviewer/execution gate. Existing Notion records were not modified: the linked source page was not accessible to the current Notion integration at review time.

For a weekly review, separately track: **prepared**, **reviewed/accepted**, **implemented**, **CI-tested**, **deployed**, and **verified on the target environment**. Close only genuinely satisfied task criteria with evidence. Additional task references: [07 Technology Stack](07_TECHNOLOGY_STACK.md), [08 API routes](08_API_ROUTE_INVENTORY.md), [09 Deployment Strategy](09_DEPLOYMENT_STRATEGY.md), [10 Deployment Diagram](10_DEPLOYMENT_DIAGRAM.md), [11 KPIs](11_KPIS_AND_MEASUREMENT.md).
