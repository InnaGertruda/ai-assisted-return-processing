<script setup lang="ts">
import { emptyInspectorState } from './emptyInspectorState.mjs'
</script>

<template>
  <section class="rounded-3xl bg-ink p-6 text-white shadow-[0_18px_60px_rgb(19_35_31/0.14)] sm:p-8" aria-labelledby="inspector-title">
    <div class="flex items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-[0.16em] text-limewash/60">Evidence</p>
        <h2 id="inspector-title" class="mt-2 text-2xl font-semibold tracking-tight">AI Execution Inspector</h2>
      </div>
      <span class="rounded-full border border-white/10 bg-white/5 px-3 py-1 text-xs font-medium text-white/60">
        {{ emptyInspectorState.status }}
      </span>
    </div>

    <div class="mt-8 rounded-2xl border border-dashed border-white/15 bg-white/[0.035] px-6 py-12 text-center">
      <div class="mx-auto grid size-12 place-items-center rounded-2xl bg-limewash/10 text-xl text-limewash" aria-hidden="true">—</div>
      <h3 class="mt-5 text-base font-semibold">{{ emptyInspectorState.heading }}</h3>
      <p class="mx-auto mt-2 max-w-sm text-sm leading-6 text-white/50">
        {{ emptyInspectorState.description }}
      </p>
    </div>

    <dl class="mt-6 grid grid-cols-2 gap-3 text-xs sm:grid-cols-4">
      <div v-for="label in ['Model proposal', 'Verification', 'Business decision', 'Commit']" :key="label" class="rounded-xl bg-white/[0.045] p-3">
        <dt class="text-white/45">{{ label }}</dt>
        <dd class="mt-1 font-medium text-white/75">{{ emptyInspectorState.status }}</dd>
      </div>
    </dl>
  </section>
</template>
