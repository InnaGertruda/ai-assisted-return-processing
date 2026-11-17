<script setup>
import { computed, onMounted, ref, watch } from 'vue'
import { createSession, getOrder, getReturnCases, getScenarioCatalog, resetSession, runReplay } from './demoApi.mjs'
import { requestBodyPreview } from './scenarioPresentation.mjs'
import { quantityChanges } from './scenarioPresentation.mjs'

const emit = defineEmits(['execution'])
const apiBase = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
/** @type {import('vue').Ref<any[]>} */
const scenarios = ref([])
const selectedId = ref('S01')
/** @type {import('vue').Ref<any>} */
const session = ref(null)
/** @type {import('vue').Ref<any>} */
const order = ref(null)
/** @type {import('vue').Ref<any[]>} */
const returnCases = ref([])
/** @type {import('vue').Ref<any>} */
const lastExecution = ref(null)
const catalogLoading = ref(true)
const sessionLoading = ref(false)
const running = ref(false)
const resetting = ref(false)
const error = ref('')
let sessionRequest = 0

const selectedScenario = computed(() => scenarios.value.find((item) => item.id === selectedId.value))
const message = computed(() => selectedScenario.value?.message ?? '')
const requestPreview = computed(() => requestBodyPreview(selectedScenario.value))
const busy = computed(() => catalogLoading.value || sessionLoading.value || running.value || resetting.value)

onMounted(async () => {
  try {
    scenarios.value = await getScenarioCatalog(fetch, apiBase)
    if (scenarios.value.length && !scenarios.value.some((item) => item.id === selectedId.value)) {
      selectedId.value = scenarios.value[0].id
    }
    await startSession()
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'The simulator could not be initialized.'
  } finally {
    catalogLoading.value = false
  }
})

watch(selectedId, async () => {
  if (!catalogLoading.value) await startSession()
})

async function startSession() {
  const requestId = ++sessionRequest
  sessionLoading.value = true
  error.value = ''
  session.value = null
  order.value = null
  returnCases.value = []
  lastExecution.value = null
  emit('execution', undefined)

  try {
    const created = await createSession(fetch, apiBase, selectedId.value)
    if (requestId !== sessionRequest) return
    session.value = created
    order.value = created.order
    returnCases.value = await getReturnCases(fetch, apiBase, created.sessionId)
  } catch (reason) {
    if (requestId === sessionRequest) {
      error.value = reason instanceof Error ? reason.message : 'A fictional demo session could not be created.'
    }
  } finally {
    if (requestId === sessionRequest) sessionLoading.value = false
  }
}

async function run() {
  if (!session.value || !order.value) return
  running.value = true
  error.value = ''
  const activeSession = session.value
  const beforeOrder = JSON.parse(JSON.stringify(order.value))

  try {
    const trace = await runReplay(fetch, apiBase, {
      sessionId: activeSession.sessionId,
      orderId: activeSession.order.id,
      submissionKey: crypto.randomUUID(),
      message: message.value,
    })
    const [afterOrder, cases] = await Promise.all([
      getOrder(fetch, apiBase, activeSession.sessionId, activeSession.order.id),
      getReturnCases(fetch, apiBase, activeSession.sessionId),
    ])
    order.value = afterOrder
    returnCases.value = cases
    lastExecution.value = {
      trace,
      beforeOrder,
      afterOrder,
      returnCases: cases,
      quantityChanges: quantityChanges(beforeOrder, afterOrder),
    }
    emit('execution', lastExecution.value)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'The Replay run could not be completed.'
  } finally {
    running.value = false
  }
}

async function reset() {
  if (!session.value) return
  resetting.value = true
  error.value = ''
  try {
    const restored = await resetSession(fetch, apiBase, session.value.sessionId)
    session.value = restored
    order.value = restored.order
    returnCases.value = await getReturnCases(fetch, apiBase, restored.sessionId)
    lastExecution.value = null
    emit('execution', undefined)
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'The fictional demo session could not be reset.'
  } finally {
    resetting.value = false
  }
}
</script>

<template>
  <section class="rounded-3xl border border-black/5 bg-white p-6 shadow-[0_18px_60px_rgb(19_35_31/0.07)] sm:p-8" aria-labelledby="simulator-title">
    <div class="flex items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-[0.16em] text-moss/60">Interactive input</p>
        <h2 id="simulator-title" class="mt-2 text-2xl font-semibold tracking-tight text-ink">Return Simulator</h2>
      </div>
      <span class="rounded-full bg-limewash px-3 py-1 text-xs font-semibold text-moss">Replay · no AI call</span>
    </div>

    <p class="mt-4 text-sm leading-6 text-ink/60">
      Choose a fixed fictional scenario. Runs and reset stay inside one opaque visitor session until you choose another scenario.
    </p>

    <div class="mt-7 grid gap-4 sm:grid-cols-[1fr_auto] sm:items-end">
      <div>
        <label for="scenario" class="block text-sm font-medium text-ink">Replay scenario</label>
        <select id="scenario" v-model="selectedId" :disabled="busy || !scenarios.length" class="mt-2 w-full rounded-2xl border border-black/10 bg-mist px-4 py-3 text-sm disabled:cursor-not-allowed disabled:opacity-60">
          <option v-if="catalogLoading" disabled value="S01">Loading Replay scenarios…</option>
          <option v-else-if="!scenarios.length" disabled value="S01">Replay scenarios unavailable</option>
          <option v-for="scenarioItem in scenarios" :key="scenarioItem.id" :value="scenarioItem.id">
            {{ scenarioItem.id }} · {{ scenarioItem.name }}
          </option>
        </select>
      </div>
      <button type="button" :disabled="busy || !session" class="rounded-2xl border border-black/10 px-4 py-3 text-sm font-semibold text-ink disabled:cursor-not-allowed disabled:opacity-35" @click="reset">
        {{ resetting ? 'Resetting…' : 'Reset session' }}
      </button>
    </div>
    <p class="mt-2 text-xs leading-5 text-ink/45">{{ selectedScenario?.description }}</p>

    <div v-if="sessionLoading" role="status" class="mt-5 rounded-2xl bg-mist p-5 text-sm text-ink/60">Creating an isolated fictional session…</div>

    <section v-else-if="order" class="mt-5 overflow-hidden rounded-2xl border border-black/10" aria-labelledby="order-title">
      <div class="flex flex-wrap items-center justify-between gap-2 border-b border-black/5 bg-limewash/45 px-4 py-3">
        <div>
          <p class="text-[11px] font-semibold uppercase tracking-[0.14em] text-moss/65">Authoritative API state</p>
          <h3 id="order-title" class="mt-1 text-base font-semibold text-ink">Order {{ order.displayNumber }}</h3>
        </div>
        <span class="text-xs text-ink/45">{{ returnCases.length }} return case{{ returnCases.length === 1 ? '' : 's' }}</span>
      </div>

      <div class="space-y-5 p-4">
        <ul class="space-y-2" aria-label="Current order availability">
          <li v-for="item in order.items" :key="item.id" class="rounded-xl bg-mist px-3 py-3">
            <div class="flex items-baseline justify-between gap-3"><span class="text-sm font-medium text-ink">{{ item.productName }}</span><span class="text-xs text-ink/45">{{ item.productCode }}</span></div>
            <dl class="mt-2 grid grid-cols-2 gap-2 text-xs sm:grid-cols-4">
              <div><dt class="text-ink/45">Purchased</dt><dd class="mt-0.5 font-semibold">{{ item.purchasedQuantity }}</dd></div>
              <div><dt class="text-ink/45">Active</dt><dd class="mt-0.5 font-semibold">{{ item.activeAllocatedQuantity }}</dd></div>
              <div><dt class="text-ink/45">Completed</dt><dd class="mt-0.5 font-semibold">{{ item.completedConsumedQuantity }}</dd></div>
              <div><dt class="text-ink/45">Available now</dt><dd class="mt-0.5 font-semibold text-moss">{{ item.availableQuantity }}</dd></div>
            </dl>
          </li>
        </ul>

        <div class="rounded-xl border border-black/5 p-3">
          <div class="flex items-center justify-between gap-3"><p class="text-xs font-medium text-ink/45">Fixed customer message</p><span class="text-[11px] text-ink/40">Fixture v{{ selectedScenario?.fixtureVersion }}</span></div>
          <p class="mt-2 text-sm leading-5 text-ink">“{{ message }}”</p>
          <p class="mt-2 text-[11px] leading-4 text-ink/40">Replay accepts this exact text only; arbitrary text is not presented as model output.</p>
        </div>

        <details class="rounded-xl border border-black/5 px-3 py-3">
          <summary class="cursor-pointer text-sm font-medium text-ink">API request shape</summary>
          <pre class="mt-2 overflow-x-auto rounded-lg bg-ink p-3 text-[11px] leading-5 text-limewash">{{ requestPreview }}</pre>
        </details>
      </div>
    </section>

    <div class="mt-5 grid gap-3 sm:grid-cols-[1fr_auto]">
      <button type="button" :disabled="busy || !session || !order" class="rounded-2xl bg-ink px-4 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-35" @click="run">
        {{ running ? 'Running guarded pipeline…' : lastExecution ? 'Run again in this session' : 'Run Replay pipeline' }}
      </button>
      <div class="rounded-2xl border border-black/10 px-4 py-2 text-xs text-ink/50"><span class="block font-semibold text-ink/65">Mode</span>Replay <span aria-label="Live mode unavailable">· Live unavailable</span></div>
    </div>

    <p v-if="error" role="alert" class="mt-4 rounded-xl bg-coral/10 px-4 py-3 text-sm text-coral">{{ error }}</p>
    <p class="mt-4 text-xs leading-5 text-ink/45">Simulation only · no refund, shipment, payment, or legal eligibility decision · Accepted means only that a fictional Return Case committed</p>
  </section>
</template>
