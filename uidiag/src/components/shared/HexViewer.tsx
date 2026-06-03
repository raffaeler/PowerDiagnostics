import { useMemo, useCallback, useState, useRef, type CSSProperties } from 'react'
import { useTheme } from '@mui/material/styles'
import styles from './HexViewer.module.css'

interface HexViewerProps {
  bytes: Uint8Array
  baseAddress?: number
}

const BYTES_PER_ROW = 16
const INITIAL_ROWS = 100
const LOAD_MORE_ROWS = 100
const HEADER_BYTES = formatHex(Uint8Array.from({ length: BYTES_PER_ROW }, (_, i) => i))

/**
 * Custom hex viewer component — zero npm dependencies, self-contained CSS module.
 * Renders a 3-column layout: Offset | Hex (16 bytes/row) | ASCII preview.
 *
 * Each column is an independent &lt;div&gt; so the browser's native text selection
 * is confined to a single column. Selecting hex bytes across rows will NEVER
 * spill into the offset or ASCII columns.
 *
 * Row-hover highlighting is synchronized across all three columns via JS
 * (onMouseMove in the container), because the three columns are independent
 * DOM elements.
 *
 * Incremental rendering for large buffers (> 100 rows).
 */
export default function HexViewer({ bytes, baseAddress = 0 }: HexViewerProps) {
  const theme = useTheme()
  const [visibleRows, setVisibleRows] = useState(INITIAL_ROWS)
  const [hoverRow, setHoverRow] = useState(-1)
  const containerRef = useRef<HTMLDivElement>(null)
  const totalRows = Math.ceil(bytes.length / BYTES_PER_ROW)
  const maxAddress = baseAddress + Math.max(0, bytes.length - 1)
  const offsetDigits = Math.max(8, maxAddress.toString(16).toUpperCase().length)

  const rows = useMemo(() => {
    const result: { offset: string; hex: string; ascii: string }[] = []
    const maxRows = Math.min(visibleRows, totalRows)
    for (let i = 0; i < maxRows; i++) {
      const offset = i * BYTES_PER_ROW
      const slice = bytes.slice(offset, Math.min(offset + BYTES_PER_ROW, bytes.length))
      result.push({
        offset: (baseAddress + offset).toString(16).toUpperCase().padStart(offsetDigits, '0'),
        hex: formatHex(slice),
        ascii: formatAscii(slice),
      })
    }
    return result
  }, [bytes, baseAddress, visibleRows, totalRows, offsetDigits])

  const loadMore = useCallback(() => {
    setVisibleRows((prev) => Math.min(prev + LOAD_MORE_ROWS, totalRows))
  }, [totalRows])

  // Compute per-column text blocks — each is a single string with \n line breaks.
  // Because they're separate <div> elements, browser text selection cannot cross
  // between columns.
  const { offsetText, hexText, asciiText } = useMemo(() => {
    const offsets: string[] = []
    const hexes: string[] = []
    const asciis: string[] = []
    for (const row of rows) {
      offsets.push(row.offset)
      hexes.push(row.hex)
      asciis.push(row.ascii)
    }
    return {
      offsetText: offsets.join('\n'),
      hexText: hexes.join('\n'),
      asciiText: asciis.join('\n'),
    }
  }, [rows])

  // Row-hover syncing: on mouse move, figure out which line the cursor is on
  // from the mouse Y relative to any column div, then highlight that row across
  // all three columns.
  const handleMouseMove = useCallback((e: React.MouseEvent<HTMLDivElement>) => {
    const target = e.currentTarget
    // Find the first child column that has data lines
    const hexCol = target.querySelector(`.${styles.hexCol}`) as HTMLElement | null
    if (!hexCol) return
    const style = window.getComputedStyle(hexCol)
    const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4
    const rect = hexCol.getBoundingClientRect()
    const y = e.clientY - rect.top
    const row = Math.floor(y / lineHeight)
    setHoverRow(row >= 0 && row < rows.length ? row : -1)
  }, [rows.length])

  const handleMouseLeave = useCallback(() => {
    setHoverRow(-1)
  }, [])

  if (bytes.length === 0) {
    return <div className={styles.empty}>No data to display</div>
  }

  const isDark = theme.palette.mode === 'dark'

  return (
    <div
      ref={containerRef}
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
        '--hex-line-height': '1.4',
      } as CSSProperties}
      onMouseMove={handleMouseMove}
      onMouseLeave={handleMouseLeave}
    >
      {/* Header row */}
      <div className={styles.headerRow}>
        <span className={styles.offsetHdr}>Offset</span>
        <span className={styles.hexHdr}>{HEADER_BYTES}</span>
        <span className={styles.asciiHdr}>ASCII</span>
      </div>

      {/* Three independent columns — text selection is isolated per column */}
      <div className={styles.columnsBody}>
        <div className={`${styles.col} ${styles.offsetCol}`}>
          {offsetText}
        </div>
        <div className={`${styles.col} ${styles.hexCol}`}>
          {hexText}
        </div>
        <div className={`${styles.col} ${styles.asciiCol}`}>
          {asciiText}
        </div>
        {/* Synchronized row-hover highlight strip across all three columns */}
        {hoverRow >= 0 && (
          <div
            className={styles.hoverHighlight}
            style={{
              top: `calc(${hoverRow} * 1.4em)`,
              height: '1.4em',
            }}
          />
        )}
      </div>

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
