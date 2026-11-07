<script setup lang="ts">
import { X } from '@lucide/vue'
import { computed } from 'vue'
import { RouterLink, useRoute } from 'vue-router'

import { apiMode } from '@/api'
import { useAuthStore } from '@/stores/auth'
import { NAV_GROUPS } from './navigation'

defineProps<{ open: boolean }>()
const emit = defineEmits<{ close: [] }>()

const auth = useAuthStore()
const route = useRoute()

function isActive(item: { name: string; path: string }): boolean {
  if (item.path === '/') return route.path === '/'
  return route.path === item.path || route.path.startsWith(`${item.path}/`)
}

const groups = computed(() =>
  NAV_GROUPS.map((group) => ({
    ...group,
    items: group.items.filter((item) => auth.can(item.permission)),
  })).filter((group) => group.items.length > 0),
)
</script>

<template>
  <aside
    class="fixed inset-y-0 left-0 z-40 flex w-[248px] flex-col border-r border-line bg-surface transition-transform duration-200 lg:translate-x-0"
    :class="open ? 'translate-x-0 shadow-xl' : '-translate-x-full'"
  >
    <div class="flex h-14 shrink-0 items-center gap-2.5 border-b border-line px-4">
      <span class="grid h-8 w-8 place-items-center rounded-lg bg-brand text-sm font-bold text-white">N</span>
      <div class="min-w-0 flex-1">
        <p class="truncate text-sm font-semibold leading-tight text-ink">{{ $t('app.name') }}</p>
        <p class="truncate text-[11px] leading-tight text-muted">{{ $t('app.tagline') }}</p>
      </div>
      <button
        type="button"
        class="rounded-md p-1 text-muted hover:bg-canvas hover:text-ink lg:hidden"
        :aria-label="$t('nav.closeMenu')"
        @click="emit('close')"
      >
        <X class="h-4 w-4" />
      </button>
    </div>

    <nav class="flex-1 overflow-y-auto px-2.5 py-3" :aria-label="$t('app.name')">
      <div v-for="(group, index) in groups" :key="group.labelKey ?? index" class="mb-4">
        <p v-if="group.labelKey" class="mb-1.5 px-2 text-[11px] font-medium uppercase tracking-wider text-muted/80">
          {{ $t(group.labelKey) }}
        </p>
        <ul class="space-y-0.5">
          <li v-for="item in group.items" :key="item.name">
            <RouterLink
              :to="{ name: item.name }"
              class="group flex items-center gap-2.5 rounded-md px-2.5 py-2 text-sm text-muted transition-colors hover:bg-canvas hover:text-ink"
              :class="isActive(item) ? '!bg-brand-soft !text-brand font-medium' : ''"
              @click="emit('close')"
            >
              <component :is="item.icon" class="h-4 w-4 shrink-0" aria-hidden="true" />
              <span class="truncate">{{ $t(item.labelKey) }}</span>
            </RouterLink>
          </li>
        </ul>
      </div>
    </nav>

    <div class="shrink-0 space-y-2 border-t border-line px-4 py-3">
      <span
        class="inline-flex items-center gap-1.5 rounded-full px-2 py-0.5 text-[11px] font-medium"
        :class="apiMode === 'mock' ? 'bg-amber-50 text-amber-700' : 'bg-emerald-50 text-emerald-700'"
      >
        <span class="h-1.5 w-1.5 rounded-full" :class="apiMode === 'mock' ? 'bg-amber-500' : 'bg-emerald-500'" />
        {{ apiMode === 'mock' ? $t('nav.modeMock') : $t('nav.modeLive') }}
      </span>
      <p class="text-[11px] leading-relaxed text-muted">{{ $t('nav.footer') }}</p>
    </div>
  </aside>
</template>
