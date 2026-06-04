import { useEffect, useCallback, useState } from 'react'
import { Box, Paper, Typography, Button, CircularProgress, TextField } from '@mui/material'
import RefreshIcon from '@mui/icons-material/Refresh'
import ProcessItem from './ProcessItem'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

export default function ProcessPicker() {
  const { processes, selectedProcess, selectProcess, fetchProcesses, processesFetched } =
    useDiagnosticsStore()
  const [filterText, setFilterText] = useState('')

  useEffect(() => {
    fetchProcesses()
  }, [fetchProcesses])

  const filteredProcesses = processes.filter((p) => {
    if (!filterText.trim()) return true
    const lower = filterText.toLowerCase()
    return p.name.toLowerCase().includes(lower) || String(p.id).includes(lower)
  })

  const handleSelect = useCallback(
    (p: typeof processes[number]) => {
      // Deselect if clicking the already-selected process
      if (selectedProcess?.id === p.id) {
        selectProcess(null)
      } else {
        selectProcess(p)
      }
    },
    [selectedProcess, selectProcess],
  )

  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Box sx={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', mb: 2 }}>
        <Typography variant="h6">.NET Processes</Typography>
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1 }}>
          <TextField
            size="small"
            placeholder="Filter"
            value={filterText}
            onChange={(e) => setFilterText(e.target.value)}
            sx={{ width: 180 }}
          />
          <Button startIcon={<RefreshIcon />} size="small" onClick={fetchProcesses}>
            Refresh
          </Button>
        </Box>
      </Box>

      {!processesFetched ? (
        <Box sx={{ display: 'flex', alignItems: 'center', gap: 1, py: 4, justifyContent: 'center' }}>
          <CircularProgress size={20} />
          <Typography color="text.secondary">Loading processes…</Typography>
        </Box>
      ) : filteredProcesses.length === 0 ? (
        <Box sx={{ py: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            {filterText ? 'No matching processes' : 'No .NET processes found'}
          </Typography>
        </Box>
      ) : (
        filteredProcesses.map((p) => (
          <ProcessItem
            key={p.id}
            process={p}
            selected={selectedProcess?.id === p.id}
            onSelect={handleSelect}
          />
        ))
      )}
    </Paper>
  )
}
