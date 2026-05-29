import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { BookForm } from '@/components/library/BookForm'
import { createWrapper } from '@/__tests__/helpers/queryWrapper'

vi.mock('@/hooks/useReferenceData', () => ({
  useSubgenres: () => ({ data: [], isFetching: false }),
}))

vi.mock('@/api/bookLookupApi', () => ({
  lookupBook: vi.fn().mockResolvedValue([]),
}))

const GENRES    = ['Clasico', 'Novela contemporánea']
const ENERGIES  = ['Alta', 'Media', 'Baja']
const MOODS     = ['Aventurero', 'Reflexivo']
const ROTATIONS = ['Debe', 'Quiere', 'Puede']

function renderForm(onSubmit = vi.fn()) {
  render(
    <BookForm
      onSubmit={onSubmit}
      genres={GENRES}
      mentalEnergyLevels={ENERGIES}
      moods={MOODS}
      rotationCategories={ROTATIONS}
    />,
    { wrapper: createWrapper() },
  )
  return { onSubmit }
}

describe('BookForm — estructura', () => {
  it('tiene inputs para todos los campos obligatorios', () => {
    renderForm()
    expect(screen.getByLabelText(/título/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/autor/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^género \*/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/^subgénero/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/país/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/prioridad/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/energía mental/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/ánimo/i)).toBeInTheDocument()
    expect(screen.getByLabelText(/categoría de rotación/i)).toBeInTheDocument()
  })

  it('los selects muestran las opciones del mock de referencia', () => {
    renderForm()
    expect(screen.getByRole('option', { name: 'Clasico' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Alta' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Aventurero' })).toBeInTheDocument()
    expect(screen.getByRole('option', { name: 'Debe' })).toBeInTheDocument()
  })
})

describe('BookForm — validación', () => {
  it('enviar vacío muestra errores de createBookSchema', async () => {
    const user = userEvent.setup()
    renderForm()
    await user.click(screen.getByRole('button', { name: /guardar/i }))
    expect(await screen.findAllByText('Obligatorio.')).not.toHaveLength(0)
  })
})

describe('BookForm — submit', () => {
  it('en modo creación llama a onSubmit con los valores del formulario', async () => {
    const user = userEvent.setup()
    const { onSubmit } = renderForm()

    await user.type(screen.getByLabelText(/título/i), 'El Aleph')
    await user.type(screen.getByLabelText(/autor/i), 'Borges')
    await user.selectOptions(screen.getByLabelText(/^género \*/i), 'Clasico')
    await user.type(screen.getByLabelText(/país/i), 'Argentina')
    await user.selectOptions(screen.getByLabelText(/energía mental/i), 'Alta')
    await user.selectOptions(screen.getByLabelText(/ánimo/i), 'Aventurero')
    await user.selectOptions(screen.getByLabelText(/categoría de rotación/i), 'Debe')
    await user.click(screen.getByRole('button', { name: /guardar/i }))

    await vi.waitFor(() => expect(onSubmit).toHaveBeenCalledOnce())
    expect(onSubmit).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'El Aleph', author: 'Borges', genre: 'Clasico' }),
      expect.anything(),
    )
  })
})
