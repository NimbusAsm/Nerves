<script setup lang="ts">
import { ChevronLeft, ChevronRight } from '@lucide/vue'

import { cn } from '@/lib/utils'
import { buttonVariants } from './ui/button-variants'

defineProps<{
  pageIndex: number
  hasPrev: boolean
  hasNext: boolean
  count: number
  loading?: boolean
}>()

const emit = defineEmits<{ prev: []; next: [] }>()
</script>

<template>
  <div class="flex flex-wrap items-center justify-between gap-3 border-t border-line px-4 py-3">
    <p class="text-xs text-muted">
      {{ $t('common.pageInfo', { page: pageIndex + 1, count }) }}
    </p>
    <div class="flex items-center gap-1.5">
      <button
        type="button"
        :class="cn(buttonVariants({ variant: 'secondary', size: 'sm' }))"
        :disabled="!hasPrev || loading"
        @click="emit('prev')"
      >
        <ChevronLeft class="h-3.5 w-3.5" aria-hidden="true" />
        {{ $t('common.prev') }}
      </button>
      <button
        type="button"
        :class="cn(buttonVariants({ variant: 'secondary', size: 'sm' }))"
        :disabled="!hasNext || loading"
        @click="emit('next')"
      >
        {{ $t('common.next') }}
        <ChevronRight class="h-3.5 w-3.5" aria-hidden="true" />
      </button>
    </div>
  </div>
</template>
