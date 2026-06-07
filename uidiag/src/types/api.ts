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
  children: GcRootPathNode[]
  referencingObjects: GcReferenceInfo[]
}

/** GC root path analysis result. */
export interface GcRootPathResult {
  paths: GcRootPathNode[]
  totalPaths: number
  totalReferences: number
}

/** Address object data returned by POST /api/sessions/{id}/address/{addr}. */
export interface HexDataResult {
  objectAddress: string
  typeName: string
  mt: string
  size: number
  bytesBase64: string
}

/** MethodTable result returned by POST /api/sessions/{id}/methodTable/{mt}. */
export interface MethodTableResult {
  mt: string
  typeName: string
  graphSize: number
  objectCount: number
  objects: MethodTableObject[]
}

/** Individual object entry in a MethodTable result. */
export interface MethodTableObject {
  address: string
  size: string
  type: { name: string }
}

/** GC root progress payload from SignalR onGcRootProgress. */
export interface GcRootProgress {
  sessionId: string
  objectAddress: string
  count: number
  status: string
}

/** Query progress payload from SignalR onQueryProgress. */
export interface QueryProgress {
  sessionId: string
  queryName: string
  count: number
  status: string
}

// ──────────────────────── Memory Map ────────────────────────

/** Heap segment info from GET /api/sessions/{id}/memorymap. */
export interface MemorySegmentInfo {
  startAddress: string
  endAddress: string
  committedStart: string
  committedEnd: string
  reservedStart: string
  reservedEnd: string
  segmentKind: string
  isLargeObject: boolean
  isPinnedObject: boolean
  objectCount: number
  size: number
}

// ──────────────────────── Raw Memory ────────────────────────

/** Classified sub-range of raw memory (who owns these bytes). */
export interface MemoryRegion {
  offset: number
  length: number
  kind: string
  objectAddress?: string | null
  objectTypeName?: string | null
  objectSize?: number | null
  offsetWithinObject?: number | null
}

/** Raw memory read result from POST /api/sessions/{id}/memory/{addr}. */
export interface RawMemoryResult {
  address: string
  length: number
  bytesBase64: string
  regionKind: string
  regions: MemoryRegion[]
  containingObjectAddress?: string | null
  containingObjectTypeName?: string | null
  offsetWithinObject?: number | null
}

// ──────────────────────── Field Layout ────────────────────────

/** Single field within an object layout. */
export interface FieldInfo {
  offset: number
  fieldName: string
  typeName: string
  isObjectReference: boolean
  valueHex: string
  targetAddressHex?: string | null
}

/** Object field layout from POST /api/sessions/{id}/layout/{addr}. */
export interface ObjectFieldLayout {
  objectAddress: string
  typeName: string
  mt: string
  totalSize: number
  fields: FieldInfo[]
}

// ──────────────────────── Data Owner ────────────────────────

/** Data owner result from POST /api/sessions/{id}/dataowner/{addr}. */
export interface DataOwnerResult {
  address: string
  kind: string
  containingObjectAddress?: string | null
  containingObjectTypeName?: string | null
  offsetWithinObject?: number | null
  objectSize?: number | null
  isObjectStart: boolean
  referencingObjects?: GcReferenceInfo[] | null
}

/** Referencing objects result from POST /api/sessions/{id}/referencing/{addr}. */
export interface ReferencingObjectsResult {
  targetAddress: string
  isObjectStart: boolean
  referencingObjects: GcReferenceInfo[]
}

/** Combined address info from POST /api/sessions/{id}/addressinfo/{addr}. */
export interface AddressInfoResult {
  address: string
  kind: string
  containingObjectAddress?: string | null
  containingObjectTypeName?: string | null
  offsetWithinObject?: number | null
  objectSize?: number | null
  isObjectStart: boolean
  fieldLayout?: ObjectFieldLayout | null
  referencingObjects?: GcReferenceInfo[] | null
}
