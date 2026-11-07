<script setup lang="ts">
import { cn } from '@/lib/utils'

const model = defineModel<string>({ default: '' })

withDefaults(
  defineProps<{
    id?: string
    type?: string
    placeholder?: string
    autocomplete?: string
    disabled?: boolean
    invalid?: boolean
  }>(),
  { type: 'text', disabled: false, invalid: false },
)
</script>

<template>
  <input
    :id="id"
    v-model="model"
    :type="type"
    :placeholder="placeholder"
    :autocomplete="autocomplete"
    :disabled="disabled"
    :aria-invalid="invalid || undefined"
    :class="
      cn(
        'h-9 w-full rounded-md border bg-surface px-3 text-sm text-ink transition-colors placeholder:text-muted/60 focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/20 disabled:cursor-not-allowed disabled:bg-canvas disabled:text-muted',
        invalid ? 'border-red-400 focus:border-red-500 focus:ring-red-500/20' : 'border-line',
      )
    "
  />
</template>
