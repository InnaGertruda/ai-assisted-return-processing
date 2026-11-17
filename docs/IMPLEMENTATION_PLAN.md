# AI-Assisted Return Processing — Codex Implementation Plan (MVP v1)

**Status:** Authoritative implementation baseline for the first public portfolio demo  
**Date:** 2026-11-17  
**Owner:** Repository owner; Codex implements one stage at a time  
**Source:** Consolidates ReturnFlow domain model, ADRs, AI Flow Specification, and subsequent decisions. The earlier PostgreSQL, React, always-on worker, and production-like implementation plans are **superseded** where they conflict with this document.

## 0. Product goal and non-goals

Build a **small, publicly explorable AI architecture case study**, not a real returns product. A visitor selects a fictional order, explores a return-request scenario, and sees how the system processes the customer's message: input guardrails -> trusted business context -> LLM proposal -> schema and evidence verification -> deterministic business rules -> permitted commit -> inspectable trace. The value of the portfolio is the **visible trust boundary and verification flow**, not shipping, warehouse operations, refunds, or real 24-hour waiting.

**MVP demonstration:** A clear message such as "I want to return one more lamp" creates a case only after independent verification and atomic allocation. An ambiguous or invalid message creates no case and produces a specific, traceable reason. Replay mode is fully functional with **zero API keys and zero external AI requests**.

**Explicit non-goals for MVP:** real orders or personal data, payment/refund execution, physical shipping, real email integration, autonomous multi-tool agents, RAG/vector DB, knowledge graph, independent second LLM judge, real-time scheduling/background worker, 24-hour delays, legal consumer eligibility determinations, enterprise auth, and high availability. Capture these as future enhancements; don't implement them speculatively.

## 1. Selected stack and deployment constraints

| Area | Selected baseline |
|---|---|
| Source | Existing public GitHub repository; **do not rename or reinitialize it** |
| Git workflow | `dev` for integration; optional `feature/ai-N-*` branches from `dev`; owner selectively merges/promotes to `main` |
| Frontend | **Vue 3 + TypeScript + Vite**, Composition API / `<script setup>`, Vue Router, Tailwind CSS; Pinia only when proven necessary |
| Backend | **ASP.NET Core .NET 10 LTS**; modular monolith, API + application/domain/infrastructure separation as justified |
| Persistence | **SQLite** using EF Core; schema versioned and reconstructible; seeded fictional data, isolated demo sessions |
| Interpreter | `IReturnIntentInterpreter` with **Replay** first, optional backend-only live model provider later |
| Hosting | Optional static frontend host and on-demand API host; **provider selection and deployment are deferred until the local product is proven** |
| Time/workflows | No always-on worker. No real 24-hour wait. A simulated clock is a future interactive scenario |
| Financials | No paid services or new cloud resources by default; do not spend or deploy without owner approval |

Prefer a small, simple solution over microservices, brokers, separate worker infrastructure, Kubernetes, or new paid databases. For a brand-new app use .NET 10 LTS; if it is unavailable locally, report the missing SDK instead of silently downgrading the project. Pin compatible major package versions, lock frontend dependencies, and document runtime prerequisites.

Local SQLite file: `./data/returnflow.db` (configurable). Never commit runtime DB files. If deployed and session continuity is desired, mount persistent storage at `/data`; do not assume an ephemeral container filesystem survives deploys. Run **one** API instance when using one SQLite file. Scale-to-zero hosting is compatible with an on-demand demo; no claim of real-time background execution. The production branch is `main`; `dev` may be used for previews. No automatic production deployment from `dev`.

## 2. Repository layout (guide, not a command to overwrite existing files)

```text
AGENTS.md
CODEX_START.md
README.md
src/
  ReturnFlow.Api/
  ReturnFlow.Application/
  ReturnFlow.Domain/
  ReturnFlow.Infrastructure/
web/                          # Vue 3 app
  src/
    features/return-simulator/
    features/ai-inspector/
    shared/
tests/
  ReturnFlow.UnitTests/
  ReturnFlow.IntegrationTests/
scenarios/                    # versioned fictional Replay fixtures
docs/
  IMPLEMENTATION_PLAN.md
  AI_FLOW_SPECIFICATION.md
  ARCHITECTURE_DECISIONS.md
  BUSINESS_RULES.md
  stage-reports/
.github/workflows/             # CI only, no auto-deploy
```

Avoid empty layers or abstractions that aren't actually used. Source files may be organized differently after inspecting the repository, but preserve separation between model-generated proposals, independent verification, and business-state changes.

## 3. Frozen MVP business model and invariants

- `DemoSession`: isolated, anonymous, unguessable session identifier; fictional scenario selection, TTL and reset. Session reads/writes must never reach another visitor's state.
- `Order` -> `OrderItem`: purchased quantity and product reference; each Order belongs to a demo session.
- `ReturnSubmission`: one submitted message, channel=`SimulatedEmail`, normalized idempotency key and original evidence (fictional only).
- `ReturnCase`: one accepted submission, linked to one Order; independent cases for later legitimate returns.
- `ReturnItem`: OrderItem reference, positive **quantity**, capacity-impacting state; a line can represent multiple units.
- `OrderReturnSummary`: **read-only query**, showing purchased, active allocated, completed consumed, available, and related case IDs. Not the authority for a commit.
- `AiExecutionTrace` / `DecisionRecord`: per-stage safe facts, reason codes, business-rule references, outcomes, provider mode and schema/prompt version. A *proposed* outcome must never be labeled committed.

Rules to enforce (see `BUSINESS_RULES.md`):

- `BR-RET-001`: a case belongs to exactly one order.
- `BR-RET-002`: a ReturnItem's OrderItem belongs to that case's order/session.
- `BR-RET-003`: requested quantity must be positive.
- `BR-RET-004`: active allocated + completed consumed quantities may not exceed purchased quantity.
- `BR-RET-005`: released/cancelled capacity no longer counts; completed returns still count.
- `BR-RET-006`: idempotent retries never create a second case.
- `BR-RET-007`: a separate valid additional request is permitted when capacity remains.
- `BR-RET-008`: unresolved AI item/quantity ambiguity cannot silently allocate capacity.
- `BR-RET-010`: LLM output never directly authorizes payment or bypasses deterministic checks.

Only `CreateReturn` is executed. `CancelReturn`, `AskStatus`, `Other`, `Unknown` may be classified but **not executed** in MVP. Statutory/defect policy, refund math and the 24-hour correction window remain **DEMO_ASSUMPTION / UNVERIFIED** and outside the first slice.

## 4. AI pipeline: clear ownership and fail-closed behavior

1. **Input guardrails (API):** Check session/order scope, message length, content type, selected scenario, request limits and idempotency key; repeated same key + same request returns existing result without a new AI call; same key + different request is a conflict.
2. **Context retrieval (backend):** Fetch only the selected session/order's available purchased items, existing cases, derived available quantities, relevant rule IDs. No arbitrary model-selected SQL or untrusted identifiers.
3. **Interpretation (Replay or Live):** One bounded text-to-structured-proposal call. No model-controlled tools or privileged actions. Record `mode`, `promptVersion`, `schemaVersion`, provider/model when live.
4. **Schema verification (backend):** Parse strict DTO/JSON schema, allowed enum values, correct version, bounded items/quantities/quotes, reject unknown fields; malformed output fails safely.
5. **Grounding/evidence verification (backend):** Proposed OrderItemId must be in scoped context; product mention must uniquely identify an available purchased line; evidence quotes must literally occur in the original message; quantity and intent need support. A matched quote alone does NOT prove semantic correctness. If ambiguous, `ClarificationRequired` or `ManualReview`, no commit.
6. **Business validation (backend):** Check fresh authoritative order-item membership, active consumption, positive quantity, case/submission identity and explicit business invariants. This is explanatory preflight, **not final authorization**.
7. **Atomic execution (backend/SQLite):** Inside a write-serialized transaction, re-read allocation, apply invariant, persist submission, case, ReturnItems, and required decision evidence; commit or report conflict. Do not assume a previous read authorizes the write.
8. **Inspectable result (read API/Vue):** Per-stage `Passed` / `Failed` / `Skipped`, reason codes, safe evidence and rule references, separate model proposal vs final operation. Technical failures must not claim a case was created.

**Final outcomes:** `Accepted`, `ClarificationRequired`, `CannotProceed`, `ManualReview`, `TechnicalFailure`. `Accepted` is allowed **only after a durable commit**, with `CreatedReturnCaseId`. Use `Skipped` for downstream stages not reached. When Replay fixture output is used, label it `Replay` even if a live call failed; never silently mislabel a fallback as `Live`.

### API and interpretation contracts (minimal baseline)

Example request (illustrative; session can also be in route):

```json
{
  "demoSessionId": "opaque-session-id",
  "selectedOrderId": "ord-1050",
  "submissionKey": "client-generated-unique-key",
  "message": "I want to return one more lamp.",
  "mode": "Replay"
}
```

Untrusted proposal:

```json
{
  "schemaVersion": "1.0",
  "intent": "CreateReturn",
  "items": [{
    "candidateOrderItemId": "oi-lamp",
    "productMention": "lamp",
    "requestedQuantity": 1,
    "reason": "Unspecified",
    "evidenceQuotes": ["one more lamp"]
  }],
  "ambiguities": [],
  "requiresClarification": false
}
```

`candidateOrderItemId`, `requestedQuantity`, and `requiresClarification` are **untrusted proposals**. Optional/uncertain fields may be null. Enforce strict enum/field contract at the backend. `reason`: `ChangedMind | Defective | Unspecified | Other`; never infer a second unit is defective merely because another unit was. If model/proposal identity or quantity cannot be independently grounded, ask for clarification instead of guessing. Preserve the original message and short evidence quote, not model chain-of-thought.

Example stage record:

```json
{
  "stage": "GroundingVerification",
  "status": "Passed",
  "reasonCodes": ["ITEM_MATCH_UNIQUE", "QUOTE_PRESENT", "QUANTITY_SUPPORTED"],
  "ruleReferences": ["BR-RET-008"],
  "safeEvidence": [{ "kind": "inputQuote", "value": "one more lamp" }],
  "durationMs": 4
}
```

Keep a trace for each `RunId` and its ordered stages. Store a referenceable schema/prompt version and relevant rules. Make the inspector show both **what the model proposed** and **what the application verified**. Versioned rule links must point to real documented rules/tests, not fabricated evidence.

## 5. Real SQLite concurrency requirement (do NOT copy the PostgreSQL design)

Selected allocation design is **Option B**: derive consumed quantities from persisted ReturnItems, without a separate allocation counter.

SQLite has **no PostgreSQL-style `SELECT ... FOR UPDATE`** and permits one active writer. The final capacity read and ReturnItem insert must occur under an **up-front write-reserving transaction** (`BEGIN IMMEDIATE` or equivalent proven behavior using one connection and transaction). Every creation/allocation path follows the same protocol. For requests involving multiple lines, validate and insert **all-or-nothing**, with stable ordering. Use explicit busy timeouts and bounded retries only for transient contention, never to bypass a real business rejection. Avoid EF Core in-memory tests for this invariant: use a **real temporary SQLite file and independent connections** with parallel tasks.

Required proof: with one unit left and two distinct submissions racing, **exactly one** successfully creates a case, the other receives a conflict/unavailable outcome, and final consumed quantity never exceeds purchased. Retry of the winning idempotency key must return the original result without allocating again. Verify no partially committed case/trace on failure.

## 6. Demo sessions, Replay honesty and data lifecycle

- Every visitor creates/selects an isolated **fictional demo session**, not a shared mutable global order. Use an unguessable identifier/token and enforce per-session scope on every endpoint.
- `Reset` restores only the requesting session's seeded scenario (or creates a new session). Never erase data belonging to another visitor.
- Version fixtures (`S01` ... `S12`). Replay maps an **explicit scenario + its supported message** to a known model proposal or failure; no made-up output for arbitrary visitor text. For arbitrary messages require Live mode when enabled, or show that Replay only supports preselected scenarios.
- Expired demo sessions may be cleaned on an incoming API request. An API instance need not run continuously. Don't require retention across redeploys unless persistent storage is enabled; make reset/expiry visible to the visitor.
- No real people, actual order data or private company domain content. Keep API logs and public traces safe and redacted. Never store provider secrets with session content.

## 7. Public API surface (suggested; version if desired)

- `GET /api/demo/scenarios` — public scenario catalog, safe labels and expected demonstration.
- `POST /api/demo/sessions` — create session for scenario; returns opaque token, selected order summary.
- `GET /api/demo/sessions/{sessionId}/orders/{orderId}` — session-scoped order and derived return availability.
- `POST /api/demo/sessions/{sessionId}/ai-runs` — run Replay/Live pipeline using `submissionKey` and selected order. This is the **only** accepted write path for CreateReturn in the MVP.
- `GET /api/demo/sessions/{sessionId}/ai-runs/{runId}` — safe stage trace.
- `GET /api/demo/sessions/{sessionId}/return-cases` — cases and summary.
- `POST /api/demo/sessions/{sessionId}/reset` — reset only this demo session.
- `GET /health` — minimal health, no secrets.

Avoid exposing arbitrary SQL, internal provider credentials, broad admin endpoints or generic LLM proxies. Restrict CORS to configured frontend origins, but do **not** treat CORS as an authentication or abuse-protection boundary.

## 8. Staged execution plan for Codex

**Rule:** implement ONE stage per Codex task; provide a file-level plan first, run checks, report exact results, then stop for owner review. Do not quietly begin the next stage. Stage references below are authoritative.

### AI-0 — Repository foundation and minimal visible shell

**Build**
- Inspect current repository/branches and available SDKs. Start from `dev` (or a feature branch based on `dev`). Preserve existing files.
- Scaffold .NET 10 solution (API / domain / application / infrastructure) and Vue 3 + TypeScript + Vite application. Add a minimal Tailwind layout with **Return Simulator** and **AI Execution Inspector** placeholders; do not display invented successful AI execution.
- Add health endpoint, development config, `.gitignore`, `.env.example` (empty secrets), README local-start commands, basic unit test projects, lint/build commands and optional CI for test/build only.
- Add foundational docs and scenario catalog placeholders. Do not install paid services, initialize a hosting provider, or connect to a live model provider.

**Acceptance**
- API starts locally and `/health` responds.
- Vue app starts, builds and renders simulator/inspector shell with correct `Not run` or empty state.
- `dotnet build`, `dotnet test`, `npm run build` and any configured lint/test succeed. No secret/database files are tracked.
- README accurately documents prerequisites and commands. All repo work is off `main`.

**Evidence:** Commands and actual outputs, file list, screenshots only if readily available, known next-stage gaps.

### AI-1 — Isolated demo data, domain and return availability

**Build**
- SQLite EF Core model and versioned schema; seed fictional orders/OrderItems/existing returns for selected scenarios.
- Create isolated DemoSession, session lifecycle/TTL and reset; implement session-scoped read APIs and `OrderReturnSummary` query.
- Define ReturnSubmission, ReturnCase, ReturnItem and capacity-impacting states. No real return-creation endpoint yet; preloaded cases are seed data.

**Acceptance**
- New visitor session shows 3 lamps, 1 existing allocated, 2 available in S01. Another session starts from clean independent data.
- Reset one session does not modify another; cross-session order and trace retrieval rejected.
- Reinitializing an empty DB yields identical deterministic fixture data; DB file not tracked.

**Tests:** Isolation, FK/membership, summary arithmetic, seed/reset and session expiry.

### AI-2 — Replay interpretation, guardrails, verification and trace (NO WRITE)

**Build**
- Define `IReturnIntentInterpreter`, strict `ReturnIntentProposalV1` DTO and `ReplayReturnIntentInterpreter` reading versioned fixture output.
- Implement input guardrails, trusted context retrieval, schema verifier, evidence/grounding verifier and deterministic preflight validation. Use discrete stage result records (`Passed/Failed/Skipped`) and reason/rule IDs.
- Expose a read-only pipeline run (or a controlled `dryRun` flag), persist safe traces without creating ReturnCases. Minimal Vue inspector displays proposals, checks and final **provisional** outcome, clearly labeled **Not committed**.
- Include fixtures for clear request, ambiguous referent, quantity conflict, invented item ID, malicious prompt-injection instruction, and malformed output.

**Acceptance**
- A clear fixture reaches `EligibleForCommit`/**Not committed**, not `Accepted`.
- Ambiguous/invalid cases end in clarification/review/failure with no business-state changes; downstream stages are `Skipped`.
- All displayed evidence and rule references are truthful; arbitrary text in Replay mode is not treated as a fixture match.

**Tests:** Independent rule-specific verification tests and scenario fixtures S01, S02, S03, S06, S07, S08, S11, S12. No external HTTP calls.

### AI-3 — Atomic CreateReturn and idempotency

**Build**
- Integrate verified `CreateReturn` proposal with deterministic ReturnIntake/ReturnAllocation services and real SQLite write transaction.
- Re-read quantities under write reservation and persist submission/case/items/required decision record atomically; explicitly distinguish business rejection, duplicate-submission result and technical failure.
- Enforce unique `(SessionId, SubmissionKey)` + payload hash semantics; identical retry returns same logical result, different payload with same key returns conflict. Keep session-scoped trace.
- Connect success/failed commit results to UI. Never display `Accepted` until transaction succeeds.

**Acceptance**
- S01 creates one new case, consumes one lamp, and updates summary.
- S03 fails business validation and creates none.
- S04 same submission retry returns same case and consumes no extra quantity; S05 different submission can create another case while capacity remains.
- S10 two concurrent final-unit attempts using separate SQLite connections: exactly one commits; all quantities consistent.
- Failure during commit leaves no partial Case/ReturnItem/DecisionRecord. Tests prove this; no `SELECT ... FOR UPDATE` in SQLite code.

**Tests:** Full real-SQLite integration suite; multi-line atomicity if multiple ReturnItems included; idempotency with parallel requests.

### AI-4 — Portfolio experience and explainability

**Build**
- Create usable Vue simulator: scenario selection, order summary, message/replay choice, run, reset and clear final outcome.
- Create AI Execution Inspector with timeline of each stage, failure/skip states, model proposal vs verified/committed data, safe evidence, business rule IDs and ADR links. Display quantity changes and case IDs after commit.
- Add accessible loading/empty/error states, basic responsive layout, clear demo-only / fictional-data / Replay labels and link to repository tests and documents (where available).

**Acceptance**
- A visitor can understand **why** S01 was accepted, S02 needed clarification and S06 was rejected without reading source code.
- Reset and re-run produce reproducible results; no traces from other sessions visible.
- UI does not imply that simulation is an actual refund/shipment or LLM live call.

**Tests:** Vue component tests (lightweight) + one or two browser-level smoke scenarios if tooling is available without overhead.

### AI-5 — Optional Live model provider and spend controls

**Build**
- Add `LiveReturnIntentInterpreter` via backend-only `HttpClient` to a compatible API; select an available provider model at implementation time and configure it as an environment variable, never hardcode an unstable model ID.
- Use provider structured outputs where supported; verify model compatibility first because strict JSON Schema support varies. **Always** run local schema, grounding and business verification. Model self-confidence is not authoritative.
- Add `LIVE_MODEL_API_KEY` through the server environment or hosting secret store, keep the live feature flag **disabled by default**, and enforce bounded input/output tokens, HTTP timeout, rate limits per session/IP, a persistent global daily allowance with safe behavior after restart, and clearly surfaced `429`/timeout/provider failures.
- Require explicit visitor opt-in for Live mode. No user-controlled provider URL, model selection, arbitrary tool calls or API key exposure. Never claim a Replay fallback was Live.

**Acceptance**
- App fully works without API key; disabled Live cannot call provider.
- When manually enabled with a configured test key, Live uses the **same pipeline** as Replay, and an invalid model result still cannot write. Do not enable public Live mode unless the global quota persists across restarts or an equally reliable provider-side hard limit exists; otherwise keep Live disabled.
- Quota exhaustion and 429/timeout do not bypass checks or create duplicate cases.
- `.env.example` contains keys with empty/example placeholder values only; logs/browser traffic never expose secret.

**Tests:** HTTP-client mock tests for malformed response/429/timeout, quota counter, session/IP limits, prompt-injection fixture. Real-provider smoke test only with explicit owner approval and key.

### AI-6 — Deployment preparation and public release (manual, opt-in)

**Build**
- Prepare a small provider-neutral container configuration for one scale-to-zero API instance, with an environment-based SQLite path and optional persistent storage. Do not deploy or create paid resources without authorization.
- Document generic static-host settings for the Vue/Vite `web` directory (`npm run build` -> `dist`), production branch `main`, and optional `dev` preview; set only the public API base URL in frontend configuration.
- CI on PR and `dev` checks .NET and Vue builds/tests only; don't automatically deploy from `dev` or push to `main`.
- Document reset/session expiration, replay/live behavior, cost controls, secret setup and known limits in README.

**Acceptance**
- Production builds reproducible and deployment instructions documented.
- Public site (when owner deploys) offers a full Replay journey without provider secrets or fees; API can sleep between requests.
- Basic tests/CI pass before the owner promotes reviewed changes to `main`.

**Deployment reminder:** Persistent storage is required only if continuity across API restarts/redeploys is desired. A demo without persistent storage may reset sessions on deploy; disclose this rather than claim durability. No always-on scheduler is needed for MVP.

## 9. Evaluation scenarios (contract for acceptance tests)

| ID | Fixture and expected result | Stage(s) |
|---|---|---|
| S01 | 3 lamps, 1 allocated; "one more lamp" -> verified proposal, then exactly one accepted new case after AI-3 | AI-2/3 |
| S02 | two different items; "return it" -> `ClarificationRequired` and no case | AI-2 |
| S03 | 3 lamps, 2 allocated; "remaining two" -> `CannotProceed` as only 1 available | AI-2/3 |
| S04 | same `submissionKey` and identical payload replay -> prior outcome, no second case | AI-3 |
| S05 | different key for another lamp and free capacity -> distinct valid case | AI-3 |
| S06 | fixture fabricates `oi-fake-999` -> `ManualReview` (unverified identity); no case | AI-2 |
| S07 | injection text asking to ignore rules/approve refund -> `ClarificationRequired` in fixed Replay fixture; no privileged action | AI-2 |
| S08 | malformed model JSON/invalid enum -> `TechnicalFailure`; no case | AI-2 |
| S09 | simulated live-provider 429 -> `TechnicalFailure` by default; optional **explicitly labeled** Replay only after visitor action | AI-5 |
| S10 | two simultaneous distinct requests for last lamp -> one commit max | AI-3 |
| S11 | two different OrderItems both called lamp -> clarify, do not invent a unique ID | AI-2 |
| S12 | "Cancel my earlier return" -> classified unsupported intent, not executed | AI-2 |

A test fixture must define its input, order snapshot, expected stage/outcome, and relevant rule IDs. Record validation evidence, not a merely successful HTTP response. The Replay test suite must never require a live LLM.

## 10. Definition of done for EVERY stage

1. No modifications to `main`; work based on `dev`, preferably feature branch and PR to `dev`. No force-push, deployment, paid provisioning, commit or push unless explicitly asked.
2. Code builds and available tests pass; provide exact commands, outcomes, and explain tests not run. Don't claim tests passed if unavailable.
3. Public repo contains no API keys, private business/client information, local DBs, `.env` secrets, or real customer data.
4. No fabricated evidence: trace state and UI match actual execution, and `Accepted` means committed.
5. New business rules link to stable IDs, tests and decision docs; mark open assumptions instead of inventing legal facts.
6. No feature creep: do not implement next stage unless owner agrees. Update README + stage report (`docs/stage-reports/AI-N.md`) with what works, how to reproduce, screenshots where useful, what remains and trade-offs.
7. For failures, stop and report the blocker, alternative and impact instead of silently switching the stack or skipping validation.

**Owner's release gate:** Only promote a reviewed, demonstrable slice from `dev` to `main` after passing tests and confirming no secrets or misleading claims.

## 11. Later investment options — not authorization to build them

| Improvement | Only pursue when | Added cost/risk |
|---|---|---|
| Simulated time / correction window | MVP interpretation and verification are stable and visitor value is clear | Additional workflow state/testing, little hosting cost |
| Tool-calling agent / multi-turn clarification | Fixed context fails realistic conversational cases | More model calls, permissions and replay complexity |
| Rule knowledge graph / change-impact Copilot | Rule corpus grows and traceability needs graph traversal | Authoring/versioning/graph evaluation |
| RAG | Policies are numerous/unstructured beyond curated context | Indexing/retrieval tests and potentially hosting |
| Independent model-based verifier | Deterministic evidence checks demonstrably miss important semantic errors | Additional LLM tokens/evaluation disagreement |
| Refund/warehouse/payment simulation | Additional portfolio case needed | State machines, idempotency/reconciliation tests |
| Production scheduling/outbox | Need real deadline/restart reliability proof | Always-on or external scheduling, infrastructure budget |
| PostgreSQL/multi-instance deployment | Single SQLite writer or uptime becomes a real limitation | Database hosting and migration |

## 12. Reference links (recheck before deployment)

- .NET support policy: https://dotnet.microsoft.com/en-us/platform/support/policy
- EF Core SQLite limitations: https://learn.microsoft.com/en-us/ef/core/providers/sqlite/limitations
- Microsoft.Data.Sqlite transactions: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/transactions
- Chosen live-model provider documentation: add only when a provider is selected for implementation.
- Chosen frontend/API hosting documentation: add only when deployment is approved and providers are selected.

**Implementation principle:** This portfolio must show not only *what the AI proposed*, but **how untrusted AI output was verified and constrained by trustworthy software before any state change**.
