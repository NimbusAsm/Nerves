<script setup lang="ts">
import { CircleCheck, CircleExclamationPoint, Info, X } from '@lucide/vue'

import { useToastStore, type ToastTone } from '@/stores/toast'
import { cn } from '@/lib/utils'

const toasts = useToastStore()

const ICONS = {
  success: CircleCheck,
  error: CircleExclamationPoint,
  info: Info,
}

function iconFor(tone: ToastTone) {
  return ICONS[tone]
}

const TONES: Record<ToastTone, string> = {
  success: 'text-emerald-600',
  error: 'text-red-600',
  info: 'text-brand',
}
</script>

<template>
  <div
    class="pointer-events-none fixed bottom-4 right-4 z-[60] flex w-full max-w-sm flex-col gap-2"
    aria-live="polite"
  >
    <TransitionGroup name="toast">
      <div
        v-for="toast in toasts.items"
        :key="toast.id"
        class="pointer-events-auto flex items-start gap-3 rounded-lg border border-line bg-surface p-3 shadow-lg"
      >
        <component :is="iconFor(toast.tone)" :class="cn('mt-0.5 h-4 w-4 shrink-0', TONES[toast.tone])" aria-hidden="true" />
        <div class="min-w-0 flex-1">
          <p class="text-sm font-medium text-ink">{{ toast.title }}</p>
          <p v-if="toast.description" class="mt-0.5 text-xs leading-relaxed text-muted">{{ toast.description }}</p>
        </div>
        <button
          type="button"
          class="rounded p-0.5 text-muted transition-colors hover:bg-canvas hover:text-ink"
          :aria-label="$t('common.close')"
          @click="toasts.dismiss(toast.id)"
        >
          <X class="h-3.5 w-3.5" />
        </button>
      </div>
    </TransitionGroup>
  </div>
</template>

<style scoped>
.toast-enter-active,
.toast-leave-active {
  transition: opacity 150ms ease, transform 150ms ease;
}

.toast-enter-from,
.toast-leave-to {
  opacity: 0;
  transform: translateY(8px);
}
</style>
