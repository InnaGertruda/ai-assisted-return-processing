<template>
  <section class="rounded-3xl border border-black/5 bg-white p-6 shadow-[0_18px_60px_rgb(19_35_31/0.07)] sm:p-8" aria-labelledby="simulator-title">
    <div class="flex items-start justify-between gap-4">
      <div>
        <p class="text-xs font-semibold uppercase tracking-[0.16em] text-moss/60">Input</p>
        <h2 id="simulator-title" class="mt-2 text-2xl font-semibold tracking-tight text-ink">Return Simulator</h2>
      </div>
      <span class="rounded-full bg-black/5 px-3 py-1 text-xs font-medium text-ink/55">Not connected</span>
    </div>

    <p class="mt-4 text-sm leading-6 text-ink/60">
      S01 fixture data is available through the AI-1 API. Replay interpretation and this interactive control begin
      in later stages, so it remains intentionally inactive.
    </p>

    <label for="message" class="mt-7 block text-sm font-medium text-ink">Simulated customer message</label>
    <textarea
      id="message"
      disabled
      rows="7"
      placeholder="No scenario is loaded in AI-0."
      class="mt-2 w-full resize-none rounded-2xl border border-black/10 bg-mist px-4 py-3 text-sm text-ink/60 placeholder:text-ink/35 disabled:cursor-not-allowed"
    />

    <button
      type="button"
      disabled
      class="mt-4 w-full cursor-not-allowed rounded-2xl bg-ink px-4 py-3 text-sm font-semibold text-white opacity-35"
    >
      Run verification
    </button>

    <p class="mt-4 text-xs leading-5 text-ink/45">No external AI calls · No AI execution · No new return created</p>
  </section>
</template>
