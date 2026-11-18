# Stage AI-5 report

**Status:** Complete locally on `feature/ai-5-live-groq` for owner review  
**Date:** 2026-11-18

## Scope delivered

- Added a backend-only Groq Chat Completions adapter behind the existing `IReturnIntentInterpreter` boundary. It sends one bounded request to the fixed provider endpoint, requests strict JSON-schema output matching `ReturnIntentProposalV1`, exposes no tools, and performs no automatic provider retry.
- Added a mode router so Replay remains fixture-backed, no-key and independent of Groq. Live is disabled by default and must be explicitly selected by the visitor after the backend reports it enabled.
- Kept one shared application pipeline after interpretation. Live output passes the existing strict JSON parsing, unknown-field rejection, item/context grounding, evidence checks, deterministic business validation, idempotency check and authoritative SQLite allocation transaction.
- Strengthened quantity grounding for both modes: an explicit quantity in evidence must uniquely match the proposed quantity. Conflicting or absent quantity evidence cannot commit.
- Added disabled-by-default backend configuration with bounded timeout (1–30 seconds), output tokens (64–2,000), per-session minute/day limits, hashed-client minute limit and global daily limit. Enabled startup fails closed when the API key/model or numeric bounds are invalid.
- Added a versioned SQLite `LiveModelUsageRecords` table. A write-reserved transaction durably reserves each Live attempt before HTTP, so concurrent requests and process restarts cannot bypass limits while using the same database.
- Added safe provider outcomes for disabled Live, local quota rejection/unavailability, timeout, HTTP 429, authentication failure, provider unavailability, other HTTP failure and malformed response envelope. These become `TechnicalFailure` traces and never a Replay fallback or accepted interpretation.
- Added a public-safe `/api/demo/live-status` response containing only `enabled`; it never returns the API key, model ID, endpoint, quota counters or provider response details.
- Updated the Vue simulator with explicit Replay/Live selection. Live is unavailable unless the backend enables it. The selected mode is part of the immutable request retained by **Retry same request**, preserving AI-3 idempotency and preventing another provider call for an identical completed retry.

## Replay versus Live

| Concern | Replay | Live |
|---|---|---|
| Default | Enabled | Disabled |
| External request | None | One backend-only Groq request |
| Input | Exact versioned fixture message | Fictional scenario message plus scoped trusted order context |
| Proposal source | Embedded fixture JSON | Provider response content |
| Verification and commit | Shared strict pipeline | Same shared strict pipeline |
| Failure fallback | Not applicable | No automatic Replay fallback |
| Usage accounting | None | Durable session/client/global reservation before HTTP |

## Security and usage-limit behavior

- `LIVE_MODEL_API_KEY` and `LIVE_MODEL_ID` are read only by the backend. They are absent from frontend code, API responses, traces, logs and tracked configuration values.
- The provider URL is fixed in backend code; a visitor cannot choose an endpoint, model, tool or authorization header.
- Input remains capped at 2,000 characters by the pipeline. Provider output is bounded by `LIVE_MODEL_MAX_OUTPUT_TOKENS`, a 64 KiB HTTP response buffer and strict local schema validation.
- Provider calls use a configured timeout and are not automatically retried. HTTP 429 is reported as `LIVE_PROVIDER_RATE_LIMITED`.
- A quota reservation is intentionally consumed even when the provider subsequently fails. This favors predictable spend limits over maximum availability.
- Per-session and hashed-client minute windows limit bursts. Per-session and global UTC-day limits cap longer usage. The global limit is persistent for the configured SQLite file.
- Identical completed retries are returned from AI-3 idempotency storage before interpretation, quota reservation or provider HTTP.

## Verification evidence

- `GroqReturnIntentInterpreterTests`: valid structured response/request bounds, disabled mode, quota rejection, malformed envelope, timeout, 429, unavailable/authentication errors, no automatic retry and invalid configuration.
- `ReturnInterpretationPipelineTests`: valid Live proposal through shared verification/commit; malformed JSON; hallucinated OrderItem ID; missing/conflicting evidence; ambiguity; conflicting quantity; and unsupported operation rejection.
- `LiveUsageLimitTests`: real SQLite persistence across dependency scopes, session burst limit, client partition limit, global daily limit and next-day reset.
- `AiLiveApiTests`: default-disabled capability and proof that a Live attempt cannot change return availability.
- Existing Replay, session isolation, idempotency, rollback and concurrent allocation tests remain unchanged and passing.

## Commands and actual results

- `dotnet format ReturnFlow.slnx --verify-no-changes --no-restore` → passed with no diagnostics.
- `dotnet test ReturnFlow.slnx --no-restore --verbosity minimal` → 30 unit tests and 27 real-SQLite integration tests passed; 0 failed, 0 skipped.
- `dotnet build ReturnFlow.slnx --configuration Release --no-restore` → succeeded with 0 warnings and 0 errors.
- From `web`, `npm.cmd run test` → 15 passed, 0 failed.
- From `web`, `npm.cmd run lint` → passed with 0 warnings/errors.
- From `web`, `npm.cmd run build` → TypeScript checks and Vite production build passed; 18 modules transformed.
- `git diff --check` → passed; checkout line-ending notices only.
- No real Groq request, API-key validation request, deployment, paid resource or external spend occurred.

### Local Groq compatibility correction

- A local owner-run smoke request initially received HTTP 400. The configured `openai/gpt-oss-20b` model supports strict structured output, but the submitted schema included `maxItems`, which is not part of Groq's documented strict-schema subset.
- Removed provider-side `maxItems` keywords while preserving the authoritative local `MaximumItems` and `MaximumEvidenceQuotes` validation after every response. A request-shape regression test prevents the unsupported keyword from returning.
- Groq subsequently returned `json_validate_failed`, confirming request acceptance but invalid generated JSON. The prompt now explicitly requires one schema-only object and defines conservative null/ambiguity behavior; GPT-OSS uses supported low reasoning with reasoning omitted from the response. The bounded completion allowance is 1,024 tokens so reasoning cannot prematurely consume the earlier 500-token budget. Provider-side `minimum` was removed while positive quantity remains enforced locally.

## Local configuration without exposing secrets

Replay needs no Live configuration. For an explicitly approved local Live test, store values outside Git with the API project's existing .NET User Secrets support:

```powershell
dotnet user-secrets set "LIVE_MODEL_ENABLED" "true" --project src/ReturnFlow.Api
dotnet user-secrets set "LIVE_MODEL_API_KEY" "<your-secret>" --project src/ReturnFlow.Api
dotnet user-secrets set "LIVE_MODEL_ID" "<current-Groq-model-with-strict-structured-output-support>" --project src/ReturnFlow.Api
```

`LIVE_MODEL_TIMEOUT_SECONDS`, `LIVE_MODEL_MAX_OUTPUT_TOKENS`, `LIVE_MODEL_SESSION_REQUESTS_PER_MINUTE`, `LIVE_MODEL_SESSION_REQUESTS_PER_DAY`, `LIVE_MODEL_CLIENT_REQUESTS_PER_MINUTE` and `LIVE_MODEL_GLOBAL_REQUESTS_PER_DAY` are optional bounded overrides documented with non-secret defaults in `.env.example`. On Fly.io, use Fly Secrets for the backend values; do not use `VITE_` variables or tracked files for secrets.

The implementation follows Groq's official [Chat Completions API](https://console.groq.com/docs/api-reference), [Structured Outputs](https://console.groq.com/docs/structured-outputs), [error codes](https://console.groq.com/docs/errors) and [rate-limit behavior](https://console.groq.com/docs/rate-limits). A model ID is deliberately not hardcoded because provider availability and structured-output support can change.

## Changed components

- Application contracts and shared pipeline: `src/ReturnFlow.Application/AiContracts.cs`, `ReturnInterpretationPipeline.cs`.
- Provider and quota infrastructure: `src/ReturnFlow.Infrastructure/Ai/GroqReturnIntentInterpreter.cs`, `ModeRoutingReturnIntentInterpreter.cs`, `LiveModelOptions.cs`, `SqliteLiveUsageLimiter.cs`, and registration.
- Persistent quota model: `src/ReturnFlow.Domain/LiveModelUsageRecord.cs`, DbContext, migration and model snapshot.
- API/configuration: `Program.cs`, `DemoEndpoints.cs`, `LiveModelRuntimeSettings.cs`, `.env.example`.
- Vue mode selection and request preservation: simulator, API, workflow and presentation helpers/tests.
- Tests: Groq adapter unit tests, shared pipeline Live tests, real-SQLite quota tests and default-disabled API integration test.
- Documentation: README, ADR-012 and this report.

## Known limitations and remaining risks

- No real Groq call was made, as required. Actual model/schema compatibility remains unverified until the owner selects a current supported model and explicitly approves a local smoke call.
- Live currently interprets the selected fictional scenario message; free-form visitor text remains outside this stage.
- Strict structured-output support is model-dependent. An incompatible configured model fails safely but cannot provide Live functionality.
- SQLite quotas coordinate one shared database/single API instance. A future multi-instance deployment needs a shared durable quota store or provider-side hard allowance.
- The client partition uses a one-way hash of the server-observed remote address. Reverse-proxy forwarding/trust configuration is deployment-specific and deferred to AI-6; session/global limits remain effective without it.
- Live quota records store attempts rather than provider-reported token usage. The output cap bounds each call, while exact billing/usage reconciliation is outside this portfolio slice.
- Public Live access, deployment and AI-6 are **not implemented or approved**.
