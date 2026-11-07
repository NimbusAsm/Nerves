/// <reference types="vite/client" />

interface ImportMetaEnv {
  /** 管理 API 基址，默认 /api/v1 */
  readonly VITE_API_BASE_URL?: string
  /** 'true' 时使用内置 mock 数据源 */
  readonly VITE_USE_MOCK?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}

declare module '*.vue' {
  import type { DefineComponent } from 'vue'

  const component: DefineComponent<Record<string, never>, Record<string, never>, unknown>
  export default component
}
