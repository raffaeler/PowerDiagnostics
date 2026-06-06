import { useMemo, useState, useCallback } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Box, Typography, Button, Paper, Breadcrumbs, Link } from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import GenericDataGrid from '@/components/shared/GenericDataGrid'
import HexViewerDialog from '@/components/debug/HexViewerDialog'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { getGridConfig } from '@/config/gridRegistry'
import { buildGridColumns } from '@/utils/gridUtils'

/**
 * Detail page shown when clicking a row in a master grid that has detail data.
 * Route: /debug/:sessionId/detail/:queryName/:rowIndex
 *
 * Shows a back button that returns to the exact previous debug view (grid state preserved in store).
 * Uses the same GenericDataGrid, customized by the detail column definitions from gridRegistry.
 */
export default function DetailPage() {
  const { sessionId, queryName, rowIndex } = useParams<{
    sessionId: string
    queryName: string
    rowIndex: string
  }>()
  const navigate = useNavigate()
  const location = useLocation()

  const detailGridData = useDiagnosticsStore((s) => s.detailGridData)
  const queryMetadata = useDiagnosticsStore((s) => s.queryMetadata)
  const activeSessionId = useDiagnosticsStore((s) => s.activeSessionId)

  const [hexDialogOpen, setHexDialogOpen] = useState(false)

  // ── Build detail columns ──
  const detailColumns = useMemo(() => {
    // Prefer server metadata
    const meta = queryMetadata.find((m) => m.queryName === queryName)
    if (meta && meta.detailColumns.length > 0) {
      return buildGridColumns(meta.detailColumns, detailGridData ?? [], sessionId, location.pathname)
    }

    // Fall back to client gridRegistry — we need the resultType to find detail columns
    if (meta?.detailType) {
      const cfg = getGridConfig(meta.detailType)
      if (cfg.detailColumns.length > 0) return buildGridColumns(cfg.detailColumns, detailGridData ?? [], sessionId, location.pathname)
    }

    // Try gridRegistry by queryName
    const cfg = getGridConfig(queryName ?? '')
    if (cfg.detailColumns.length > 0) return buildGridColumns(cfg.detailColumns, detailGridData ?? [], sessionId, location.pathname)

    return []
  }, [queryMetadata, queryName, detailGridData, sessionId, location.pathname])

  // ── Row click → select object for GC root ──
  const handleRowClick = useCallback(
    (_row: Record<string, unknown>, address: string | null) => {
      if (address && activeSessionId) {
        useDiagnosticsStore.getState().fetchGcRootPath(activeSessionId, address)
      }
    },
    [activeSessionId],
  )

  // ── Row double-click → hex viewer ──
  const handleRowDoubleClick = useCallback(
    (_row: Record<string, unknown>, address: string | null) => {
      if (address && activeSessionId) {
        useDiagnosticsStore.getState().fetchHexData(activeSessionId, address)
        setHexDialogOpen(true)
      }
    },
    [activeSessionId],
  )

  const backTarget =
    typeof location.state === 'object' &&
    location.state !== null &&
    'from' in location.state &&
    typeof (location.state as { from?: unknown }).from === 'string'
      ? (location.state as { from: string }).from
      : `/debug/${sessionId}`

  // ── Empty / error states ──
  if (!detailGridData || detailGridData.length === 0) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2, mt: 8 }}>
        <Typography variant="h6" color="text.secondary">
          No detail data available
        </Typography>
        <Typography variant="body2" color="text.secondary">
          The detail data may have been lost. Return to the query results and click the row again.
        </Typography>
        <Button variant="contained" startIcon={<ArrowBackIcon />} onClick={() => navigate(backTarget)}>
          Back to results
        </Button>
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {/* Breadcrumb + Back */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <Button
          variant="outlined"
          startIcon={<ArrowBackIcon />}
          onClick={() => navigate(backTarget)}
          size="small"
        >
          Back
        </Button>
        <Breadcrumbs>
          <Link
            underline="hover"
            color="inherit"
            href={`/debug/${sessionId}`}
            onClick={(e) => {
              e.preventDefault()
              navigate(`/debug/${sessionId}`)
            }}
          >
            Debug: {sessionId}
          </Link>
          <Typography color="text.primary">
            {queryName} row #{rowIndex}
          </Typography>
        </Breadcrumbs>
      </Box>

      {/* Header */}
      <Paper variant="outlined" sx={{ p: 1.5, mb: 2 }}>
        <Typography variant="h6">
          Details: {queryName}
          <Typography component="span" variant="body2" color="text.secondary" sx={{ ml: 2 }}>
            Row {rowIndex} of session {sessionId}
          </Typography>
        </Typography>
      </Paper>

      {/* Detail grid */}
      <Box sx={{ mb: 1 }}>
        <GenericDataGrid
          rows={detailGridData}
          columns={detailColumns}
          title={`${detailGridData.length} item${detailGridData.length !== 1 ? 's' : ''}`}
          onRowClick={handleRowClick}
          onRowDoubleClick={handleRowDoubleClick}
          pageSizeOptions={[25, 50, 100]}
          defaultPageSize={50}
        />
      </Box>

      {/* Hex Viewer Dialog */}
      <HexViewerDialog open={hexDialogOpen} onClose={() => setHexDialogOpen(false)} />
    </Box>
  )
}
