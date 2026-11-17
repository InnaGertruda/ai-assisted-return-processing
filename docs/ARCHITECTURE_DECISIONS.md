# AI-Assisted Return Processing — ADR Baseline for MVP

**Date:** 2026-11-17  
**Purpose:** Preserve *why* we selected these design constraints. This file supersedes incompatible technology/deployment guidance in earlier ReturnFlow drafts. Any later change needs explicit rationale, consequences, updated tests, and a new/revised ADR.

## ADR-001 — Independent return cases and read-only order return summary

**Status:** Accepted for MVP.

**Decision:** One accepted `ReturnSubmission` creates one independent `ReturnCase`, with one or more quantity-based `ReturnItems` referencing purchased OrderItems. One Order can have many ReturnCases. An `OrderReturnSummary` is a query/view derived from authoritative items, **not** a mutable aggregate or write authorization source.

**Why:** Correctly handle distinct customer submissions, multi-item cases, prior returns, and later additional returns while retaining an overview useful to the AI interpretation context.

**Trade-off:** Quantity validation crosses case boundaries; a dedicated allocation service/transaction is necessary.

**Evidence:** S01, S04, S05; summary arithmetic and idempotency tests.

## ADR-002 — Channel-aware intake with AI as interpreter, not authority

**Status:** Accepted direction; advanced workflow **deferred**.

**Decision:** Structured web return selection does not need LLM interpretation. The first demo uses a **simulated customer email** and AI interpretation; ambiguous wording cannot trigger an automatic case. A validated clear request may create a case, subject to atomic allocation. The earlier 24-hour customer correction deadline plus internal execution buffer is a **future simulated scenario**, not a real scheduled requirement of MVP.

**Why:** A visitor should see the trust boundary between natural-language interpretation and business-safe execution without having to wait 24 hours.

**Trade-off:** The MVP omits the full return/ship/refund lifecycle and real legal deadlines; it does not claim production consumer-law compliance.

**Evidence:** S01, S02, S11 and traces for clarification vs acceptance.

## ADR-003 — Derive allocated quantities and serialize SQLite writes

**Status:** Accepted for MVP; must be proven by integration tests.

**Decision:** Use Option B: compute active/completed capacity consumption from persisted ReturnItems, not a redundant quantity counter. Use **SQLite write-serialized transaction** (`BEGIN IMMEDIATE` or equivalent proven by test) to re-read authoritative capacity and atomically insert cases/items. Idempotency and decision evidence are transactionally coordinated. Do **not** use PostgreSQL `SELECT ... FOR UPDATE`.

**Why:** Avoid duplicate state in a small single-instance demo while demonstrating a genuine business invariant and concurrency boundary.

**Trade-off:** Single-writer coordination, possible busy/lock contention, need bounded retries and measured concurrency tests. SQLite is not a horizontal-scale production choice here.

**Evidence:** S03, S04, S05, S10; real SQLite file, independent connections, two requests for last unit.

## ADR-004 — Low-cost simulation-oriented technology and deployment

**Status:** Accepted baseline; public deployment optional until approved.

**Decision:** Vue 3 + TypeScript + Vite UI, ASP.NET Core **.NET 10 LTS** modular monolith, EF Core SQLite and seeded fictional scenarios. Local-first. Any future public demo should use a static frontend host and a **single** scale-to-zero backend instance. Provider selection remains deferred. No always-on worker, real email, shipping or payment services. Use simulated time only when adding scheduled-flow demonstration later.

**Why:** Focus implementation effort on observable AI architecture, not infrastructure expenditure. No one needs to wait 24 hours to explore a portfolio case.

**Trade-off:** Demo sessions can expire or reset; no uptime or scheduling guarantee. Persistent storage is optional for session continuity across redeploys and may introduce storage charges.

**Evidence:** Clean local startup, fully functional no-key Replay demo, session-reset tests, and provider-neutral deployment preparation without deployment.

## ADR-005 — Replay-first, constrained LLM output, independent verification

**Status:** Accepted for MVP.

**Decision:** `IReturnIntentInterpreter` has Replay and an optional Live adapter. Replay runs fixed versioned scenario fixtures, not invented responses to arbitrary free text. Live is disabled until explicit opt-in and must use a server-only API key plus rate, token and daily caps. LLM produces **typed proposals**, no tool calls or direct writes. Deterministic schema, grounding, business and transactional validation remain mandatory regardless of provider-side structured-output guarantees.

**Why:** Safe, repeatable portfolio experience within budget. Schema correctness is not proof of factual correctness or authorization.

**Trade-off:** Fixed-context one-call AI cannot resolve every multi-turn conversation. Inconclusive cases ask for clarification.

**Evidence:** S02, S06, S07, S08, S09, S11, S12.

## ADR-006 — Explainable results and safe decision evidence

**Status:** Accepted for MVP.

**Decision:** Persist per-run stage results and rule/version references. Every stage is `Passed`, `Failed` or `Skipped`; the inspector distinguishes AI proposal, independently verified facts, deterministic decision and actual committed result. Mark dry-run and Replay clearly. No API keys, private hidden reasoning or real customer data in public traces. A committed case must be paired with required decision evidence in a consistent transaction.

**Why:** The portfolio must show evidence for architectural decisions and business-rule enforcement, not just a chatbot response.

**Evidence:** Trace redaction, `Accepted`-only-after-commit, inspector successful/failed scenario UI tests.

## ADR-007 — Isolated, resettable anonymous demo sessions

**Status:** Accepted for MVP.

**Decision:** Each visitor gets a cryptographically unguessable demo-session ID/token. Session-scoped entities cannot be read/changed by other sessions. Reset affects only that session. Automatic cleanup can occur on later requests; no continuously active job. Local SQLite files are ignored by Git; persistent storage is added only if durability across restarts is desired.

**Why:** Prevent visitors from overwriting each other's scenarios while avoiding a paid user/auth platform.

**Trade-off:** Not a customer authentication system; hard limits, fictional-only data and abuse protection remain necessary before public Live mode.

**Evidence:** Two-session isolation, reset, expiry and API scope tests.

## ADR-008 — Defer investment-heavy capabilities until justified

**Status:** Accepted scope limit.

**Decision:** Do not implement RAG/vector DB, knowledge graph, multi-agent coordination, LLM-as-judge, separate message broker, real outbox/scheduling, real refunds or multi-instance database hosting in the initial slice. Document extension points and revisit when a demonstrated limitation justifies cost/complexity.

**Why:** The project's primary value is an affordable demonstration of guarded AI decision-making.

**Evidence:** Running MVP architecture and a visible future improvements section in the README/inspector.

## ADR-009 — Materialize deterministic fixture copies per opaque demo session

**Status:** Accepted for AI-1.

**Decision:** Creating an S01 demo session materializes a separate Order, OrderItem, seeded ReturnSubmission, ReturnCase and ReturnItem in SQLite. The session token is a random 128-bit identifier. Entity IDs are derived deterministically from the full session-token hash, so reset restores stable IDs inside that session while another session receives different IDs. All reads and resets require the session ID and entity relationship together. Expiry cleanup runs opportunistically on incoming session creation; no worker is required. Timestamps are stored as SQLite integers so expiry filters and ordering execute in the database.

**Why:** Per-session copies make isolation and reset behavior explicit and testable without a user/auth platform or shared mutable fixture. Stable session-local IDs make reset reproducible, while distinct IDs allow cross-session access attempts to fail closed.

**Trade-off:** Fixture rows are duplicated per visitor and expired rows remain until a later request triggers cleanup. This is acceptable for the small demo and avoids always-on infrastructure. A larger public workload would need bounded cleanup and retention monitoring.

**Evidence:** `DemoSessionApiTests` proves independent clean sessions, cross-session 404 responses, reset isolation, expiry, deterministic empty-database reconstruction and real SQLite foreign-key rejection. `MigrationModelTests` prevents unversioned schema drift.

## ADR-010 — Exact Replay fixtures and dry-run trace persistence

**Status:** Accepted for AI-2.

**Decision:** Replay interpretation reads a versioned embedded fixture catalog and requires the session scenario plus exact supported message to match. Fixture item placeholders are resolved only from the already retrieved, session-scoped order context. Model-shaped JSON is then parsed with unknown fields and invalid enum values rejected. AI-2 persists one public-safe JSON trace record per run, linked to the demo session, but does not persist a new ReturnSubmission, ReturnCase or ReturnItem. The final dry-run state is `EligibleForCommit` / `Not committed`; `Accepted` remains unavailable until AI-3 performs a durable atomic allocation.

**Why:** Exact fixtures make the no-cost portfolio behavior deterministic and prevent arbitrary visitor wording from being misrepresented as AI output. Persisting the complete bounded trace payload keeps the inspector contract reconstructible without introducing premature normalized trace aggregates.

**Trade-off:** Replay is deliberately not a general natural-language interpreter, and JSON trace payloads are optimized for a small inspectable demo rather than analytical querying. A later schema change needs an explicit trace-version migration or compatibility reader.

**Evidence:** `ReplayReturnIntentInterpreterTests`, `ReturnInterpretationPipelineTests`, and `AiReplayApiTests` cover exact matching, strict schema checks, evidence grounding, all AI-2 scenarios, session isolation, and unchanged order/case state.

## ADR-011 — Transactional idempotency and authoritative SQLite allocation

**Status:** Accepted for AI-3.

**Decision:** A normalized SHA-256 payload fingerprint covers the selected order, trimmed message and normalized mode. A session-scoped idempotency record uniquely owns `(DemoSessionId, SubmissionKey)` and points to the original execution trace. An identical retry returns that original logical result before interpretation when already present; a different fingerprint returns a conflict. Verified `CreateReturn` proposals enter a non-deferred SQLite transaction, which reserves the single writer before re-reading session/order membership and quantities. Requested lines are aggregated and sorted, then the submission, case, all ReturnItems, decision record, idempotency record and accepted trace are saved in one transaction. Business rejection may persist an uncommitted trace/idempotency result, but never a partial case. Transient SQLite busy/locked errors have bounded retries; capacity rejection does not.

**Why:** AI-2 preflight is necessarily stale by commit time. Reserving the SQLite writer before the final read prevents two independent requests from both consuming the final unit, while persisted fingerprints make retries stable without trusting client state or calling Replay again.

**Trade-off:** SQLite remains a single-writer, single-instance demo store. Idempotency is scoped to a demo session and preserves the first logical outcome, including failures; retrying a technical failure therefore requires a new submission key. Payload fingerprints deliberately exclude raw session/key values because those form the record key, but include every request field that can change interpretation or allocation. A future multi-instance design requires a different coordination store.

**Evidence:** `AtomicReturnApiTests` uses real temporary SQLite files and independent HTTP scopes/connections to prove S01, S03, S04, S05, parallel identical requests, S10 final-unit contention, multi-line all-or-nothing rejection, and rollback after an injected decision-record failure. `MigrationModelTests` proves the versioned model matches the migration snapshot.

---

## ADR hygiene

For every changed decision: record status (`Proposed`, `Accepted`, `Superseded`, `Rejected`), motivation, option rejected, consequences, evidence/tests, and version/date. Maintain stable rule IDs from `BUSINESS_RULES.md`. Business/legal assumptions stay explicitly **UNVERIFIED** until independently verified.
