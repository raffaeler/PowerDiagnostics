import { create } from 'zustand'
import { authService } from '@/services/authService'

const STORAGE_KEY = 'darkMode'

function getInitialDarkMode(): boolean {
  try {
    const raw = localStorage.getItem(STORAGE_KEY)
    return raw === 'true'
  } catch {
    return false
  }
}

interface AppState {
  username: string | null
  isLoggedIn: boolean
  darkMode: boolean
  login: (username: string) => void
  logout: () => void
  toggleDarkMode: () => void
}

export const useAppStore = create<AppState>((set) => ({
  username: authService.getUsername(),
  isLoggedIn: authService.getUsername() !== null,
  darkMode: getInitialDarkMode(),

  login: (username: string) => {
    authService.login(username)
    set({ username: username.trim(), isLoggedIn: true })
  },

  logout: () => {
    authService.logout()
    set({ username: null, isLoggedIn: false })
  },

  toggleDarkMode: () =>
    set((state) => {
      const next = !state.darkMode
      try {
        localStorage.setItem(STORAGE_KEY, String(next))
      } catch {
        // ignore
      }
      return { darkMode: next }
    }),
}))
