import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { Grid, Typography, Button, Box, Alert, CircularProgress } from '@mui/material'
import CloudUploadIcon from '@mui/icons-material/CloudUpload'
import ProcessPicker from '@/components/home/ProcessPicker'
import SessionActions from '@/components/home/SessionActions'
import DumpUploadDialog from '@/components/home/DumpUploadDialog'
import EventsBar from '@/components/debug/EventsBar'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { useSignalRStore } from '@/stores/useSignalRStore'

export default function HomePage() {
  const navigate = useNavigate()
  const [dumpDialogOpen, setDumpDialogOpen] = useState(false)
  const processes = useDiagnosticsStore((s) => s.processes)
  const processesFetched = useDiagnosticsStore((s) => s.processesFetched)
  const fetchProcesses = useDiagnosticsStore((s) => s.fetchProcesses)
  const selectedProcess = useDiagnosticsStore((s) => s.selectedProcess)
  const attachToProcess = useDiagnosticsStore((s) => s.attachToProcess)
  const detachFromProcess = useDiagnosticsStore((s) => s.detachFromProcess)
  const clearEvents = useSignalRStore((s) => s.clearEvents)

  // Track the previously selected process PID for auto-attach/detach
  const prevPidRef = useRef<number | null>(null)

  useEffect(() => {
    fetchProcesses()
  }, [fetchProcesses])

  // Auto-attach/detach when selected process changes
  useEffect(() => {
    const currentPid = selectedProcess?.id ?? null
    const prevPid = prevPidRef.current

    if (prevPid !== currentPid) {
      // Detach from previous process
      if (prevPid !== null) {
        detachFromProcess()
        clearEvents()
      }
      // Attach to new process
      if (currentPid !== null) {
        attachToProcess(currentPid)
      }
      prevPidRef.current = currentPid
    }
  }, [selectedProcess, attachToProcess, detachFromProcess, clearEvents])

  const handleSessionCreated = (sessionId: string) => {
    navigate(`/debug/${sessionId}`)
  }

  return (
    <>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 3 }}>
        <Typography variant="h5">Home</Typography>
        <Button
          variant="contained"
          startIcon={<CloudUploadIcon />}
          onClick={() => setDumpDialogOpen(true)}
        >
          Open Dump
        </Button>
      </Box>

      {/* Empty state: no processes found */}
      {processesFetched && processes.length === 0 && (
        <Alert severity="info" sx={{ mb: 3 }}>
          No .NET processes found. Make sure a .NET application is running, or open a crash dump file instead.
        </Alert>
      )}

      {/* Real-time events (auto-subscribed when a process is selected) */}
      <EventsBar />

      <Grid container spacing={3}>
        <Grid size={{ xs: 12, md: 7 }}>
          {!processesFetched ? (
            <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, py: 4 }}>
              <CircularProgress size={24} />
              <Typography color="text.secondary">Scanning for .NET processes…</Typography>
            </Box>
          ) : (
            <ProcessPicker />
          )}
        </Grid>
        <Grid size={{ xs: 12, md: 5 }}>
          <SessionActions />
        </Grid>
      </Grid>

      <DumpUploadDialog
        open={dumpDialogOpen}
        onClose={() => setDumpDialogOpen(false)}
        onSessionCreated={handleSessionCreated}
      />
    </>
  )
}
