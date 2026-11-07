<script setup lang="ts">
withDefaults(
  defineProps<{
    title?: string
    description?: string
    padded?: boolean
  }>(),
  { padded: true },
)
</script>

<template>
  <section class="overflow-hidden rounded-lg border border-line bg-surface shadow-xs">
    <header
      v-if="title || description || $slots.header || $slots.actions"
      class="flex flex-wrap items-start justify-between gap-3 border-b border-line px-4 py-3"
    >
      <div class="min-w-0">
        <slot name="header">
          <h2 class="text-sm font-semibold text-ink">{{ title }}</h2>
          <p v-if="description" class="mt-0.5 text-xs leading-relaxed text-muted">{{ description }}</p>
        </slot>
      </div>
      <div v-if="$slots.actions" class="flex shrink-0 items-center gap-2">
        <slot name="actions" />
      </div>
    </header>
    <div :class="padded ? 'p-4' : ''">
      <slot />
    </div>
    <footer v-if="$slots.footer" class="border-t border-line bg-canvas/50 px-4 py-3">
      <slot name="footer" />
    </footer>
  </section>
</template>
