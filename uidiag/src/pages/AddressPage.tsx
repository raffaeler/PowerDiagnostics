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
import GcRootPanel from '@/components/debug/GcRootPanel'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import type { HexDataResult } from '@/types/api'

/**
 * Dedicated address detail page — navigated to when clicking an address in any grid.
 * Route: /debug/:sessionId/address/:address
 *
 * Shows object details (type, size, MT, address), GC root paths,
 * and the raw hex content. Fetches data from the backend on mount.
 */
export default function AddressPage() {
  const { sessionId, address } = useParams<{
    sessionId: string
    address: string
  }>()
  const navigate = useNavigate()
  const location = useLocation()

  const hexData = useDiagnosticsStore((s) => s.hexData)
  const fetchHexData = useDiagnosticsStore((s) => s.fetchHexData)
  const fetchGcRootPath = useDiagnosticsStore((s) => s.fetchGcRootPath)

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
    // Fetch hex data first (fast) so the page content renders immediately.
    fetchHexData(sessionId, address).finally(() => {
      setLoading(false)
    })
    // Fetch GC root paths in the background — progress is streamed via SignalR
    // and displayed by GcRootPanel, so we don't block the page on it.
    fetchGcRootPath(sessionId, address)
  }, [sessionId, address, fetchHexData, fetchGcRootPath])

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
          Loading object data for 0x{address}…
        </Typography>
      </Box>
    )
  }

  // ── Error state ──
  if (error || !hexData) {
    return (
      <Box sx={{ display: 'flex', flexDirection: 'column', alignItems: 'center', justifyContent: 'center', height: '60vh', gap: 2 }}>
        <Typography variant="h6" color="error">
          {error || 'Unable to load object data'}
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
    <Box sx={{ display: 'flex', flexDirection: 'column', gap: 2 }}>
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
            Debug: {sessionId}
          </Link>
          <Typography color="text.primary">Address View</Typography>
        </Breadcrumbs>
      </Box>

      {/* Object Details Panel */}
      <ObjectDetailsPanel hexData={hexData} />

      {/* GC Root Paths */}
      <Box sx={{ mb: 2 }}>
        <GcRootPanel />
      </Box>

      {/* Hex Viewer */}
      <Box sx={{ mb: 1 }}>
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
