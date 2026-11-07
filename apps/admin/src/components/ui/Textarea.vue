<script setup lang="ts">
import { cn } from '@/lib/utils'

const model = defineModel<string>({ default: '' })

withDefaults(defineProps<{ id?: string; placeholder?: string; rows?: number; invalid?: boolean }>(), {
  rows: 4,
  invalid: false,
})
</script>

<template>
  <textarea
    :id="id"
    v-model="model"
    :rows="rows"
    :placeholder="placeholder"
    :aria-invalid="invalid || undefined"
    :class="
      cn(
        'w-full resize-y rounded-md border bg-surface px-3 py-2 text-sm leading-relaxed text-ink transition-colors placeholder:text-muted/60 focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/20 disabled:bg-canvas',
        invalid ? 'border-red-400' : 'border-line',
      )
    "
  />
</template>
