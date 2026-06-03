import { create } from 'zustand'
import { apiService } from '@/services/apiService'
import { toastSuccess, toastWarning } from '@/stores/useToastStore'
import { debugLog, debugError, debugWarn } from '@/utils/debug'
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
  API_SESSIONS_METHODTABLE,
} from '@/config'
import type {
  ProcessInfo,
  SessionInfo,
  QueryResult,
  QueryMetadata,
  QueryResultData,
  GcRootPathResult,
  HexDataResult,
  MethodTableResult,
  GcRootProgress,
} from '@/types/api'

const ACTIVE_SESSION_ID_STORAGE_KEY = 'uidiag_active_session_id'

function readStoredActiveSessionId(): string | null {
  if (typeof window === 'undefined') return null
  const value = window.sessionStorage.getItem(ACTIVE_SESSION_ID_STORAGE_KEY)
  return value && value.trim().length > 0 ? value : null
}

function writeStoredActiveSessionId(sessionId: string | null): void {
  if (typeof window === 'undefined') return
  if (!sessionId) {
    window.sessionStorage.removeItem(ACTIVE_SESSION_ID_STORAGE_KEY)
    return
  }
  window.sessionStorage.setItem(ACTIVE_SESSION_ID_STORAGE_KEY, sessionId)
}

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
  methodTableData: MethodTableResult | null
  masterGridFilter: string
  detailGridData: unknown[] | null
  masterPaginationModel: { pageSize: number; page: number }
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
  fetchMethodTableObjects: (sessionId: string, mt: string) => Promise<void>
  setActiveSessionId: (id: string | null) => void
  setMasterFilter: (filter: string) => void
  setMasterPaginationModel: (model: { pageSize: number; page: number }) => void
  setGcRootProgress: (progress: GcRootProgress | null) => void
}

export const useDiagnosticsStore = create<DiagnosticsState>((set) => ({
  processes: [],
  selectedProcess: null,
  isAttached: false,
  sessions: [],
  activeSessionId: readStoredActiveSessionId(),
  queries: [],
  selectedQuery: null,
  queryResults: null,
  queryMetadata: [],
  queryResult: null,
  gcRootResult: null,
  gcRootProgress: null,
  hexData: null,
  methodTableData: null,
  masterGridFilter: '',
  detailGridData: null,
  masterPaginationModel: { pageSize: 50, page: 0 },
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

  selectProcess: (p) =>
    set((state) => {
      const prevPid = state.selectedProcess?.id ?? null
      const nextPid = p?.id ?? null
      const isDifferentProcess = prevPid !== null && nextPid !== null && prevPid !== nextPid

      if (isDifferentProcess) {
        writeStoredActiveSessionId(null)
        return {
          selectedProcess: p,
          activeSessionId: null,
          queryResult: null,
          detailGridData: null,
          gcRootResult: null,
          hexData: null,
        }
      }

      return { selectedProcess: p }
    }),

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
      writeStoredActiveSessionId(res.result)
      set({ activeSessionId: res.result })
      return res.result
    }
    return null
  },

  createDump: async (pid) => {
    const res = await apiService.post<string>(`${API_PROCESS_DUMP}/${pid}`)
    if (!res.isError && typeof res.result === 'string') {
      writeStoredActiveSessionId(res.result)
      set({ activeSessionId: res.result })
      return res.result
    }
    return null
  },

  uploadDump: async (file) => {
    const res = await apiService.upload<{ sessionId: string }>(API_SESSIONS_OPEN_DUMP, file)
    if (!res.isError && typeof res.result === 'object') {
      toastSuccess('Dump file uploaded successfully')
      writeStoredActiveSessionId(res.result.sessionId)
      set({ activeSessionId: res.result.sessionId })
      return res.result.sessionId
    }
    return null
  },

  openDumpPath: async (path) => {
    const res = await apiService.post<{ sessionId: string }>(API_SESSIONS_OPEN_DUMP_PATH, { path })
    if (!res.isError && typeof res.result === 'object') {
      toastSuccess('Dump file opened from server path')
      writeStoredActiveSessionId(res.result.sessionId)
      set({ activeSessionId: res.result.sessionId })
      return res.result.sessionId
    }
    return null
  },

  closeSession: async (sessionId) => {
    const res = await apiService.delete<unknown>(`${API_SESSIONS}/${sessionId}`)
    if (!res.isError) {
      if (sessionId === readStoredActiveSessionId()) {
        writeStoredActiveSessionId(null)
      }
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
      debugLog('query', `Fetched metadata for ${res.result.length} queries`)
      debugLog('query', 'Query names:', res.result.map((m: QueryMetadata) => m.queryName))
      // Log first query's columns for debugging casing
      if (res.result.length > 0) {
        const firstMeta = res.result[0]
        debugLog('query', `Metadata for "${firstMeta.queryName}": columns=[${firstMeta.columns.map((c: { path: string }) => c.path).join(', ')}], detailColumns=[${firstMeta.detailColumns.map((c: { path: string }) => c.path).join(', ')}]`)
      }
      set({ queryMetadata: res.result })
    }
  },

  selectQuery: (name) => set({ selectedQuery: name }),

  runQuery: async (sessionId, queryName, filter) => {
    set({ isLoading: true, queryResult: null })
    debugLog('query', `Running query: "${queryName}" on session "${sessionId}"${filter ? ` with filter "${filter}"` : ''}`)
    try {
      const url = filter
        ? `${API_SESSIONS}/${sessionId}/${queryName}?filter=${encodeURIComponent(filter)}`
        : `${API_SESSIONS}/${sessionId}/${queryName}`
      const res = await apiService.post<QueryResultData>(url)
      if (!res.isError) {
        const data = res.result as QueryResultData
        const rows = data.rows as unknown[]
        debugLog('query', `Query "${queryName}" succeeded: ${rows?.length ?? 0} rows, resultType="${data.resultType}", hasDetails=${data.hasDetails}`)
        if (rows && rows.length > 0) {
          const firstRow = rows[0] as Record<string, unknown>
          debugLog('query', `First row type: ${typeof firstRow}, isArray: ${Array.isArray(firstRow)}, keys: ${typeof firstRow === 'object' && firstRow !== null ? Object.keys(firstRow).join(', ') : 'N/A'}`)
          debugLog('query', 'First row sample:', firstRow)
        } else {
          debugWarn('query', `Query "${queryName}" returned zero rows`)
        }
        toastSuccess(`Query "${queryName}" completed`)
        set({ queryResult: data, detailGridData: null, gcRootResult: null })
      } else {
        debugError('query', `Query "${queryName}" failed: ${String(res.result)}`)
      }
    } catch (err) {
      debugError('query', `Query "${queryName}" exception:`, err)
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
    const url = `${API_SESSIONS}/${sessionId}/address/${encodeURIComponent(objectAddress)}`
    const res = await apiService.post<HexDataResult>(url)
    if (!res.isError) {
      set({ hexData: res.result as HexDataResult })
    }
  },

  fetchMethodTableObjects: async (sessionId, mt) => {
    set({ methodTableData: null, isLoading: true })
    const url = `${API_SESSIONS_METHODTABLE}/${sessionId}/methodTable/${encodeURIComponent(mt)}`
    const res = await apiService.post<MethodTableResult>(url)
    if (!res.isError) {
      set({ methodTableData: res.result as MethodTableResult })
    }
    set({ isLoading: false })
  },

  setActiveSessionId: (id) => {
    writeStoredActiveSessionId(id)
    set({ activeSessionId: id })
  },

  setMasterFilter: (filter) => set({ masterGridFilter: filter }),

  setMasterPaginationModel: (model) => set({ masterPaginationModel: model }),

  setGcRootProgress: (progress) => set({ gcRootProgress: progress }),
}))
