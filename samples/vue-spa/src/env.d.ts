/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_NERVES_IDP?: string
  readonly VITE_NERVES_GATEWAY?: string
  readonly VITE_NERVES_CLIENT_ID?: string
  readonly VITE_NERVES_REDIRECT_URI?: string
  readonly VITE_NERVES_SCOPES?: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
