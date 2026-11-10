<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { useRouter } from 'vue-router'

import { describeError } from '@/api'
import { safeRedirect } from '@/lib/url'
import { useAuthStore } from '@/stores/auth'

const auth = useAuthStore()
const router = useRouter()
const error = ref<string | null>(null)

/** Hash routing keeps the query inside the fragment, not location.search. */
function authorizationResponse(): string {
  const hash = window.location.hash
  const query = hash.indexOf('?')
  return query >= 0 ? hash.slice(query) : window.location.search
}

onMounted(async () => {
  try {
    const redirectTo = await auth.finishLogin(authorizationResponse())
    await router.replace(safeRedirect(redirectTo))
  } catch (cause) {
    error.value = describeError(cause)
  }
})
</script>

<template>
  <div class="grid min-h-screen place-items-center bg-canvas">
    <div class="w-full max-w-sm rounded-xl border border-ink-900/10 bg-white p-8 text-center shadow-sm">
      <p v-if="!error" class="text-sm text-ink-600">正在完成登录…</p>
      <template v-else>
        <p class="text-sm text-red-600">{{ error }}</p>
        <RouterLink class="mt-4 inline-block text-sm font-medium text-brand-600" to="/login">
          返回登录
        </RouterLink>
      </template>
    </div>
  </div>
</template>
