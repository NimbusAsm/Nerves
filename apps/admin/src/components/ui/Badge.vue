<script setup lang="ts">
import { computed } from 'vue'

import { cn } from '@/lib/utils'
import type { Tone } from './tone'

const props = withDefaults(defineProps<{ tone?: Tone; size?: 'sm' | 'md' }>(), {
  tone: 'neutral',
  size: 'md',
})

const TONES: Record<Tone, string> = {
  neutral: 'bg-slate-100 text-slate-600',
  success: 'bg-emerald-50 text-emerald-700',
  warning: 'bg-amber-50 text-amber-700',
  danger: 'bg-red-50 text-red-700',
  info: 'bg-sky-50 text-sky-700',
  brand: 'bg-brand-soft text-brand',
}

const classes = computed(() =>
  cn(
    'inline-flex items-center gap-1 rounded-full font-medium',
    props.size === 'sm' ? 'px-2 py-0.5 text-[11px]' : 'px-2.5 py-0.5 text-xs',
    TONES[props.tone],
  ),
)
</script>

<template>
  <span :class="classes"><slot /></span>
</template>
