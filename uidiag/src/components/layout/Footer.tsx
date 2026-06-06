import { Box, Typography, Chip } from '@mui/material'
import { useSignalRStore } from '@/stores/useSignalRStore'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { HubConnectionState } from '@microsoft/signalr'

export default function Footer() {
  const connectionState = useSignalRStore((s) => s.connectionState)
  const sessions = useDiagnosticsStore((s) => s.sessions)
  const activeSessionId = useDiagnosticsStore((s) => s.activeSessionId)

  const stateLabel =
    connectionState === HubConnectionState.Connected
      ? 'Connected'
      : connectionState === HubConnectionState.Reconnecting
        ? 'Reconnecting…'
        : 'Disconnected'

  const stateColor =
    connectionState === HubConnectionState.Connected
      ? 'success'
      : connectionState === HubConnectionState.Reconnecting
        ? 'warning'
        : 'error'

  return (
    <Box
      component="footer"
      sx={{
        py: 1,
        px: 3,
        borderTop: 1,
        borderColor: 'divider',
        bgcolor: 'background.paper',
        display: 'flex',
        alignItems: 'center',
        gap: 2,
        flexWrap: 'wrap',
      }}
    >
      <Typography variant="caption" color="text.secondary">
        PowerDiagnostics v0.1
      </Typography>

      <Chip label={`SignalR: ${stateLabel}`} color={stateColor} size="small" variant="outlined" />

      <Chip label={`Sessions: ${sessions.length}`} size="small" variant="outlined" />

      {activeSessionId && (
        <Typography variant="caption" color="text.secondary" sx={{ fontFamily: 'monospace' }}>
          Active: {activeSessionId}
        </Typography>
      )}

      <Box sx={{ flexGrow: 1 }} />

      <Typography variant="caption" color="text.secondary">
        Engineered by Raffaele Rialdi,{' '}
        <a href="https://github.com/raffaeler/PowerDiagnostics" target="_blank" rel="noopener noreferrer" style={{ color: 'inherit' }}>
          https://github.com/raffaeler/PowerDiagnostics
        </a>
      </Typography>
    </Box>
  )
}
