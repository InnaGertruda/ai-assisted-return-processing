export const businessRules = Object.freeze([
  {
    id: 'BR-RET-001',
    title: 'One order per return case',
    description: 'Every accepted return case must reference exactly one order.',
    appliesAt: 'Case commit',
    proof: 'Foreign-key and domain tests',
  },
  {
    id: 'BR-RET-002',
    title: 'Items stay inside the trusted order',
    description:
      'Every return item must reference an order item belonging to the same order and demo session as its return case.',
    appliesAt: 'Output verification and commit',
    proof: 'Cross-order and cross-session rejection',
  },
  {
    id: 'BR-RET-003',
    title: 'Quantity must be positive',
    description: 'A requested return quantity must be a positive integer.',
    appliesAt: 'Output verification and commit',
    proof: 'Zero and negative quantities are rejected',
  },
  {
    id: 'BR-RET-004',
    title: 'Purchased quantity cannot be exceeded',
    description:
      'For each order item, active allocated quantity plus completed consumed quantity must never exceed purchased quantity.',
    appliesAt: 'Business preflight and atomic allocation commit',
    proof: 'Capacity and concurrent final-unit tests',
  },
  {
    id: 'BR-RET-005',
    title: 'Released capacity becomes available',
    description:
      'Released or cancelled reservations stop consuming capacity; completed returns continue to consume purchased quantity.',
    appliesAt: 'Capacity calculation',
    proof: 'Return-item state semantics tests',
  },
  {
    id: 'BR-RET-006',
    title: 'Retries are idempotent',
    description:
      'The same session and submission key with the same payload returns the prior result; reusing the key with a different payload conflicts.',
    appliesAt: 'Pre-interpreter check and commit',
    proof: 'Replay, retry, and parallel tests in AI-3',
  },
  {
    id: 'BR-RET-007',
    title: 'Additional returns are allowed',
    description:
      'A separate legitimate return submission is allowed when purchased capacity remains.',
    appliesAt: 'Business validation and commit',
    proof: 'Separate-submission test in AI-3',
  },
  {
    id: 'BR-RET-008',
    title: 'Ambiguity blocks automatic creation',
    description:
      'An AI-interpreted request cannot create a case automatically while its item, quantity, or supporting evidence remains ambiguous.',
    appliesAt: 'Post-model grounding verification',
    proof: 'Missing-referent and duplicate-name scenarios',
  },
  {
    id: 'BR-RET-010',
    title: 'AI output is only a proposal',
    description:
      'Model output cannot directly authorize a refund, mutate business state, issue arbitrary SQL, or bypass application guardrails.',
    appliesAt: 'Interpreter trust boundary',
    proof: 'Prompt-injection and no-business-write tests',
  },
  {
    id: 'BR-AUD-001',
    title: 'Public traces stay safe and truthful',
    description:
      'Public decision traces retain safe evidence, outcomes, rule and version references, and the actual commit status.',
    appliesAt: 'Trace writer and read API',
    proof: 'Trace redaction and dry-run status tests',
  },
])

const rulesById = new Map(businessRules.map((rule) => [rule.id, rule]))

/** @param {string} ruleId */
export function businessRuleById(ruleId) {
  return rulesById.get(ruleId)
}

