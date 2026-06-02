import { useState, useEffect } from 'react'
import { useNavigate } from 'react-router-dom'
import { Grid, Typography, Button, Box, Alert, CircularProgress } from '@mui/material'
import CloudUploadIcon from '@mui/icons-material/CloudUpload'
import ProcessPicker from '@/components/home/ProcessPicker'
import SessionActions from '@/components/home/SessionActions'
import DumpUploadDialog from '@/components/home/DumpUploadDialog'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

export default function HomePage() {
  const navigate = useNavigate()
  const [dumpDialogOpen, setDumpDialogOpen] = useState(false)
  const processes = useDiagnosticsStore((s) => s.processes)
  const processesFetched = useDiagnosticsStore((s) => s.processesFetched)
  const fetchProcesses = useDiagnosticsStore((s) => s.fetchProcesses)

  useEffect(() => {
    fetchProcesses()
  }, [fetchProcesses])

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
