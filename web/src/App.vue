<script setup>
import { ref } from 'vue'
import AiExecutionInspector from './features/ai-inspector/AiExecutionInspector.vue'
import ReturnSimulator from './features/return-simulator/ReturnSimulator.vue'

/** @type {import('vue').Ref<Record<string, any> | undefined>} */
const trace = ref(undefined)
</script>

<template>
  <div class="min-h-screen">
    <header class="border-b border-black/5 bg-white/65 backdrop-blur">
      <div class="mx-auto flex max-w-7xl items-center justify-between px-5 py-4 lg:px-8">
        <a href="#main" class="font-semibold tracking-tight text-ink">ReturnFlow</a>
        <span class="rounded-full border border-moss/15 bg-limewash/45 px-3 py-1 text-xs font-medium text-moss">
          Architecture demo · AI-3 · Replay
        </span>
      </div>
    </header>

    <main id="main" class="mx-auto max-w-7xl px-5 py-10 lg:px-8 lg:py-16">
      <section class="max-w-3xl" aria-labelledby="page-title">
        <p class="mb-3 text-sm font-semibold uppercase tracking-[0.18em] text-coral">Fictional data only</p>
        <h1 id="page-title" class="text-4xl font-semibold tracking-[-0.04em] text-ink sm:text-5xl">
          See the boundary between an AI proposal and a trusted decision.
        </h1>
        <p class="mt-5 max-w-2xl text-base leading-7 text-ink/65">
          Run a fixed Replay scenario to inspect an untrusted proposal, independent verification, and deterministic
          preflight, and the atomic SQLite commit boundary. Accepted appears only after a durable Return Case commit.
        </p>
      </section>

      <div class="mt-10 grid gap-6 lg:grid-cols-[minmax(0,0.92fr)_minmax(0,1.08fr)]">
        <ReturnSimulator @trace="trace = $event" />
        <AiExecutionInspector :trace="trace" />
      </div>
    </main>
  </div>
</template>
