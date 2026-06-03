import { useEffect, useState } from 'react'
import { useParams, useNavigate, useLocation } from 'react-router-dom'
import {
  Box,
  Typography,
  Button,
  Paper,
  Chip,
  CircularProgress,
  Breadcrumbs,
  Link,
  Divider,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import HexViewer from '@/components/shared/HexViewer'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import type { HexDataResult } from '@/types/api'

/**
 * Dedicated hex editor page — navigated to when clicking an address in any grid.
 * Route: /debug/:sessionId/hex/:address
 *
 * Shows a prominent object details panel (type, size, MT, address) above
 * the raw hex content. Fetches data from the backend on mount.
 */
export default function HexEditorPage() {
  const { sessionId, address } = useParams<{
    sessionId: string
    address: string
  }>()
  const navigate = useNavigate()
  const location = useLocation()

  const hexData = useDiagnosticsStore((s) => s.hexData)
  const fetchHexData = useDiagnosticsStore((s) => s.fetchHexData)

  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    if (!sessionId || !address) {
      setError('Missing session ID or address')
      setLoading(false)
      return
    }
    setLoading(true)
    setError(null)
    fetchHexData(sessionId, address).then(() => {
      setLoading(false)
    })
  }, [sessionId, address, fetchHexData])

  const backTarget =
    typeof location.state === 'object' &&
    location.state !== null &&
    'from' in location.state &&
    typeof (location.state as { from?: unknown }).from === 'string'
      ? (location.state as { from: string }).from
      : `/debug/${sessionId}`

  // Decode base64 bytes
  const bytes = useBase64Decode(hexData?.bytesBase64)

  // ── Loading state ──
  if (loading) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '60vh', gap: 2 }}>
        <CircularProgress />
        <Typography variant="body2" color="text.secondary">
          Loading hex data for 0x{address}…
        </Typography>
      </Box>
    )
  }

  // ── Error state ──
  if (error || !hexData) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '60vh', gap: 2 }}>
        <Typography variant="h6" color="error">
          {error || 'Unable to load hex data'}
        </Typography>
        <Typography variant="body2" color="text.secondary">
          The object at 0x{address} may not be available.
        </Typography>
        <Button variant="outlined" startIcon={<ArrowBackIcon />} onClick={() => navigate(backTarget)}>
          Go back
        </Button>
      </Box>
    )
  }

  return (
    <Box sx={{ display: 'flex', flexDirection: 'column', height: 'calc(100vh - 120px)' }}>
      {/* Breadcrumb + Back */}
      <Box sx={{ display: 'flex', alignItems: 'center', gap: 2, mb: 2 }}>
        <Button
          variant="outlined"
          startIcon={<ArrowBackIcon />}
          onClick={() => navigate(backTarget)}
          size="small"
        >
          Back
        </Button>
        <Breadcrumbs>
          <Link
            underline="hover"
            color="inherit"
            href={`/debug/${sessionId}`}
            onClick={(e) => {
              e.preventDefault()
              navigate(`/debug/${sessionId}`)
            }}
          >
            Debug: {sessionId?.substring(0, 8)}…
          </Link>
          <Typography color="text.primary">Hex View</Typography>
        </Breadcrumbs>
      </Box>

      {/* Object Details Panel */}
      <ObjectDetailsPanel hexData={hexData} />

      {/* Hex Viewer */}
      <Box sx={{ flex: 1, minHeight: 0, overflow: 'auto' }}>
        {bytes ? (
          <HexViewer
            bytes={bytes}
            baseAddress={parseInt(hexData.objectAddress, 16)}
          />
        ) : (
          <Typography color="text.secondary" sx={{ p: 4 }}>
            Unable to decode hex data.
          </Typography>
        )}
      </Box>
    </Box>
  )
}

// ── Object Details Panel ──

interface ObjectDetailsPanelProps {
  hexData: HexDataResult
}

function ObjectDetailsPanel({ hexData }: ObjectDetailsPanelProps) {
  return (
    <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
      <Typography variant="subtitle1" sx={{ fontWeight: 600 }} gutterBottom>
        Object Details
      </Typography>
      <Divider sx={{ mb: 1.5 }} />
      <Box sx={{ display: 'flex', flexWrap: 'wrap', gap: 2 }}>
        <DetailChip label="Address" value={hexData.objectAddress} />
        <DetailChip label="Type" value={hexData.typeName} />
        {hexData.mt && <DetailChip label="MT" value={hexData.mt} />}
        <DetailChip label="Size" value={`${hexData.size.toLocaleString()} bytes`} />
      </Box>
    </Paper>
  )
}

function DetailChip({ label, value }: { label: string; value: string }) {
  return (
    <Box>
      <Typography variant="caption" color="text.secondary" sx={{ display: 'block' }}>
        {label}
      </Typography>
      <Chip
        label={value}
        variant="outlined"
        size="small"
        sx={{
          fontFamily: label === 'Address' || label === 'MT' ? 'monospace' : undefined,
          fontWeight: 500,
        }}
      />
    </Box>
  )
}

// ── Base64 decode hook (same as in HexViewerDialog) ──

function useBase64Decode(base64: string | undefined | null): Uint8Array | null {
  if (!base64) return null
  try {
    const b64 = base64.includes(',') ? base64.split(',')[1] : base64
    const binary = atob(b64)
    const bytes = new Uint8Array(binary.length)
    for (let i = 0; i < binary.length; i++) {
      bytes[i] = binary.charCodeAt(i)
    }
    return bytes
  } catch {
    return null
  }
}
