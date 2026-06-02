/** Generic API response wrapper matching the backend pattern. */
export interface ApiResponse<T> {
  isError: boolean
  result: T | string
}

/** Lightweight process info from GET /api/processes. */
export interface ProcessInfo {
  id: number
  name: string
}

/** Session summary from GET /api/sessions. */
export interface SessionInfo {
  sessionId: string
  investigationKind: string
  created: string
}

/** Query result shape (varies by query). */
// eslint-disable-next-line @typescript-eslint/no-explicit-any
export type QueryResult = any
