import { useEffect, useMemo, useState, useCallback } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import { Box, Typography, Button, Paper, Breadcrumbs, Link, Skeleton } from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import GenericDataGrid from '@/components/shared/GenericDataGrid'
import HexViewerDialog from '@/components/debug/HexViewerDialog'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { buildGridColumns } from '@/utils/gridUtils'
import type { ColumnDefinition } from '@/types/api'

/**
 * MethodTable page — shows all heap objects for a given MethodTable address.
 * Route: /debug/:sessionId/MethodTable/:mt
 *
 * Replaces the old /detail/DumpHeapStat/:rowIndex pattern.
 * The MT hex value is a stable identifier that does not depend on sort order.
 */
export default function MethodTablePage() {
  const { sessionId, mt } = useParams<{
    sessionId: string
    mt: string
  }>()
  const navigate = useNavigate()
  const location = useLocation()

  const methodTableData = useDiagnosticsStore((s) => s.methodTableData)
  const isLoading = useDiagnosticsStore((s) => s.isLoading)
  const activeSessionId = useDiagnosticsStore((s) => s.activeSessionId)
  const fetchMethodTableObjects = useDiagnosticsStore((s) => s.fetchMethodTableObjects)
  const fetchGcRootPath = useDiagnosticsStore((s) => s.fetchGcRootPath)
  const fetchHexData = useDiagnosticsStore((s) => s.fetchHexData)

  const [hexDialogOpen, setHexDialogOpen] = useState(false)

  // Fetch data when page loads or MT changes
  useEffect(() => {
    if (sessionId && mt) {
      fetchMethodTableObjects(sessionId, mt)
    }
  }, [sessionId, mt, fetchMethodTableObjects])

  // ── Detail columns (Address, Size, Type) ──
  const detailColumns: ColumnDefinition[] = useMemo(() => [
    { header: 'Address', path: 'address', format: '0:X16', alignRight: true, tooltip: 'Address' },
    { header: 'Size', path: 'size', format: '0:N0', alignRight: true, tooltip: 'Size' },
    { header: 'Type', path: 'type', tooltip: 'Type' },
  ], [])

  // ── Build DataGrid columns ──
  const gridColumns = useMemo(() => {
    const objects = methodTableData?.objects ?? []
    return buildGridColumns(detailColumns, objects, activeSessionId ?? undefined, location.pathname)
  }, [detailColumns, methodTableData, activeSessionId, location.pathname])

  // ── Row click → GC root ──
  const handleRowClick = useCallback(
    (_row: Record<string, unknown>, address: string | null) => {
      if (address && activeSessionId) {
        fetchGcRootPath(activeSessionId, address)
      }
    },
    [activeSessionId, fetchGcRootPath],
  )

  // ── Row double-click → hex viewer ──
  const handleRowDoubleClick = useCallback(
    (_row: Record<string, unknown>, address: string | null) => {
      if (address && activeSessionId) {
        fetchHexData(activeSessionId, address)
        setHexDialogOpen(true)
      }
    },
    [activeSessionId, fetchHexData],
  )

  const backTarget =
    typeof location.state === 'object' &&
    location.state !== null &&
    'from' in location.state &&
    typeof (location.state as { from?: unknown }).from === 'string'
      ? (location.state as { from: string }).from
      : `/debug/${sessionId}`

  const mtDisplay = mt ? `0x${mt.toUpperCase().padStart(16, '0')}` : ''

  // ── Loading state ──
  if (isLoading || (!methodTableData && !isLoading)) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
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
              Debug: {sessionId?.substring(0, 8)}…
            </Link>
            <Typography color="text.primary">
              MethodTable {mtDisplay}
            </Typography>
          </Breadcrumbs>
        </Box>
        <Skeleton variant="rounded" sx={{ flex: 1, minHeight: 400 }} />
      </Box>
    )
  }

  // ── Not found ──
  if (!methodTableData) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', gap: 2, mt: 8 }}>
        <Typography variant="h6" color="text.secondary">
          MethodTable not found
        </Typography>
        <Typography variant="body2" color="text.secondary">
          No objects found for MT {mtDisplay}
        </Typography>
        <Button variant="contained" startIcon={<ArrowBackIcon />} onClick={() => navigate(backTarget)}>
          Back to results
        </Button>
      </Box>
    )
  }

  const rows = methodTableData.objects as unknown as Record<string, unknown>[]
  const typeName = methodTableData.typeName ?? 'Unknown'

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
            Debug: {sessionId?.substring(0, 8)}…
          </Link>
          <Typography color="text.primary">
            MethodTable {mtDisplay}
          </Typography>
        </Breadcrumbs>
      </Box>

      {/* Header */}
      <Paper variant="outlined" sx={{ p: 1.5, mb: 2 }}>
        <Typography variant="h6">
          {typeName}
          <Typography component="span" variant="body2" color="text.secondary" sx={{ ml: 2 }}>
            MT: {mtDisplay} — {methodTableData.objectCount} object{methodTableData.objectCount !== 1 ? 's' : ''}, {(methodTableData.graphSize ?? 0).toLocaleString()} bytes
          </Typography>
        </Typography>
      </Paper>

      {/* Objects grid */}
      <Box sx={{ mb: 1 }}>
        <GenericDataGrid
          rows={rows}
          columns={gridColumns}
          title={`${rows.length} object${rows.length !== 1 ? 's' : ''}`}
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
