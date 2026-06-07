import { useMemo, useCallback, useState, useRef, type CSSProperties } from 'react'
import { useTheme } from '@mui/material/styles'
import styles from './HexViewer.module.css'
import type { ObjectFieldLayout } from '@/types/api'

interface HexViewerProps {
  bytes: Uint8Array
  baseAddress?: number
  /** Field layout for annotating reference bytes as clickable links. */
  fieldLayout?: ObjectFieldLayout | null
  /** Called when user clicks on a reference byte range in the hex view. */
  onAddressClick?: (address: string) => void
}

const BYTES_PER_ROW = 16
const INITIAL_ROWS = 100
const LOAD_MORE_ROWS = 100
const HEADER_BYTES = '00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F'

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
export default function HexViewer({ bytes, baseAddress = 0, fieldLayout, onAddressClick }: HexViewerProps) {
  const theme = useTheme()
  const [visibleRows, setVisibleRows] = useState(INITIAL_ROWS)
  const [hoverRow, setHoverRow] = useState(-1)
  const containerRef = useRef<HTMLDivElement>(null)
  const totalRows = Math.ceil(bytes.length / BYTES_PER_ROW)
  const maxAddress = baseAddress + Math.max(0, bytes.length - 1)
  const offsetDigits = Math.max(8, maxAddress.toString(16).toUpperCase().length)

  // Build a set of byte offsets that are part of object references
  const refOffsetSet = useMemo(() => {
    if (!fieldLayout?.fields) return new Set<number>()
    const set = new Set<number>()
    for (const f of fieldLayout.fields) {
      if (f.isObjectReference && f.targetAddressHex) {
        // On 64-bit, a reference is 8 bytes starting at the field offset
        for (let i = 0; i < 8; i++) {
          set.add(f.offset + i)
        }
      }
    }
    return set
  }, [fieldLayout])

  // Build a map of byte offset → target address (for click/tooltip)
  const refClickMap = useMemo(() => {
    if (!fieldLayout?.fields) return new Map<number, string>()
    const map = new Map<number, string>()
    for (const f of fieldLayout.fields) {
      if (f.isObjectReference && f.targetAddressHex) {
        for (let i = 0; i < 8; i++) {
          map.set(f.offset + i, f.targetAddressHex)
        }
      }
    }
    return map
  }, [fieldLayout])

  // Build a map of byte offset → target type name (for tooltip)
  const refTypeMap = useMemo(() => {
    if (!fieldLayout?.fields) return new Map<number, string>()
    const map = new Map<number, string>()
    for (const f of fieldLayout.fields) {
      if (f.isObjectReference && f.targetAddressHex) {
        for (let i = 0; i < 8; i++) {
          map.set(f.offset + i, f.typeName)
        }
      }
    }
    return map
  }, [fieldLayout])

  const rows = useMemo(() => {
    const result: { offset: string; hexParts: { text: string; isRef: boolean; targetAddr?: string; targetTypeName?: string }[]; ascii: string }[] = []
    const maxRows = Math.min(visibleRows, totalRows)
    for (let i = 0; i < maxRows; i++) {
      const rowOffset = i * BYTES_PER_ROW
      const hexParts: { text: string; isRef: boolean; targetAddr?: string; targetTypeName?: string }[] = []
      for (let j = 0; j < BYTES_PER_ROW; j++) {
        if (j === 8) hexParts.push({ text: ' ', isRef: false })
        const byteIdx = rowOffset + j
        if (byteIdx < bytes.length) {
          const byteHex = bytes[byteIdx].toString(16).toUpperCase().padStart(2, '0')
          // Include trailing space for alignment (except last byte in row)
          const trailing = j < BYTES_PER_ROW - 1 && j !== 7 ? ' ' : (j === 7 ? '  ' : '')
          const isRef = refOffsetSet.has(byteIdx)
          const targetAddr = refClickMap.get(byteIdx)
          const targetTypeName = refTypeMap.get(byteIdx)
          hexParts.push({ text: byteHex + trailing, isRef, targetAddr, targetTypeName })
        } else {
          const trailing = j < BYTES_PER_ROW - 1 && j !== 7 ? ' ' : (j === 7 ? '  ' : '')
          hexParts.push({ text: '  ' + trailing, isRef: false })
        }
      }
      const slice = bytes.slice(rowOffset, Math.min(rowOffset + BYTES_PER_ROW, bytes.length))
      result.push({
        offset: (baseAddress + rowOffset).toString(16).toUpperCase().padStart(offsetDigits, '0'),
        hexParts,
        ascii: formatAscii(slice),
      })
    }
    return result
  }, [bytes, baseAddress, visibleRows, totalRows, offsetDigits, refOffsetSet, refClickMap])

  const loadMore = useCallback(() => {
    setVisibleRows((prev) => Math.min(prev + LOAD_MORE_ROWS, totalRows))
  }, [totalRows])

  // Per-row render for clickable reference bytes
  const handleRowHexClick = useCallback(
    (e: React.MouseEvent<HTMLSpanElement>) => {
      const addr = e.currentTarget.getAttribute('data-target-addr')
      if (addr && onAddressClick) {
        e.stopPropagation()
        onAddressClick(addr)
      }
    },
    [onAddressClick],
  )

  // Row-hover syncing
  const handleMouseMove = useCallback(
    (e: React.MouseEvent<HTMLDivElement>) => {
      const target = e.currentTarget
      const hexCol = target.querySelector(`.${styles.hexCol}`) as HTMLElement | null
      if (!hexCol) return
      const style = window.getComputedStyle(hexCol)
      const lineHeight = parseFloat(style.lineHeight) || parseFloat(style.fontSize) * 1.4
      const rect = hexCol.getBoundingClientRect()
      const y = e.clientY - rect.top
      const row = Math.floor(y / lineHeight)
      setHoverRow(row >= 0 && row < rows.length ? row : -1)
    },
    [rows.length],
  )

  const handleMouseLeave = useCallback(() => setHoverRow(-1), [])

  if (bytes.length === 0) {
    return <div className={styles.empty}>No data to display</div>
  }

  const isDark = theme.palette.mode === 'dark'
  const hexLineHeight = '1.4em'

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
        '--hex-line-height': hexLineHeight,
        '--hex-ref': theme.palette.info.main,
        '--hex-ref-bg': isDark ? 'rgba(33, 150, 243, 0.15)' : 'rgba(33, 150, 243, 0.08)',
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

      {/* Row-based rendering with per-byte clickable references */}
      <div className={styles.columnsBody}>
        <div className={`${styles.col} ${styles.offsetCol}`}>
          {rows.map((row, ri) => (
            <div key={ri} style={{ lineHeight: hexLineHeight }}>
              {row.offset}
            </div>
          ))}
        </div>
        <div className={`${styles.col} ${styles.hexCol}`}>
          {rows.map((row, ri) => (
            <div key={ri} style={{ lineHeight: hexLineHeight }}>
              {(() => {
                // Merge consecutive reference bytes into single spans
                const merged: { text: string; isRef: boolean; targetAddr?: string; targetTypeName?: string }[] = []
                let i = 0
                while (i < row.hexParts.length) {
                  const part = row.hexParts[i]
                  if (part.isRef && part.targetAddr) {
                    // Start a ref group — collect consecutive ref bytes with same target
                    let groupText = part.text
                    const groupAddr = part.targetAddr
                    const groupTypeName = part.targetTypeName
                    i++
                    while (i < row.hexParts.length && row.hexParts[i].isRef && row.hexParts[i].targetAddr === groupAddr) {
                      groupText += row.hexParts[i].text
                      i++
                    }
                    merged.push({ text: groupText, isRef: true, targetAddr: groupAddr, targetTypeName: groupTypeName })
                  } else {
                    merged.push(part)
                    i++
                  }
                }
                return merged.map((part, mi) =>
                  part.isRef && part.targetAddr ? (
                    <span
                      key={mi}
                      data-target-addr={part.targetAddr}
                      onClick={handleRowHexClick}
                      title={part.targetTypeName ? `"${part.targetTypeName}" @ ${part.targetAddr}` : `→ ${part.targetAddr}`}
                      style={{
                        color: 'var(--hex-ref, #1976d2)',
                        backgroundColor: 'var(--hex-ref-bg, rgba(33,150,243,0.08))',
                        cursor: 'pointer',
                        textDecoration: 'underline',
                        textDecorationStyle: 'dotted',
                        textUnderlineOffset: 2,
                        borderRadius: 2,
                      }}
                    >
                      {part.text}
                    </span>
                  ) : (
                    <span key={mi}>{part.text}</span>
                  ),
                )
              })()}
            </div>
          ))}
        </div>
        <div className={`${styles.col} ${styles.asciiCol}`}>
          {rows.map((row, ri) => (
            <div key={ri} style={{ lineHeight: hexLineHeight }}>
              {row.ascii}
            </div>
          ))}
        </div>
        {/* Synchronized row-hover highlight */}
        {hoverRow >= 0 && (
          <div
            className={styles.hoverHighlight}
            style={{
              top: `calc(${hoverRow} * ${hexLineHeight})`,
              height: hexLineHeight,
            }}
          />
        )}
      </div>

      {/* Load more button */}
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

/** Format a byte slice as printable ASCII, replacing non-printable chars with '.'. */
function formatAscii(slice: Uint8Array): string {
  let result = ''
  for (let i = 0; i < slice.length; i++) {
    const b = slice[i]
    result += b >= 0x20 && b <= 0x7e ? String.fromCharCode(b) : '.'
  }
  return result
}
