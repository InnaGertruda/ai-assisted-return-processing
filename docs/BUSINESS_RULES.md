# AI-Assisted Return Processing — Business Rules for MVP

**Scope:** Fictional seeded orders and technical return-capacity decisions. This is not a definitive statement of Polish/EU consumer law. Values such as the 24-hour cancellation period are **UNVERIFIED** and outside MVP.

| Rule ID | Requirement | Applies at | Proof |
|---|---|---|---|
| BR-RET-001 | Every accepted ReturnCase references exactly one Order. | Case commit | FK/domain test |
| BR-RET-002 | Every ReturnItem must reference an OrderItem belonging to the same Order **and demo session** as its ReturnCase. | Output verification and commit | Cross-order/cross-session rejection |
| BR-RET-003 | Requested quantity is a positive integer. | Output verification and commit | Zero/negative rejected |
| BR-RET-004 | Across all relevant ReturnCases, `active allocated + completed consumed <= purchased quantity` for every OrderItem. | Atomic allocation commit | Two final-unit requests: exactly one succeeds |
| BR-RET-005 | Released/cancelled reservation stops consuming capacity; completed returned quantity continues to count. | Capacity calculation | State semantics tests; cancellation action itself is future scope |
| BR-RET-006 | Retry with same session + submission key + same payload returns prior result, not a new case; key reuse with different payload conflicts. | Pre-LLM idempotency check and commit | Replay/retry/parallel tests |
| BR-RET-007 | Separate legitimate additional return submission is allowed while capacity remains. | Business validation/commit | Separate-key test |
| BR-RET-008 | An AI-interpreted request with unresolved item, quantity, or evidence ambiguity cannot create a case automatically. | Post-LLM grounding | Missing referent and duplicate item names |
| BR-RET-010 | AI output is only a proposal; no direct refund, state mutation, arbitrary SQL or guardrail bypass from model content. | Interpreter boundary | Prompt injection fixture |
| BR-AUD-001 | Public decision traces retain safe evidence, outcome, rule IDs/version, schema/prompt version and actual commit status. | Trace writer/read API | Trace redaction and accepted-vs-dry-run tests |

## Quantity semantics for first slice

- `ActiveAllocatedQuantity`: accepted ReturnItems currently reserving purchased capacity.
- `CompletedConsumedQuantity`: successfully completed returns that remain permanently unavailable for new returns.
- `AvailableQuantity = PurchasedQuantity - ActiveAllocatedQuantity - CompletedConsumedQuantity`.
- An individual ReturnItem must belong to exactly one of active, completed, released/nonconsuming states at any time; **do not double count**.
- MVP can use minimal capacity states (`Reserved`, `Released`, `CompletedReturn`) rather than building full warehouse/refund lifecycles. Operational state changes other than creation are outside first slice.
- The OrderReturnSummary query is for display/context only. Always revalidate during the SQLite write transaction.

## Unverified / deferred business topics

Return/withdrawal eligibility periods, defect claims, original shipping reimbursement, refund amounts, statutory deadlines, late cancellations, and whether an already-effective withdrawal can be reversed. They must not be silently encoded as settled policies. A later business-rules review and legal verification would be necessary before any real retailer deployment.

## Evidence convention

For each rule, link to: the implementation/check, a specific automated test, and at least one trace/scenario that demonstrates the rule. The inspector may show an explicit "documentation only / not yet linked" status, but must not invent missing evidence.
