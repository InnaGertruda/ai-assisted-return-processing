/** @param {any} trace */
export function outcomeLabel(trace) {
  if (!trace) return 'Not run'
  return `${trace.finalOutcome} · ${trace.commitStatus}`
}

/** @param {string} status */
export function stageTone(status) {
  if (status === 'Passed') return 'text-limewash'
  if (status === 'Failed') return 'text-coral'
  return 'text-white/45'
}

/** @param {any} trace */
export function proposalSummary(trace) {
  return (trace?.proposal?.items ?? [])
    .map(/** @param {any} item */ (item) => `${item.requestedQuantity ?? '?'} × ${item.productMention ?? 'unresolved item'}`)
    .join(', ')
}

/** @param {any} trace */
export function outcomeKind(trace) {
  if (!trace) return 'empty'
  if (trace.finalOutcome === 'Accepted' && trace.commitStatus === 'Committed') return 'committed'
  if (trace.finalOutcome === 'TechnicalFailure') return 'technical-failure'
  if (trace.finalOutcome === 'ClarificationRequired') return 'clarification'
  return 'business-rejection'
}

/** @param {any} trace */
export function outcomeExplanation(trace) {
  const kind = outcomeKind(trace)
  if (kind === 'committed') return 'Verified and committed as a fictional Return Case. No refund or shipment was initiated.'
  if (kind === 'technical-failure') return 'The pipeline failed technically and did not claim a business action was committed.'
  if (kind === 'clarification') return 'The request was too ambiguous for automatic action. Nothing was committed.'
  if (kind === 'business-rejection') return 'The request reached a deterministic stop or review outcome. Nothing was committed.'
  return ''
}
