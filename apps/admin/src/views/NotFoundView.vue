<script setup lang="ts">
import { CircleQuestionMark } from '@lucide/vue'
import { RouterLink } from 'vue-router'

import { buttonVariants } from '@/components/ui/button-variants'
import { cn } from '@/lib/utils'
</script>

<template>
  <div class="flex min-h-[60vh] flex-col items-center justify-center text-center">
    <span class="grid h-12 w-12 place-items-center rounded-full bg-slate-100 text-slate-500">
      <CircleQuestionMark class="h-6 w-6" aria-hidden="true" />
    </span>
    <h1 class="mt-4 text-xl font-semibold text-ink">{{ $t('errors.notFoundTitle') }}</h1>
    <p class="mt-2 max-w-md text-sm leading-relaxed text-muted">{{ $t('errors.notFoundDescription') }}</p>
    <RouterLink :to="{ name: 'dashboard' }" :class="cn(buttonVariants({ variant: 'secondary' }), 'mt-5')">
      {{ $t('errors.backHome') }}
    </RouterLink>
  </div>
</template>
