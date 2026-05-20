import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, fireEvent, waitFor } from '@testing-library/react'
import { act } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { AppShell }    from '@/components/layout/AppShell'
import { useAuthStore } from '@/stores/useAuthStore'

vi.mock('@/api/authApi')
import { authApi } from '@/api/authApi'

function renderShell(initialPath = '/library') {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
        <Route element={<AppShell />}>
          <Route path="/library" element={<div>Library Page</div>} />
          <Route path="/queue"   element={<div>Queue Page</div>} />
          <Route path="/stats"   element={<div>Stats Page</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  act(() => useAuthStore.setState({
    accessToken: 'tok', refreshToken: 'ref',
    userId: 1, displayName: 'Ana García', isAuthenticated: true,
  }))
})

describe('AppShell — Sidebar', () => {
  it('muestra el displayName del usuario desde el store', () => {
    renderShell()
    expect(screen.getByText('Ana García')).toBeInTheDocument()
  })

  it('tiene links de navegación a /library, /queue y /stats', () => {
    renderShell()
    expect(screen.getByRole('link', { name: /biblioteca/i })).toHaveAttribute('href', '/library')
    expect(screen.getByRole('link', { name: /cola/i })).toHaveAttribute('href', '/queue')
    expect(screen.getByRole('link', { name: /estadísticas/i })).toHaveAttribute('href', '/stats')
  })

  it('el link activo tiene aria-current="page"', () => {
    renderShell('/library')
    expect(screen.getByRole('link', { name: /biblioteca/i })).toHaveAttribute('aria-current', 'page')
  })

  it('logout llama a authApi.logout, luego store.logout y redirige a /login (CA-19)', async () => {
    vi.mocked(authApi.logout).mockResolvedValueOnce({} as never)
    renderShell()

    fireEvent.click(screen.getByRole('button', { name: /cerrar sesión/i }))

    await waitFor(() => expect(authApi.logout).toHaveBeenCalledWith('ref'))
    await waitFor(() => expect(screen.getByText('Login Page')).toBeInTheDocument())
    expect(useAuthStore.getState().isAuthenticated).toBe(false)
  })
})
