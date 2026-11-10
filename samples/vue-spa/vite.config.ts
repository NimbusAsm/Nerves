import { fileURLToPath, URL } from 'node:url'
import { defineConfig, loadEnv } from 'vite'
import vue from '@vitejs/plugin-vue'

/**
 * The dev server doubles as a same-origin proxy for the IdP and the gateway.
 *
 * That is what keeps this sample free of CORS configuration: the browser only
 * ever talks to `http://127.0.0.1:5178`, and Vite forwards `/idp/*` and
 * `/gateway/*` server-side. In production you either allow the SPA origin in
 * `Cors:AdminOrigins`, or switch to the BFF pattern (see samples/node-bff).
 */
export default defineConfig(({ mode }) => {
  const env = loadEnv(mode, process.cwd(), '')
  const idp = env.NERVES_IDP ?? 'http://127.0.0.1:5101'
  const gateway = env.NERVES_GATEWAY ?? 'http://127.0.0.1:5100'
  const port = Number(env.SPA_PORT ?? '5178')

  return {
    plugins: [vue()],
    resolve: {
      alias: {
        '@': fileURLToPath(new URL('./src', import.meta.url)),
      },
    },
    server: {
      host: '127.0.0.1',
      port,
      strictPort: true,
      proxy: {
        '/idp': {
          target: idp,
          changeOrigin: false,
          rewrite: (path) => path.replace(/^\/idp/, ''),
        },
        '/gateway': {
          target: gateway,
          changeOrigin: false,
          rewrite: (path) => path.replace(/^\/gateway/, ''),
        },
      },
    },
  }
})
