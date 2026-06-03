import { useMemo, useCallback, useState, type CSSProperties } from 'react'
import { useTheme } from '@mui/material/styles'
import styles from './HexViewer.module.css'

interface HexViewerProps {
  bytes: Uint8Array
  baseAddress?: number
}

const BYTES_PER_ROW = 16
const INITIAL_ROWS = 100
const LOAD_MORE_ROWS = 100

/**
 * Custom hex viewer component — zero npm dependencies, self-contained CSS module.
 * Renders a 3-column layout: Offset | Hex (16 bytes/row) | ASCII preview.
 * Uses incremental rendering for large buffers (> 100 rows).
 */
export default function HexViewer({ bytes, baseAddress = 0 }: HexViewerProps) {
  const theme = useTheme()
  const [visibleRows, setVisibleRows] = useState(INITIAL_ROWS)
  const totalRows = Math.ceil(bytes.length / BYTES_PER_ROW)

  const rows = useMemo(() => {
    const result: { offset: number; hex: string; ascii: string }[] = []
    const maxRows = Math.min(visibleRows, totalRows)
    for (let i = 0; i < maxRows; i++) {
      const offset = i * BYTES_PER_ROW
      const slice = bytes.slice(offset, Math.min(offset + BYTES_PER_ROW, bytes.length))
      result.push({
        offset: baseAddress + offset,
        hex: formatHex(slice),
        ascii: formatAscii(slice),
      })
    }
    return result
  }, [bytes, baseAddress, visibleRows, totalRows])

  const loadMore = useCallback(() => {
    setVisibleRows((prev) => Math.min(prev + LOAD_MORE_ROWS, totalRows))
  }, [totalRows])

  if (bytes.length === 0) {
    return <div className={styles.empty}>No data to display</div>
  }

  const isDark = theme.palette.mode === 'dark'

  return (
    <div
      className={styles.hexViewer}
      style={{
        '--hex-bg': isDark ? theme.palette.grey[900] : theme.palette.grey[50],
        '--hex-fg': theme.palette.text.primary,
        '--hex-row-hover': isDark ? 'rgba(255, 255, 255, 0.05)' : 'rgba(0, 0, 0, 0.04)',
        '--hex-offset': theme.palette.primary.main,
        '--hex-ascii': isDark ? '#8BC34A' : '#2E7D32',
        '--hex-non-printable': theme.palette.text.disabled,
        '--hex-header': theme.palette.text.secondary,
        '--hex-border': theme.palette.divider,
        '--hex-button-bg': isDark ? theme.palette.grey[800] : theme.palette.grey[200],
        '--hex-button-bg-hover': isDark ? theme.palette.grey[700] : theme.palette.grey[300],
        '--hex-button-fg': theme.palette.text.secondary,
        '--hex-button-fg-hover': theme.palette.text.primary,
      } as CSSProperties}
    >
      {/* Header */}
      <div className={`${styles.row} ${styles.header}`}>
        <span className={styles.offset}>Offset</span>
        <span className={styles.hexBytes}>
          00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F
        </span>
        <span className={styles.ascii}>ASCII</span>
      </div>

      {/* Data rows */}
      {rows.map((row) => (
        <div key={row.offset} className={styles.row}>
          <span className={styles.offset}>
            {row.offset.toString(16).toUpperCase().padStart(8, '0')}
          </span>
          <span className={styles.hexBytes}>{row.hex}</span>
          <span className={styles.ascii}>{row.ascii}</span>
        </div>
      ))}

      {/* Load more button for large buffers */}
      {visibleRows < totalRows && (
        <div className={styles.loadMore}>
          <button className={styles.loadMoreBtn} onClick={loadMore}>
            Show {Math.min(LOAD_MORE_ROWS, totalRows - visibleRows)} more rows
            ({(visibleRows * BYTES_PER_ROW).toLocaleString()}
            {' / '}
            {bytes.length.toLocaleString()} bytes)
          </button>
        </div>
      )}
    </div>
  )
}

/** Format a byte slice as space-separated hex pairs with 8-byte gap. */
function formatHex(slice: Uint8Array): string {
  const parts: string[] = []
  for (let i = 0; i < BYTES_PER_ROW; i++) {
    if (i === 8) parts.push(' ') // 8-byte gap
    if (i < slice.length) {
      parts.push(slice[i].toString(16).toUpperCase().padStart(2, '0'))
    } else {
      parts.push('  ')
    }
  }
  return parts.join(' ')
}

/** Format a byte slice as printable ASCII, replacing non-printable chars with '.'. */
function formatAscii(slice: Uint8Array): string {
  let result = ''
  for (let i = 0; i < slice.length; i++) {
    const b = slice[i]
    result += b >= 0x20 && b <= 0x7e ? String.fromCharCode(b) : '.'
  }
  return result
}
