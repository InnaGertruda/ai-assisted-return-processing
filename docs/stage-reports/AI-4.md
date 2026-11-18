# Stage AI-4 report

**Status:** Complete locally on `feature/ai-4-portfolio-experience` for owner review  
**Date:** 2026-11-18

## Scope delivered

- Reworked the Vue simulator so selecting a scenario creates one isolated fictional session and repeated runs reuse that same session. Selecting another scenario creates a different session; Reset calls the session-scoped reset API and clears the inspector.
- Displays authoritative order quantities and return-case count from the API. After every run, the UI re-reads order and case state and compares actual before/after availability; it does not synthesize a successful frontend outcome.
- Keeps Replay input honest: the exact fixture message and version are visible, Live is explicitly unavailable, and arbitrary text is not presented as an AI result.
- Expanded the AI Execution Inspector to show final outcome/commit status, proposal fields, all returned pipeline stages, `Passed`/`Failed`/`Skipped`, reason codes, safe evidence, rule references, schema/prompt versions, quantity changes, and created Return Case ID.
- Separately explains committed acceptance, clarification, business rejection/manual review, and technical failure. The UI states that no refund, shipment, payment, legal determination, or external AI call occurs.
- Added accessible loading, empty, reset and HTTP-error states plus responsive layouts and links to the public business-rule, architecture-decision and test documentation.
- Added lightweight frontend tests for persistent-session API behavior, session-scoped reset, safe API reason-code errors, actual quantity comparison, outcome categories and existing rule/empty-state presentation.
- Refined the simulator actions into **Submit new return** and **Retry same request**. Only the new-return action creates a submission key; retry retains the same immutable session/order/key/message request and delegates idempotency to the existing AI-3 backend behavior.
- Blocks both actions while a request is in flight, displays the actual backend `RunId` and `CreatedReturnCaseId` for a committed acceptance, and keeps a distinct new-return path available while authoritative capacity remains.
- Updated the README from AI-3 to the implemented AI-4 experience. No backend business behavior, Live provider, deployment, payment, shipping, scheduling or external service was added.

## Acceptance evidence

- S01 browser smoke: initial API state showed 3 purchased, 1 active and 2 available. The first run returned `Accepted / Committed`, a created case ID and `2 -> 1 available`; a second run used the same displayed session state and returned another actual case with `1 -> 0 available`.
- Reset browser smoke: the same selected scenario returned to 1 seeded case and 2 available, and the inspector returned to the honest `Not run` empty state.
- S02 browser smoke: returned `ClarificationRequired / Not committed`; grounding failed with `AMBIGUITY_UNRESOLVED`, business validation and commit were skipped, both item quantities stayed unchanged, and no case ID was shown.
- S06 browser smoke: returned `ManualReview / Not committed`; the proposal visibly contained `oi-fake-999`, grounding failed with `ITEM_ID_NOT_IN_CONTEXT`, later stages were skipped, availability stayed `2 -> 2`, and no case ID was shown.
- The responsive browser view kept simulator controls and inspector content readable in a narrow single-column layout.
- Actual API/browser behavior was used for the smoke evidence; no mocked frontend success state was used.
- Frontend workflow tests prove that retry sends the identical request object and key, duplicate in-flight clicks execute once, an accepted retry retains the same Run ID / Return Case ID without reducing availability again, and a later new submission receives a new key and can consume another available unit.
- The existing real-SQLite `AtomicReturnApiTests` remain the backend authority: S04 returns the original Run ID / Return Case ID with no second allocation, while S05 permits another independent case for a distinct submission key when capacity remains. No new idempotency mechanism was introduced.

## Commands and actual results

- Branch inspection found a clean `feature/ai-3-atomic-return` worktree. Local `dev` was four commits behind but was the direct ancestor of the completed AI-1 through AI-3 line. `feature/ai-4-portfolio-experience` was created at the AI-3 head to preserve all completed stages without changing `dev` or `main`.
- `dotnet format ReturnFlow.slnx --verify-no-changes --no-restore` -> passed with no diagnostics.
- `dotnet build ReturnFlow.slnx --no-restore` -> succeeded with 0 warnings and 0 errors.
- `dotnet test ReturnFlow.slnx --no-build --verbosity minimal` -> 13 unit tests and 24 real-SQLite integration tests passed; 0 failed and 0 skipped.
- From `web`, `npm.cmd run test` -> 10 passed, 0 failed.
- From `web`, `npm.cmd run lint` -> passed with 0 warnings/errors.
- From `web`, `npm.cmd run build` -> passed TypeScript checks and Vite production build; 17 modules transformed.
- 2026-11-18 refinement: from `web`, `npm.cmd run test` -> 13 passed, 0 failed, including 3 submission-workflow tests.
- 2026-11-18 refinement: from `web`, `npm.cmd run lint` -> passed with 0 warnings/errors.
- 2026-11-18 refinement: from `web`, `npm.cmd run build` -> passed TypeScript checks and Vite production build; 18 modules transformed. One concurrent build attempt failed during Vite HTML emission; an immediate isolated rerun passed, so the recorded result is the non-concurrent build.
- 2026-11-18 refinement: `dotnet test ReturnFlow.slnx --filter "FullyQualifiedName~AtomicReturnApiTests" --verbosity minimal` -> 8 real-SQLite integration tests passed, 0 failed and 0 skipped. The unit-test project reported no tests matching that integration-test filter, as expected.
- Browser-level smoke used the locally running Vite app and .NET API with no external calls. It verified S01 twice in one session, Reset, S02 and S06. The first attempt exposed a Vue reactive-proxy clone error before the API request; the clone was corrected and every smoke flow then passed.
- `git diff --check` -> no whitespace errors; Git printed only the repository's existing LF-to-CRLF checkout notices.

## Changed files

- Simulator: `web/src/features/return-simulator/ReturnSimulator.vue`, `demoApi.mjs` and `demoApi.test.mjs`, scenario presentation helpers/tests, and new `submissionWorkflow.mjs` / `submissionWorkflow.test.mjs`.
- Inspector: `web/src/features/ai-inspector/AiExecutionInspector.vue` and trace presentation helpers/tests.
- Shell/documentation: `web/src/App.vue`, `README.md`, and this report.

## Known limitations and next stage not implemented

- Replay remains exact fixture playback; arbitrary visitor text is intentionally unsupported without a future Live adapter.
- Retry state is intentionally kept only for the current browser session; selecting another scenario or resetting the demo clears it together with the inspector. Durable idempotency remains owned by the AI-3 backend records.
- The public rule/document links target the curated `main` locations and resolve to the promoted content only after the owner reviews and promotes it.
- SQLite remains a single-writer, single-instance demo store. Session persistence across redeploys still depends on configured persistent storage.
- No refund, shipment, cancellation execution, legal policy decision, worker, deployment, cloud resource, paid service, or Live provider was added.

AI-5 (optional Live model adapter and strict spend/abuse controls, disabled by default) is **not implemented**. Owner review is required before any commit, push, PR, merge, deployment, or next-stage work.
