# DareCare — Mahmoud Gamal's Notion Tasks: Delivery Traceability

## Evidence boundary
This inventory transcribes the **10 visible task titles and priorities from the user's 19-Nov-2026 Notion screenshot**. The underlying Notion database/page was not accessible to the connected Notion integration when checked; individual task descriptions, deadline end dates and hidden acceptance criteria could **not** be inspected. The screenshot shows **To Do** for all ten tasks. Do not change their Notion status based on GitHub documentation alone.

This document maps **document preparation**, not team acceptance, live deployment, or a claim of completed integration.

## Task-to-artifact matrix

| Notion task (owner shown: Mahmoud Gamal) | Priority shown | Source-backed deliverable | Documentation stage | Remaining acceptance |
| --- | --- | --- | --- | --- |
| Problem Statement & Objectives | High | `01_PROJECT_PROPOSAL.md` → Problem statement and objectives | Draft documented | Supervisor validates problem and measurable objectives |
| Technology Stack | Medium | `07_TECHNOLOGY_STACK.md` | Draft documented | Team confirms stack and Flutter/Web final choices |
| Project Proposal | High | `01_PROJECT_PROPOSAL.md` | Draft documented | Academic format, signatures and exclusions reviewed |
| Project Plan | High | `02_DELIVERY_PLAN.md` | Draft documented | Deadline, estimates and milestones confirmed with team |
| Task Assignment & Roles | High | `02_DELIVERY_PLAN.md` and `06_TEAM_FOLLOWUP.md` | Roles drafted | Assign actual people/GitHub users and confirm capacity |
| Software Architecture | High | `03_BACKEND_ARCHITECTURE.md` | As-is architecture documented | Backend reviewers validate integration/invariants |
| API Documentation | High | `04_API_DOCUMENTATION.md` plus `08_API_ROUTE_INVENTORY.md` | 100 source operations mapped | Verify deployed runtime OpenAPI, schemas and policy mapping |
| Deployment Strategy | Medium | `09_DEPLOYMENT_STRATEGY.md` | Strategy/runbook drafted | Approve hosting, execute/stage and test deployment steps |
| Deployment Diagram | Medium | `10_DEPLOYMENT_DIAGRAM.md` | Editable Mermaid diagrams prepared | Confirm target topology and actual live deployment |
| KPIs | Medium | `11_KPIS_AND_MEASUREMENT.md` | Measurable definitions drafted | Set approved thresholds and instrument/collect data |

## Acceptance checklist — documentation vs execution
- [x] Deliver an explicit problem statement and goals.
- [x] Reference real source package versions and explain stack boundaries.
- [x] Produce scope and proposal plus roles/milestones.
- [x] Describe layered modular-monolith backend and state/data security invariants.
- [x] Publish searchable route inventory of the **source** API.
- [x] Provide release strategy, deployment diagram and rollback guidance.
- [x] Define KPI formulas, owners, measurement sources and review template.
- [ ] Inspect the detailed Notion task pages and reconcile hidden criteria.
- [ ] Reconcile source API vs actually deployed operations (issue #12).
- [ ] Demonstrate persistent staging and recovery (issue #13).
- [ ] Confirm real Flutter/Web/Admin integrations and negative-path tests (issues #15–#18).
- [ ] Record approved thresholds and supervisor/team sign-off.
- [ ] Change Notion task statuses **only** after review and permitted access.

## How to hand over
1. Share `docs/project-management/README.md` with academic supervisor and team.
2. For each Notion row, attach the exact GitHub document/PR link and ask the reviewer to note required changes.
3. Where a task is solely to *write a document*, it may be marked accepted after its required reviewer approves it. Do **not** treat runtime staging and device tests as documentation tasks.
4. For API and deployment execution, attach staging URL, deploy SHA, live OpenAPI and test evidence before declaring those engineering milestones complete.
5. Update GitHub issues (#12–#20) with evidence. Do not create duplicate tasks blindly; preserve original Notion history.

**Overall:** ten deliverable subjects have documentation coverage after this package, but **zero statuses are asserted changed in Notion**. The status of live technical acceptance remains mixed/pending.
