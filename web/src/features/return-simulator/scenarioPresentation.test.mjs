import assert from 'node:assert/strict'
import test from 'node:test'
import { requestBodyPreview } from './scenarioPresentation.mjs'

test('request preview shows runtime identifiers as placeholders and preserves the fixture message', () => {
  const preview = JSON.parse(requestBodyPreview({ message: 'I want to return one more lamp.' }))

  assert.equal(preview.selectedOrderId, '<created from this order on Run>')
  assert.equal(preview.submissionKey, '<generated on Run>')
  assert.equal(preview.message, 'I want to return one more lamp.')
  assert.equal(preview.mode, 'Replay')
})
