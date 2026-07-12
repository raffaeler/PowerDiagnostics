import type { ColumnDefinition } from '@/types/api'

/**
 * Mirror of the WPF KnownGrids registry (§5 in WPF-Functionality-Reference.md).
 * Maps result type name → master & detail column definitions.
 * Used by the client to render DataGrid columns without hardcoding.
 */
interface GridConfig {
  masterColumns: ColumnDefinition[]
  detailColumns: ColumnDefinition[]
}

/** Standard ClrObject detail columns. */
const clrObjectDetail: ColumnDefinition[] = [
  { header: 'Address', path: 'address', format: '0:X16', alignRight: true, tooltip: 'Address' },
  { header: 'Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Size' },
  { header: 'Type', path: 'type', tooltip: 'Type' },
]

/** StackFrame detail columns. */
const stackFrameDetail: ColumnDefinition[] = [
  { header: 'FrameName', path: 'frameName', tooltip: 'FrameName' },
  { header: 'Method', path: 'method', tooltip: 'Method' },
  { header: 'Kind', path: 'kind', tooltip: 'Kind' },
  { header: 'StackPointer', path: 'stackPointer', tooltip: 'StackPointer' },
]

const registry: Record<string, GridConfig> = {
  // §5.1 DbmDumpHeapStat → MethodTable page for detail (MT column navigates to /MethodTable/<hex>)
  DbmDumpHeapStat: {
    masterColumns: [
      { header: 'Type', path: 'typeName', tooltip: 'Type' },
      { header: 'MT', path: 'mt', format: '0:X16', tooltip: 'MethodTable — click to see all objects with this MT' },
      { header: 'Graph Size', path: 'graphSize', format: '0:N0', alignRight: true, tooltip: 'GraphSize' },
    ],
    detailColumns: clrObjectDetail,
  },
  // §5.4 DbmStaticFields → ClrObject details
  DbmStaticFields: {
    masterColumns: [
      { header: 'Static field name', path: 'field.name', tooltip: 'Field.Name' },
      { header: 'Field Type', path: 'field.type.name', tooltip: 'Field.Type.Name' },
      { header: 'Graph Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Graph size (includes referenced objects)' },
      { header: 'Obj Address', path: 'obj.address', format: '0:X16', alignRight: true, tooltip: 'Object address — click to inspect' },
      { header: 'Obj Type', path: 'obj.type.name', tooltip: 'Object type name' },
      { header: 'Obj Size', path: 'obj.size', format: '0:N0', alignRight: true, tooltip: 'Object size (individual)' },
    ],
    detailColumns: clrObjectDetail,
  },
  // §5.5 DbmDupStrings — no details
  DbmDupStrings: {
    masterColumns: [
      { header: 'String', path: 'text', tooltip: 'Text' },
      { header: 'Count', path: 'count', format: '0:N0', alignRight: true, tooltip: 'Count' },
    ],
    detailColumns: [],
  },
  // §5.6 DbmStringsBySize — no details
  DbmStringsBySize: {
    masterColumns: [
      { header: 'Address', path: 'obj.address', format: '0:X16', alignRight: true, tooltip: 'Object address — click to inspect' },
      { header: 'Type', path: 'obj.type.name', tooltip: 'Type' },
      { header: 'Text', path: 'text', tooltip: 'The string content' },
      { header: 'Length', path: 'size', format: '0:N0', alignRight: true, tooltip: 'String length in bytes' },
    ],
    detailColumns: [],
  },
  // §5.7 ModuleDataLight — inline detail panel for ModuleDataDetail
  ModuleDataLight: {
    masterColumns: [
      { header: 'Address', path: 'address', tooltip: 'Address' },
      { header: 'Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Size' },
      { header: 'Dynamic', path: 'isDynamic', tooltip: 'IsDynamic' },
      { header: 'Native', path: 'isNative', tooltip: 'IsNative' },
      { header: 'AssemblyName', path: 'assemblyName', tooltip: 'AssemblyName' },
      { header: 'FileName', path: 'name', tooltip: 'FileName' },
      { header: 'FilePath', path: 'filePath', tooltip: 'FilePath' },
    ],
    detailColumns: [],
  },
  // ModuleDataDetail — for the inline detail panel (not grid details)
  ModuleDataDetail: {
    masterColumns: [],
    detailColumns: [],
  },
  // §5.8–5.9 DbmStackFrame → ClrStackFrame details
  DbmStackFrame: {
    masterColumns: [
      { header: 'IsAlive', path: 'thread.isAlive', tooltip: 'Thread.IsAlive' },
      { header: 'ManagedThreadId', path: 'thread.managedThreadId', tooltip: 'Thread.ManagedThreadId' },
      { header: 'Address', path: 'thread.address', tooltip: 'Thread.Address' },
    ],
    detailColumns: stackFrameDetail,
  },
  // §5.10 ClrRoot — no details
  // Columns: Root Kind, IsPinned, Object Address (clickable ↗), Object Type, Object Size,
  //          Object MT (MethodTable), Assembly, Module File, IsFree, ALC
  ClrRoot: {
    masterColumns: [
      { header: 'Root Kind', path: 'rootKind', tooltip: 'Type of GC root (StaticVar, StackLocal, Handle, Finalizer, etc.)' },
      { header: 'IsPinned', path: 'isPinned', tooltip: 'Whether this root reference is pinned' },
      { header: 'Object Address', path: 'object.address', format: '0:X16', alignRight: true, tooltip: 'Managed heap object address — click to inspect' },
      { header: 'Object Type', path: 'object.type.name', tooltip: 'Type name of the managed object this root points to' },
      { header: 'Object Size', path: 'object.size', format: '0:N0', alignRight: true, tooltip: 'Size of the managed object in bytes' },
      { header: 'Object MT', path: 'object.type.address', format: '0:X16', alignRight: true, tooltip: 'MethodTable address of the object\'s type — click to inspect' },
      { header: 'Assembly', path: 'object.type.module.assemblyName', tooltip: 'Assembly that defines this type' },
      { header: 'Module', path: 'object.type.module.name', tooltip: 'Module file path' },
      { header: 'IsFree', path: 'object.type.isFree', tooltip: 'Whether this is a free (unused) object' },
      { header: 'ALC', path: 'object.type.assemblyLoadContextAddress', format: '0:X16', alignRight: true, tooltip: 'AssemblyLoadContext that loaded this type — useful for ALC leak diagnosis' },
    ],
    detailColumns: [],
  },
  // §5.2–5.3 ClrObject (standalone) — no details
  ClrObject: {
    masterColumns: clrObjectDetail,
    detailColumns: [],
  },
  // §5.11 DbmAllocatorGroup → ClrObject details
  DbmAllocatorGroup: {
    masterColumns: [
      { header: 'Allocator Address', path: 'allocator.address', format: '0:X16', alignRight: true, tooltip: 'Allocator.Address' },
      { header: 'Allocator Size', path: 'allocator.size', format: '0:N0', alignRight: true, tooltip: 'Allocator.Size' },
      { header: 'Allocator Type', path: 'allocator.type', tooltip: 'Allocator.Type' },
      { header: 'Allocator Name', path: 'name', tooltip: 'Name' },
    ],
    detailColumns: clrObjectDetail,
  },
}

/**
 * Returns the column definitions for a query result type.
 * Falls back to auto-columns from the first row if unknown.
 */
export function getGridConfig(resultType: string): GridConfig {
  // Try exact match first
  if (registry[resultType]) {
    console.warn(`[DIAG] getGridConfig exact match for "${resultType}": ${registry[resultType].masterColumns.length} cols`)
    return registry[resultType]
  }
  // Try partial match (resultType may include namespace)
  const key = Object.keys(registry).find((k) => resultType.endsWith(k) || resultType.includes(k))
  if (key) {
    console.warn(`[DIAG] getGridConfig partial match: "${resultType}" → key "${key}": ${registry[key].masterColumns.length} cols`)
    return registry[key]
  }
  console.warn(`[DIAG] getGridConfig NO MATCH for "${resultType}" — returning empty config`)
  return { masterColumns: [], detailColumns: [] }
}
