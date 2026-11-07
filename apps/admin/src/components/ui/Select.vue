<script setup lang="ts">
import { ChevronDown } from '@lucide/vue'

const model = defineModel<string>({ default: '' })

withDefaults(defineProps<{ id?: string; disabled?: boolean }>(), { disabled: false })
</script>

<template>
  <div class="relative">
    <select
      :id="id"
      v-model="model"
      :disabled="disabled"
      class="h-9 w-full appearance-none rounded-md border border-line bg-surface pl-3 pr-8 text-sm text-ink transition-colors focus:border-brand focus:outline-none focus:ring-2 focus:ring-brand/20 disabled:cursor-not-allowed disabled:bg-canvas disabled:text-muted"
    >
      <slot />
    </select>
    <ChevronDown
      class="pointer-events-none absolute right-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted"
      aria-hidden="true"
    />
  </div>
</template>
