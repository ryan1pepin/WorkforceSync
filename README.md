# WorkforceSync

An end-to-end **HR data integration platform** — a working demonstration of the
integration patterns used in enterprise HR Technology (Oracle HCM Cloud ↔ enterprise
systems): ATOM feed ingestion, data transformation & validation, message queuing,
JWT-secured REST APIs, and an Angular dashboard.

Built as a portfolio/interview project targeting an **Application, Integrations &
Extensions Developer** role (C# + Angular, HR integrations, ATOM feeds, OAuth/JWT,
middleware/queuing patterns).

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
                                   │  └────────────────────┘  │
                                   │                          │
                                   │  JWT auth (access +      │
                                   │  refresh rotation)       │
                                   └────────────┬─────────────┘
                                                │ REST + OpenAPI
                                                ▼
                                   ┌──────────────────────────┐
                                   │  Angular 20 dashboard    │
                                   │  (signals, guards,       │
                                   │   interceptors, SSR)     │
                                   └──────────────────────────┘
```

## Projects

| Project | Purpose |
|---|---|
| `WorkforceSync.Core` | Domain models, ATOM→entity transformation, validation. Framework-agnostic, heavily unit-tested. |
| `WorkforceSync.Api` | ASP.NET Core Web API: JWT auth, workforce endpoints, background ingestion, `Channel<T>` queue, EF Core + SQLite, OpenAPI. |
| `WorkforceSync.HcmSource` | Mock Oracle HCM Cloud: emits an ATOM feed of hire/terminate/position/comp events. |
| `WorkforceSync.Core.Tests` | xUnit unit + integration tests for Core (and API via WebApplicationFactory). |
| `frontend/` | Angular 20 dashboard (standalone, signals, zone.js, Tailwind, Jest). |

## Quick start

```bash
# Backend
dotnet run --project WorkforceSync.Api
# API: http://localhost:5080  (Swagger: /swagger)

# Frontend (after M4)
cd frontend && npm install && npm start
# App: http://localhost:4200
```

## Tests

```bash
dotnet test          # C# unit + integration tests
cd frontend && npm test   # Angular Jest tests
```

## Milestones

- **M0** — repo, solution structure, CI, git baseline ✅
- **M1** — C# core: ATOM parse + transform + validation + unit tests
- **M2** — C# API + JWT auth (access + refresh rotation) + EF Core
- **M3** — C# background ingestion + `Channel<T>` queue + mock HCM ATOM source
- **M4** — Angular scaffold + auth (signals store, guard, 401-refresh interceptor)
- **M5** — Angular dashboard: headcount, hires/terminations, position changes, live audit log
- **M6** — integration tests, README, architecture diagram, polish

## Conventions

See `AGENTS.md` — it is the source of truth for code style and is followed by all
AI-assisted tooling (Codex, Copilot) on this repo.
