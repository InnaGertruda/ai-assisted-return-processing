<script setup>
import { ref } from 'vue'
import { businessRuleById, businessRules } from './businessRules.mjs'
import { emptyInspectorState } from './emptyInspectorState.mjs'
import { outcomeLabel, proposalSummary, stageTone } from './tracePresentation.mjs'

const activeTab = ref('evidence')

defineProps({
  trace: {
    type: Object,
    default: undefined,
  },
})
</script>

<template>
  <section class="rounded-3xl bg-ink p-6 text-white shadow-[0_18px_60px_rgb(19_35_31/0.14)] sm:p-8" aria-labelledby="inspector-title">
    <div class="flex items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-[0.16em] text-limewash/60">Evidence</p>
        <h2 id="inspector-title" class="mt-2 text-2xl font-semibold tracking-tight">AI Execution Inspector</h2>
      </div>
      <span class="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs font-medium text-white/60">
        {{ outcomeLabel(trace) }}
      </span>
    </div>

    <div class="mt-6 flex gap-1 rounded-xl bg-white/[0.045] p-1" role="tablist" aria-label="Inspector views">
      <button
        id="evidence-tab"
        type="button"
        role="tab"
        :aria-selected="activeTab === 'evidence'"
        aria-controls="evidence-panel"
        :class="activeTab === 'evidence' ? 'bg-white/10 text-white' : 'text-white/50 hover:text-white/75'"
        class="flex-1 rounded-lg px-3 py-2 text-sm font-medium transition"
        @click="activeTab = 'evidence'"
      >
        Run evidence
      </button>
      <button
        id="rules-tab"
        type="button"
        role="tab"
        :aria-selected="activeTab === 'rules'"
        aria-controls="rules-panel"
        :class="activeTab === 'rules' ? 'bg-white/10 text-white' : 'text-white/50 hover:text-white/75'"
        class="flex-1 rounded-lg px-3 py-2 text-sm font-medium transition"
        @click="activeTab = 'rules'"
      >
        Rule catalog
        <span class="ml-1 text-xs text-white/40">{{ businessRules.length }}</span>
      </button>
    </div>

    <div
      v-if="activeTab === 'evidence' && !trace"
      id="evidence-panel"
      role="tabpanel"
      aria-labelledby="evidence-tab"
      class="mt-5 rounded-2xl border border-dashed border-white/15 bg-white/[0.035] px-6 py-12 text-center"
    >
      <div class="mx-auto grid size-12 place-items-center rounded-2xl bg-limewash/10 text-xl text-limewash" aria-hidden="true">—</div>
      <h3 class="mt-5 text-base font-semibold">{{ emptyInspectorState.heading }}</h3>
      <p class="mx-auto mt-2 max-w-sm text-sm leading-6 text-white/50">
        {{ emptyInspectorState.description }}
      </p>
    </div>

    <div
      v-else-if="activeTab === 'evidence' && trace"
      id="evidence-panel"
      role="tabpanel"
      aria-labelledby="evidence-tab"
    >
      <div class="mt-6 rounded-2xl bg-white/[0.045] p-4 text-sm">
        <p class="text-white/45">Model proposal · untrusted</p>
        <p class="mt-2 font-medium">{{ trace.proposal?.intent ?? 'No valid proposal' }}</p>
        <p v-if="trace.proposal?.items.length" class="mt-1 text-white/60">
          {{ proposalSummary(trace) }}
        </p>
      </div>

      <ol class="mt-5 space-y-3" aria-label="Execution stages">
        <li v-for="stage in trace.stages" :key="stage.stage" class="rounded-xl bg-white/[0.045] p-4 text-sm">
          <div class="flex items-center justify-between gap-3">
            <span class="font-medium">{{ stage.stage }}</span>
            <span :class="stageTone(stage.status)" class="text-xs font-semibold">{{ stage.status }}</span>
          </div>
          <p class="mt-2 text-xs text-white/55">{{ stage.reasonCodes.join(' · ') }}</p>
          <div v-if="stage.ruleReferences.length" class="mt-3 space-y-2">
            <details
              v-for="ruleId in stage.ruleReferences"
              :key="ruleId"
              class="rounded-lg border border-white/10 bg-black/10 px-3 py-2"
            >
              <summary class="cursor-pointer text-xs font-semibold text-limewash/80">
                {{ ruleId }} · {{ businessRuleById(ruleId)?.title ?? 'Rule description unavailable' }}
              </summary>
              <p class="mt-2 text-xs leading-5 text-white/60">
                {{ businessRuleById(ruleId)?.description ?? 'This trace references a rule that is not present in the current catalog.' }}
              </p>
              <p v-if="businessRuleById(ruleId)" class="mt-2 text-[11px] text-white/40">
                Applies at: {{ businessRuleById(ruleId)?.appliesAt }}
              </p>
            </details>
          </div>
        </li>
      </ol>

      <div class="mt-5 rounded-xl border border-coral/30 bg-coral/10 p-4">
        <p class="text-xs uppercase tracking-[0.14em] text-coral">Commit boundary</p>
        <p class="mt-1 font-semibold">{{ trace.commitStatus }}</p>
        <p v-if="trace.createdReturnCaseId" class="mt-2 text-xs text-white/60">
          Return Case {{ trace.createdReturnCaseId }}
        </p>
      </div>
    </div>

    <div v-else id="rules-panel" role="tabpanel" aria-labelledby="rules-tab" class="mt-5">
      <p class="text-sm leading-6 text-white/55">
        These are the documented MVP rules. A trace references the rules involved in each check; it does not imply that every rule ran.
      </p>
      <div class="mt-4 space-y-3">
        <article v-for="rule in businessRules" :key="rule.id" class="rounded-xl bg-white/[0.045] p-4">
          <div class="flex flex-wrap items-baseline gap-x-3 gap-y-1">
            <span class="text-xs font-semibold text-limewash">{{ rule.id }}</span>
            <h3 class="text-sm font-semibold">{{ rule.title }}</h3>
          </div>
          <p class="mt-2 text-sm leading-6 text-white/60">{{ rule.description }}</p>
          <dl class="mt-3 grid gap-2 text-xs text-white/40 sm:grid-cols-2">
            <div>
              <dt class="font-semibold text-white/55">Applies at</dt>
              <dd class="mt-1">{{ rule.appliesAt }}</dd>
            </div>
            <div>
              <dt class="font-semibold text-white/55">Evidence</dt>
              <dd class="mt-1">{{ rule.proof }}</dd>
            </div>
          </dl>
        </article>
      </div>
    </div>
  </section>
</template>
