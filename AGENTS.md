# AGENTS.md — WorkforceSync

Guidance for AI coding agents (Codex, Copilot, etc.) working in this repo.
This file is the source of truth for conventions.

## What this project is
An HR data integration platform: a mock Oracle HCM Cloud emits an **ATOM feed** of
workforce events (hire, terminate, position change, comp change). A .NET 10 service
polls the feed, transforms + validates payloads into normalized entities, queues them
through a `Channel<T>`, and persists via EF Core (SQLite). A JWT-secured REST API
exposes the data. An Angular 20 dashboard displays it.

This is a **portfolio/interview project** — code quality and clarity matter more than
cleverness. Every file should be explainable in an interview.

## Stack
- Backend: .NET 10 (`net10.0`), C# latest, ASP.NET Core Web API (controllers),
  EF Core + SQLite, `Microsoft.AspNetCore.Authentication.JwtBearer`, Swashbuckle.
- Frontend: Angular 20 (standalone components, signals, zone.js), Tailwind CSS v4,
  Jest + jest-preset-angular.
- Tests: xUnit (C#), Jest (Angular).

## Project layout
- `WorkforceSync.Core` — domain models, ATOM→entity transformation, validation.
  **No framework dependencies** (no ASP.NET, no EF). Pure C#. Heavily unit-tested.
- `WorkforceSync.Api` — ASP.NET Core host: controllers, DI, JWT auth, background
  ingestion service, `Channel<T>` queue, EF Core DbContext, OpenAPI.
- `WorkforceSync.HcmSource` — mock Oracle HCM: generates/serves an ATOM feed of
  workforce events.
- `WorkforceSync.Core.Tests` — xUnit tests for Core (and API integration tests via
  `WebApplicationFactory`).
- `frontend/` — Angular app (added at M4).

## Code conventions (C#)
- Nullable reference types enabled; `ImplicitUsings` enabled.
- File-scoped namespaces.
- Records for immutable DTOs / value objects; classes for entities.
- One public type per file; file name matches type name.
- Services take dependencies via constructor (primary constructors OK).
- No `async void`. No blocking calls (`.Result`, `.Wait()`) in library code.
- Exceptions: throw specific types; catch only what you can handle.
- XML doc comments on public API surface (Core especially).
- Keep Core framework-free: if a Core type needs `Microsoft.AspNetCore.*` or EF,
  it belongs in Api.

## Code conventions (Angular)
- Standalone components only; no NgModules.
- Signals for state (`signal`, `computed`, `effect`); no RxJS in stores.
- Functional HTTP interceptors (not class-based).
- Reactive forms for user input.
- Tailwind utility classes; no component-level CSS files unless truly needed.
- `@if`/`@for` control flow (not `*ngIf`/`*ngFor`).

## Testing
- Core: unit tests for every transform/validation rule, including edge cases and
  failure scenarios (malformed ATOM, missing fields, bad dates).
- Api: integration tests via `WebApplicationFactory` (auth flow, CRUD, 401/refresh).
- Angular: Jest + jest-preset-angular; `@jest/globals` imports (no `@types/jest`).
- Tests must pass before a change is considered done: `dotnet test` and
  `cd frontend && npm test`.

## Git
- Small, focused commits with imperative messages (`add ATOM feed parser`, not
  `did some stuff`).
- One logical unit per commit.
- Never commit `bin/`, `obj/`, `node_modules/`, `dist/`, `*.db`, `.env`.

## Do NOT
- Do not add real Oracle HCM SDK dependencies — the HcmSource is a mock.
- Do not introduce a second state management library in Angular.
- Do not store secrets in code; use `appsettings.json` / environment variables.
- Do not `git commit` unless the task explicitly asks for it.
