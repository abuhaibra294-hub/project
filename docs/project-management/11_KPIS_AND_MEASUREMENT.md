# DareCare — KPIs & Success Measurement | مؤشرات قياس نجاح المشروع

**Status:** proposed measurable framework for a graduation-project MVP, **not measured performance claims**. Confirm thresholds with academic supervisor and engineering owners. Use synthetic data only; do not log personal medical details.

## 1. Delivery and engineering readiness KPIs

| KPI / owner | Definition / calculation | Proposed acceptance target | Data source / frequency |
| --- | --- | --- | --- |
| P0 delivery completion — Lead | P0 issues accepted with linked evidence ÷ all in-scope P0 issues ×100 | 100% before final rehearsal | GitHub Issues/PRs, weekly |
| Workstream transparency — Lead | In-progress tasks with owner, blocker, PR/status and next checkpoint ÷ all in-progress tasks ×100 | ≥95% weekly | Issue board, Friday |
| Critical CI pass — Backend | Required CI workflows passing for release commit ÷ required workflows ×100 | 100% (no skipped gating jobs counted as passed) | GitHub Actions on candidate |
| API contract parity — Backend A | Exact matching method/path/policy operations in runtime vs approved manifest ÷ approved operations ×100 | 100% before staging sign-off | Live OpenAPI + manifest, each release |
| Blocker resolution age — Lead | Median working time from blocker reported to explicit decision | ≤1 workday for triage (not necessarily fix) | Issue activity, weekly |
| Escaped critical defects — QA | Count of unresolved P0 defects at rehearsal | 0 | GitHub bug labels and QA evidence |

## 2. API, security and integration quality KPIs

| KPI / owner | Calculation | Proposed target | How to verify |
| --- | --- | --- | --- |
| Core flow E2E pass rate — Integration | Passed core scenarios ÷ executed core scenarios ×100 | 100% for the 2 required journeys | Real client HTTP/browser evidence on same staging revision |
| Access-control negative tests — Cyber | Passed cross-account/role-denial cases ÷ executed cases ×100 | 100% for agreed security matrix | Automated 401/403/404 tests |
| Booking conflict integrity — Backend B | Successful second active booking of the same slot | 0 in concurrent synthetic test suite | SQL Server concurrency test |
| Auth and MFA regression — Backend A | Passed login, rotation, OTP expiry, MFA, recovery reuse checks ÷ planned checks ×100 | 100% critical cases | Auth CI and staging test run |
| HTTP 5xx rate — Backend | Requests resulting 5xx ÷ all eligible requests ×100, excluding deliberate chaos tests | <1% during controlled rehearsal | Sanitized request logs; instrumentation required |
| API p95 latency — Backend | 95th percentile of completed request durations over representative synthetic workflow | Draft goal ≤1000ms for selected read APIs at documented load | Instrumented staging smoke/perf script (not yet measured) |
| Persistent data after restart — Backend B | Test records and matching files found after controlled restart ÷ expected artifacts ×100 | 100% | Repeatable staging SQL/file readback |
| Backup restore success — Backend B + Cyber | Rehearsed restore attempts passing consistency checks ÷ attempts ×100 | 100% of planned dry-runs | Timestamped restore log |

## 3. Product/demo experience KPIs (instrumentation work required)

| KPI | Calculation | Why it matters | Proposed demo goal / caveat |
| --- | --- | --- | --- |
| Booking task completion — UI/UX | Participants who complete synthetic booking without assistance ÷ participants tested ×100 | Usability of Patient flow | ≥80% small pilot; record sample size |
| Booking conversion — Product | Confirmed bookings ÷ eligible synthetic service/slot selections ×100 | Funnel drop-off | No target until event instrumentation and defined denominator |
| Pharmacy quotation completion — Product | Fictional prescription requests that reach a quote ÷ valid assigned requests ×100 | Pharmacy coordination | Report baseline only |
| Time to complete flow — UI/UX | Median time from start of synthetic booking task to confirmation | Friction and cognitive load | Benchmark after first pilot, then improve |
| Accessibility/RTL QA — UI/UX | Passed keyboard/mobile/RTL acceptance checks ÷ executed checks ×100 | Inclusive usable UI | 100% of critical checks |
| Demo rehearsal success — Lead | Required steps completed without unsafe manual data modification ÷ required steps ×100 | Reliable university presentation | 100% of steps, with fallback recorded |

**Important:** an empty dashboard, uninstrumented system, or one developer's anecdote is **not** a measured KPI. Do not invent conversion, availability, or latency percentages.

## 4. Event and instrumentation proposal (not necessarily implemented)
Use *aggregate, non-PII* technical events or test records to measure:
- `service_search_performed`, `slot_selected`, `booking_created`, `booking_completed`
- `prescription_demo_uploaded`, `pharmacy_quote_created`, `order_confirmed`, `order_delivered_status`
- `api_request` tags for sanitized method, route template, status class and duration (do not log tokens, query values, prescription content)
- `ci_pipeline_completed` and `test_scenario_completed` with commit + environment.

Avoid saving raw user emails, full address, file content, JWT or OTP. Product events above are **proposed instrumentation names**, not endpoint names or existing analytics.

## 5. Weekly KPI review (fillable, no invented observations)

| Reporting field | Entry |
| --- | --- |
| Week ending | [date] |
| Repo / deployed revision | [commit SHA] / [verified deployment SHA or Unknown] |
| In-scope P0 issues | [count] |
| Accepted P0 issues (evidence links) | [count and URLs] |
| API contract parity | [actual / expected / percentage OR Not measured] |
| Required CI | [runs and result] |
| Critical E2E flows | [passed / executed + environment] |
| Security tests | [passed / executed + suite] |
| Outstanding blocker | [owner / issue / resolution checkpoint] |
| Reviewer sign-off | [name / date / evidence] |

## 6. Acceptance and limitations
- Agree metric definitions and proposed targets before recording project success.
- Use the *same* test environment and commit SHA for comparisons.
- Track live data separately from CI smoke data and simulated UI tests.
- Capture reproducible source logs and issue/PR references for any claimed pass.
- KPI measurement work may require instrumentation and a dedicated test cohort; those are future implementation tasks, not implicitly delivered by this document.

Status: **KPI definitions + templates prepared**, measurement and targets pending team approval.
