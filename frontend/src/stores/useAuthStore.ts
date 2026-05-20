import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AuthState {
  accessToken:     string | null
  refreshToken:    string | null
  userId:          number | null
  displayName:     string | null
  isAuthenticated: boolean

  setSession: (data: {
    accessToken:  string
    refreshToken: string
    userId:       number
    displayName:  string
  }) => void
  setTokens: (accessToken: string, refreshToken: string) => void
  logout:    () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken:     null,
      refreshToken:    null,
      userId:          null,
      displayName:     null,
      isAuthenticated: false,

      setSession: (data) => set({
        accessToken:     data.accessToken,
        refreshToken:    data.refreshToken,
        userId:          data.userId,
        displayName:     data.displayName,
        isAuthenticated: true,
      }),

      setTokens: (accessToken, refreshToken) =>
        set({ accessToken, refreshToken }),

      logout: () => set({
        accessToken:     null,
        refreshToken:    null,
        userId:          null,
        displayName:     null,
        isAuthenticated: false,
      }),
    }),
    {
      name:       'auth-storage',
      partialize: (s) => ({
        accessToken:     s.accessToken,
        refreshToken:    s.refreshToken,
        userId:          s.userId,
        displayName:     s.displayName,
        isAuthenticated: s.isAuthenticated,
      }),
    }
  )
)
