import { create } from 'zustand'
import { apiService } from '@/services/apiService'
import {
  API_PROCESSES,
  API_PROCESS_ATTACH,
  API_PROCESS_DETACH,
  API_PROCESS_SNAPSHOT,
  API_PROCESS_DUMP,
  API_SESSIONS,
  API_SESSIONS_QUERIES,
} from '@/config'
import type { ProcessInfo, SessionInfo, QueryResult } from '@/types/api'

interface DiagnosticsState {
  processes: ProcessInfo[]
  selectedProcess: ProcessInfo | null
  isAttached: boolean
  sessions: SessionInfo[]
  activeSessionId: string | null
  queries: string[]
  selectedQuery: string | null
  queryResults: QueryResult | null
  isLoading: boolean
  processesFetched: boolean

  // Actions
  fetchProcesses: () => Promise<void>
  selectProcess: (p: ProcessInfo | null) => void
  attachToProcess: (pid: number) => Promise<boolean>
  detachFromProcess: () => Promise<boolean>
  takeSnapshot: (pid: number) => Promise<string | null>
  createDump: (pid: number) => Promise<string | null>
  fetchSessions: () => Promise<void>
  fetchQueries: () => Promise<void>
  selectQuery: (name: string | null) => void
  runQuery: (sessionId: string, queryName: string) => Promise<void>
  setActiveSessionId: (id: string | null) => void
}

export const useDiagnosticsStore = create<DiagnosticsState>((set) => ({
  processes: [],
  selectedProcess: null,
  isAttached: false,
  sessions: [],
  activeSessionId: null,
  queries: [],
  selectedQuery: null,
  queryResults: null,
  isLoading: false,
  processesFetched: false,

  fetchProcesses: async () => {
    const res = await apiService.get<ProcessInfo[]>(API_PROCESSES)
    if (!res.isError && Array.isArray(res.result)) {
      set({ processes: res.result, processesFetched: true })
    } else {
      set({ processesFetched: true })
    }
  },

  selectProcess: (p) => set({ selectedProcess: p }),

  attachToProcess: async (pid) => {
    const res = await apiService.post<unknown>(`${API_PROCESS_ATTACH}/${pid}`)
    if (!res.isError) {
      set({ isAttached: true })
      return true
    }
    return false
  },

  detachFromProcess: async () => {
    const res = await apiService.post<unknown>(API_PROCESS_DETACH)
    if (!res.isError) {
      set({ isAttached: false })
      return true
    }
    return false
  },

  takeSnapshot: async (pid) => {
    const res = await apiService.post<string>(`${API_PROCESS_SNAPSHOT}/${pid}`)
    if (!res.isError && typeof res.result === 'string') {
      set({ activeSessionId: res.result })
      return res.result
    }
    return null
  },

  createDump: async (pid) => {
    const res = await apiService.post<string>(`${API_PROCESS_DUMP}/${pid}`)
    if (!res.isError && typeof res.result === 'string') {
      set({ activeSessionId: res.result })
      return res.result
    }
    return null
  },

  fetchSessions: async () => {
    const res = await apiService.get<SessionInfo[]>(API_SESSIONS)
    if (!res.isError && Array.isArray(res.result)) {
      set({ sessions: res.result })
    }
  },

  fetchQueries: async () => {
    const res = await apiService.get<string[]>(API_SESSIONS_QUERIES)
    if (!res.isError && Array.isArray(res.result)) {
      set({ queries: res.result })
    }
  },

  selectQuery: (name) => set({ selectedQuery: name }),

  runQuery: async (sessionId, queryName) => {
    set({ isLoading: true, queryResults: null })
    try {
      const res = await apiService.post<QueryResult>(`${API_SESSIONS}/${sessionId}/${queryName}`)
      if (!res.isError) {
        set({ queryResults: res.result })
      }
    } finally {
      set({ isLoading: false })
    }
  },

  setActiveSessionId: (id) => set({ activeSessionId: id }),
}))
