import { describe, it, expect, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import { act } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { ProtectedRoute } from '@/components/layout/ProtectedRoute'
import { useAuthStore } from '@/stores/useAuthStore'

function renderWithRouter(initialPath: string) {
  return render(
    <MemoryRouter initialEntries={[initialPath]}>
      <Routes>
        <Route path="/login" element={<div>Login Page</div>} />
        <Route element={<ProtectedRoute />}>
          <Route path="/library" element={<div>Library Page</div>} />
          <Route path="/queue"   element={<div>Queue Page</div>} />
        </Route>
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  act(() => useAuthStore.setState({
    accessToken: null, refreshToken: null,
    userId: null, displayName: null, isAuthenticated: false,
  }))
})

describe('ProtectedRoute', () => {
  it('redirige a /login cuando el usuario no está autenticado (CA-01)', () => {
    renderWithRouter('/library')
    expect(screen.getByText('Login Page')).toBeInTheDocument()
    expect(screen.queryByText('Library Page')).not.toBeInTheDocument()
  })

  it('muestra el contenido protegido cuando el usuario está autenticado', () => {
    act(() => useAuthStore.getState().setSession({
      accessToken: 'tok', refreshToken: 'ref', userId: 1, displayName: 'U',
    }))
    renderWithRouter('/library')
    expect(screen.getByText('Library Page')).toBeInTheDocument()
    expect(screen.queryByText('Login Page')).not.toBeInTheDocument()
  })

  it('protege cualquier ruta anidada, no solo /library', () => {
    renderWithRouter('/queue')
    expect(screen.getByText('Login Page')).toBeInTheDocument()
  })
})
