import { createPinia } from 'pinia'
import { createApp } from 'vue'

import App from './App.vue'
import { i18n } from './i18n'
import { router } from './router'
import { useAuthStore } from './stores/auth'
import './styles/main.css'

const app = createApp(App)
const pinia = createPinia()

app.use(pinia)
app.use(i18n)
app.use(router)

// A 401 from any request drops the local session and sends the operator back.
if (typeof window !== 'undefined') {
  window.addEventListener('nerves:unauthorized', () => {
    const auth = useAuthStore(pinia)
    if (!auth.isAuthenticated) return
    auth.handleUnauthorized()
    void router.replace({ name: 'login', query: { reason: 'expired' } })
  })
}

app.mount('#app')
