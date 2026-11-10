<script setup lang="ts">
import { onMounted, ref } from 'vue'
import { createClient } from '@nerves/sdk'
import {
  beginLogin,
  clearTokens,
  completeLogin,
  configFromEnv,
  currentTokens,
  fetchUserInfo,
  type Profile,
  webStorageStore,
} from './auth'

const config = configFromEnv()
const store = webStorageStore()

const username = ref('')
const password = ref('')
const busy = ref(false)
const error = ref<string | null>(null)
const profile = ref<Profile | null>(null)
const accounts = ref<Array<{ username: string; realm: string; status: string }>>([])

async function loadAccounts(): Promise<void> {
  const tokens = currentTokens()
  if (tokens === null) {
    return
  }

  const nerves = createClient({ baseUrl: config.gatewayBase, token: tokens.accessToken })
  const page = await nerves.accounts.list().limit(5).send()
  accounts.value = page.items.map((item) => ({
    username: item.username,
    realm: item.realm,
    status: item.status,
  }))
}

onMounted(async () => {
  if (window.location.pathname !== '/auth/callback') {
    return
  }

  busy.value = true
  const params = new URLSearchParams(window.location.search)

  try {
    await completeLogin(config, store, {
      code: params.get('code'),
      state: params.get('state'),
    })

    const tokens = currentTokens()
    if (tokens !== null) {
      profile.value = await fetchUserInfo(config, tokens.accessToken)
      await loadAccounts()
    }

    window.history.replaceState({}, '', '/')
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'sign-in failed'
  } finally {
    busy.value = false
  }
})

async function signIn(): Promise<void> {
  busy.value = true
  error.value = null

  try {
    const url = await beginLogin(config, store, username.value, password.value)
    window.location.assign(url)
  } catch (caught) {
    error.value = caught instanceof Error ? caught.message : 'sign-in failed'
    busy.value = false
  }
}

function signOut(): void {
  clearTokens()
  profile.value = null
  accounts.value = []
  window.location.replace('/')
}
</script>

<template>
  <main>
    <h1>Nerves SPA sample</h1>

    <template v-if="profile === null">
      <p class="muted">
        Public client, authorization code + PKCE. Tokens stay in memory; only the
        transient PKCE verifier uses <code>sessionStorage</code>.
      </p>
      <form @submit.prevent="signIn">
        <input v-model="username" placeholder="username" autocomplete="username" required />
        <input
          v-model="password"
          type="password"
          placeholder="password"
          autocomplete="current-password"
          required
        />
        <button type="submit" :disabled="busy">{{ busy ? 'Signing in…' : 'Sign in' }}</button>
      </form>
    </template>

    <template v-else>
      <p>
        <strong>{{ profile.name ?? profile.sub }}</strong>
        <span v-if="profile.email"> &lt;{{ profile.email }}&gt;</span>
      </p>
      <p class="muted">
        sub <code>{{ profile.sub }}</code>
        <span v-if="profile.leid"> · leid <code>{{ profile.leid }}</code></span>
      </p>

      <h2>First accounts page (via @nerves/sdk)</h2>
      <table v-if="accounts.length > 0">
        <thead>
          <tr><th>username</th><th>realm</th><th>status</th></tr>
        </thead>
        <tbody>
          <tr v-for="account in accounts" :key="account.username">
            <td>{{ account.username }}</td>
            <td>{{ account.realm }}</td>
            <td>{{ account.status }}</td>
          </tr>
        </tbody>
      </table>
      <p v-else class="muted">No accounts visible to this session.</p>

      <button @click="signOut">Sign out</button>
    </template>

    <p v-if="error" class="error">{{ error }}</p>
  </main>
</template>

<style>
body {
  margin: 0;
  background: #0b0f14;
  color: #e6edf3;
  font: 15px/1.55 ui-sans-serif, system-ui, sans-serif;
}
main {
  max-width: 46rem;
  margin: 3rem auto;
  padding: 0 1.25rem;
}
h1 { font-size: 1.35rem; margin: 0 0 0.4rem; }
h2 { font-size: 1rem; margin: 2rem 0 0.6rem; }
.muted { color: #8b98a5; }
.error { color: #f85149; }
form { display: grid; gap: 0.75rem; max-width: 22rem; margin-top: 1rem; }
input {
  background: #111823;
  border: 1px solid #26313d;
  color: inherit;
  padding: 0.55rem 0.65rem;
  border-radius: 0.4rem;
}
button {
  background: #2f81f7;
  border: 0;
  color: #fff;
  padding: 0.6rem 0.9rem;
  border-radius: 0.4rem;
  cursor: pointer;
  justify-self: start;
}
button:disabled { opacity: 0.6; cursor: progress; }
table { border-collapse: collapse; width: 100%; font-size: 0.9rem; }
th, td { text-align: left; padding: 0.45rem 0.6rem; border-bottom: 1px solid #1c2530; }
code { background: #111823; padding: 0.1rem 0.3rem; border-radius: 0.25rem; }
</style>
