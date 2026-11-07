<script setup lang="ts">
import { LogOut, Menu } from '@lucide/vue'
import { computed, ref, watch } from 'vue'
import { useRoute, useRouter } from 'vue-router'

import { apiMode } from '@/api'
import { currentLocale, setLocale, SUPPORTED_LOCALES, type SupportedLocale } from '@/i18n'
import { initials } from '@/lib/format'
import { useAuthStore } from '@/stores/auth'

const emit = defineEmits<{ openMenu: [] }>()

const route = useRoute()
const router = useRouter()
const auth = useAuthStore()

const sectionTitle = computed(() =>
  typeof route.meta.sectionKey === 'string' ? route.meta.sectionKey : null,
)
const pageTitle = computed(() => (typeof route.meta.titleKey === 'string' ? route.meta.titleKey : ''))

const locale = ref<SupportedLocale>(currentLocale())
watch(locale, (next) => setLocale(next))

function signOut(): void {
  auth.logout()
  void router.push({ name: 'login' })
}
</script>

<template>
  <header class="sticky top-0 z-30 flex h-14 items-center gap-3 border-b border-line bg-surface/95 px-4 backdrop-blur">
    <button
      type="button"
      class="rounded-md p-1.5 text-muted hover:bg-canvas hover:text-ink lg:hidden"
      :aria-label="$t('nav.openMenu')"
      @click="emit('openMenu')"
    >
      <Menu class="h-5 w-5" />
    </button>

    <div class="min-w-0 flex-1">
      <p v-if="sectionTitle" class="text-[11px] leading-tight text-muted">{{ $t(sectionTitle) }}</p>
      <p class="truncate text-sm font-medium leading-tight text-ink">{{ pageTitle ? $t(pageTitle) : '' }}</p>
    </div>

    <span
      v-if="apiMode === 'mock'"
      class="hidden rounded-full bg-amber-50 px-2 py-0.5 text-[11px] font-medium text-amber-700 sm:inline"
    >
      {{ $t('nav.modeMock') }}
    </span>

    <select
      v-model="locale"
      class="h-8 rounded-md border border-line bg-surface px-1.5 text-xs text-muted focus:border-brand focus:outline-none"
      :aria-label="$t('nav.userMenu')"
    >
      <option v-for="option in SUPPORTED_LOCALES" :key="option.value" :value="option.value">
        {{ option.label }}
      </option>
    </select>

    <div class="flex items-center gap-2 border-l border-line pl-3">
      <span class="hidden text-right sm:block">
        <span class="block text-xs font-medium leading-tight text-ink">{{ auth.displayName }}</span>
        <span class="block text-[11px] leading-tight text-muted">{{ auth.roles.join(' · ') || $t('common.unknown') }}</span>
      </span>
      <span
        class="grid h-8 w-8 place-items-center rounded-full bg-brand-soft text-xs font-semibold text-brand"
        :title="auth.displayName"
      >
        {{ initials(auth.displayName) }}
      </span>
      <button
        type="button"
        class="rounded-md p-1.5 text-muted transition-colors hover:bg-canvas hover:text-ink"
        :aria-label="$t('common.logout')"
        :title="$t('common.logout')"
        @click="signOut"
      >
        <LogOut class="h-4 w-4" />
      </button>
    </div>
  </header>
</template>
