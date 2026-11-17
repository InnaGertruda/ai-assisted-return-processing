import assert from 'node:assert/strict'
import { test } from 'node:test'
import { emptyInspectorState } from './emptyInspectorState.mjs'

test('the inspector exposes an honest empty state before any run exists', () => {
  assert.equal(emptyInspectorState.status, 'Not run')
  assert.equal(emptyInspectorState.heading, 'No execution trace')
  assert.doesNotMatch(JSON.stringify(emptyInspectorState), /Accepted/)
})
