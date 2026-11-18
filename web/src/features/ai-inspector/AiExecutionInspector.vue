<script setup>
import { computed, ref } from 'vue'
import { businessRuleById, businessRules } from './businessRules.mjs'
import { emptyInspectorState } from './emptyInspectorState.mjs'
import { outcomeExplanation, outcomeKind, outcomeLabel, proposalSummary, stageTone } from './tracePresentation.mjs'

const repository = 'https://github.com/InnaGertruda/ai-assisted-return-processing'
/** @type {Record<string, number>} */
const ruleLines = { 'BR-RET-001': 7, 'BR-RET-002': 8, 'BR-RET-003': 9, 'BR-RET-004': 10, 'BR-RET-005': 11, 'BR-RET-006': 12, 'BR-RET-007': 13, 'BR-RET-008': 14, 'BR-RET-010': 15, 'BR-AUD-001': 16 }
const activeTab = ref('evidence')
const props = defineProps({ execution: { type: Object, default: undefined } })
const trace = computed(() => props.execution?.trace)

/** @param {string} ruleId */
function ruleUrl(ruleId) {
  return `${repository}/blob/main/docs/BUSINESS_RULES.md?plain=1#L${ruleLines[ruleId] ?? 1}`
}
</script>

<template>
  <section class="rounded-3xl bg-ink p-6 text-white shadow-[0_18px_60px_rgb(19_35_31/0.14)] sm:p-8" aria-labelledby="inspector-title">
    <div class="flex flex-wrap items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-[0.16em] text-limewash/60">Actual API evidence</p>
        <h2 id="inspector-title" class="mt-2 text-2xl font-semibold tracking-tight">AI Execution Inspector</h2>
      </div>
      <div class="text-right">
        <span class="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs font-medium text-white/70">{{ outcomeLabel(trace) }}</span>
        <p v-if="trace" class="mt-2 text-[11px] text-limewash/60">{{ trace.mode }} · prompt {{ trace.promptVersion }}</p>
      </div>
    </div>

    <div class="mt-6 flex gap-1 rounded-xl bg-white/[0.045] p-1" role="tablist" aria-label="Inspector views">
      <button id="evidence-tab" type="button" role="tab" :aria-selected="activeTab === 'evidence'" aria-controls="evidence-panel" :class="activeTab === 'evidence' ? 'bg-white/10 text-white' : 'text-white/50 hover:text-white/75'" class="flex-1 rounded-lg px-3 py-2 text-sm font-medium transition" @click="activeTab = 'evidence'">Run evidence</button>
      <button id="rules-tab" type="button" role="tab" :aria-selected="activeTab === 'rules'" aria-controls="rules-panel" :class="activeTab === 'rules' ? 'bg-white/10 text-white' : 'text-white/50 hover:text-white/75'" class="flex-1 rounded-lg px-3 py-2 text-sm font-medium transition" @click="activeTab = 'rules'">Rule catalog <span class="ml-1 text-xs text-white/40">{{ businessRules.length }}</span></button>
    </div>

    <div v-if="activeTab === 'evidence' && !trace" id="evidence-panel" role="tabpanel" aria-labelledby="evidence-tab" class="mt-5 rounded-2xl border border-dashed border-white/15 bg-white/[0.035] px-6 py-12 text-center">
      <div class="mx-auto grid size-12 place-items-center rounded-2xl bg-limewash/10 text-xl text-limewash" aria-hidden="true">—</div>
      <h3 class="mt-5 text-base font-semibold">{{ emptyInspectorState.heading }}</h3>
      <p class="mx-auto mt-2 max-w-sm text-sm leading-6 text-white/50">{{ emptyInspectorState.description }}</p>
      <p class="mx-auto mt-4 max-w-sm text-xs leading-5 text-white/35">The panel stays empty after reset and never shows another session’s trace.</p>
    </div>

    <div v-else-if="activeTab === 'evidence' && trace" id="evidence-panel" role="tabpanel" aria-labelledby="evidence-tab">
      <div class="mt-6 rounded-2xl border border-white/10 bg-white/[0.045] p-4">
        <div class="flex flex-wrap items-center justify-between gap-2">
          <p class="text-xs font-semibold uppercase tracking-[0.14em] text-white/45">Final application outcome</p>
          <span :class="outcomeKind(trace) === 'committed' ? 'text-limewash' : outcomeKind(trace) === 'technical-failure' ? 'text-coral' : 'text-amber-200'" class="text-xs font-semibold">{{ trace.finalOutcome }}</span>
        </div>
        <p class="mt-2 text-sm leading-6 text-white/70">{{ outcomeExplanation(trace) }}</p>
        <p class="mt-2 text-xs text-white/40">Run {{ trace.runId }} · schema {{ trace.schemaVersion }}</p>
        <a :href="`${repository}/blob/main/docs/ARCHITECTURE_DECISIONS.md#adr-006--explainable-results-and-safe-decision-evidence`" target="_blank" rel="noreferrer" class="mt-3 inline-block text-xs font-semibold text-limewash/75 hover:underline">ADR-006 · Explainable results and safe evidence ↗</a>
      </div>

      <div class="mt-4 grid gap-3 sm:grid-cols-2">
        <article class="rounded-xl bg-white/[0.045] p-4 text-sm">
          <p class="text-white/45">AI-shaped proposal · untrusted</p>
          <p class="mt-2 font-semibold">{{ trace.proposal?.intent ?? 'No valid proposal' }}</p>
          <p v-if="trace.proposal?.items?.length" class="mt-1 text-white/60">{{ proposalSummary(trace) }}</p>
          <ul v-if="trace.proposal?.items?.length" class="mt-3 space-y-2 text-xs text-white/50">
            <li v-for="(item, index) in trace.proposal.items" :key="index" class="rounded-lg border border-white/10 p-2">
              Candidate {{ item.candidateOrderItemId ?? 'unresolved' }} · quantity {{ item.requestedQuantity ?? 'unresolved' }} · {{ item.reason }}
            </li>
          </ul>
          <p v-if="trace.proposal?.ambiguities?.length" class="mt-3 text-xs text-amber-200/75">Ambiguities: {{ trace.proposal.ambiguities.join(' · ') }}</p>
        </article>

        <article class="rounded-xl bg-white/[0.045] p-4 text-sm">
          <p class="text-white/45">Authoritative quantity result</p>
          <ul class="mt-2 space-y-2">
            <li v-for="change in execution?.quantityChanges ?? []" :key="change.id" class="flex items-center justify-between gap-3">
              <span class="text-white/65">{{ change.productName }}</span>
              <span class="font-semibold">{{ change.beforeAvailable }} → {{ change.afterAvailable }} available</span>
            </li>
          </ul>
          <p v-if="!execution?.quantityChanges?.length" class="mt-2 text-white/50">No order lines changed.</p>
          <p class="mt-3 text-xs text-white/40">Read from the order API before and after this run.</p>
        </article>
      </div>

      <ol class="mt-5 space-y-3" aria-label="Execution stages">
        <li v-for="(stage, index) in trace.stages" :key="`${stage.stage}-${index}`" class="rounded-xl bg-white/[0.045] p-4 text-sm">
          <div class="flex items-center gap-3">
            <span class="grid size-7 shrink-0 place-items-center rounded-full border border-white/10 text-xs text-white/45">{{ Number(index) + 1 }}</span>
            <div class="min-w-0 flex-1">
              <div class="flex flex-wrap items-center justify-between gap-2"><span class="font-medium">{{ stage.stage }}</span><span :class="stageTone(stage.status)" class="text-xs font-semibold">{{ stage.status }}</span></div>
              <p class="mt-1 text-xs text-white/55">Reason: {{ stage.reasonCodes.length ? stage.reasonCodes.join(' · ') : 'No reason code supplied' }}</p>
            </div>
          </div>

          <dl v-if="stage.safeEvidence.length" class="mt-3 grid gap-2 border-l border-white/10 pl-4 text-xs">
            <div v-for="(evidence, evidenceIndex) in stage.safeEvidence" :key="`${evidence.kind}-${evidenceIndex}`">
              <dt class="text-white/35">{{ evidence.kind }}</dt><dd class="mt-0.5 break-words text-white/65">{{ evidence.value }}</dd>
            </div>
          </dl>

          <div v-if="stage.ruleReferences.length" class="mt-3 flex flex-wrap gap-2">
            <a v-for="ruleId in stage.ruleReferences" :key="ruleId" :href="ruleUrl(ruleId)" target="_blank" rel="noreferrer" class="rounded-full border border-limewash/15 bg-limewash/5 px-2.5 py-1 text-xs font-semibold text-limewash/80 hover:bg-limewash/10" :title="businessRuleById(ruleId)?.description">
              {{ ruleId }} · {{ businessRuleById(ruleId)?.title ?? 'Documented rule' }}
            </a>
          </div>
        </li>
      </ol>

      <div class="mt-5 rounded-xl border border-coral/30 bg-coral/10 p-4">
        <p class="text-xs uppercase tracking-[0.14em] text-coral">Atomic commit boundary</p>
        <p class="mt-1 font-semibold">{{ trace.commitStatus }}</p>
        <p v-if="trace.createdReturnCaseId" class="mt-2 break-all text-xs text-white/70">Created Return Case {{ trace.createdReturnCaseId }}</p>
        <p v-else class="mt-2 text-xs text-white/55">No Return Case ID was created by this run.</p>
      </div>
    </div>

    <div v-else id="rules-panel" role="tabpanel" aria-labelledby="rules-tab" class="mt-5">
      <p class="text-sm leading-6 text-white/55">Documented MVP rules are explanatory references, not evidence that every rule ran. Each run lists only the rules returned by the API trace.</p>
      <div class="mt-4 space-y-3">
        <article v-for="rule in businessRules" :key="rule.id" class="rounded-xl bg-white/[0.045] p-4">
          <div class="flex flex-wrap items-baseline gap-x-3 gap-y-1"><a :href="ruleUrl(rule.id)" target="_blank" rel="noreferrer" class="text-xs font-semibold text-limewash hover:underline">{{ rule.id }}</a><h3 class="text-sm font-semibold">{{ rule.title }}</h3></div>
          <p class="mt-2 text-sm leading-6 text-white/60">{{ rule.description }}</p>
          <dl class="mt-3 grid gap-2 text-xs text-white/40 sm:grid-cols-2"><div><dt class="font-semibold text-white/55">Applies at</dt><dd class="mt-1">{{ rule.appliesAt }}</dd></div><div><dt class="font-semibold text-white/55">Evidence</dt><dd class="mt-1">{{ rule.proof }}</dd></div></dl>
        </article>
      </div>
      <div class="mt-5 flex flex-wrap gap-3 text-xs">
        <a :href="`${repository}/blob/main/docs/ARCHITECTURE_DECISIONS.md`" target="_blank" rel="noreferrer" class="text-limewash hover:underline">Architecture decisions ↗</a>
        <a :href="`${repository}/tree/main/tests`" target="_blank" rel="noreferrer" class="text-limewash hover:underline">Repository tests ↗</a>
      </div>
    </div>
  </section>
</template>
