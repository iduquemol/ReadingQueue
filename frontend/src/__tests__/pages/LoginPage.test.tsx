import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { act } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { LoginPage }   from '@/pages/LoginPage'
import { useAuthStore } from '@/stores/useAuthStore'

vi.mock('@/api/authApi')
import { authApi } from '@/api/authApi'

const AUTH_RESPONSE = {
  accessToken:  'acc',
  refreshToken: 'ref',
  userId:       1,
  displayName:  'Test User',
}

function renderLogin() {
  return render(
    <MemoryRouter initialEntries={['/login']}>
      <Routes>
        <Route path="/login"   element={<LoginPage />} />
        <Route path="/library" element={<div>Library Page</div>} />
      </Routes>
    </MemoryRouter>,
  )
}

beforeEach(() => {
  vi.clearAllMocks()
  act(() => useAuthStore.setState({
    accessToken: null, refreshToken: null,
    userId: null, displayName: null, isAuthenticated: false,
  }))
})

describe('LoginPage — estructura', () => {
  it('renderiza campos email y password', () => {
    renderLogin()
    expect(screen.getByLabelText(/email/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/contraseña/i)).toBeInTheDocument()
  })

  it('tiene un link a /register', () => {
    renderLogin()
    expect(screen.getByRole('link', { name: /registr/i })).toBeInTheDocument()
  })
})

describe('LoginPage — validación Zod', () => {
  it('muestra error si el email es inválido', async () => {
    const user = userEvent.setup()
    renderLogin()
    await user.type(screen.getByLabelText(/email/i), 'no-es-email')
    await user.click(screen.getByRole('button', { name: /iniciar sesión/i }))
    expect(await screen.findByText('Email inválido.')).toBeInTheDocument()
  })

  it('muestra error si la contraseña está vacía', async () => {
    const user = userEvent.setup()
    renderLogin()
    await user.type(screen.getByLabelText(/email/i), 'test@test.com')
    await user.click(screen.getByRole('button', { name: /iniciar sesión/i }))
    expect(await screen.findByText('La contraseña es obligatoria.')).toBeInTheDocument()
  })
})

describe('LoginPage — login exitoso (CA-03)', () => {
  it('guarda tokens en el store y redirige a /library', async () => {
    vi.mocked(authApi.login).mockResolvedValueOnce({ data: AUTH_RESPONSE } as never)
    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'test@test.com')
    await user.type(screen.getByLabelText(/contraseña/i), 'Password1')
    await user.click(screen.getByRole('button', { name: /iniciar sesión/i }))

    await waitFor(() =>
      expect(screen.getByText('Library Page')).toBeInTheDocument(),
    )
    expect(useAuthStore.getState().accessToken).toBe('acc')
    expect(useAuthStore.getState().isAuthenticated).toBe(true)
  })
})

describe('LoginPage — login fallido (CA-04)', () => {
  it('muestra "Credenciales inválidas." ante un 401', async () => {
    // axios.isAxiosError comprueba error.isAxiosError === true
    const error = Object.assign(new Error('Unauthorized'), {
      isAxiosError: true,
      response: { status: 401 },
    })
    vi.mocked(authApi.login).mockRejectedValueOnce(error)

    const user = userEvent.setup()
    renderLogin()

    await user.type(screen.getByLabelText(/email/i), 'wrong@test.com')
    await user.type(screen.getByLabelText(/contraseña/i), 'WrongPass1')
    await user.click(screen.getByRole('button', { name: /iniciar sesión/i }))

    expect(await screen.findByText('Credenciales inválidas.')).toBeInTheDocument()
  })
})
