# AI-Assisted Return Processing

AI-Assisted Return Processing is a public architecture portfolio demo showing how an application can keep natural-language interpretation separate from trusted verification and business decisions.

The current implementation is **Stage AI-1**: a .NET 10 API, isolated fictional demo sessions backed by SQLite, a deterministic S01 order/return fixture, derived return availability, and a Vue 3 shell with an honest empty AI Execution Inspector. It does not interpret messages, call an AI provider, create new return cases, or produce AI execution traces yet.

## Prerequisites

- .NET SDK 10.0 or later within the .NET 10 release line
- Node.js 22 and npm 10

No API key, separately installed database server, paid service, or cloud account is required.

## Local start

Start the API from the repository root:

```powershell
dotnet run --project src/ReturnFlow.Api
```

The API listens on `http://localhost:5080` by default, applies the versioned SQLite schema, and creates `./data/returnflow.db` on first start. The database and its WAL sidecars are ignored by Git. Check the API with:

```powershell
Invoke-RestMethod http://localhost:5080/health
```

Create an isolated S01 session and inspect its derived order summary:

```powershell
$session = Invoke-RestMethod `
  -Method Post `
  -Uri http://localhost:5080/api/demo/sessions `
  -ContentType 'application/json' `
  -Body '{"scenarioId":"S01"}'

$session.order.items
Invoke-RestMethod "http://localhost:5080/api/demo/sessions/$($session.sessionId)/return-cases"
Invoke-RestMethod -Method Post "http://localhost:5080/api/demo/sessions/$($session.sessionId)/reset"
```

S01 reports three purchased lamps, one active allocated unit, zero completed consumed units, and two available units. Session IDs are opaque; every order/case read and reset is scoped to its session. Sessions expire after 120 minutes by default (`Demo:SessionLifetimeMinutes`).

In a second terminal, install and start the frontend:

```powershell
Set-Location web
npm install
npm run dev
```

Vite prints the local frontend URL, normally `http://localhost:5173`.

## Checks

```powershell
dotnet build ReturnFlow.slnx
dotnet test ReturnFlow.slnx --no-build
Set-Location web
npm run lint
npm run test
npm run build
```

`npm run build` includes TypeScript checking. The frontend's optional `VITE_API_BASE_URL` is documented in [`.env.example`](.env.example); the inactive AI-1 shell does not call the API yet.

## Repository structure

- `src/ReturnFlow.Api` — ASP.NET Core HTTP boundary, `/health`, and session-scoped demo read/reset endpoints
- `src/ReturnFlow.Application` — session lifecycle service, persistence boundary, and safe API contracts
- `src/ReturnFlow.Domain` — fictional order/return entities and deterministic capacity semantics
- `src/ReturnFlow.Infrastructure` — EF Core SQLite model, migration, S01 seed, and derived queries
- `tests` — unit tests plus real temporary-file SQLite integration tests
- `web` — Vue 3, TypeScript, Vite, and Tailwind shell
- `docs` — implementation plan, architecture decisions, business rules, and stage evidence
- `scenarios` — scenario catalog notes; Replay model-output fixtures are still future scope

## Scope and safety

All demo data is fictional. AI-1 stores only session-local seeded fixture data and exposes no command for creating a new return. Replay interpretation, AI verification/traces, atomic return-case commits, optional live-model integration, and deployment belong to later reviewed stages. The UI never labels an operation as proposed, accepted, committed, or completed.
