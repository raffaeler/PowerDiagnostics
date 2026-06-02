import { Snackbar, Alert, type AlertColor } from '@mui/material'
import { useToastStore } from '@/stores/useToastStore'

/**
 * Toast provider that renders MUI Snackbar notifications from `useToastStore`.
 * Include once near the root of the app (e.g., in AppLayout).
 */
export default function ToastProvider() {
  const toasts = useToastStore((s) => s.toasts)
  const removeToast = useToastStore((s) => s.removeToast)

  if (toasts.length === 0) return null

  // Show the most recent toast (avoids stacking issues)
  const latest = toasts[toasts.length - 1]

  const severityMap: Record<string, AlertColor> = {
    success: 'success',
    info: 'info',
    warning: 'warning',
    error: 'error',
  }

  return (
    <Snackbar
      open={!!latest}
      autoHideDuration={latest.durationMs ?? 6000}
      onClose={() => removeToast(latest.id)}
      anchorOrigin={{ vertical: 'bottom', horizontal: 'center' }}
    >
      <Alert
        severity={severityMap[latest.severity] ?? 'info'}
        variant="filled"
        onClose={() => removeToast(latest.id)}
        sx={{ minWidth: 320 }}
      >
        {latest.message}
      </Alert>
    </Snackbar>
  )
}
