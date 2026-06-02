import { create } from 'zustand'
import { HubConnectionState } from '@microsoft/signalr'
import { signalRService } from '@/services/signalRService'
import { useDiagnosticsStore } from '@/stores/useDiagnosticsStore'
import type { EvsEvent } from '@/types/signalr'
import type { GcRootProgress } from '@/types/api'

interface SignalRState {
  connectionState: HubConnectionState
  events: EvsEvent[]
  lastEvent: EvsEvent | null
  lastEventByCategory: Record<string, EvsEvent>
  connect: () => Promise<void>
  disconnect: () => Promise<void>
  clearEvents: () => void
}

export const useSignalRStore = create<SignalRState>((set, get) => {
  // Keep store in sync with the real service
  signalRService.onStateChange((state) => {
    set({ connectionState: state })
  })

  // Subscribe to diagnostic events
  signalRService.onEvent('onEvs', (evsString: unknown) => {
    console.log('[DEBUG onEvs] FIRED — raw type:', typeof evsString, 'value:', evsString)
    try {
      const evs: EvsEvent = typeof evsString === 'string' ? JSON.parse(evsString) : (evsString as EvsEvent)
      console.log('[DEBUG onEvs] PARSED — cat:', evs.cat, 'val:', evs.val, 'uom:', evs.uom)
      const prev = get().lastEventByCategory
      console.log('[DEBUG onEvs] prev lastEventByCategory keys:', Object.keys(prev))
      const events = [...get().events, evs].slice(-50) // keep last 50
      const lastEventByCategory = { ...get().lastEventByCategory, [evs.cat]: evs }
      console.log('[DEBUG onEvs] new lastEventByCategory keys:', Object.keys(lastEventByCategory))
      set({ events, lastEvent: evs, lastEventByCategory })
    } catch (err) {
      console.error('[DEBUG onEvs] ERROR:', err)
    }
  })

  // Handle server→client messages (DiagnosticHub.SendMessage)
  signalRService.onEvent('onMessage', (_user: unknown, _message: unknown) => {
    // Messages are received but not displayed by default.
    // Add a log handler or UI toast here if needed.
  })

  // Subscribe to GC root path progress
  signalRService.onEvent('onGcRootProgress', (progress: unknown) => {
    useDiagnosticsStore.getState().setGcRootProgress(progress as GcRootProgress)
  })

  // Clear progress when GC root path completes
  signalRService.onEvent('onGcRootComplete', () => {
    useDiagnosticsStore.getState().setGcRootProgress(null)
  })

  // Refresh session list when sessions are created or closed
  signalRService.onEvent('onSessionCreated', () => {
    useDiagnosticsStore.getState().fetchSessions()
  })

  signalRService.onEvent('onSessionClosed', () => {
    useDiagnosticsStore.getState().fetchSessions()
  })

  return {
    connectionState: signalRService.state,
    events: [],
    lastEvent: null,
    lastEventByCategory: {},

    connect: async () => {
      await signalRService.start()
    },

    disconnect: async () => {
      await signalRService.stop()
    },

    clearEvents: () => set({ events: [], lastEvent: null, lastEventByCategory: {} }),
  }
})
