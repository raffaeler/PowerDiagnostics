import { Box, Paper, Typography, Button, Stack, Alert } from '@mui/material'
import { useNavigate } from 'react-router-dom'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { useState } from 'react'

export default function SessionActions() {
  const navigate = useNavigate()
  const { selectedProcess, isAttached, attachToProcess, detachFromProcess, takeSnapshot, createDump, fetchSessions } =
    useDiagnosticsStore()
  const [message, setMessage] = useState<{ text: string; severity: 'success' | 'error' } | null>(null)

  const showMessage = (text: string, severity: 'success' | 'error') => {
    setMessage({ text, severity })
    setTimeout(() => setMessage(null), 3000)
  }

  const handleAttach = async () => {
    if (!selectedProcess) return
    const ok = await attachToProcess(selectedProcess.id)
    showMessage(ok ? 'Events attached' : 'Attach failed', ok ? 'success' : 'error')
  }

  const handleDetach = async () => {
    const ok = await detachFromProcess()
    showMessage(ok ? 'Events detached' : 'Detach failed', ok ? 'success' : 'error')
  }

  const handleSnapshot = async () => {
    if (!selectedProcess) return
    const sessionId = await takeSnapshot(selectedProcess.id)
    if (sessionId) {
      showMessage(`Snapshot created: ${sessionId.substring(0, 8)}…`, 'success')
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
      showMessage(`Dump created: ${sessionId.substring(0, 8)}…`, 'success')
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
        <Box sx={{ display: 'flex', gap: 1 }}>
          <Button variant="contained" disabled={disabled || isAttached} onClick={handleAttach} fullWidth>
            Attach Events
          </Button>
          <Button variant="outlined" disabled={disabled || !isAttached} onClick={handleDetach} fullWidth>
            Detach Events
          </Button>
        </Box>

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
