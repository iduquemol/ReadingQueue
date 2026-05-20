import { beforeEach, describe, expect, it } from 'vitest'
import { act } from 'react'
import { useAuthStore } from '@/stores/useAuthStore'

const SESSION = {
  accessToken:  'access-abc',
  refreshToken: 'refresh-xyz',
  userId:       42,
  displayName:  'Test User',
}

beforeEach(() => {
  // Resetear el store entre tests
  act(() => useAuthStore.setState({
    accessToken:     null,
    refreshToken:    null,
    userId:          null,
    displayName:     null,
    isAuthenticated: false,
  }))
  localStorage.clear()
})

describe('useAuthStore — estado inicial', () => {
  it('todos los campos son null o false', () => {
    const s = useAuthStore.getState()
    expect(s.accessToken).toBeNull()
    expect(s.refreshToken).toBeNull()
    expect(s.userId).toBeNull()
    expect(s.displayName).toBeNull()
    expect(s.isAuthenticated).toBe(false)
  })
})

describe('useAuthStore — setSession', () => {
  it('asigna todos los campos y pone isAuthenticated=true', () => {
    act(() => useAuthStore.getState().setSession(SESSION))
    const s = useAuthStore.getState()
    expect(s.accessToken).toBe('access-abc')
    expect(s.refreshToken).toBe('refresh-xyz')
    expect(s.userId).toBe(42)
    expect(s.displayName).toBe('Test User')
    expect(s.isAuthenticated).toBe(true)
  })
})

describe('useAuthStore — setTokens', () => {
  it('actualiza solo los tokens sin afectar displayName ni userId', () => {
    act(() => useAuthStore.getState().setSession(SESSION))
    act(() => useAuthStore.getState().setTokens('new-access', 'new-refresh'))
    const s = useAuthStore.getState()
    expect(s.accessToken).toBe('new-access')
    expect(s.refreshToken).toBe('new-refresh')
    expect(s.displayName).toBe('Test User')
    expect(s.userId).toBe(42)
    expect(s.isAuthenticated).toBe(true)
  })
})

describe('useAuthStore — logout', () => {
  it('resetea todos los campos a null/false', () => {
    act(() => useAuthStore.getState().setSession(SESSION))
    act(() => useAuthStore.getState().logout())
    const s = useAuthStore.getState()
    expect(s.accessToken).toBeNull()
    expect(s.refreshToken).toBeNull()
    expect(s.userId).toBeNull()
    expect(s.displayName).toBeNull()
    expect(s.isAuthenticated).toBe(false)
  })
})

describe('useAuthStore — persistencia localStorage', () => {
  it('persiste la sesión con clave auth-storage', () => {
    act(() => useAuthStore.getState().setSession(SESSION))
    const raw = localStorage.getItem('auth-storage')
    expect(raw).not.toBeNull()
    const parsed = JSON.parse(raw!)
    expect(parsed.state.accessToken).toBe('access-abc')
    expect(parsed.state.isAuthenticated).toBe(true)
  })

  it('tras logout, localStorage ya no contiene tokens', () => {
    act(() => useAuthStore.getState().setSession(SESSION))
    act(() => useAuthStore.getState().logout())
    const raw = localStorage.getItem('auth-storage')
    const parsed = JSON.parse(raw!)
    expect(parsed.state.accessToken).toBeNull()
    expect(parsed.state.isAuthenticated).toBe(false)
  })
})
