import { Box, Typography, Chip } from '@mui/material'
import { useSignalRStore } from '@/stores/useSignalRStore'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import { HubConnectionState } from '@microsoft/signalr'

export default function Footer() {
  const connectionState = useSignalRStore((s) => s.connectionState)
  const sessionCount = useDiagnosticsStore((s) => s.sessions.length)

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

      <Chip label={`Sessions: ${sessionCount}`} size="small" variant="outlined" />

      <Box sx={{ flexGrow: 1 }} />

      <Typography variant="caption" color="text.secondary">
        Built with React + Vite + MUI + Zustand
      </Typography>
    </Box>
  )
}
