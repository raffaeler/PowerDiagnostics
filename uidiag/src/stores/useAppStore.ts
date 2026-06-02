import { create } from 'zustand'
import { authService } from '@/services/authService'

interface AppState {
  username: string | null
  isLoggedIn: boolean
  login: (username: string) => void
  logout: () => void
}

export const useAppStore = create<AppState>((set) => ({
  username: authService.getUsername(),
  isLoggedIn: authService.getUsername() !== null,

  login: (username: string) => {
    authService.login(username)
    set({ username: username.trim(), isLoggedIn: true })
  },

  logout: () => {
    authService.logout()
    set({ username: null, isLoggedIn: false })
  },
}))
