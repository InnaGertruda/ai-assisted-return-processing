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

/** @param {any} beforeOrder @param {any} afterOrder */
export function quantityChanges(beforeOrder, afterOrder) {
  if (!beforeOrder || !afterOrder) return []

  return afterOrder.items.map((/** @type {any} */ afterItem) => {
    const beforeItem = beforeOrder.items.find((/** @type {any} */ item) => item.id === afterItem.id)
    return {
      id: afterItem.id,
      productName: afterItem.productName,
      beforeAvailable: beforeItem?.availableQuantity ?? afterItem.availableQuantity,
      afterAvailable: afterItem.availableQuantity,
      allocatedDelta:
        afterItem.activeAllocatedQuantity - (beforeItem?.activeAllocatedQuantity ?? afterItem.activeAllocatedQuantity),
    }
  })
}

