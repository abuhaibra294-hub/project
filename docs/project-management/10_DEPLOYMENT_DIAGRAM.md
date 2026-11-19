# DareCare — Deployment Diagrams | مخططات النشر

**Scope:** diagrams of the environment represented by existing source/configuration versus a proposed persistent staging environment. These are editable Mermaid diagrams rendered directly in GitHub; neither diagram is proof of a live deployment.

## A. Existing Render Free demonstration topology

```mermaid
flowchart TB
  subgraph CLIENTS["Clients (synthetic demo only)"]
    BROWSER["Web browser"]
    MOBILE["Flutter client (integration target)"]
  end

  EDGE["Render HTTPS ingress"]

  subgraph FREE["Render Free: demo configuration"]
    API["DareCare.Api / ASP.NET Core .NET 10"]
    WEB["DareCare Lite + Admin UI (same origin, Staging)"]
    SQLITEDB[("Disposable SQLite at /tmp/darecare-demo")]
    FILES[("Disposable prescription files at /tmp/darecare-demo/prescriptions")]
    API --- WEB
    API --> SQLITEDB
    API --> FILES
  end

  BROWSER -->|HTTPS| EDGE
  MOBILE -.->|HTTPS if integrated| EDGE
  EDGE --> API
```

**Source:** `Dockerfile`, `render.yaml`, `src/DareCare.Api/appsettings.Staging.json`, `docs/DARECARE_LITE_FRONTEND_AR.md`. SQLite and prescription uploads are temporary; restart/redeploy can remove them. No live integration success is inferred by the dashed Flutter edge.

## B. Proposed durable SQL Server staging (not verified active)

```mermaid
flowchart TB
  subgraph CLIENTS["Clients"]
    F["Flutter Patient App"]
    W["External Web Nurse / Pharmacy"]
    A["Admin browser"]
  end

  EDGE["HTTPS ingress: Render Starter or Caddy"]
  subgraph APPHOST["Proposed durable staging application host"]
    API["DareCare.Api (.NET 10)"]
    STATIC["Optional same-origin DareCare Lite / Admin assets"]
    STORAGE[("Persistent private prescription storage")]
    KEYS[("Persistent protected Data Protection keys")]
    API --- STATIC
    API -->|authorized file access| STORAGE
    API -->|protected key ring| KEYS
  end

  DB[("External SQL Server - durable database")]
  SMTP["SMTP provider for OTP/invitations"]
  GOOGLE["Google Identity verification"]
  BACKUP["Encrypted backup + restore process"]
  CI["GitHub Actions build/test + reviewed manual migrations"]

  F -->|HTTPS REST + bearer| EDGE
  W -->|HTTPS REST + exact CORS origin| EDGE
  A -->|HTTPS Admin / bearer| EDGE
  EDGE --> API
  API -->|TLS / EF Core| DB
  API -->|email| SMTP
  API -->|validate patient identity token| GOOGLE
  DB -.-> BACKUP
  STORAGE -.-> BACKUP
  KEYS -.-> BACKUP
  CI -.->|approved release| API
  CI -.->|reviewed schema migration| DB
```

Notes:
- The existing `deployment/render-sqlserver-staging.yaml` configures SQL Server and a disk but **does not yet declare the Data Protection key directory**. It must be configured/mounted persistently before MFA is considered restart-safe.
- VPS alternative uses `deployment/compose.staging.yml` + `deployment/Caddyfile`; volumes and reverse proxy differ, while the logical design stays the same.
- SQL Server is **external** to the Render web service in the proposed Render blueprint; not hosted inside a temporary Docker container alongside the API.
- File storage and relational DB require consistent backup/restore. "Delivered" is an order state, not a third-party courier integration.
- Some external Web/Flutter clients are handoff deliverables, not confirmed deployed applications.

## C. Release-to-staging sequence

```mermaid
sequenceDiagram
  participant Dev as Backend/Integration owner
  participant CI as GitHub Actions
  participant DB as External SQL Server
  participant Host as Staging API
  participant QA as Flutter/Web/Cyber QA

  Dev->>CI: Open PR / submit candidate commit
  CI->>CI: Build, unit, contract and SQL tests
  CI-->>Dev: CI evidence
  Dev->>DB: Review backup and apply approved EF migration
  DB-->>Dev: Migration outcome
  Dev->>Host: Deploy approved app revision + secrets/volume
  Host-->>Dev: Health/readiness and revision evidence
  Dev->>QA: Publish confirmed base URL and live OpenAPI
  QA->>Host: Authenticated synthetic role flows
  Host-->>QA: Verified HTTP results
  QA-->>Dev: QA evidence or blockers
  Dev->>Dev: Approve release or rollback
```

## What is tested vs proposed
| Element | Evidence in repository | External verification pending |
| --- | --- | --- |
| Render Free Docker/SQLite | Blueprint + CI container tests | Current public SHA and new routes |
| SQL Server schema and CI persistence | Committed EF migration + SQL CI | Dedicated durable staging database |
| Render Starter private disk | Config in YAML | Live private mount and backup/restore |
| Data Protection key persistence | Supported in Program.cs | Configured secure mounted directory and restart test |
| Web/Admin same origin | Source + local/CI tests | Actual public role-based browsing |
| Flutter and separate web portals | API starter/handoff docs | Same-backend HTTPS client E2E |

Related docs: `03_BACKEND_ARCHITECTURE.md`, `09_DEPLOYMENT_STRATEGY.md`, `docs/SPRINT8_SQLSERVER_AR.md`. This page fulfills the **diagram-documentation deliverable**, not the infrastructure rollout.
