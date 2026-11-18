import assert from 'node:assert/strict'
import test from 'node:test'
import { quantityChanges, requestBodyPreview } from './scenarioPresentation.mjs'

test('request preview shows runtime identifiers as placeholders and preserves the fixture message', () => {
  const preview = JSON.parse(requestBodyPreview({ message: 'I want to return one more lamp.' }))

  assert.equal(preview.selectedOrderId, '<created from this order on Run>')
  assert.equal(preview.submissionKey, '<generated for a new submission; reused for retry>')
  assert.equal(preview.message, 'I want to return one more lamp.')
  assert.equal(preview.mode, 'Replay')
})

test('quantity changes compare actual before and after order summaries', () => {
  const changes = quantityChanges(
    { items: [{ id: 'oi-1', productName: 'Arc floor lamp', availableQuantity: 2, activeAllocatedQuantity: 1 }] },
    { items: [{ id: 'oi-1', productName: 'Arc floor lamp', availableQuantity: 1, activeAllocatedQuantity: 2 }] },
  )

  assert.deepEqual(changes, [
    {
      id: 'oi-1',
      productName: 'Arc floor lamp',
      beforeAvailable: 2,
      afterAvailable: 1,
      allocatedDelta: 1,
    },
  ])
})

test('request preview labels explicit Live opt-in', () => {
  const preview = JSON.parse(requestBodyPreview({ message: 'Return one lamp.' }, 'Live'))

  assert.equal(preview.mode, 'Live')
})
