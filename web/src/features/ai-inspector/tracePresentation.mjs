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
