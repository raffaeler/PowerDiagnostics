import { create } from 'zustand'
import { apiService } from '@/services/apiService'
import { toastSuccess, toastWarning } from '@/stores/useToastStore'
import {
  API_PROCESSES,
  API_PROCESS_ATTACH,
  API_PROCESS_DETACH,
  API_PROCESS_SNAPSHOT,
  API_PROCESS_DUMP,
  API_SESSIONS,
  API_SESSIONS_QUERIES,
  API_SESSIONS_QUERIES_METADATA,
  API_SESSIONS_OPEN_DUMP,
  API_SESSIONS_OPEN_DUMP_PATH,
} from '@/config'
import type {
  ProcessInfo,
  SessionInfo,
  QueryResult,
  QueryMetadata,
  QueryResultData,
  GcRootPathResult,
  HexDataResult,
  GcRootProgress,
} from '@/types/api'

interface DiagnosticsState {
  processes: ProcessInfo[]
  selectedProcess: ProcessInfo | null
  isAttached: boolean
  sessions: SessionInfo[]
  activeSessionId: string | null
  queries: string[]
  selectedQuery: string | null
  queryResults: QueryResult | null
  queryMetadata: QueryMetadata[]
  queryResult: QueryResultData | null
  gcRootResult: GcRootPathResult | null
  gcRootProgress: GcRootProgress | null
  hexData: HexDataResult | null
  masterGridFilter: string
  detailGridData: unknown[] | null
  isLoading: boolean
  processesFetched: boolean

  // Actions
  fetchProcesses: () => Promise<void>
  selectProcess: (p: ProcessInfo | null) => void
  attachToProcess: (pid: number) => Promise<boolean>
  detachFromProcess: () => Promise<boolean>
  takeSnapshot: (pid: number) => Promise<string | null>
  createDump: (pid: number) => Promise<string | null>
  uploadDump: (file: File) => Promise<string | null>
  openDumpPath: (path: string) => Promise<string | null>
  closeSession: (sessionId: string) => Promise<boolean>
  fetchSessions: () => Promise<void>
  fetchQueries: () => Promise<void>
  fetchQueriesMetadata: () => Promise<void>
  selectQuery: (name: string | null) => void
  runQuery: (sessionId: string, queryName: string, filter?: string) => Promise<void>
  fetchGcRootPath: (sessionId: string, objectAddress: string) => Promise<void>
  fetchHexData: (sessionId: string, objectAddress: string) => Promise<void>
  setActiveSessionId: (id: string | null) => void
  setMasterFilter: (filter: string) => void
  setGcRootProgress: (progress: GcRootProgress | null) => void
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
  queryMetadata: [],
  queryResult: null,
  gcRootResult: null,
  gcRootProgress: null,
  hexData: null,
  masterGridFilter: '',
  detailGridData: null,
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

  uploadDump: async (file) => {
    const res = await apiService.upload<{ sessionId: string }>(API_SESSIONS_OPEN_DUMP, file)
    if (!res.isError && typeof res.result === 'object') {
      toastSuccess('Dump file uploaded successfully')
      set({ activeSessionId: res.result.sessionId })
      return res.result.sessionId
    }
    return null
  },

  openDumpPath: async (path) => {
    const res = await apiService.post<{ sessionId: string }>(API_SESSIONS_OPEN_DUMP_PATH, { path })
    if (!res.isError && typeof res.result === 'object') {
      toastSuccess('Dump file opened from server path')
      set({ activeSessionId: res.result.sessionId })
      return res.result.sessionId
    }
    return null
  },

  closeSession: async (sessionId) => {
    const res = await apiService.delete<unknown>(`${API_SESSIONS}/${sessionId}`)
    if (!res.isError) {
      toastSuccess('Session closed')
      return true
    }
    return false
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

  fetchQueriesMetadata: async () => {
    const res = await apiService.get<QueryMetadata[]>(API_SESSIONS_QUERIES_METADATA)
    if (!res.isError && Array.isArray(res.result)) {
      set({ queryMetadata: res.result })
    }
  },

  selectQuery: (name) => set({ selectedQuery: name }),

  runQuery: async (sessionId, queryName, filter) => {
    set({ isLoading: true, queryResult: null })
    try {
      const url = filter
        ? `${API_SESSIONS}/${sessionId}/${queryName}?filter=${encodeURIComponent(filter)}`
        : `${API_SESSIONS}/${sessionId}/${queryName}`
      const res = await apiService.post<QueryResultData>(url)
      if (!res.isError) {
        const data = res.result as QueryResultData
        toastSuccess(`Query "${queryName}" completed`)
        set({ queryResult: data, detailGridData: null, gcRootResult: null })
      }
    } finally {
      set({ isLoading: false })
    }
  },

  fetchGcRootPath: async (sessionId, objectAddress) => {
    set({ gcRootResult: null, gcRootProgress: null })
    const url = `${API_SESSIONS}/${sessionId}/gcroot/${encodeURIComponent(objectAddress)}`
    const res = await apiService.post<GcRootPathResult>(url)
    if (!res.isError) {
      const result = res.result as GcRootPathResult
      if (result.totalPaths === 0) {
        toastWarning('No GC root paths found for this object')
      }
      set({ gcRootResult: result })
    }
  },

  fetchHexData: async (sessionId, objectAddress) => {
    set({ hexData: null })
    const url = `${API_SESSIONS}/${sessionId}/hex/${encodeURIComponent(objectAddress)}`
    const res = await apiService.post<HexDataResult>(url)
    if (!res.isError) {
      set({ hexData: res.result as HexDataResult })
    }
  },

  setActiveSessionId: (id) => set({ activeSessionId: id }),

  setMasterFilter: (filter) => set({ masterGridFilter: filter }),

  setGcRootProgress: (progress) => set({ gcRootProgress: progress }),
}))
