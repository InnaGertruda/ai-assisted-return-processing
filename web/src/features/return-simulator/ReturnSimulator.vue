<script setup>
import { computed, onMounted, ref } from 'vue'
import { requestBodyPreview } from './scenarioPresentation.mjs'

const emit = defineEmits(['trace'])
const apiBase = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/$/, '')
/** @type {import('vue').Ref<Array<{ id: string, name: string, description: string, fixtureVersion: string, message: string, expectedOutcome: string, expectedExplanation: string, order: { displayNumber: string, items: Array<{ productCode: string, productName: string, purchasedQuantity: number, activeAllocatedQuantity: number, completedConsumedQuantity: number, availableQuantity: number }> } }>>} */
const scenarios = ref([])
const selectedId = ref('S01')
/** @type {import('vue').Ref<null | { sessionId: string, order: { id: string } }>} */
const session = ref(null)
const catalogLoading = ref(true)
const loading = ref(false)
const error = ref('')

const selectedScenario = computed(() => scenarios.value.find((item) => item.id === selectedId.value))
const message = computed(() => selectedScenario.value?.message ?? '')
const requestPreview = computed(() => requestBodyPreview(selectedScenario.value))

onMounted(async () => {
  try {
    const response = await fetch(`${apiBase}/api/demo/scenarios`)
    if (!response.ok) throw new Error('Scenario catalog could not be loaded.')
    scenarios.value = await response.json()
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'Scenario catalog could not be loaded.'
  } finally {
    catalogLoading.value = false
  }
})

async function run() {
  loading.value = true
  error.value = ''
  try {
    const sessionResponse = await fetch(`${apiBase}/api/demo/sessions`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ scenarioId: selectedId.value }),
    })
    if (!sessionResponse.ok) throw new Error('A fictional demo session could not be created.')
    session.value = await sessionResponse.json()
    if (!session.value) throw new Error('The demo session response was empty.')

    const runResponse = await fetch(`${apiBase}/api/demo/sessions/${session.value.sessionId}/ai-runs`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        selectedOrderId: session.value.order.id,
        submissionKey: crypto.randomUUID(),
        message: message.value,
        mode: 'Replay',
      }),
    })
    if (!runResponse.ok) throw new Error('The Replay verification run failed.')
    emit('trace', await runResponse.json())
  } catch (reason) {
    error.value = reason instanceof Error ? reason.message : 'The Replay verification run failed.'
  } finally {
    loading.value = false
  }
}
</script>

<template>
  <section class="rounded-3xl border border-black/5 bg-white p-6 shadow-[0_18px_60px_rgb(19_35_31/0.07)] sm:p-8" aria-labelledby="simulator-title">
    <div class="flex items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-[0.16em] text-moss/60">Input</p>
        <h2 id="simulator-title" class="mt-2 text-2xl font-semibold tracking-tight text-ink">Return Simulator</h2>
      </div>
      <span class="rounded-full bg-limewash px-3 py-1 text-xs font-medium text-moss">Replay only</span>
    </div>

    <p class="mt-4 text-sm leading-6 text-ink/60">
      Select a fixed fictional fixture. Replay accepts only its exact versioned message and makes no external calls.
    </p>

    <label for="scenario" class="mt-7 block text-sm font-medium text-ink">Replay scenario</label>
    <select
      id="scenario"
      v-model="selectedId"
      :disabled="catalogLoading || !scenarios.length"
      class="mt-2 w-full rounded-2xl border border-black/10 bg-mist px-4 py-3 text-sm disabled:cursor-not-allowed disabled:opacity-60"
    >
      <option v-if="catalogLoading" disabled value="S01">Loading Replay scenarios…</option>
      <option v-else-if="!scenarios.length" disabled value="S01">Replay scenarios unavailable</option>
      <option v-for="scenario in scenarios" :key="scenario.id" :value="scenario.id">
        {{ scenario.id }} · {{ scenario.name }}
      </option>
    </select>
    <p class="mt-2 text-xs leading-5 text-ink/45">{{ selectedScenario?.description }}</p>

    <section v-if="selectedScenario" class="mt-5 overflow-hidden rounded-2xl border border-black/10" aria-labelledby="briefing-title">
      <div class="border-b border-black/5 bg-limewash/45 px-4 py-3">
        <p class="text-[11px] font-semibold uppercase tracking-[0.14em] text-moss/65">Before you run</p>
        <h3 id="briefing-title" class="mt-1 text-base font-semibold text-ink">Scenario briefing</h3>
      </div>

      <div class="space-y-5 p-4">
        <div>
          <div class="flex items-center justify-between gap-3">
            <h4 class="text-sm font-semibold text-ink">Order {{ selectedScenario.order.displayNumber }}</h4>
            <span class="text-xs text-ink/45">Fictional fixture</span>
          </div>
          <ul class="mt-3 space-y-2" aria-label="Order items">
            <li
              v-for="item in selectedScenario.order.items"
              :key="item.productCode"
              class="rounded-xl bg-mist px-3 py-3"
            >
              <div class="flex items-baseline justify-between gap-3">
                <span class="text-sm font-medium text-ink">{{ item.productName }}</span>
                <span class="text-xs text-ink/45">{{ item.productCode }}</span>
              </div>
              <dl class="mt-2 grid grid-cols-3 gap-2 text-xs">
                <div>
                  <dt class="text-ink/45">Purchased</dt>
                  <dd class="mt-0.5 font-semibold text-ink">{{ item.purchasedQuantity }}</dd>
                </div>
                <div>
                  <dt class="text-ink/45">Already allocated</dt>
                  <dd class="mt-0.5 font-semibold text-ink">{{ item.activeAllocatedQuantity }}</dd>
                </div>
                <div>
                  <dt class="text-ink/45">Available</dt>
                  <dd class="mt-0.5 font-semibold text-moss">{{ item.availableQuantity }}</dd>
                </div>
              </dl>
            </li>
          </ul>
        </div>

        <div class="grid gap-3 sm:grid-cols-2">
          <div class="rounded-xl border border-black/5 p-3">
            <p class="text-xs font-medium text-ink/45">Customer message</p>
            <p class="mt-2 text-sm leading-5 text-ink">“{{ message }}”</p>
          </div>
          <div class="rounded-xl border border-black/5 p-3">
            <p class="text-xs font-medium text-ink/45">Expected demonstration result</p>
            <p class="mt-2 text-sm font-semibold text-ink">{{ selectedScenario.expectedOutcome }}</p>
            <p class="mt-1 text-xs leading-5 text-ink/60">{{ selectedScenario.expectedExplanation }}</p>
            <p class="mt-2 text-[11px] text-ink/40">Expectation only · actual trace is shown after Run</p>
          </div>
        </div>

        <details open class="rounded-xl border border-black/5 px-3 py-3">
          <summary class="cursor-pointer text-sm font-medium text-ink">Request sent on Run</summary>
          <p class="mt-2 text-xs leading-5 text-ink/45">
            POST /api/demo/sessions/&lt;created on Run&gt;/ai-runs
          </p>
          <pre class="mt-2 overflow-x-auto rounded-lg bg-ink p-3 text-[11px] leading-5 text-limewash">{{ requestPreview }}</pre>
        </details>
      </div>
    </section>

    <button
      type="button"
      :disabled="catalogLoading || loading || !scenarios.length"
      class="mt-5 w-full rounded-2xl bg-ink px-4 py-3 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-35"
      @click="run"
    >
      {{ loading ? 'Running verification…' : 'Run Replay verification' }}
    </button>

    <p v-if="error" role="alert" class="mt-4 text-sm text-coral">{{ error }}</p>
    <p class="mt-4 text-xs leading-5 text-ink/45">No external AI calls · Proposal only · No new return created</p>
  </section>
</template>
