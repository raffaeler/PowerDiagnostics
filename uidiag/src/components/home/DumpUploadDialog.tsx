import { useState, useCallback, useRef } from 'react'
import {
  Dialog,
  DialogTitle,
  DialogContent,
  DialogActions,
  Button,
  Tabs,
  Tab,
  Box,
  Typography,
  LinearProgress,
  Alert,
  FormControl,
  InputLabel,
  Select,
  MenuItem,
} from '@mui/material'
import CloudUploadIcon from '@mui/icons-material/CloudUpload'
import FolderOpenIcon from '@mui/icons-material/FolderOpen'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'

interface DumpUploadDialogProps {
  open: boolean
  onClose: () => void
  onSessionCreated?: (sessionId: string) => void
}

/**
 * Dialog for opening a crash dump file.
 * Two modes:
 * - Upload: drag-and-drop or file picker for .dmp files (multipart upload to server)
 * - Server dumps: dropdown of .dmp files available on the server
 */
export default function DumpUploadDialog({
  open,
  onClose,
  onSessionCreated,
}: DumpUploadDialogProps) {
  const uploadDump = useDiagnosticsStore((s) => s.uploadDump)
  const openDumpPath = useDiagnosticsStore((s) => s.openDumpPath)
  const availableDumps = useDiagnosticsStore((s) => s.availableDumps)
  const fetchAvailableDumps = useDiagnosticsStore((s) => s.fetchAvailableDumps)
  const [tab, setTab] = useState(0)
  const [file, setFile] = useState<File | null>(null)
  const [selectedDump, setSelectedDump] = useState('')
  const [uploading, setUploading] = useState(false)
  const [error, setError] = useState<string | null>(null)
  const fileInputRef = useRef<HTMLInputElement>(null)

  const handleFileChange = useCallback(
    (e: React.ChangeEvent<HTMLInputElement>) => {
      const f = e.target.files?.[0] ?? null
      setFile(f)
      setError(null)
    },
    [],
  )

  const handleDrop = useCallback((e: React.DragEvent) => {
    e.preventDefault()
    const f = e.dataTransfer.files?.[0] ?? null
    setFile(f)
    setError(null)
  }, [])

  const handleDragOver = useCallback((e: React.DragEvent) => {
    e.preventDefault()
  }, [])

  const handleSubmit = useCallback(async () => {
    setError(null)
    setUploading(true)
    try {
      let sessionId: string | null = null

      if (tab === 0 && file) {
        sessionId = await uploadDump(file)
      } else if (tab === 1 && selectedDump) {
        sessionId = await openDumpPath(selectedDump)
      }

      if (sessionId) {
        onSessionCreated?.(sessionId)
        onClose()
        // Reset form
        setFile(null)
        setSelectedDump('')
        setError(null)
      } else {
        setError('Failed to open dump file. Check server logs for details.')
      }
    } finally {
      setUploading(false)
    }
  }, [tab, file, selectedDump, uploadDump, openDumpPath, onSessionCreated, onClose])

  const handleClose = useCallback(() => {
    if (!uploading) {
      setFile(null)
      setSelectedDump('')
      setError(null)
      onClose()
    }
  }, [uploading, onClose])

  const canSubmit =
    !uploading &&
    ((tab === 0 && !!file) || (tab === 1 && selectedDump.length > 0))

  return (
    <Dialog open={open} onClose={handleClose} maxWidth="sm" fullWidth>
      <DialogTitle>Open Crash Dump</DialogTitle>

      <DialogContent>
        <Tabs value={tab} onChange={(_, v) => { setTab(v); if (v === 1) { fetchAvailableDumps() } }} sx={{ mb: 2 }}>
          <Tab icon={<CloudUploadIcon />} label="Upload" />
          <Tab icon={<FolderOpenIcon />} label="Server Dumps" />
        </Tabs>

        {error && <Alert severity="error" sx={{ mb: 2 }}>{error}</Alert>}

        {tab === 0 && (
          <Box>
            {/* Drop zone */}
            <Box
              onDrop={handleDrop}
              onDragOver={handleDragOver}
              onClick={() => fileInputRef.current?.click()}
              sx={{
                border: '2px dashed',
                borderColor: file ? 'primary.main' : 'divider',
                borderRadius: 2,
                p: 4,
                textAlign: 'center',
                cursor: 'pointer',
                bgcolor: file ? 'action.selected' : 'background.default',
                '&:hover': { bgcolor: 'action.hover' },
              }}
            >
              <input
                ref={fileInputRef}
                type="file"
                accept=".dmp,.mdmp"
                onChange={handleFileChange}
                style={{ display: 'none' }}
              />
              {file ? (
                <Box>
                  <Typography variant="body2" sx={{ fontWeight: 600 }}>
                    {file.name}
                  </Typography>
                  <Typography variant="caption" color="text.secondary">
                    {(file.size / (1024 * 1024)).toFixed(1)} MB
                  </Typography>
                </Box>
              ) : (
                <Box>
                  <CloudUploadIcon sx={{ fontSize: 48, color: 'text.disabled', mb: 1 }} />
                  <Typography>Drop a .dmp file here or click to browse</Typography>
                  <Typography variant="caption" color="text.secondary">
                    Maximum file size: 2 GB
                  </Typography>
                </Box>
              )}
            </Box>
          </Box>
        )}

        {tab === 1 && (
          <FormControl fullWidth disabled={uploading || availableDumps.length === 0}>
            <InputLabel id="dump-select-label">Select dump</InputLabel>
            <Select
              labelId="dump-select-label"
              value={selectedDump}
              label="Select dump"
              onChange={(e) => setSelectedDump(e.target.value)}
            >
              {availableDumps.length === 0 && (
                <MenuItem disabled value="">
                  No dump files found
                </MenuItem>
              )}
              {availableDumps.map((dump) => (
                <MenuItem key={dump} value={dump}>
                  {dump}
                </MenuItem>
              ))}
            </Select>
          </FormControl>
        )}

        {uploading && <LinearProgress sx={{ mt: 2 }} />}
      </DialogContent>

      <DialogActions>
        <Button onClick={handleClose} disabled={uploading}>
          Cancel
        </Button>
        <Button
          variant="contained"
          onClick={handleSubmit}
          disabled={!canSubmit}
        >
          Open Dump
        </Button>
      </DialogActions>
    </Dialog>
  )
}
