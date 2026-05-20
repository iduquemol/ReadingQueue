import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { act } from 'react'
import { MemoryRouter, Route, Routes } from 'react-router-dom'
import { RegisterPage } from '@/pages/RegisterPage'
import { useAuthStore }  from '@/stores/useAuthStore'

vi.mock('@/api/authApi')
import { authApi } from '@/api/authApi'

const AUTH_RESPONSE = {
  accessToken: 'acc', refreshToken: 'ref', userId: 2, displayName: 'Nuevo',
}

function renderRegister() {
  return render(
    <MemoryRouter initialEntries={['/register']}>
      <Routes>
        <Route path="/register" element={<RegisterPage />} />
        <Route path="/library"  element={<div>Library Page</div>} />
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

describe('RegisterPage — validación Zod', () => {
  it('muestra error cuando confirmPassword no coincide con password (CA-05)', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/email/i), 'new@test.com')
    await user.type(screen.getByLabelText(/nombre/i), 'Nuevo Usuario')
    await user.type(screen.getByLabelText(/^contraseña$/i), 'Password1')
    await user.type(screen.getByLabelText(/confirmar/i), 'Different1')
    await user.click(screen.getByRole('button', { name: /registrarse/i }))

    expect(await screen.findByText('Las contraseñas no coinciden.')).toBeInTheDocument()
  })

  it('muestra error cuando la contraseña no tiene mayúscula', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/email/i), 'new@test.com')
    await user.type(screen.getByLabelText(/nombre/i), 'Nuevo')
    await user.type(screen.getByLabelText(/^contraseña$/i), 'password1')
    await user.type(screen.getByLabelText(/confirmar/i), 'password1')
    await user.click(screen.getByRole('button', { name: /registrarse/i }))

    expect(await screen.findByText('Debe incluir una mayúscula.')).toBeInTheDocument()
  })

  it('muestra error cuando el email es inválido', async () => {
    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/email/i), 'no-email')
    await user.click(screen.getByRole('button', { name: /registrarse/i }))

    expect(await screen.findByText('Email inválido.')).toBeInTheDocument()
  })
})

describe('RegisterPage — registro exitoso', () => {
  it('guarda tokens y redirige a /library', async () => {
    vi.mocked(authApi.register).mockResolvedValueOnce({ data: AUTH_RESPONSE } as never)
    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/email/i), 'new@test.com')
    await user.type(screen.getByLabelText(/nombre/i), 'Nuevo Usuario')
    await user.type(screen.getByLabelText(/^contraseña$/i), 'Password1')
    await user.type(screen.getByLabelText(/confirmar/i), 'Password1')
    await user.click(screen.getByRole('button', { name: /registrarse/i }))

    await waitFor(() =>
      expect(screen.getByText('Library Page')).toBeInTheDocument(),
    )
    expect(useAuthStore.getState().isAuthenticated).toBe(true)
  })
})

describe('RegisterPage — error 409', () => {
  it('muestra "Este email ya está registrado."', async () => {
    const error = Object.assign(new Error('Conflict'), {
      isAxiosError: true,
      response: { status: 409 },
    })
    vi.mocked(authApi.register).mockRejectedValueOnce(error)

    const user = userEvent.setup()
    renderRegister()

    await user.type(screen.getByLabelText(/email/i), 'dup@test.com')
    await user.type(screen.getByLabelText(/nombre/i), 'Dup')
    await user.type(screen.getByLabelText(/^contraseña$/i), 'Password1')
    await user.type(screen.getByLabelText(/confirmar/i), 'Password1')
    await user.click(screen.getByRole('button', { name: /registrarse/i }))

    expect(await screen.findByText('Este email ya está registrado.')).toBeInTheDocument()
  })
})

describe('RegisterPage — navegación', () => {
  it('tiene un link a /login', () => {
    renderRegister()
    expect(screen.getByRole('link', { name: /iniciar sesión/i })).toBeInTheDocument()
  })
})
