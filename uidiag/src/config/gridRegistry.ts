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
  { header: 'StackPointer', path: 'stackPointer', format: '0:X16', alignRight: true, tooltip: 'StackPointer' },
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
      { header: 'Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Size' },
      { header: 'Object', path: 'obj', tooltip: 'Obj' },
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
      { header: 'Object', path: 'obj', tooltip: 'Obj' },
      { header: 'String', path: 'text', tooltip: 'Text' },
      { header: 'Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Size' },
    ],
    detailColumns: [],
  },
  // §5.7 ModuleDataLight — inline detail panel for ModuleDataDetail
  ModuleDataLight: {
    masterColumns: [
      { header: 'AssemblyName', path: 'assemblyName', tooltip: 'AssemblyName' },
      { header: 'Name', path: 'name', tooltip: 'Name' },
      { header: 'Address', path: 'address', tooltip: 'Address' },
      { header: 'Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Size' },
      { header: 'Dynamic', path: 'isDynamic', tooltip: 'IsDynamic' },
      { header: 'Native', path: 'isNative', tooltip: 'IsNative' },
      { header: 'File', path: 'fileName', tooltip: 'FileName' },
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
      { header: 'Address', path: 'thread.address', format: '0:X16', alignRight: true, tooltip: 'Thread.Address' },
    ],
    detailColumns: stackFrameDetail,
  },
  // §5.10 ClrRoot — no details
  ClrRoot: {
    masterColumns: [
      { header: 'IsPinned', path: 'isPinned', tooltip: 'IsPinned' },
      { header: 'Address', path: 'object.address', format: '0:X16', alignRight: true, tooltip: 'Object Address' },
      { header: 'Object', path: 'object', tooltip: 'Object' },
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
  if (registry[resultType]) return registry[resultType]
  // Try partial match (resultType may include namespace)
  const key = Object.keys(registry).find((k) => resultType.endsWith(k) || resultType.includes(k))
  if (key) return registry[key]
  return { masterColumns: [], detailColumns: [] }
}
