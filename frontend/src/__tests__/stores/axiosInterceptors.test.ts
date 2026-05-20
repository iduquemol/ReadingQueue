import { beforeEach, describe, expect, it, vi } from 'vitest'
import { act } from 'react'
import axios, { type AxiosRequestConfig } from 'axios'
import MockAdapter from 'axios-mock-adapter'
import { apiClient } from '@/lib/axios'
import { useAuthStore } from '@/stores/useAuthStore'

// axios-mock-adapter puede no estar instalado — se instala en este bloque
// Si no está disponible, los tests se marcan como skip

let mock: InstanceType<typeof MockAdapter>

beforeEach(async () => {
  mock = new MockAdapter(apiClient)
  act(() => useAuthStore.setState({
    accessToken:     null,
    refreshToken:    null,
    userId:          null,
    displayName:     null,
    isAuthenticated: false,
  }))
  localStorage.clear()
  vi.clearAllMocks()
})

afterEach(() => {
  mock.restore()
})

describe('Interceptor de request', () => {
  it('adjunta Authorization: Bearer {token} cuando hay accessToken', async () => {
    act(() => useAuthStore.getState().setSession({
      accessToken:  'my-token',
      refreshToken: 'ref',
      userId:       1,
      displayName:  'U',
    }))

    let capturedConfig: AxiosRequestConfig | undefined

    mock.onGet('/books').reply(config => {
      capturedConfig = config
      return [200, []]
    })

    await apiClient.get('/books')

    expect(capturedConfig?.headers?.Authorization).toBe('Bearer my-token')
  })

  it('no adjunta Authorization cuando no hay accessToken', async () => {
    let capturedConfig: AxiosRequestConfig | undefined
    mock.onGet('/books').reply(config => {
      capturedConfig = config
      return [200, []]
    })

    await apiClient.get('/books')

    expect(capturedConfig?.headers?.Authorization).toBeUndefined()
  })
})

describe('Interceptor de response — 401 con refresh', () => {
  it('reintenta la request original con el nuevo token tras refresh exitoso', async () => {
    act(() => useAuthStore.getState().setSession({
      accessToken:  'expired-token',
      refreshToken: 'refresh-token',
      userId:       1,
      displayName:  'U',
    }))

    let callCount = 0
    mock.onGet('/books').reply(() => {
      callCount++
      if (callCount === 1) return [401, { error: 'Unauthorized' }]
      return [200, [{ id: 1 }]]
    })

    // Mock del endpoint de refresh (axios directo, no apiClient)
    const axiosMock = new MockAdapter(axios)
    axiosMock.onPost('/api/auth/refresh').reply(200, {
      accessToken:  'new-access-token',
      refreshToken: 'new-refresh-token',
    })

    const res = await apiClient.get('/books')

    expect(res.data).toEqual([{ id: 1 }])
    expect(callCount).toBe(2)
    expect(useAuthStore.getState().accessToken).toBe('new-access-token')

    axiosMock.restore()
  })

  it('llama a logout y redirige a /login cuando el refresh falla', async () => {
    const originalHref = window.location.href
    Object.defineProperty(window, 'location', {
      writable: true,
      value:    { href: originalHref },
    })

    act(() => useAuthStore.getState().setSession({
      accessToken:  'expired-token',
      refreshToken: 'bad-refresh',
      userId:       1,
      displayName:  'U',
    }))

    mock.onGet('/books').reply(401, { error: 'Unauthorized' })

    const axiosMock = new MockAdapter(axios)
    axiosMock.onPost('/api/auth/refresh').reply(401, { error: 'Invalid refresh' })

    try {
      await apiClient.get('/books')
    } catch {
      // se espera rechazo
    }

    expect(useAuthStore.getState().isAuthenticated).toBe(false)
    expect(window.location.href).toBe('/login')

    axiosMock.restore()
  })

  it('no reintenta el refresh si _retry=true (evita loop infinito)', async () => {
    act(() => useAuthStore.getState().setSession({
      accessToken:  'token',
      refreshToken: 'ref',
      userId:       1,
      displayName:  'U',
    }))

    let refreshCount = 0
    mock.onGet('/books').reply(401)

    const axiosMock = new MockAdapter(axios)
    axiosMock.onPost('/api/auth/refresh').reply(() => {
      refreshCount++
      return [401]
    })

    try {
      await apiClient.get('/books')
    } catch {
      // se espera rechazo
    }

    expect(refreshCount).toBeLessThanOrEqual(1)

    axiosMock.restore()
  })
})
