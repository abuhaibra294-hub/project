# DareCare — Project Management & Handoff Pack
**Prepared:** 19 November 2026 | **Baseline:** `darecare-demo-free` @ `94ebc1e18ffc4ead8960658943af33f4ed46ba43` | **Status:** planning documentation, not deployment certification.

## Contents
1. [Project proposal and scope](01_PROJECT_PROPOSAL.md)
2. [Delivery plan, responsibilities and milestones](02_DELIVERY_PLAN.md)
3. [Backend architecture and technical decisions](03_BACKEND_ARCHITECTURE.md)
4. [API contract and documentation reference](04_API_DOCUMENTATION.md)
5. [Integration and testing playbook](05_INTEGRATION_PLAYBOOK.md)
6. [Team follow-up, risks and demo readiness](06_TEAM_FOLLOWUP.md)

## Source of truth, evidence and important caveats
- Repository is `abuhaibra294-hub/project`; DareCare development target is **`darecare-demo-free`**, not the repository default `main`.
- The committed `integration/DareCare_API_Endpoints.json` currently contains **100** declared operations, while the previously documented 16-Nov public staging probe verified **93**. This pack does **not** claim 100 operations are deployed.
- Live contract is `GET /openapi/v1.json` from the **target environment** after verifying its deployed commit. The source snapshot and Postman requests are handoff aids, not proof of live behavior.
- Source includes Patient, Nurse, Pharmacy and Admin, with ASP.NET Core .NET 10 / Identity / EF Core, REST `/api/v1`, SQL Server as intended persistent schema, disposable SQLite for Render Free demonstration.
- Merged PR #11 adds email OTP, authenticator MFA and patient Google sign-in to source. Real email/Google flows require staging configuration and tests.
- Public demonstration uses fictional people and prescriptions only. This is a graduation MVP, **not a clinically certified or production-ready healthcare service**.
- Sample staging URL in existing handoff: `https://project-kv53.onrender.com`; re-verify service health and deployed revision before using.

## Active work queue
GitHub issues [#12](https://github.com/abuhaibra294-hub/project/issues/12) through [#20](https://github.com/abuhaibra294-hub/project/issues/20) contain prioritized ownership by role and explicit acceptance gates. Do not auto-assign GitHub usernames absent team confirmation.

## Reporting convention
Every completed deliverable must link a PR/commit, CI run, endpoint contract or design, and real test evidence (where applicable). Distinguish **designed**, **implemented in source**, **CI-tested**, **deployed**, and **verified in target environment**.

## Mahmoud Gamal — 10 Notion deliverables coverage (19 Nov 2026 screenshot)

The screenshot lists 10 **To Do** tasks assigned to Mahmoud Gamal. The linked Notion task pages are not accessible through the current integration, so the files below are **documentation prepared for review** rather than official Notion completion or live system verification:

7. [Technology Stack](07_TECHNOLOGY_STACK.md) — exact .NET, EF, Identity, SDK and frontend/proposed tool boundaries.
8. [Complete API inventory](08_API_ROUTE_INVENTORY.md) — all 100 operations in the source manifest, with the Pharmacy available-role mismatch flagged.
9. [Deployment Strategy](09_DEPLOYMENT_STRATEGY.md) — disposable vs durable environments, manual migrations, secrets, backup/restore and rollback.
10. [Deployment Diagrams](10_DEPLOYMENT_DIAGRAM.md) — editable Mermaid topology for existing free demo and proposed persistent staging.
11. [KPIs and Measurement](11_KPIS_AND_MEASUREMENT.md) — definition, formulas, proposed targets, owners and data sources, **not** observed statistics.
12. [Mahmoud's Notion Task Traceability](12_MAHMOUD_TASK_TRACEABILITY.md) — 10-task-to-document map, priority and remaining reviewer/engineering acceptance gates.

**Review workflow:** inspect these files, approve requirements/dates/metrics with the team, test deployment/integration on a known revision and then update Notion task statuses when access is available. The Readme/earlier package captures code baseline `94ebc1e...` for PR #11; PR #21 merged the original documentation only, and this extension does not claim newer runtime deployment.
