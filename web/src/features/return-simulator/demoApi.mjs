/** @param {Response} response @param {string} fallbackMessage */
async function readResponse(response, fallbackMessage) {
  if (response.ok) return response.json()

  let reasonCode = ''
  try {
    reasonCode = (await response.json()).reasonCode ?? ''
  } catch {
    // An HTTP failure can legitimately have no JSON body.
  }

  const detail = reasonCode ? ` (${reasonCode})` : ''
  throw new Error(`${fallbackMessage}${detail}`)
}

/** @param {string} apiBase @param {string} path */
function url(apiBase, path) {
  return `${apiBase}${path}`
}

/** @param {DemoFetcher} fetcher @param {string} apiBase */
export async function getScenarioCatalog(fetcher, apiBase) {
  const response = await fetcher(url(apiBase, '/api/demo/scenarios'))
  return readResponse(response, 'Scenario catalog could not be loaded.')
}

/** @param {DemoFetcher} fetcher @param {string} apiBase */
export async function getLiveStatus(fetcher, apiBase) {
  const response = await fetcher(url(apiBase, '/api/demo/live-status'))
  return readResponse(response, 'Live mode status could not be loaded.')
}

/** @param {DemoFetcher} fetcher @param {string} apiBase @param {string} scenarioId */
export async function createSession(fetcher, apiBase, scenarioId) {
  const response = await fetcher(url(apiBase, '/api/demo/sessions'), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ scenarioId }),
  })
  return readResponse(response, 'A fictional demo session could not be created.')
}

/** @param {DemoFetcher} fetcher @param {string} apiBase @param {string} sessionId @param {string} orderId */
export async function getOrder(fetcher, apiBase, sessionId, orderId) {
  const response = await fetcher(url(apiBase, `/api/demo/sessions/${sessionId}/orders/${orderId}`))
  return readResponse(response, 'The authoritative order summary could not be loaded.')
}

/** @param {DemoFetcher} fetcher @param {string} apiBase @param {string} sessionId */
export async function getReturnCases(fetcher, apiBase, sessionId) {
  const response = await fetcher(url(apiBase, `/api/demo/sessions/${sessionId}/return-cases`))
  return readResponse(response, 'Return cases could not be loaded.')
}

/**
 * @param {DemoFetcher} fetcher
 * @param {string} apiBase
 * @param {{ sessionId: string, orderId: string, submissionKey: string, message: string, mode: 'Replay' | 'Live' }} request
 */
export async function runInterpretation(fetcher, apiBase, request) {
  const response = await fetcher(url(apiBase, `/api/demo/sessions/${request.sessionId}/ai-runs`), {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      selectedOrderId: request.orderId,
      submissionKey: request.submissionKey,
      message: request.message,
      mode: request.mode,
    }),
  })
  return readResponse(response, `${request.mode} run could not be completed.`)
}

/**
 * Backward-compatible Replay helper for the existing AI-4 API tests.
 * @param {DemoFetcher} fetcher
 * @param {string} apiBase
 * @param {{ sessionId: string, orderId: string, submissionKey: string, message: string }} request
 */
export function runReplay(fetcher, apiBase, request) {
  return runInterpretation(fetcher, apiBase, { ...request, mode: 'Replay' })
}

/** @param {DemoFetcher} fetcher @param {string} apiBase @param {string} sessionId */
export async function resetSession(fetcher, apiBase, sessionId) {
  const response = await fetcher(url(apiBase, `/api/demo/sessions/${sessionId}/reset`), { method: 'POST' })
  return readResponse(response, 'The fictional demo session could not be reset.')
}

/** @typedef {(url: string, options?: RequestInit) => Promise<Response>} DemoFetcher */

