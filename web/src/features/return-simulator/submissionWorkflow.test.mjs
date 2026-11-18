import assert from 'node:assert/strict'
import test from 'node:test'
import { createSubmissionWorkflow } from './submissionWorkflow.mjs'

const input = {
  sessionId: 'session-1',
  orderId: 'order-1',
  message: 'I want to return one more lamp.',
}

test('new submissions get new keys while retry reuses the identical request object', async () => {
  /** @type {Array<Readonly<{ sessionId: string, orderId: string, submissionKey: string, message: string }>>} */
  const requests = []
  const keys = ['key-1', 'key-2']
  const workflow = createSubmissionWorkflow({
    keyFactory: () => keys.shift() ?? assert.fail('Unexpected extra key request'),
    execute: async (request) => requests.push(request),
  })

  await workflow.submitNew(input)
  await workflow.retrySame()
  await workflow.submitNew(input)

  assert.equal(requests.length, 3)
  assert.strictEqual(requests[0], requests[1])
  assert.equal(requests[0].submissionKey, 'key-1')
  assert.equal(requests[1].message, input.message)
  assert.equal(requests[2].submissionKey, 'key-2')
  assert.notStrictEqual(requests[0], requests[2])
})

test('a second click is ignored while a request is processing', async () => {
  /** @type {((value: { finalOutcome: string }) => void) | undefined} */
  let release
  const pending = new Promise((resolve) => {
    release = resolve
  })
  const requests = []
  /** @type {boolean[]} */
  const processingStates = []
  const workflow = createSubmissionWorkflow({
    keyFactory: () => 'key-1',
    execute: async (request) => {
      requests.push(request)
      return pending
    },
    onProcessingChange: (processing) => processingStates.push(processing),
  })

  const firstClick = workflow.submitNew(input)
  const duplicateClick = workflow.submitNew(input)

  assert.equal(await duplicateClick, undefined)
  assert.equal(requests.length, 1)
  assert.ok(release)
  release({ finalOutcome: 'Accepted' })
  await firstClick
  assert.deepEqual(processingStates, [true, false])
})

test('accepted retry keeps Run ID, Return Case ID and quantity; a new submission can consume another unit', async () => {
  let availableQuantity = 2
  const storedResults = new Map()
  let runNumber = 0
  let caseNumber = 0
  const workflow = createSubmissionWorkflow({
    keyFactory: () => `key-${storedResults.size + 1}`,
    execute: async (request) => {
      if (storedResults.has(request.submissionKey)) return storedResults.get(request.submissionKey)

      availableQuantity -= 1
      const result = {
        runId: `run-${++runNumber}`,
        createdReturnCaseId: `case-${++caseNumber}`,
        finalOutcome: 'Accepted',
        availableQuantity,
      }
      storedResults.set(request.submissionKey, result)
      return result
    },
  })

  const first = await workflow.submitNew(input)
  const retry = await workflow.retrySame()
  const second = await workflow.submitNew(input)

  assert.equal(retry.runId, first.runId)
  assert.equal(retry.createdReturnCaseId, first.createdReturnCaseId)
  assert.equal(retry.availableQuantity, 1)
  assert.equal(second.availableQuantity, 0)
  assert.notEqual(second.runId, first.runId)
  assert.notEqual(second.createdReturnCaseId, first.createdReturnCaseId)
})
