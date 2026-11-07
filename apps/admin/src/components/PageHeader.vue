<script setup lang="ts">
import { ArrowLeft } from '@lucide/vue'
import { RouterLink } from 'vue-router'
import type { RouteLocationRaw } from 'vue-router'

defineProps<{
  title: string
  description?: string
  backTo?: RouteLocationRaw
  backLabel?: string
}>()
</script>

<template>
  <header class="mb-5 flex flex-wrap items-start justify-between gap-3">
    <div class="min-w-0">
      <RouterLink
        v-if="backTo"
        :to="backTo"
        class="mb-2 inline-flex items-center gap-1 text-xs text-muted transition-colors hover:text-ink"
      >
        <ArrowLeft class="h-3.5 w-3.5" aria-hidden="true" />
        {{ backLabel ?? $t('common.back') }}
      </RouterLink>
      <h1 class="text-xl font-semibold tracking-tight text-ink">{{ title }}</h1>
      <p v-if="description" class="mt-1 max-w-3xl text-sm leading-relaxed text-muted">{{ description }}</p>
    </div>
    <div class="flex shrink-0 flex-wrap items-center gap-2">
      <slot name="actions" />
    </div>
  </header>
</template>
