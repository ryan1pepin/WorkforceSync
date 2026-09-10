# WorkforceSync

An end-to-end **HR data integration platform** — a working demonstration of the
integration patterns used in enterprise HR technology (Oracle HCM Cloud ↔ enterprise
systems): ATOM feed ingestion, data transformation & validation, message queuing,
JWT-secured REST APIs, and a live Angular dashboard.

Built with C# and Angular to work through the integration patterns I use in
production — ATOM feeds, OAuth/JWT, message queuing, and middleware — end to end.

## Demo

A ~50-second walkthrough: sign in, watch the live feed land events, drill into an
employee's change history, and see the pipeline reject a bad one.

<p align="center">
  <img src="docs/screenshots/dashboard.png" alt="WorkforceSync dashboard" width="100%">
</p>

<video src="docs/WorkforceSync-Demo.mp4" controls width="100%"></video>

## What it does

A mock **Oracle HCM Cloud** publishes an ATOM feed of workforce events — hires,
promotions, comp changes, terminations. A .NET service polls the feed, transforms and
validates each payload, queues it through a bounded `Channel<T>`, and applies it
idempotently. A JWT-secured REST API exposes the data, and an Angular dashboard shows
it live.

The feed is *not* a fixed script — it's a weighted-random stream that keeps evolving
(people get promoted, paid more, leave, and new people join), so the pipeline is
exercised against a realistic, unbounded workload. It also occasionally emits a
**stale event** (a comp or role change for someone who already left), which the
processor correctly **rejects** — exactly the kind of late/duplicated message real
feeds produce.

### The dashboard

- **Live KPIs** — pipeline health, headcount, active/terminated, departments.
- **Employee table** — auto-refreshes as the feed publishes; search + per-column
  filters (department, status); click any row for its **change history**.
- **Change history** — a per-employee audit trail showing each field's old → new value
  (hire → promotion → comp), so you can see *how* a record evolved.
- **Integration audit log** — every event the pipeline applied *or rejected*, with
  status, type, and a human-readable description.

<p align="center">
  <img src="docs/screenshots/change-history.png" alt="Per-employee change history" width="100%">
</p>

<p align="center">
  <img src="docs/screenshots/audit.png" alt="Integration audit log" width="100%">
</p>

## Architecture

```
┌─────────────────┐   ATOM feed    ┌──────────────────────────┐
│  HcmSource      │ ─────────────► │  WorkforceSync.Api       │
│  (mock Oracle   │  hire/term/    │  ┌────────────────────┐  │
│   HCM Cloud)    │  position/comp │  │ Background poller  │  │
└─────────────────┘   events       │  └─────────┬──────────┘  │
                                   │            ▼             │
                                   │  ┌────────────────────┐  │
                                   │  │ Channel<T> queue   │  │
                                   │  └─────────┬──────────┘  │
                                   │            ▼             │
                                   │  ┌────────────────────┐  │
                                   │  │ Transform +        │  │
                                   │  │ validate (Core)    │  │
                                   │  └─────────┬──────────┘  │
                                   │            ▼             │
                                   │  ┌────────────────────┐  │
                                   │  │ EF Core + SQLite   │  │
                                   │  └─────────┬──────────┘  │
                                   │                          │
                                   │  JWT auth (access +      │
                                   │  refresh rotation)       │
                                   └────────────┬─────────────┘
                                                │ REST + OpenAPI
                                                ▼
                                   ┌──────────────────────────┐
                                   │  Angular 20 dashboard    │
                                   │  (signals, guards,       │
                                   │   interceptors)          │
                                   └──────────────────────────┘
```

## Projects

| Project | Purpose |
|---|---|
| `WorkforceSync.Core` | Domain models, ATOM→entity transformation, validation. Framework-agnostic, heavily unit-tested. |
| `WorkforceSync.Api` | ASP.NET Core Web API: JWT auth, workforce endpoints, background ingestion, `Channel<T>` queue, EF Core + SQLite, OpenAPI. |
| `WorkforceSync.HcmSource` | Mock Oracle HCM Cloud: emits an ATOM feed of hire/terminate/position/comp events. |
| `WorkforceSync.Core.Tests` | xUnit unit + integration tests for Core (and API via WebApplicationFactory). |
| `frontend/` | Angular 20 dashboard (standalone, signals, Tailwind). |

## Quick start

```bash
# Backend
dotnet run --project WorkforceSync.Api
# API: http://localhost:5140  (Swagger: /swagger)

# Frontend
cd frontend && npm install && npm start
# App: http://localhost:4200
```

Sign in with the demo account (`demo@corp.example` / `Demo123!`) — it's pre-filled on
the login screen. The mock HCM publishes a new workforce event every few seconds; watch
the dashboard update live.

## Tests

```bash
dotnet test          # C# unit + integration tests
cd frontend && npm test   # Angular (Karma/Jasmine)
```

## Conventions

See `AGENTS.md` for the code conventions used across the repo.
