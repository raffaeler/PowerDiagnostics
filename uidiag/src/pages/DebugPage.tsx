import { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Box, Typography, Button, Stack, Paper, Alert, LinearProgress, TextField } from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import MapIcon from '@mui/icons-material/Map'
import SearchIcon from '@mui/icons-material/Search'
import QueryPicker from '@/components/debug/QueryPicker'
import FilterBar from '@/components/debug/FilterBar'
import MasterDetailGrid from '@/components/debug/MasterDetailGrid'
import GcRootPanel from '@/components/debug/GcRootPanel'
import HexViewerDialog from '@/components/debug/HexViewerDialog'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

export default function DebugPage() {
  const { sessionId } = useParams<{ sessionId: string }>()
  const navigate = useNavigate()
  const {
    activeSessionId,
    selectedQuery,
    isLoading,
    queryProgress,
    sessions,
    fetchSessions,
    setActiveSessionId,
    runQuery,
    fetchGcRootPath,
    fetchHexData,
    clearAddressState,
    fetchModuleDetail,
  } = useDiagnosticsStore()

  const [hexDialogOpen, setHexDialogOpen] = useState(false)
  const [addressInput, setAddressInput] = useState('')

  // Sync sessionId from URL
  useEffect(() => {
    if (sessionId && sessionId !== activeSessionId) {
      setActiveSessionId(sessionId)
    }
  }, [sessionId, activeSessionId, setActiveSessionId])

  // Fetch sessions on mount
  useEffect(() => {
    fetchSessions()
  }, [fetchSessions])

  // Clear any address-specific state (GC root paths, hex data) when entering
  // the DebugPage context — this stale data belongs to the Address/Detail pages.
  useEffect(() => {
    clearAddressState()
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  // Detect expired/missing session (sessionId from URL not in sessions list)
  const sessionExpired = sessionId && sessions.length > 0 && !sessions.some((s) => s.sessionId === sessionId)

  // ── Handlers ──
  const handleRun = useCallback(() => {
    if (!activeSessionId || !selectedQuery) return
    runQuery(activeSessionId, selectedQuery)
  }, [activeSessionId, selectedQuery, runQuery])

  // View object at address
  const handleAddressView = useCallback(() => {
    const trimmed = addressInput.trim()
    if (!trimmed || !activeSessionId) return
    navigate(`/debug/${activeSessionId}/address/${encodeURIComponent(trimmed)}`)
  }, [addressInput, activeSessionId, navigate])

  // Double-click a row → open hex viewer
  const handleObjectDoubleClick = useCallback(
    (objectAddress: string) => {
      if (!activeSessionId) return
      fetchHexData(activeSessionId, objectAddress)
      setHexDialogOpen(true)
    },
    [activeSessionId, fetchHexData],
  )

  // Single-click a ClrObject row → fetch GC root path
  const handleObjectSelect = useCallback(
    (objectAddress: string) => {
      if (!activeSessionId) return
      fetchGcRootPath(activeSessionId, objectAddress)
    },
    [activeSessionId, fetchGcRootPath],
  )

  // Single-click a module row → show detail panel
  const handleModuleSelect = useCallback(
    (moduleName: string) => {
      if (!activeSessionId) return
      fetchModuleDetail(activeSessionId, moduleName)
    },
    [activeSessionId, fetchModuleDetail],
  )

  // ── Empty state: no session ──
  if (!activeSessionId) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '50vh', gap: 2 }}>
        <Typography variant="h5" color="text.secondary">
          No active session
        </Typography>
        <Typography variant="body1" color="text.secondary">
          Open a dump file or take a snapshot from the{' '}
          <Button variant="text" onClick={() => navigate('/')} sx={{ textTransform: 'none', verticalAlign: 'baseline' }}>
            Home
          </Button>{' '}
          page to get started.
        </Typography>
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
      {/* Header */}
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h5">
          Snapshot / Dump Analysis
          {activeSessionId && (
            <Typography
              component="span"
              variant="body2"
              color="text.secondary"
              sx={{ ml: 2, fontFamily: 'monospace' }}
            >
              Session: {activeSessionId}
            </Typography>
          )}
        </Typography>
      </Box>

      {/* Session expired warning */}
      {sessionExpired && (
        <Alert severity="warning" onClose={() => navigate('/')} sx={{ mb: 2 }}>
          Session <strong>{sessionId}</strong> has expired or was closed. Return to Home to start a new session.
        </Alert>
      )}

      {/* Toolbar */}
      <Paper variant="outlined" sx={{ p: 1.5, mb: 2 }}>
        <Stack direction="row" spacing={1.5} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap' }}>
          <QueryPicker />
          <FilterBar />
          <Button
            variant="contained"
            startIcon={isLoading ? undefined : <PlayArrowIcon />}
            onClick={handleRun}
            disabled={!activeSessionId || !selectedQuery || isLoading}
            size="medium"
          >
            {isLoading ? 'Running…' : 'Run'}
          </Button>
          {activeSessionId && (
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
              <TextField
                size="small"
                placeholder="0x00000000..."
                value={addressInput}
                onChange={(e) => setAddressInput(e.target.value)}
                onKeyDown={(e) => e.key === 'Enter' && handleAddressView()}
                sx={{ width: 220, '& .MuiInputBase-input': { fontFamily: 'monospace', fontSize: 13 } }}
              />
              <Button
                variant="outlined"
                size="small"
                startIcon={<SearchIcon />}
                onClick={handleAddressView}
                disabled={!addressInput.trim()}
              >
                View
              </Button>
            </Stack>
          )}
          {activeSessionId && (
            <Button
              variant="outlined"
              startIcon={<MapIcon />}
              onClick={() => navigate(`/debug/${activeSessionId}/memorymap`)}
              size="medium"
            >
              Memory Map
            </Button>
          )}
        </Stack>
        {queryProgress && (
          <Box sx={{ mt: 1.5 }}>
            <Typography variant="caption" color="text.secondary">
              {queryProgress.status}
            </Typography>
            <LinearProgress sx={{ mt: 0.5 }} />
          </Box>
        )}
      </Paper>

      {/* Master-Detail Grid + GC Root Path Panel — share space naturally */}
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <MasterDetailGrid
          onObjectDoubleClick={handleObjectDoubleClick}
          onObjectSelect={handleObjectSelect}
          onModuleSelect={handleModuleSelect}
        />

        {/* GC Root Path Panel */}
        <GcRootPanel />
      </Box>

      {/* Hex Viewer Dialog */}
      <HexViewerDialog open={hexDialogOpen} onClose={() => setHexDialogOpen(false)} sessionId={activeSessionId} />
    </Box>
  )
}
