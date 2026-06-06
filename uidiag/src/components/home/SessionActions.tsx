import { Paper, Typography, Button, Stack, Alert } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { useState } from 'react'

export default function SessionActions() {
  const navigate = useNavigate()
  const { selectedProcess, takeSnapshot, createDump, fetchSessions } =
    useDiagnosticsStore()
  const [message, setMessage] = useState<{ text: string; severity: 'success' | 'error' } | null>(null)

  const showMessage = (text: string, severity: 'success' | 'error') => {
    setMessage({ text, severity })
    setTimeout(() => setMessage(null), 3000)
  }

  const handleSnapshot = async () => {
    if (!selectedProcess) return
    const sessionId = await takeSnapshot(selectedProcess.id)
    if (sessionId) {
      showMessage(`Snapshot created: ${sessionId}`, 'success')
      await fetchSessions()
      navigate('/debug')
    } else {
      showMessage('Snapshot failed', 'error')
    }
  }

  const handleDump = async () => {
    if (!selectedProcess) return
    const sessionId = await createDump(selectedProcess.id)
    if (sessionId) {
      showMessage(`Dump created: ${sessionId}`, 'success')
      await fetchSessions()
      navigate('/debug')
    } else {
      showMessage('Dump failed', 'error')
    }
  }

  const disabled = !selectedProcess

  return (
    <Paper variant="outlined" sx={{ p: 2, height: '100%' }}>
      <Typography variant="h6" sx={{ mb: 2 }}>
        Actions
      </Typography>

      {disabled && (
        <Typography variant="body2" color="text.secondary" sx={{ mb: 2 }}>
          Select a process first
        </Typography>
      )}

      <Stack spacing={1.5}>
        <Button variant="contained" color="secondary" disabled={disabled} onClick={handleSnapshot} fullWidth>
          Take Snapshot
        </Button>

        <Button variant="outlined" color="secondary" disabled={disabled} onClick={handleDump} fullWidth>
          Create Dump
        </Button>
      </Stack>

      {message && (
        <Alert severity={message.severity} sx={{ mt: 2 }} onClose={() => setMessage(null)}>
          {message.text}
        </Alert>
      )}
    </Paper>
  )
}
