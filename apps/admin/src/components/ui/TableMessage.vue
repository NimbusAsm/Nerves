<script setup lang="ts">
import { CircleExclamationPoint, RefreshCw } from '@lucide/vue'

withDefaults(
  defineProps<{
    colspan: number
    loading?: boolean
    error?: string | null
    empty?: boolean
    emptyText?: string
  }>(),
  { loading: false, error: null, empty: false },
)

const emit = defineEmits<{ retry: [] }>()
</script>

<template>
  <tr v-if="loading">
    <td :colspan="colspan" class="px-4 py-12 text-center text-sm text-muted">
      <span class="inline-flex items-center gap-2">
        <RefreshCw class="h-4 w-4 animate-spin" aria-hidden="true" />
        {{ $t('common.loading') }}
      </span>
    </td>
  </tr>
  <tr v-else-if="error">
    <td :colspan="colspan" class="px-4 py-12 text-center text-sm">
      <span class="inline-flex flex-col items-center gap-2 text-red-600">
        <span class="inline-flex items-center gap-2">
          <CircleExclamationPoint class="h-4 w-4" aria-hidden="true" />
          {{ error }}
        </span>
        <button type="button" class="text-xs text-brand underline" @click="emit('retry')">
          {{ $t('common.retry') }}
        </button>
      </span>
    </td>
  </tr>
  <tr v-else-if="empty">
    <td :colspan="colspan" class="px-4 py-12 text-center text-sm text-muted">
      {{ emptyText ?? $t('common.emptyText') }}
    </td>
  </tr>
</template>
