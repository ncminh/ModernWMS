/// <reference types="vite/client" />

interface ImportMetaEnv {
  readonly VITE_CLI_PORT: string
  readonly VITE_BASE_PATH: string
  readonly VITE_SERVER_PORT: string
}

interface ImportMeta {
  readonly env: ImportMetaEnv
}
