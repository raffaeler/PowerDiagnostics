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

/** Column definition matching server QueryMetadata.Columns. */
export interface ColumnDefinition {
  header: string
  path: string
  format?: string
  alignRight?: boolean
  tooltip?: string
}

/** Query metadata returned by GET /api/sessions/queries/metadata. */
export interface QueryMetadata {
  queryName: string
  resultType: string
  hasDetails: boolean
  detailType?: string
  detailProperty?: string
  columns: ColumnDefinition[]
  detailColumns: ColumnDefinition[]
}

/** Query execution result returned by POST /api/sessions/{id}/{query}. */
export interface QueryResultData {
  queryName: string
  resultType: string
  rows: unknown[]
  hasDetails: boolean
  detailType?: string
  detailProperty?: string
}

/** GC root path reference info. */
export interface GcReferenceInfo {
  address: string
  typeName: string
  fieldName: string
  isStatic: boolean
}

/** Single node in a GC root path. */
export interface GcRootPathNode {
  objectAddress: string
  typeName: string
  rootKind: string
  depth: number
  referencingObjects: GcReferenceInfo[]
}

/** GC root path analysis result. */
export interface GcRootPathResult {
  paths: GcRootPathNode[]
  totalPaths: number
  totalReferences: number
}

/** Hex viewer data returned by POST /api/sessions/{id}/hex/{addr}. */
export interface HexDataResult {
  objectAddress: string
  typeName: string
  size: number
  bytesBase64: string
}

/** GC root progress payload from SignalR onGcRootProgress. */
export interface GcRootProgress {
  sessionId: string
  objectAddress: string
  percent: number
  status: string
}
