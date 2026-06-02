const STORAGE_KEY = 'uidiag_username'

/**
 * Mock authentication service.
 * Persists username to localStorage — no real auth.
 */
export const authService = {
  login(username: string): void {
    localStorage.setItem(STORAGE_KEY, username.trim())
  },

  logout(): void {
    localStorage.removeItem(STORAGE_KEY)
  },

  getUsername(): string | null {
    return localStorage.getItem(STORAGE_KEY)
  },
}
