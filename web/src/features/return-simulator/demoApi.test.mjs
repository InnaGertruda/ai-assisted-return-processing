import assert from 'node:assert/strict'
import test from 'node:test'
import { createSession, getOrder, getReturnCases, resetSession, runReplay } from './demoApi.mjs'

/** @param {any} body @param {number} status */
function jsonResponse(body, status = 200) {
  return new Response(JSON.stringify(body), {
    status,
    headers: { 'Content-Type': 'application/json' },
  })
}

test('multiple Replay runs reuse one session and refresh its authoritative state', async () => {
  /** @type {Array<{ url: string, options: RequestInit }>} */
  const calls = []
  /** @param {string} url @param {RequestInit} options */
  const fetcher = async (url, options = {}) => {
    calls.push({ url, options })
    if (url.endsWith('/sessions')) return jsonResponse({ sessionId: 'session-1', order: { id: 'order-1' } }, 201)
    if (url.endsWith('/ai-runs')) return jsonResponse({ runId: `run-${calls.length}`, finalOutcome: 'Accepted' })
    if (url.endsWith('/orders/order-1')) return jsonResponse({ id: 'order-1', items: [] })
    if (url.endsWith('/return-cases')) return jsonResponse([])
    throw new Error(`Unexpected URL ${url}`)
  }

  const session = await createSession(fetcher, '', 'S01')
  await runReplay(fetcher, '', {
    sessionId: session.sessionId,
    orderId: session.order.id,
    submissionKey: 'key-1',
    message: 'I want to return one more lamp.',
  })
  await getOrder(fetcher, '', session.sessionId, session.order.id)
  await getReturnCases(fetcher, '', session.sessionId)
  await runReplay(fetcher, '', {
    sessionId: session.sessionId,
    orderId: session.order.id,
    submissionKey: 'key-2',
    message: 'I want to return one more lamp.',
  })

  assert.equal(calls.filter((call) => call.url.endsWith('/sessions')).length, 1)
  assert.equal(calls.filter((call) => call.url.endsWith('/sessions/session-1/ai-runs')).length, 2)
  assert.deepEqual(
    calls.filter((call) => call.url.endsWith('/ai-runs')).map((call) => JSON.parse(String(call.options.body)).submissionKey),
    ['key-1', 'key-2'],
  )
})

test('reset targets only the active session and returns its restored order', async () => {
  /** @type {Array<{ url: string, options: RequestInit }>} */
  const calls = []
  /** @param {string} url @param {RequestInit} options */
  const fetcher = async (url, options = {}) => {
    calls.push({ url, options })
    return jsonResponse({ sessionId: 'session-1', scenarioId: 'S01', order: { id: 'order-1' } })
  }

  const snapshot = await resetSession(fetcher, '', 'session-1')

  assert.equal(snapshot.scenarioId, 'S01')
  assert.equal(calls[0].url, '/api/demo/sessions/session-1/reset')
  assert.equal(calls[0].options.method, 'POST')
})

test('API reason codes are preserved in visible errors', async () => {
  const fetcher = async () => jsonResponse({ reasonCode: 'SUBMISSION_KEY_PAYLOAD_CONFLICT' }, 409)

  await assert.rejects(
    runReplay(fetcher, '', {
      sessionId: 'session-1',
      orderId: 'order-1',
      submissionKey: 'reused-key',
      message: 'different payload',
    }),
    /SUBMISSION_KEY_PAYLOAD_CONFLICT/,
  )
})

