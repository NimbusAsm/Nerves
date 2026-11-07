<script setup lang="ts">
import { CircleCheck, CircleExclamationPoint, Eye, EyeOff, Info, Lock, UserRound } from '@lucide/vue'
import { computed, onMounted, ref } from 'vue'
import { useI18n } from 'vue-i18n'
import { useRoute, useRouter } from 'vue-router'

import { apiMode, describeError } from '@/api'
import Button from '@/components/ui/Button.vue'
import Input from '@/components/ui/Input.vue'
import { useAuthStore } from '@/stores/auth'
import { safeRedirect } from '@/lib/url'

const { t } = useI18n()
const auth = useAuthStore()
const route = useRoute()
const router = useRouter()

const username = ref('')
const password = ref('')
const showPassword = ref(false)
const submitting = ref(false)
const error = ref<string | null>(null)
const expired = ref(false)

const features = ['auth.brandFeature1', 'auth.brandFeature2', 'auth.brandFeature3']

onMounted(() => {
  expired.value = route.query.reason === 'expired'
})

const reason = computed(() => (typeof route.query.reason === 'string' ? route.query.reason : ''))

async function submit(): Promise<void> {
  error.value = null
  if (!username.value.trim() || !password.value) {
    error.value = t('auth.credentialsRequired')
    return
  }

  submitting.value = true
  try {
    await auth.login(username.value.trim(), password.value)
    await router.replace(safeRedirect(route.query.redirect))
  } catch (cause) {
    error.value = describeError(cause)
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <div class="grid min-h-screen bg-canvas lg:grid-cols-[1.05fr_1fr]">
    <section
      class="relative hidden flex-col justify-between overflow-hidden bg-[#0c1626] p-10 text-white lg:flex"
      aria-hidden="true"
    >
      <div
        class="pointer-events-none absolute inset-0 opacity-60"
        style="background-image: radial-gradient(circle at 20% 20%, rgba(31, 111, 235, 0.45), transparent 45%), radial-gradient(circle at 80% 70%, rgba(56, 189, 248, 0.18), transparent 45%)"
      />
      <div class="relative">
        <div class="flex items-center gap-3">
          <span class="grid h-10 w-10 place-items-center rounded-xl bg-brand text-base font-bold">N</span>
          <div>
            <p class="text-base font-semibold">{{ $t('app.name') }}</p>
            <p class="text-xs text-slate-400">{{ $t('app.tagline') }}</p>
          </div>
        </div>

        <h1 class="mt-16 max-w-md text-3xl font-semibold leading-snug">{{ $t('app.name') }}</h1>
        <p class="mt-3 max-w-md text-sm leading-relaxed text-slate-300">{{ $t('auth.brandTagline') }}</p>

        <ul class="mt-8 space-y-3 text-sm text-slate-200">
          <li v-for="feature in features" :key="feature" class="flex items-start gap-2.5">
            <CircleCheck class="mt-0.5 h-4 w-4 shrink-0 text-emerald-400" />
            <span>{{ $t(feature) }}</span>
          </li>
        </ul>
      </div>

      <p class="relative text-xs text-slate-500">Apache-2.0 · NimbusAsm / Nerves</p>
    </section>

    <section class="flex items-center justify-center px-5 py-10">
      <div class="w-full max-w-sm">
        <div class="mb-8 flex items-center gap-2.5 lg:hidden">
          <span class="grid h-9 w-9 place-items-center rounded-lg bg-brand text-sm font-bold text-white">N</span>
          <div>
            <p class="text-sm font-semibold text-ink">{{ $t('app.name') }}</p>
            <p class="text-[11px] text-muted">{{ $t('app.tagline') }}</p>
          </div>
        </div>

        <h2 class="text-2xl font-semibold tracking-tight text-ink">{{ $t('auth.title') }}</h2>
        <p class="mt-1.5 text-sm leading-relaxed text-muted">{{ $t('auth.subtitle') }}</p>

        <div
          v-if="expired || reason === 'expired'"
          class="mt-4 flex items-start gap-2 rounded-md border border-sky-200 bg-sky-50 px-3 py-2 text-xs text-sky-800"
          role="status"
        >
          <Info class="mt-0.5 h-4 w-4 shrink-0" />
          {{ $t('auth.sessionExpired') }}
        </div>

        <form class="mt-6 space-y-4" @submit.prevent="submit">
          <div>
            <label for="login-username" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('auth.username') }}</label>
            <div class="relative">
              <UserRound class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <Input
                id="login-username"
                v-model="username"
                class="pl-8"
                :placeholder="$t('auth.usernamePlaceholder')"
                autocomplete="username"
                :invalid="Boolean(error)"
              />
            </div>
          </div>

          <div>
            <label for="login-password" class="mb-1.5 block text-xs font-medium text-ink">{{ $t('auth.password') }}</label>
            <div class="relative">
              <Lock class="pointer-events-none absolute left-2.5 top-1/2 h-4 w-4 -translate-y-1/2 text-muted" />
              <Input
                id="login-password"
                v-model="password"
                class="pl-8 pr-9"
                :type="showPassword ? 'text' : 'password'"
                :placeholder="$t('auth.passwordPlaceholder')"
                autocomplete="current-password"
                :invalid="Boolean(error)"
              />
              <button
                type="button"
                class="absolute right-2 top-1/2 -translate-y-1/2 rounded p-0.5 text-muted hover:text-ink"
                :aria-label="showPassword ? $t('auth.hidePassword') : $t('auth.showPassword')"
                @click="showPassword = !showPassword"
              >
                <EyeOff v-if="showPassword" class="h-4 w-4" />
                <Eye v-else class="h-4 w-4" />
              </button>
            </div>
          </div>

          <div
            v-if="error"
            class="flex items-start gap-2 rounded-md border border-red-200 bg-red-50 px-3 py-2 text-xs leading-relaxed text-red-700"
            role="alert"
          >
            <CircleExclamationPoint class="mt-0.5 h-4 w-4 shrink-0" />
            <span>{{ error }}</span>
          </div>

          <Button type="submit" variant="primary" class="w-full" :loading="submitting">
            {{ submitting ? $t('auth.submitting') : $t('auth.submit') }}
          </Button>
        </form>

        <div v-if="apiMode === 'mock'" class="mt-6 rounded-md border border-dashed border-line bg-surface p-3">
          <p class="text-xs font-medium text-ink">{{ $t('auth.demoTitle') }}</p>
          <ul class="mt-1.5 space-y-1 text-xs text-muted">
            <li class="mono">{{ $t('auth.demoAdmin') }}</li>
            <li class="mono">{{ $t('auth.demoAuditor') }}</li>
          </ul>
          <p class="mt-2 text-[11px] leading-relaxed text-muted">{{ $t('auth.mockHint') }}</p>
        </div>
      </div>
    </section>
  </div>
</template>
