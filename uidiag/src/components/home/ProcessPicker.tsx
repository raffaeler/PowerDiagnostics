import { useEffect } from 'react'
import { Box, Paper, Typography, Button, CircularProgress } from '@mui/material'
import RefreshIcon from '@mui/icons-material/Refresh'
import ProcessItem from './ProcessItem'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

export default function ProcessPicker() {
  const { processes, selectedProcess, selectProcess, fetchProcesses, processesFetched } =
    useDiagnosticsStore()

  useEffect(() => {
    fetchProcesses()
  }, [fetchProcesses])

  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h6">.NET Processes</Typography>
        <Button startIcon={<RefreshIcon />} size="small" onClick={fetchProcesses}>
          Refresh
        </Button>
      </Box>

      {!processesFetched ? (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 4, justifyContent: 'center' }}>
          <CircularProgress size={20} />
          <Typography color="text.secondary">Loading processes…</Typography>
        </Box>
      ) : processes.length === 0 ? (
        <Box sx={{ py: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">No .NET processes found</Typography>
        </Box>
      ) : (
        processes.map((p) => (
          <ProcessItem
            key={p.id}
            process={p}
            selected={selectedProcess?.id === p.id}
            onSelect={selectProcess}
          />
        ))
      )}
    </Paper>
  )
}
