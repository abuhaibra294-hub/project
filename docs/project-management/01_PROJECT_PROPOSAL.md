# DareCare — Graduation Project Proposal

## Executive summary
**DareCare** is a home-care coordination platform linking patients to approved nurses and participating pharmacies, with oversight from a privileged administrator. Patients search care services, choose approved nurse availability, book home-care visits, and can submit a fictional prescription to an approved pharmacy for a price quote and status tracking. Nurses manage profiles, skills, availability and assigned bookings. Pharmacies review assigned prescriptions, quote requests and update fulfillment. Admins oversee approvals, accounts and audit activity.

## Problem statement
Patients struggle with fragmented home-care service discovery, reliable scheduling and visibility into medication request status. Nursing providers need clear availability and booking management; participating pharmacies need a secure handoff and auditable quotation workflow. The system addresses **coordination**, not medical decision-making.

## Objectives
- Provide discoverable, Admin-approved nurse profiles and available visit slots.
- Deliver an auditable booking state machine with prevention of concurrent double booking.
- Coordinate a patient-approved pharmacy quotation and fulfillment workflow.
- Protect private data through role authorization **and resource-ownership checks**.
- Demonstrate resilient integrations between .NET backend, Flutter patient app, web nurse/pharmacy/admin portals.
- Deliver a credible, reproducible university demo with documented tests and known limitations.

## User personas / actors
| Actor | Primary journey |
| --- | --- |
| Patient | Register/login, manage profile/addresses, discover nurses, book care, submit fictional prescription, review quote, track fulfillment |
| Nurse | Register, complete credentials, await Admin approval, configure skills/slots, manage booking life cycle |
| Pharmacy | Obtain admin invitation and approval, manage profile, view assigned prescriptions, quote, fulfill approved orders |
| Admin | Invite/approve participating providers, review accounts, monitor activity/audit and operational summaries |

## MVP scope — already represented in repository
Identity and role guardrails; JWT and rotating refresh sessions; patient/nurse registration; pharmacy invitation; Admin approvals; patient addresses; nurse skills/availability/search and explainable rule matching; care bookings and timelines; prescription upload and restricted download; pharmacy quote/order states; dashboards; notifications; reviews, favorites and support. Additional email OTP, authenticator MFA and Google patient login exist in the merged source, subject to environment configuration.

## Exclusions / do not overclaim
No physician consultation/diagnosis or medically validated recommendations; no real clinical approval certification; no integrated courier, live pharmacy stock system, electronic payments, medication substitution, or verified final-mile delivery service; notifications are primarily in-app/polled; deployed production-grade privacy, scanning, backup and compliance safeguards are incomplete.

## Value proposition and measurable success criteria (proposed demo targets)
- One full patient→approved nurse→completed booking flow with no manual DB manipulation.
- One approved pharmacy→synthetic prescription→quote→patient confirmation→delivery-status flow.
- No successful cross-account reads/writes of protected resources in agreed negative test suite.
- CI build/test passes and the same candidate passes staging role-based E2E flows.
- Flutter/Web clients handle 401/403/409, 202 MFA, loading, empty and failure states.
- Evaluation records completed user scenarios, observed failures and timestamps, not invented production KPIs.

## Stakeholders and constraints
Academic supervisors and an eight-person delivery team; two .NET backend roles, two web frontend roles, one Flutter, one UI/UX, one Cybersecurity, and one project lead/integration owner. Verify actual members and ownership in kickoff. Use only fictional healthcare records until legal, professional and security requirements are satisfied.

## Deliverable
A working synthetic home-care MVP with project proposal, API spec, architecture, Flutter/web interfaces, E2E evidence, security checklist and a short guided demo. Acceptance is based on runnable behavior and traceable evidence rather than screenshots alone.
