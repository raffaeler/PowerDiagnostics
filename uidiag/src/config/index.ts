/** Base URL for the DiagnosticServer API. Configurable via VITE_API_BASE_URL env var. */
export function getApiBaseUrl(): string {
  return import.meta.env.VITE_API_BASE_URL ?? 'http://localhost:5218'
}

// ── API Endpoints ──────────────────────────────────────────────

export const API_PROCESSES = '/api/processes'
export const API_PROCESS_ATTACH = '/api/processes/attach'
export const API_PROCESS_DETACH = '/api/processes/detach'
export const API_PROCESS_SNAPSHOT = '/api/processes/snapshot'
export const API_PROCESS_DUMP = '/api/processes/dump'

export const API_SESSIONS = '/api/sessions'
export const API_SESSIONS_QUERIES = '/api/sessions/queries'

export const HUB_PATH = '/diagnosticHub'
