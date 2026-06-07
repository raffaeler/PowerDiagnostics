import { useEffect } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import {
  Box, Typography, Paper, Stack, Button, LinearProgress,
  Tooltip,
} from '@mui/material'
import ArrowBackIcon from '@mui/icons-material/ArrowBack'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import type { MemorySegmentInfo } from '@/types/api'

/** Color map for segment kinds. */
const SEGMENT_COLORS: Record<string, string> = {
  Ephemeral: '#4caf50',
  Generation0: '#66bb6a',
  Generation1: '#a5d6a7',
  Generation2: '#c8e6c9',
  Large: '#ff9800',
  Pinned: '#f44336',
  Frozen: '#2196f3',
}

/** Format bytes to human-readable string. */
function formatSize(bytes: number): string {
  if (bytes >= 1_073_741_824) return `${(bytes / 1_073_741_824).toFixed(1)} GB`
  if (bytes >= 1_048_576) return `${(bytes / 1_048_576).toFixed(1)} MB`
  if (bytes >= 1024) return `${(bytes / 1024).toFixed(1)} KB`
  return `${bytes} B`
}

/** Individual segment bar. */
function SegmentBar({ segment, maxSize }: { segment: MemorySegmentInfo; maxSize: number }) {
  const widthPct = maxSize > 0 ? (segment.size / maxSize) * 100 : 0
  const color = SEGMENT_COLORS[segment.segmentKind] ?? '#9e9e9e'

  return (
    <Tooltip
      title={
        <Box sx={{ fontSize: 12 }}>
          <div><strong>{segment.segmentKind}</strong></div>
          <div>Range: {segment.startAddress} – {segment.endAddress}</div>
          <div>Size: {formatSize(segment.size)}</div>
          <div>Objects: {segment.objectCount.toLocaleString()}</div>
          <div>Committed: {segment.committedStart} – {segment.committedEnd}</div>
        </Box>
      }
      arrow
    >
      <Box
        sx={{
          width: `${Math.max(widthPct, 0.5)}%`,
          minWidth: 8,
          height: 40,
          bgcolor: color,
          borderRadius: 1,
          cursor: 'pointer',
          transition: 'opacity 0.15s',
          '&:hover': { opacity: 0.8 },
          display: 'flex',
          alignItems: 'center',
          justifyContent: 'center',
          overflow: 'hidden',
        }}
      >
        {widthPct > 8 && (
          <Typography variant="caption" sx={{ color: '#fff', fontWeight: 600, fontSize: 10, textShadow: '0 0 3px rgba(0,0,0,0.5)' }}>
            {segment.segmentKind}
          </Typography>
        )}
      </Box>
    </Tooltip>
  )
}

export default function MemoryMapPage() {
  const { sessionId } = useParams<{ sessionId: string }>()
  const navigate = useNavigate()
  const memoryMap = useDiagnosticsStore((s) => s.memoryMap)
  const fetchMemoryMap = useDiagnosticsStore((s) => s.fetchMemoryMap)
  const isLoading = useDiagnosticsStore((s) => s.isLoading)

  useEffect(() => {
    if (sessionId) {
      fetchMemoryMap(sessionId)
    }
  }, [sessionId, fetchMemoryMap])

  const maxSize = memoryMap ? Math.max(...memoryMap.map((s) => s.size), 1) : 1
  const totalSize = memoryMap?.reduce((sum, s) => sum + s.size, 0) ?? 0
  const totalObjects = memoryMap?.reduce((sum, s) => sum + s.objectCount, 0) ?? 0

  return (
    <Box>
      {/* Header */}
      <Stack direction="row" spacing={2} sx={{ mb: 3, alignItems: 'center' }}>
        <Button startIcon={<ArrowBackIcon />} onClick={() => navigate(-1)} variant="text">
          Back
        </Button>
        <Typography variant="h5">Memory Map</Typography>
        <Typography variant="body2" color="text.secondary">
          Session: {sessionId}
        </Typography>
      </Stack>

      {/* Summary */}
      {memoryMap && (
        <Paper variant="outlined" sx={{ p: 2, mb: 2 }}>
          <Stack direction="row" spacing={4} useFlexGap sx={{ flexWrap: 'wrap' }}>
            <Box>
              <Typography variant="caption" color="text.secondary">Segments</Typography>
              <Typography variant="h6">{memoryMap.length}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Total Size</Typography>
              <Typography variant="h6">{formatSize(totalSize)}</Typography>
            </Box>
            <Box>
              <Typography variant="caption" color="text.secondary">Total Objects</Typography>
              <Typography variant="h6">{totalObjects.toLocaleString()}</Typography>
            </Box>
          </Stack>
        </Paper>
      )}

      {/* Loading */}
      {isLoading && <LinearProgress sx={{ mb: 2 }} />}

      {/* Segment bars */}
      {memoryMap && memoryMap.length > 0 && (
        <Paper variant="outlined" sx={{ p: 2, mb: 3 }}>
          <Typography variant="subtitle2" sx={{ mb: 1.5 }}>
            Heap Segments (proportional by size)
          </Typography>
          <Stack direction="row" spacing={0.5} useFlexGap sx={{ flexWrap: 'wrap', alignItems: 'center' }}>
            {memoryMap.map((seg, i) => (
              <SegmentBar key={i} segment={seg} maxSize={maxSize} />
            ))}
          </Stack>

          {/* Legend */}
          <Stack direction="row" spacing={2} useFlexGap sx={{ mt: 2, flexWrap: 'wrap' }}>
            {Object.entries(SEGMENT_COLORS).map(([kind, color]) => (
              <Stack key={kind} direction="row" spacing={0.5} sx={{ alignItems: 'center' }}>
                <Box sx={{ width: 14, height: 14, bgcolor: color, borderRadius: 0.5 }} />
                <Typography variant="caption">{kind}</Typography>
              </Stack>
            ))}
          </Stack>
        </Paper>
      )}

      {/* Segment detail table */}
      {memoryMap && memoryMap.length > 0 && (
        <Paper variant="outlined" sx={{ p: 2 }}>
          <Typography variant="subtitle2" sx={{ mb: 1 }}>Segment Details</Typography>
          <Box sx={{ overflowX: 'auto' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: 13 }}>
              <thead>
                <tr style={{ borderBottom: '2px solid #ddd' }}>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Kind</th>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>Start</th>
                  <th style={{ textAlign: 'left', padding: '6px 8px' }}>End</th>
                  <th style={{ textAlign: 'right', padding: '6px 8px' }}>Size</th>
                  <th style={{ textAlign: 'right', padding: '6px 8px' }}>Objects</th>
                </tr>
              </thead>
              <tbody>
                {memoryMap.map((seg, i) => (
                  <tr key={i} style={{ borderBottom: '1px solid #eee' }}>
                    <td style={{ padding: '4px 8px' }}>
                      <Box sx={{ display: 'flex', alignItems: 'center', gap: 0.5 }}>
                        <Box sx={{ width: 10, height: 10, borderRadius: '50%', bgcolor: SEGMENT_COLORS[seg.segmentKind] ?? '#9e9e9e' }} />
                        {seg.segmentKind}
                      </Box>
                    </td>
                    <td style={{ fontFamily: 'monospace', fontSize: 12, padding: '4px 8px' }}>{seg.startAddress}</td>
                    <td style={{ fontFamily: 'monospace', fontSize: 12, padding: '4px 8px' }}>{seg.endAddress}</td>
                    <td style={{ textAlign: 'right', padding: '4px 8px' }}>{formatSize(seg.size)}</td>
                    <td style={{ textAlign: 'right', padding: '4px 8px' }}>{seg.objectCount.toLocaleString()}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </Box>
        </Paper>
      )}

      {/* Empty state */}
      {(!memoryMap || memoryMap.length === 0) && !isLoading && (
        <Paper variant="outlined" sx={{ p: 4, textAlign: 'center' }}>
          <Typography color="text.secondary">
            No memory segments found. Load a dump or snapshot first, then navigate to the Memory Map.
          </Typography>
        </Paper>
      )}
    </Box>
  )
}
