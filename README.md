# AI-Assisted Return Processing

AI-Assisted Return Processing is a public architecture portfolio demo showing how an application can keep natural-language interpretation separate from trusted verification and business decisions.

The current implementation is **Stage AI-5**: a .NET 10 API, isolated fictional demo sessions backed by SQLite, eleven exact-match Replay fixtures, strict proposal/evidence verification, authoritative transactional revalidation, idempotent Return Case creation, safe persisted traces, an interactive Vue 3 simulator/inspector, and an optional backend-only Groq adapter. Replay remains the default and makes no provider calls. Live is disabled unless the owner supplies explicit backend configuration. `Accepted` is emitted only after the submission, case, items, decision evidence, idempotency record, and trace commit atomically.

## Prerequisites

- .NET SDK 10.0 or later within the .NET 10 release line
- Node.js 22 and npm 10

No API key, separately installed database server, paid service, or cloud account is required for Replay or the automated tests.

## Local start

Start the API from the repository root:

```powershell
dotnet run --project src/ReturnFlow.Api
```

Before starting locally, use **Manage User Secrets** on the `ReturnFlow.Api` project and assign `RETURNFLOW_SESSION_LIFETIME_MINUTES` an integer from 1 through 1440. User Secret values are stored outside the repository. On a deployed server, provide the same key through a server-side environment variable or secret store. `.env.example` documents required names only and intentionally contains no configured values.

Live remains disabled when `LIVE_MODEL_ENABLED` is absent or `false`. To opt in locally, keep the secret outside the repository and configure the API project with .NET User Secrets:

```powershell
dotnet user-secrets set "LIVE_MODEL_ENABLED" "true" --project src/ReturnFlow.Api
dotnet user-secrets set "LIVE_MODEL_API_KEY" "<your-secret>" --project src/ReturnFlow.Api
dotnet user-secrets set "LIVE_MODEL_ID" "<a-current-Groq-model-with-structured-output-support>" --project src/ReturnFlow.Api
```

The model ID is deliberately configuration-only because provider availability changes. Optional timeout, output-token, per-session, per-client and global-daily limits are listed in `.env.example`. Startup fails closed when enabled configuration is missing or outside supported bounds. The API key and model ID are never returned by the capability endpoint, traces, or frontend. For hosted use, store the same backend values in the hosting secret store (for example Fly.io Secrets), not frontend variables or tracked files. Public Live access remains disabled pending owner review.

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

$run.finalOutcome        # Accepted
$run.commitStatus        # Committed
$run.createdReturnCaseId # rc-...
Invoke-RestMethod "http://localhost:5080/api/demo/sessions/$($session.sessionId)/ai-runs/$($run.runId)"
```

Replay input is intentionally exact: arbitrary wording is not presented as a fixture match. S02, S03, S06, S07, S08, S11, and S12 exercise clarification, capacity rejection, fabricated identity, prompt injection, malformed output, duplicate product names, and unsupported intent. S04, S05, and S10 demonstrate idempotent retry, independent additional returns, and two distinct requests racing for the final unit. Replay traces are labeled `Replay`; Live traces are labeled `Live` and still traverse the same schema, grounding, business and transactional checks. `Accepted` always means the transaction committed durably.

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

`npm run build` includes TypeScript checking. The frontend's optional `VITE_API_BASE_URL` is documented in [`.env.example`](.env.example); the AI-5 simulator uses it for scenario, capability, session, order, return-case, reset, and interpretation APIs. A selected scenario keeps the same isolated session across repeated runs until reset or another scenario is selected.

## Repository structure

- `src/ReturnFlow.Api` — ASP.NET Core HTTP boundary, `/health`, and session-scoped demo read/reset endpoints
- `src/ReturnFlow.Application` — session lifecycle plus strict AI proposal, verification, preflight, and trace contracts
- `src/ReturnFlow.Domain` — fictional order/return entities and deterministic capacity semantics
- `src/ReturnFlow.Infrastructure` — EF Core SQLite model/migrations, fictional scenarios, embedded Replay fixtures, backend-only Groq HTTP adapter, persistent Live quotas, scoped queries, and trace persistence
- `tests` — unit tests plus real temporary-file SQLite integration tests
- `web` — Vue 3, TypeScript, Vite, and Tailwind shell
- `docs` — implementation plan, architecture decisions, business rules, and stage evidence
- `src/ReturnFlow.Infrastructure/Ai/Fixtures` — versioned embedded Replay model-output fixtures

## Scope and safety

All demo data is fictional. Replay and Live model output remain untrusted proposals; only deterministic verification and the write-reserved SQLite transaction can authorize a Return Case. Identical retries return the original logical result before another provider call, conflicting key reuse is rejected, and capacity is re-read inside the transaction. The Live adapter performs one bounded provider request with no automatic retry. Deployment remains a later reviewed stage.
