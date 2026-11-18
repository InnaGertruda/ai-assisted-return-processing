/**
 * Keeps client request identity separate from the backend idempotency mechanism.
 * The backend remains authoritative for same-key/same-payload replay behavior.
 *
 * @param {{
 *   keyFactory: () => string,
 *   execute: (request: Readonly<SubmissionRequest>) => Promise<any>,
 *   onProcessingChange?: (processing: boolean) => void,
 * }} options
 */
export function createSubmissionWorkflow({ keyFactory, execute, onProcessingChange = () => {} }) {
  /** @type {Readonly<SubmissionRequest> | null} */
  let lastRequest = null
  let processing = false

  /** @param {SubmissionInput} input */
  async function submitNew(input) {
    if (processing) return undefined

    lastRequest = Object.freeze({
      sessionId: input.sessionId,
      orderId: input.orderId,
      submissionKey: keyFactory(),
      message: input.message,
      mode: input.mode,
    })
    return perform(lastRequest)
  }

  async function retrySame() {
    if (processing || !lastRequest) return undefined
    return perform(lastRequest)
  }

  /** @param {Readonly<SubmissionRequest>} request */
  async function perform(request) {
    processing = true
    onProcessingChange(true)
    try {
      return await execute(request)
    } finally {
      processing = false
      onProcessingChange(false)
    }
  }

  function clear() {
    lastRequest = null
  }

  return {
    submitNew,
    retrySame,
    clear,
    get lastRequest() {
      return lastRequest
    },
  }
}

/** @typedef {{ sessionId: string, orderId: string, message: string, mode: 'Replay' | 'Live' }} SubmissionInput */
/** @typedef {SubmissionInput & { submissionKey: string }} SubmissionRequest */
