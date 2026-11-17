/** @param {{ message?: string } | undefined} scenario */
export function requestBodyPreview(scenario) {
  return JSON.stringify(
    {
      selectedOrderId: '<created from this order on Run>',
      submissionKey: '<generated on Run>',
      message: scenario?.message ?? '',
      mode: 'Replay',
    },
    null,
    2,
  )
}

