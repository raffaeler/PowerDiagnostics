import type { ColumnDefinition } from '@/types/api'
import type { GridColDef } from '@mui/x-data-grid'
import { Link as RouterLink } from 'react-router-dom'
import { debugLog } from '@/utils/debug'

/**
 * Shared grid utilities used across the app.
 * - Address extraction from any row
 * - Dotted path resolution (case-insensitive)
 * - Cell display formatting (handles [object Object])
 * - Column building (generic, customized per query via gridRegistry)
 */

/** Extract an address/hex string from a row object. */
export function extractAddress(row: Record<string, unknown>): string | null {
  // For rows where 'object' is a nested object (e.g., ClrRoot), prefer the
  // nested Object.Address since it's the actual managed heap object address.
  if (typeof row.object === 'object' && row.object !== null) {
    const obj = row.object as Record<string, unknown>
    const addr = obj.address ?? obj.Address
    if (typeof addr === 'string' && /^[0-9a-fA-F]+$/.test(addr)) return addr
    if (typeof addr === 'number') return addr.toString(16)
  }

  const candidates = [
    row.address,
    row.obj,
    row.object,
    row.thread,
    row.allocator,
  ]
  for (const c of candidates) {
    if (typeof c === 'string' && /^[0-9a-fA-F]+$/.test(c)) return c
    if (typeof c === 'number') return c.toString(16)
  }
  return null
}

/** Resolve a dotted path on an object (e.g., "type.name" → obj.type?.name). */
export function resolvePath(obj: unknown, path: string): unknown {
  if (obj === null || obj === undefined) return null
  const parts = path.split('.')
  let current: unknown = obj
  for (const part of parts) {
    if (current === null || current === undefined) return null
    if (typeof current === 'object') {
      const objRecord = current as Record<string, unknown>
      if (part in objRecord) {
        current = objRecord[part]
      } else {
        // Case-insensitive lookup: backend uses PascalCase, client paths may be camelCase
        const lowerPart = part.toLowerCase()
        const key = Object.keys(objRecord).find(
          (k) => k.toLowerCase() === lowerPart,
        )
        if (key) {
          current = objRecord[key]
        } else {
          return null
        }
      }
    } else {
      return null
    }
  }
  return current
}

/**
 * Convert any resolved cell value to a display string.
 * Handles objects that would otherwise render as "[object Object]".
 * - null/undefined → ''
 * - string → as-is
 * - number → as-is
 * - boolean → as-is
 * - object with `.name` → that string
 * - object with `.Name` → that string
 * - other object → JSON string
 */
export function resolveCellDisplay(value: unknown): string {
  if (value === null || value === undefined) return ''
  if (typeof value === 'string' || typeof value === 'number' || typeof value === 'boolean') {
    return String(value)
  }
  if (typeof value === 'object') {
    const obj = value as Record<string, unknown>
    if (typeof obj.name === 'string') return obj.name
    if (typeof obj.Name === 'string') return obj.Name
    return JSON.stringify(value)
  }
  return String(value)
}

// Track whether we've logged the first row resolution to avoid spam
let _firstRowLogged = false

/** Convert ColumnDefinition[] to MUI DataGrid GridColDef[]. */
export function buildGridColumns(
  columns: ColumnDefinition[],
  sampleRows: unknown[],
  sessionId?: string,
  returnToPath?: string,
): GridColDef[] {
  _firstRowLogged = false
  return columns.map((col) => {
    const field = col.path
    const isHexColumn = col.format?.startsWith('0:X') ?? false
    const def: GridColDef = {
      field,
      headerName: col.header,
      description: col.tooltip ?? col.header,
      flex: 1,
      minWidth: 100,
      sortable: true,
      valueGetter: (_value, row) => {
        const result = resolvePath(row, col.path)
        if (!_firstRowLogged && sampleRows.length > 0 && row === sampleRows[0]) {
          _firstRowLogged = true
          const firstRow = sampleRows[0] as Record<string, unknown>
          debugLog('grid', `valueGetter trace (first row) for "${col.header}" (path="${col.path}"):`, {
            rowKeys: Object.keys(firstRow),
            pathParts: col.path.split('.'),
            rowHasRootKey: Object.keys(firstRow).includes(col.path.split('.')[0]),
            resolvedValue: result,
            resolvedType: typeof result,
          })
        }
        return result
      },
      // Always provide renderCell to prevent [object Object] for nested objects
      renderCell: (params) => {
        const raw = params.value
        if (isHexColumn && sessionId && raw != null && raw !== '') {
          // Use router navigation (instead of <a href>) to preserve in-memory session and detail state.
          const addr = typeof raw === 'number'
            ? raw.toString(16).toUpperCase()
            : String(raw).replace(/^0x/i, '')
          return (
            <RouterLink
              to={`/debug/${sessionId}/address/${addr}`}
              state={returnToPath ? { from: returnToPath } : undefined}
              style={{
                color: 'var(--mui-palette-primary-main, #1976d2)',
                textDecoration: 'none',
                fontFamily: 'monospace',
                cursor: 'pointer',
              }}
              onClick={(e) => e.stopPropagation()}
              title={`Open address viewer for 0x${addr}`}
            >
              {`0x${addr.toUpperCase().padStart(16, '0')}`}
            </RouterLink>
          )
        }
        return resolveCellDisplay(raw)
      },
    }

    // Right-align numeric / hex columns
    if (col.alignRight) {
      def.headerAlign = 'right'
      def.align = 'right'
    }

    // Format hint: hex addresses get monospace
    if (col.format?.startsWith('0:X')) {
      def.valueFormatter = (value) => {
        if (value == null) return ''
        const n = typeof value === 'number' ? value : parseInt(String(value), 10)
        if (isNaN(n)) return String(value)
        return `0x${n.toString(16).toUpperCase().padStart(16, '0')}`
      }
      // Hex columns use monospace
      def.cellClassName = 'monospace-cell'
    } else if (col.format === '0:N0') {
      def.valueFormatter = (value) => {
        if (value == null) return ''
        const n = Number(value)
        if (isNaN(n)) return String(value)
        return n.toLocaleString()
      }
    }

    return def
  })
}
