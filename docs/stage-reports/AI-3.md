# Stage AI-3 report

**Status:** Complete locally on `feature/ai-3-atomic-return` for owner review  
**Date:** 2026-11-17

## Scope delivered

- Connected independently verified `CreateReturn` proposals to a SQLite allocation boundary that starts a non-deferred transaction (`BeginTransaction(deferred: false)`, SQLite `BEGIN IMMEDIATE` behavior) before the final authoritative reads.
- Revalidates the active session, order/session scope, positive aggregated quantities, item membership, and current persisted capacity inside the write-reserved transaction. Multi-line requests are aggregated, stably ordered, validated in full, and inserted all-or-nothing.
- Atomically persists the `ReturnSubmission`, `ReturnCase`, all `ReturnItems`, `DecisionRecord`, accepted `AiExecutionTrace`, and idempotency record. `Accepted` / `Committed` and `CreatedReturnCaseId` exist only in the transactionally stored trace.
- Added session-scoped `(DemoSessionId, SubmissionKey)` idempotency records with a SHA-256 fingerprint of normalized order/message/mode. An identical completed retry returns the original trace before Replay interpretation; different payload reuse returns HTTP 409 with `SUBMISSION_KEY_PAYLOAD_CONFLICT`.
- Added explicit authoritative business rejection and technical-failure outcomes. A transaction failure rolls back all business/evidence writes; a safe `TechnicalFailure` trace can then be stored separately as the first logical result.
- Uses a 5-second SQLite busy timeout plus four bounded attempts for transient `SQLITE_BUSY`/`SQLITE_LOCKED` only. Business capacity rejection is never retried. No `SELECT ... FOR UPDATE` appears in source or tests.
- Added exact Replay/demo fixtures S04, S05 and S10. S01 now commits; S03 and all AI-2 invalid/ambiguous scenarios remain non-committing and accurately labeled Replay.
- Updated the minimal Vue result presentation to display committed status and the created Return Case ID. No AI-4 interaction expansion or Live adapter was added.
- Recorded the transaction/idempotency decision as ADR-011 and linked implemented business rules to concrete tests.

## Acceptance evidence

- S01: commits one additional lamp, creates one new case and decision record, and changes availability from 2 to 1.
- S03: returns `CannotProceed` / `Not committed`; no new case is created.
- S04: identical retry returns the original `RunId` and `CreatedReturnCaseId`, consumes no additional quantity, and different-payload key reuse returns HTTP 409.
- S05: two distinct keys create two distinct cases while the two remaining units are available.
- S10: two concurrent HTTP requests use independent request scopes/SQLite connections for one remaining unit; exactly one is `Accepted`, the other is `CannotProceed`, and final active allocation equals purchased quantity without exceeding it.
- Parallel identical-key requests converge on one trace/case and one allocation.
- Multi-line authoritative rejection leaves every line unallocated when one line exceeds capacity.
- An injected SQLite trigger failure on `DecisionRecords` produces `TechnicalFailure` and leaves no new submission, case, item, decision record, or accepted trace.

## Commands and actual results

- Repository inspection: worktree was clean on completed `feature/ai-2-replay-verification`; local `dev` remained its ancestor at the initial commit. The requested `feature/ai-3-atomic-return` branch was created from the completed AI-2 tip so AI-0 through AI-2 were preserved. `main` was not checked out or changed.
- Focused backend checks after implementation:
  - `dotnet test tests/ReturnFlow.UnitTests/ReturnFlow.UnitTests.csproj --no-restore --verbosity minimal` -> 13 passed, 0 failed, 0 skipped.
  - `dotnet test tests/ReturnFlow.IntegrationTests/ReturnFlow.IntegrationTests.csproj --no-restore --verbosity minimal` -> 24 passed, 0 failed, 0 skipped, using real temporary SQLite files.
- Initial `dotnet format ReturnFlow.slnx --verify-no-changes --no-restore` reported four whitespace diagnostics on two long initializer lines. `dotnet format ReturnFlow.slnx --no-restore` corrected them; the repeated verify command passed with no output.
- Final backend sweep:
  - `dotnet build ReturnFlow.slnx --no-restore` -> succeeded, 0 warnings, 0 errors.
  - `dotnet test ReturnFlow.slnx --no-build --verbosity minimal` -> 13 unit + 24 integration tests passed, 0 failed, 0 skipped.
- Frontend sweep from `web`:
  - `npm.cmd run lint` -> passed with 0 warnings/errors.
  - `npm.cmd run test` -> 5 passed, 0 failed.
  - `npm.cmd run build` -> passed; Vite transformed 16 modules and produced the production assets.
- `rg -n "SELECT\s+.*FOR\s+UPDATE|FOR\s+UPDATE" src tests` -> no matches.
- `git diff --check` -> no whitespace errors; only Git's existing LF-to-CRLF checkout notices were printed.

## Changed files

- Application/API: `AiContracts.cs`, `ReturnInterpretationPipeline.cs`, `DemoEndpoints.cs`.
- Domain: new `AiRunIdempotencyRecord.cs` and `DecisionRecord.cs`.
- Infrastructure: `SqliteReturnInterpretationStore.cs`, Replay fixture JSON, scenario fixtures, session cleanup, `ReturnFlowDbContext.cs`, new `20261117020000_AddAtomicReturnCommit.cs`, and the model snapshot.
- Tests: new `AtomicReturnApiTests.cs`; updated AI Replay, demo-session, pipeline and test-factory coverage.
- UI: `App.vue`, `ReturnSimulator.vue`, `AiExecutionInspector.vue`, rule evidence and trace-presentation tests.
- Documentation: `README.md`, `ARCHITECTURE_DECISIONS.md`, `BUSINESS_RULES.md`, and this report.

## Known limitations and next stage not implemented

- SQLite is intentionally single-writer and the demo remains designed for one API instance. The bounded busy retry is not a horizontal-scaling mechanism.
- Idempotency preserves the first logical result, including a technical failure. Retrying after a stored technical failure requires a new submission key.
- Replay remains exact fixture playback with no external provider request. Live mode is still disabled/not implemented.
- The UI creates a fresh fictional session per Run; richer reset/state comparison and full explainability interaction remain AI-4 scope.
- No refund, shipment, cancellation execution, policy/legal determination, worker, deployment, cloud resource, or paid service was added.

AI-4 (full interactive Vue simulator and explainability inspector) is **not implemented**. Owner review is required before any commit, push, PR, merge, deployment, or next-stage work.
