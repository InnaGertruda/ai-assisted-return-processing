# AI-Assisted Return Processing

AI-Assisted Return Processing is a public architecture portfolio demo showing how an application can keep natural-language interpretation separate from trusted verification and business decisions.

The current implementation is **Stage AI-2**: a .NET 10 API, isolated fictional demo sessions backed by SQLite, eight exact-match Replay fixtures, strict proposal/evidence verification, deterministic return-capacity preflight, safe persisted traces, and a minimal Vue 3 execution inspector. AI-2 is dry-run only: it does not call a live provider or create a new return submission, case, or item.

## Prerequisites

- .NET SDK 10.0 or later within the .NET 10 release line
- Node.js 22 and npm 10

No API key, separately installed database server, paid service, or cloud account is required.

## Local start

Start the API from the repository root:

```powershell
dotnet run --project src/ReturnFlow.Api
```

Before starting locally, use **Manage User Secrets** on the `ReturnFlow.Api` project and assign `RETURNFLOW_SESSION_LIFETIME_MINUTES` an integer from 1 through 1440. User Secret values are stored outside the repository. On a deployed server, provide the same key through a server-side environment variable or secret store. `.env.example` documents required names only and intentionally contains no configured values.

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

S01 reports three purchased lamps, one active allocated unit, zero completed consumed units, and two available units. Session IDs are opaque; every order/case read and reset is scoped to its session. Session lifetime is controlled only by the backend environment variable `RETURNFLOW_SESSION_LIFETIME_MINUTES`; startup fails unless it is an integer from 1 through 1440.

Run the exact S01 Replay message and inspect the safe trace:

```powershell
$run = Invoke-RestMethod `
  -Method Post `
  -Uri "http://localhost:5080/api/demo/sessions/$($session.sessionId)/ai-runs" `
  -ContentType 'application/json' `
  -Body (@{
    selectedOrderId = $session.order.id
    submissionKey = [guid]::NewGuid().ToString('N')
    message = 'I want to return one more lamp.'
    mode = 'Replay'
  } | ConvertTo-Json)

$run.finalOutcome   # EligibleForCommit
$run.commitStatus   # Not committed
Invoke-RestMethod "http://localhost:5080/api/demo/sessions/$($session.sessionId)/ai-runs/$($run.runId)"
```

Replay input is intentionally exact: arbitrary wording is not presented as a fixture match. S02, S03, S06, S07, S08, S11, and S12 exercise clarification, capacity rejection, fabricated identity, prompt injection, malformed output, duplicate product names, and unsupported intent respectively. Every trace is labeled `Replay`; `Accepted` is reserved for a durable AI-3 commit.

In a second terminal, install and start the frontend:

```powershell
Set-Location web
npm install
npm run dev
```

Vite prints the local frontend URL, normally `http://localhost:5173`.
During local development Vite proxies `/api` requests to `http://localhost:5080`, so start the API before the frontend. A non-empty `VITE_API_BASE_URL` from the repository-root `.env` can override the same-origin API location for other environments.

## Checks

```powershell
dotnet build ReturnFlow.slnx
dotnet test ReturnFlow.slnx --no-build
Set-Location web
npm run lint
npm run test
npm run build
```

`npm run build` includes TypeScript checking. The frontend's optional `VITE_API_BASE_URL` is documented in [`.env.example`](.env.example); the AI-2 simulator uses it for the session and Replay-run APIs.

## Repository structure

- `src/ReturnFlow.Api` — ASP.NET Core HTTP boundary, `/health`, and session-scoped demo read/reset endpoints
- `src/ReturnFlow.Application` — session lifecycle plus strict AI proposal, verification, preflight, and trace contracts
- `src/ReturnFlow.Domain` — fictional order/return entities and deterministic capacity semantics
- `src/ReturnFlow.Infrastructure` — EF Core SQLite model, migrations, fictional scenario data, embedded Replay fixtures, scoped queries, and trace persistence
- `tests` — unit tests plus real temporary-file SQLite integration tests
- `web` — Vue 3, TypeScript, Vite, and Tailwind shell
- `docs` — implementation plan, architecture decisions, business rules, and stage evidence
- `src/ReturnFlow.Infrastructure/Ai/Fixtures` — versioned embedded Replay model-output fixtures

## Scope and safety

All demo data is fictional. Replay model output is untrusted and can only become `EligibleForCommit`; every AI-2 run remains `Not committed`. The only new persistence is a session-scoped safe trace. Atomic return-case creation, idempotency and concurrency belong to AI-3; optional Live integration and deployment remain later reviewed stages.
