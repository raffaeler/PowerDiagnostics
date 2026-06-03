import { useCallback, useRef, useEffect } from 'react'
import { Box, Typography } from '@mui/material'
import { DataGrid, type GridColDef, type GridRowParams, type GridPaginationModel } from '@mui/x-data-grid'
import { extractAddress } from '@/utils/gridUtils'

export interface GenericDataGridProps {
  /** Row data */
  rows: unknown[]
  /** Column definitions (from buildGridColumns) */
  columns: GridColDef[]
  /** Title shown above the grid */
  title?: string
  /** Subtitle/secondary text (e.g., row count) */
  subtitle?: string
  /** Called on row click with the row object and extracted address */
  onRowClick?: (row: Record<string, unknown>, address: string | null) => void
  /** Called on row double-click with the row object and extracted address */
  onRowDoubleClick?: (row: Record<string, unknown>, address: string | null) => void
  /** Controlled pagination model (page/pageSize) */
  paginationModel?: GridPaginationModel
  /** Called when pagination changes */
  onPaginationModelChange?: (model: GridPaginationModel) => void
  /** Row density */
  density?: 'compact' | 'standard' | 'comfortable'
  /** Maximum height before scrolling */
  maxHeight?: number | string
  /** Page size options */
  pageSizeOptions?: number[]
  /** Default page size */
  defaultPageSize?: number
  /** Message when there are no rows */
  emptyMessage?: string
}

/**
 * Generic DataGrid wrapper used across the app.
 * Provides consistent styling, address extraction, and row interaction.
 *
 * The grid is GENERIC — it renders whatever columns are passed.
 * Customization per query type happens in gridRegistry (column definitions).
 */
export default function GenericDataGrid({
  rows,
  columns,
  title,
  subtitle,
  onRowClick,
  onRowDoubleClick,
  paginationModel,
  onPaginationModelChange,
  density = 'compact',
  maxHeight = '100%',
  pageSizeOptions = [25, 50, 100],
  defaultPageSize = 50,
  emptyMessage = 'No data.',
}: GenericDataGridProps) {
  // Dedup map: track how many times each base ID has been seen in this render pass.
  // MUI DataGrid's row virtualization breaks catastrophically (wrong data, duplicates)
  // when getRowId returns the same value for multiple rows. This guarantees uniqueness.
  const dedupRef = useRef<Map<string, number>>(new Map())
  useEffect(() => { dedupRef.current.clear() })

  const getRowId = useCallback(
    (row: unknown) => {
      const r = row as Record<string, unknown>
      const ownAddr = r.address ?? r.Address
      let baseId: string
      if (typeof ownAddr === 'string' && /^[0-9a-fA-F]+$/.test(ownAddr)) {
        baseId = ownAddr
      } else if (typeof ownAddr === 'number') {
        baseId = ownAddr.toString(16)
      } else {
        const addr = extractAddress(r)
        baseId = addr ?? JSON.stringify(r)
      }
      // Ensure uniqueness: if this baseId was already seen, append a suffix
      const count = dedupRef.current.get(baseId) ?? 0
      dedupRef.current.set(baseId, count + 1)
      return count === 0 ? baseId : `${baseId}_${count}`
    },
    [],
  )

  const handleRowClick = useCallback(
    (params: GridRowParams) => {
      if (!onRowClick) return
      const row = params.row as Record<string, unknown>
      const address = extractAddress(row)
      onRowClick(row, address)
    },
    [onRowClick],
  )

  const handleRowDoubleClick = useCallback(
    (params: GridRowParams) => {
      if (!onRowDoubleClick) return
      const row = params.row as Record<string, unknown>
      const address = extractAddress(row)
      onRowDoubleClick(row, address)
    },
    [onRowDoubleClick],
  )

  if (rows.length === 0) {
    return (
      <Box
        sx={{
          display: 'flex',
          flexDirection: 'column',
          alignItems: 'center',
          justifyContent: 'center',
          minHeight: 120,
          gap: 1,
        }}
      >
        <Typography variant="body2" color="text.secondary">
          {emptyMessage}
        </Typography>
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5, flex: 1, minHeight: 0 }}>
      {(title || subtitle) && (
        <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, px: 0.5 }}>
          {title && <Typography variant="subtitle2">{title}</Typography>}
          {subtitle && (
            <Typography variant="caption" color="text.secondary">
              {subtitle}
            </Typography>
          )}
        </Box>
      )}
      <Box sx={{ flex: 1, minHeight: 0 }}>
        <DataGrid
          rows={rows as Record<string, unknown>[]}
          columns={columns}
          getRowId={getRowId}
          onRowClick={handleRowClick}
          onRowDoubleClick={handleRowDoubleClick}
          density={density}
          disableColumnMenu
          pageSizeOptions={pageSizeOptions}
          paginationModel={paginationModel}
          onPaginationModelChange={onPaginationModelChange}
          initialState={{
            pagination: { paginationModel: { pageSize: defaultPageSize, page: 0 } },
          }}
          sx={{
            maxHeight,
            border: 'none',
            '& .MuiDataGrid-row:hover': { cursor: 'pointer' },
            '& .MuiDataGrid-cell': { fontFamily: "'Segoe UI', sans-serif", fontSize: 13 },
            '& .monospace-cell': { fontFamily: "'Cascadia Code', 'Consolas', monospace" },
          }}
        />
      </Box>
    </Box>
  )
}
