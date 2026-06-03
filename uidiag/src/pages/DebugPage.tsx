import { useEffect, useState, useCallback } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { Box, Typography, Button, Stack, Paper, Alert } from '@mui/material'
import PlayArrowIcon from '@mui/icons-material/PlayArrow'
import CloseIcon from '@mui/icons-material/Close'
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
    sessions,
    fetchSessions,
    setActiveSessionId,
    runQuery,
    closeSession,
    fetchGcRootPath,
    fetchHexData,
  } = useDiagnosticsStore()

  const [hexDialogOpen, setHexDialogOpen] = useState(false)

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

  // Detect expired/missing session (sessionId from URL not in sessions list)
  const sessionExpired = sessionId && sessions.length > 0 && !sessions.some((s) => s.sessionId === sessionId)

  // ── Handlers ──
  const handleRun = useCallback(() => {
    if (!activeSessionId || !selectedQuery) return
    runQuery(activeSessionId, selectedQuery)
  }, [activeSessionId, selectedQuery, runQuery])

  const handleCloseSession = useCallback(async () => {
    if (!activeSessionId) return
    await closeSession(activeSessionId)
    navigate('/')
  }, [activeSessionId, closeSession, navigate])

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
              Session: {activeSessionId.substring(0, 8)}…
            </Typography>
          )}
        </Typography>
      </Box>

      {/* Session expired warning */}
      {sessionExpired && (
        <Alert severity="warning" onClose={() => navigate('/')} sx={{ mb: 2 }}>
          Session <strong>{sessionId?.substring(0, 8)}…</strong> has expired or was closed. Return to Home to start a new session.
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
            <Button
              variant="outlined"
              color="error"
              startIcon={<CloseIcon />}
              onClick={handleCloseSession}
              size="medium"
              sx={{ ml: 'auto' }}
            >
              Close Session
            </Button>
          )}
        </Stack>
      </Paper>

      {/* Master-Detail Grid + GC Root Path Panel — share space naturally */}
      <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
        <MasterDetailGrid
          onObjectDoubleClick={handleObjectDoubleClick}
          onObjectSelect={handleObjectSelect}
        />

        {/* GC Root Path Panel */}
        <GcRootPanel />
      </Box>

      {/* Hex Viewer Dialog */}
      <HexViewerDialog open={hexDialogOpen} onClose={() => setHexDialogOpen(false)} />
    </Box>
  )
}
