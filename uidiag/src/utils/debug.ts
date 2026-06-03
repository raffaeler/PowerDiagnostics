/**
 * Debug/trace utility for uidiag.
 *
 * ## How to use in the browser console:
 *
 * ```js
 * // Check if debug is enabled
 * __uidiag_debug.status()
 *
 * // Enable all debug logging
 * __uidiag_debug.enable()
 *
 * // Disable all debug logging
 * __uidiag_debug.disable()
 *
 * // Enable only specific categories
 * __uidiag_debug.enable("grid")       // only grid-related traces
 * __uidiag_debug.enable("grid,data")  // grid + data traces
 *
 * // Disable specific categories
 * __uidiag_debug.disable("grid")
 *
 * // Set log level (0=off, 1=error, 2=warn, 3=info, 4=debug, 5=trace)
 * __uidiag_debug.level(5)
 *
 * // Persist debug state across page reloads
 * __uidiag_debug.persist()
 *
 * // List available categories
 * __uidiag_debug.categories()
 * ```
 *
 * Debug state is NOT persisted by default. Call `persist()` to save to localStorage.
 * Call `__uidiag_debug.clearPersist()` to remove saved state.
 */

const STORAGE_KEY = 'uidiag_debug'

type DebugCategory = 'grid' | 'data' | 'signalr' | 'query' | 'api' | 'store' | 'all'

interface DebugState {
  enabled: boolean
  categories: Set<DebugCategory>
  logLevel: number // 0=off, 1=error, 2=warn, 3=info, 4=debug, 5=trace
}

const state: DebugState = {
  enabled: false,
  categories: new Set<DebugCategory>(['all']),
  logLevel: 4,
}

const ALL_CATEGORIES: DebugCategory[] = ['grid', 'data', 'signalr', 'query', 'api', 'store', 'all']

function shouldLog(category: DebugCategory): boolean {
  if (!state.enabled && state.logLevel < 2) return false
  if (state.enabled) {
    if (state.categories.has('all')) return true
    return state.categories.has(category)
  }
  return false
}

function formatTimestamp(): string {
  const now = new Date()
  return `${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}.${now.getMilliseconds().toString().padStart(3, '0')}`
}

const LABELS: Record<DebugCategory, string> = {
  grid: 'GRID',
  data: 'DATA',
  signalr: 'SIGNALR',
  query: 'QUERY',
  api: 'API',
  store: 'STORE',
  all: 'DEBUG',
}

/**
 * Core debug log function. Use in components:
 * ```ts
 * import { debugLog } from '@/utils/debug'
 * debugLog('grid', 'Column definitions:', columns)
 * ```
 */
export function debugLog(
  category: DebugCategory,
  message: string,
  ...args: unknown[]
): void {
  if (!shouldLog(category)) return
  const label = LABELS[category]
  const prefix = `%c[${label}]%c ${formatTimestamp()}`
  const labelStyle = 'color: #fff; background: #1976d2; padding: 1px 4px; border-radius: 3px; font-weight: bold'
  const resetStyle = 'color: inherit'

  if (args.length > 0) {
    console.log(prefix, labelStyle, resetStyle, message, ...args)
  } else {
    console.log(prefix, labelStyle, resetStyle, message)
  }
}

/**
 * Debug error log. Always logged at level >= 1, regardless of category filter.
 */
export function debugError(category: DebugCategory, message: string, ...args: unknown[]): void {
  if (state.logLevel < 1 && !state.enabled) return
  const label = LABELS[category]
  console.error(`[${label}] ${formatTimestamp()} ERROR:`, message, ...args)
}

/**
 * Debug warn log.
 */
export function debugWarn(category: DebugCategory, message: string, ...args: unknown[]): void {
  if (state.logLevel < 2 && !state.enabled) return
  if (!shouldLog(category)) return
  const label = LABELS[category]
  console.warn(`[${label}] ${formatTimestamp()}`, message, ...args)
}

// ── Browser console API ──

function buildApi() {
  return {
    /** Show current debug status */
    status: () => {
      console.log(
        `%c__uidiag_debug%c status:`,
        'font-weight: bold; color: #1976d2',
        'color: inherit',
      )
      console.log(`  enabled:   ${state.enabled}`)
      console.log(`  categories: ${[...state.categories].join(', ')}`)
      console.log(`  logLevel:  ${state.logLevel} (0=off, 5=trace)`)
    },

    /** Enable debug logging. Optionally specify comma-separated categories. */
    enable: (cats?: string) => {
      state.enabled = true
      if (cats) {
        state.categories = new Set<DebugCategory>(
          cats.split(',').map((c) => c.trim()) as DebugCategory[],
        )
      }
      console.log(
        `%c__uidiag_debug%c ENABLED | categories: ${[...state.categories].join(', ')} | level: ${state.logLevel}`,
        'font-weight: bold; color: #2e7d32',
        'color: inherit',
      )
    },

    /** Disable debug logging. Optionally specify categories to remove. */
    disable: (cats?: string) => {
      if (cats) {
        const toRemove = cats.split(',').map((c) => c.trim()) as DebugCategory[]
        toRemove.forEach((c) => state.categories.delete(c))
        console.log(`Disabled categories: ${toRemove.join(', ')}`)
      } else {
        state.enabled = false
        console.log('%c__uidiag_debug%c DISABLED', 'font-weight: bold; color: #d32f2f', 'color: inherit')
      }
    },

    /** Set log level: 0=off, 1=error, 2=warn, 3=info, 4=debug, 5=trace */
    level: (lvl: number) => {
      state.logLevel = Math.max(0, Math.min(5, lvl))
      const names = ['OFF', 'ERROR', 'WARN', 'INFO', 'DEBUG', 'TRACE']
      console.log(`Log level set to: ${state.logLevel} (${names[state.logLevel]})`)
    },

    /** Save current debug state to localStorage for persistence across reloads */
    persist: () => {
      localStorage.setItem(
        STORAGE_KEY,
        JSON.stringify({
          enabled: state.enabled,
          categories: [...state.categories],
          logLevel: state.logLevel,
        }),
      )
      console.log('Debug state persisted to localStorage.')
    },

    /** Remove persisted debug state */
    clearPersist: () => {
      localStorage.removeItem(STORAGE_KEY)
      console.log('Persisted debug state cleared.')
    },

    /** List all available categories */
    categories: () => {
      console.log('Available categories:', ALL_CATEGORIES.join(', '))
    },

    /** Quick toggle: if disabled, enables with all categories at level 4 */
    toggle: () => {
      if (state.enabled) {
        state.enabled = false
        console.log('%c__uidiag_debug%c DISABLED', 'font-weight: bold; color: #d32f2f', 'color: inherit')
      } else {
        state.enabled = true
        state.categories = new Set<DebugCategory>(['all'])
        state.logLevel = 4
        console.log('%c__uidiag_debug%c ENABLED (all categories, level 4)', 'font-weight: bold; color: #2e7d32', 'color: inherit')
      }
    },

    /** Dump row data shape: show keys and value types of first N rows */
    dump: (data: unknown, maxRows = 3) => {
      if (!Array.isArray(data)) {
        console.log('Data is not an array:', data)
        return
      }
      console.log(`Dumping ${Math.min(data.length, maxRows)} of ${data.length} rows:`)
      for (let i = 0; i < Math.min(data.length, maxRows); i++) {
        const row = data[i] as Record<string, unknown>
        if (typeof row !== 'object' || row === null) {
          console.log(`  [${i}]:`, row)
        } else {
          console.group(`  [${i}] ${Object.keys(row).length} keys`)
          for (const [key, val] of Object.entries(row)) {
            const type = val === null ? 'null' : typeof val === 'object' ? `object (${Object.keys(val as object).join(', ')})` : typeof val
            console.log(`    ${key}: ${type} =`, val)
          }
          console.groupEnd()
        }
      }
    },
  }
}

// Expose debug API on window
;(window as unknown as Record<string, unknown>).__uidiag_debug = buildApi()

// Restore persisted state
try {
  const saved = localStorage.getItem(STORAGE_KEY)
  if (saved) {
    const parsed = JSON.parse(saved)
    state.enabled = parsed.enabled ?? false
    state.categories = new Set<DebugCategory>(parsed.categories ?? ['all'])
    state.logLevel = parsed.logLevel ?? 4
  }
} catch {
  // Ignore parse errors
}
