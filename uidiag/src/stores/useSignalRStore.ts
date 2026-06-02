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
    try {
      const evs: EvsEvent = typeof evsString === 'string' ? JSON.parse(evsString) : (evsString as EvsEvent)
      const events = [...get().events, evs].slice(-50) // keep last 50
      set({ events, lastEvent: evs })
    } catch {
      // Ignore malformed events
    }
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

    connect: async () => {
      await signalRService.start()
    },

    disconnect: async () => {
      await signalRService.stop()
    },

    clearEvents: () => set({ events: [], lastEvent: null }),
  }
})
