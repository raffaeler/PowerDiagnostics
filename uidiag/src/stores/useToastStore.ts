import { create } from 'zustand'

export interface Toast {
  id: string
  message: string
  severity: 'success' | 'info' | 'warning' | 'error'
  /** Auto-dismiss after this many ms (default 6000). Set to 0 for persistent. */
  durationMs?: number
}

interface ToastState {
  toasts: Toast[]

  addToast: (message: string, severity?: Toast['severity'], durationMs?: number) => void
  removeToast: (id: string) => void
  clearAll: () => void
}

let _toastCounter = 0

/**
 * Global toast notification store.
 *
 * Usage from any store or component:
 *   import { useToastStore } from '@/stores/useToastStore'
 *   useToastStore.getState().addToast('File uploaded', 'success')
 *
 * This store is not React-dependent — it can be called from plain TS services.
 */
export const useToastStore = create<ToastState>((set) => ({
  toasts: [],

  addToast: (message, severity = 'info', durationMs = 6000) => {
    const id = `toast-${++_toastCounter}`
    set((s) => ({
      toasts: [...s.toasts, { id, message, severity, durationMs }],
    }))

    // Auto-dismiss
    if (durationMs > 0) {
      setTimeout(() => {
        set((s) => ({
          toasts: s.toasts.filter((t) => t.id !== id),
        }))
      }, durationMs)
    }
  },

  removeToast: (id) => {
    set((s) => ({
      toasts: s.toasts.filter((t) => t.id !== id),
    }))
  },

  clearAll: () => set({ toasts: [] }),
}))

/**
 * Convenience: add a success toast.
 */
export function toastSuccess(message: string): void {
  useToastStore.getState().addToast(message, 'success')
}

/**
 * Convenience: add an error toast.
 */
export function toastError(message: string): void {
  useToastStore.getState().addToast(message, 'error')
}

/**
 * Convenience: add a warning toast.
 */
export function toastWarning(message: string): void {
  useToastStore.getState().addToast(message, 'warning')
}

/**
 * Convenience: add an info toast.
 */
export function toastInfo(message: string): void {
  useToastStore.getState().addToast(message, 'info')
}
