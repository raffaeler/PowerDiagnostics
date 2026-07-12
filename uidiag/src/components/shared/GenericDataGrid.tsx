import { useCallback, useRef, useEffect } from 'react'
import { Box, Typography, IconButton, Tooltip, MenuItem, ListItemIcon, ListItemText } from '@mui/material'
import ViewColumnIcon from '@mui/icons-material/ViewColumn'
import TextFieldsIcon from '@mui/icons-material/TextFields'
import { DataGrid, useGridApiRef, useGridApiContext, GridColumnMenu, type GridColDef, type GridCellParams, type GridPaginationModel, type GridColumnMenuProps } from '@mui/x-data-grid'
import { extractAddress } from '@/utils/gridUtils'

/** Column menu item: resizes the current column to fill the remaining viewport width. */
function MenuItemFitColumn({ colDef, onClick }: { colDef: GridColDef; onClick: (e: React.MouseEvent) => void }) {
  const apiRef = useGridApiContext()
  const handleClick = (e: React.MouseEvent) => {
    onClick(e) // close the column menu
    const api = apiRef.current
    const dims = api.getRootDimensions()
    if (!dims) return
    const viewportWidth = dims.viewportInnerSize.width
    const otherCols = api.getVisibleColumns().filter((c) => c.field !== colDef.field)
    const otherWidth = otherCols.reduce((sum, c) => sum + (c.computedWidth ?? 100), 0)
    const fitWidth = Math.max(120, viewportWidth - otherWidth)
    api.setColumnWidth(colDef.field, fitWidth)
  }
  return (
    <MenuItem onClick={handleClick}>
      <ListItemIcon>
        <TextFieldsIcon fontSize="small" />
      </ListItemIcon>
      <ListItemText>Fit to view</ListItemText>
    </MenuItem>
  )
}

/** Wraps the default column menu to add our "Fit this column" item. */
function DefaultColumnMenu(props: GridColumnMenuProps) {
  return (
    <GridColumnMenu
      {...props}
      slots={{
        ...props.slots,
        columnMenuFitItem: MenuItemFitColumn,
      }}
      slotProps={{
        ...props.slotProps,
        columnMenuFitItem: { displayOrder: 70 },
      }}
    />
  )
}

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
  pageSizeOptions = [10, 25, 50, 100],
  defaultPageSize = 50,
  emptyMessage = 'No data.',
}: GenericDataGridProps) {
  // Grid API ref for programmatic control (auto-resize columns, etc.)
  const apiRef = useGridApiRef()

  // ── Auto-resize columns when data loads or grid mounts/updates ──
  // Runs after rows/columns change (query completes/reloads) to size columns to content.
  useEffect(() => {
    if (rows.length > 0) {
      // Immediate resize on next tick
      const timer1 = setTimeout(() => {
        apiRef.current?.autosizeColumns({
          includeOutliers: true,
          includeHeaders: true,
        })
      }, 0)

      // Delayed resize (150ms) to ensure layout has fully settled and styles are injected.
      // This handles page transitions, routing animation, or DOM dimension reflows.
      const timer2 = setTimeout(() => {
        apiRef.current?.autosizeColumns({
          includeOutliers: true,
          includeHeaders: true,
        })
      }, 150)

      return () => {
        clearTimeout(timer1)
        clearTimeout(timer2)
      }
    }
  }, [rows, columns, apiRef])

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
    (params: GridCellParams) => {
      if (!onRowClick) return
      const row = params.row as Record<string, unknown>
      const address = extractAddress(row)
      onRowClick(row, address)
    },
    [onRowClick],
  )

  const handleRowDoubleClick = useCallback(
    (params: GridCellParams) => {
      if (!onRowDoubleClick) return
      const row = params.row as Record<string, unknown>
      const address = extractAddress(row)
      onRowDoubleClick(row, address)
    },
    [onRowDoubleClick],
  )

  /** Resize all visible columns to share the available viewport width equally. */
  const fitColumnsToView = useCallback(() => {
    const api = apiRef.current
    if (!api) return
    const dims = api.getRootDimensions()
    if (!dims) return
    const availableWidth = dims.viewportInnerSize.width
    const visible = api.getVisibleColumns()
    if (visible.length === 0) return

    const equalWidth = Math.floor(availableWidth / visible.length)
    const MIN_WIDTH = 80
    const finalWidth = Math.max(MIN_WIDTH, equalWidth)

    visible.forEach((col) => {
      api.setColumnWidth(col.field, finalWidth)
    })
  }, [apiRef])

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
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 0.5 }}>
      {(title || subtitle) && (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, px: 0.5 }}>
          <Box sx={{ display: 'flex', alignItems: 'baseline', gap: 1, flex: 1 }}>
            {title && <Typography variant="subtitle2">{title}</Typography>}
            {subtitle && (
              <Typography variant="caption" color="text.secondary">
                {subtitle}
              </Typography>
            )}
          </Box>
          <Tooltip title="Fit columns to view width">
            <IconButton size="small" onClick={fitColumnsToView} sx={{ opacity: 0.5, '&:hover': { opacity: 1 } }}>
              <ViewColumnIcon fontSize="small" />
            </IconButton>
          </Tooltip>
        </Box>
      )}
      <Box sx={{ width: '100%', position: 'relative' }}>
        <DataGrid
          apiRef={apiRef}
          rows={rows as Record<string, unknown>[]}
          columns={columns}
          getRowId={getRowId}
          onCellClick={handleRowClick}
          onCellDoubleClick={handleRowDoubleClick}
          density={density}
          slots={{ columnMenu: DefaultColumnMenu }}
          pageSizeOptions={pageSizeOptions}
          paginationModel={paginationModel}
          onPaginationModelChange={onPaginationModelChange}
          initialState={{
            pagination: { paginationModel: { pageSize: defaultPageSize, page: 0 } },
          }}
          sx={{
            border: 'none',
            height: 'calc(100vh - 210px)',
            '& .MuiDataGrid-virtualScroller': {
              overflowX: 'visible',
            },
            '& .MuiDataGrid-row': { '&:hover': { cursor: 'pointer' } },
            '& .MuiDataGrid-cell': { fontFamily: "'Segoe UI', sans-serif", fontSize: 13 },
            '& .monospace-cell': { fontFamily: "'Cascadia Code', 'Consolas', monospace" },
            // Move the column menu (three-dots) icon to the left side so users
            // don't need to scroll horizontally to find it on wide columns.
            '& .MuiDataGrid-menuIcon': { order: -1, marginRight: '2px', marginLeft: 0 },
          }}
        />
      </Box>
    </Box>
  )
}
