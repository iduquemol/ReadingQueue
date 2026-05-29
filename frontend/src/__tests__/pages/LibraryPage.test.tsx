import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen, within, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { act } from 'react'

vi.mock('@/hooks/useBooks')
vi.mock('@/hooks/useReferenceData')

import { useBooks, useCreateBook, useUpdateBook, useDeleteBook, useMarkAsRead, useMarkAsUnread } from '@/hooks/useBooks'
import { useGenres, useMentalEnergy, useMoods, useRotations, useSubgenres } from '@/hooks/useReferenceData'
import { LibraryPage }  from '@/pages/LibraryPage'
import { useUIStore }   from '@/stores/useUIStore'
import type { Book }    from '@/types'

const BOOK: Book = {
  id: 1, userId: 1,
  title: 'Cien años de soledad', author: 'García Márquez',
  genre: 'Clasico', subgenre: null, country: 'Colombia',
  whyRead: null, priority: 3,
  mentalEnergy: 'Media', recommendedMood: 'Aventurero',
  rotationCategory: 'Debe', isRead: false,
  readAt: null, notes: null,
  createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
}

function setupMocks({
  books = [BOOK],
  isLoading = false,
}: { books?: Book[]; isLoading?: boolean } = {}) {
  vi.mocked(useBooks).mockReturnValue(
    { data: books, isLoading, isError: false, refetch: vi.fn() } as never,
  )
  vi.mocked(useCreateBook).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useUpdateBook).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useDeleteBook).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useMarkAsRead).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useMarkAsUnread).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useGenres).mockReturnValue({ data: ['Clasico', 'Novela contemporánea'] } as never)
  vi.mocked(useMentalEnergy).mockReturnValue({ data: ['Alta', 'Media', 'Baja'] } as never)
  vi.mocked(useMoods).mockReturnValue({ data: ['Aventurero', 'Reflexivo'] } as never)
  vi.mocked(useRotations).mockReturnValue({ data: ['Debe', 'Quiere', 'Puede'] } as never)
  vi.mocked(useSubgenres).mockReturnValue({ data: [], isFetching: false } as never)
}

beforeEach(() => {
  vi.clearAllMocks()
  act(() => useUIStore.setState({
    sidebarOpen: false, bookModalOpen: false,
    bookModalBookId: null, readModalBookId: null, deleteBookId: null,
  }))
  setupMocks()
})

describe('LibraryPage — carga (CA-06)', () => {
  it('muestra LibrarySkeleton mientras carga', () => {
    setupMocks({ isLoading: true, books: [] })
    render(<LibraryPage />)
    expect(screen.getByTestId('library-skeleton')).toBeInTheDocument()
    expect(screen.queryByText('Cien años de soledad')).not.toBeInTheDocument()
  })

  it('muestra un BookCard por libro del mock', () => {
    render(<LibraryPage />)
    expect(screen.getByText('Cien años de soledad')).toBeInTheDocument()
  })
})

describe('LibraryPage — filtros (CA-07 / CA-08)', () => {
  it('filtrar por género llama a useBooks con { genre: "Clasico" } (CA-07)', async () => {
    const user = userEvent.setup()
    render(<LibraryPage />)
    await user.selectOptions(screen.getByLabelText(/filtrar por género/i), 'Clasico')
    expect(vi.mocked(useBooks)).toHaveBeenCalledWith(
      expect.objectContaining({ genre: 'Clasico' }),
    )
  })

  it('"Limpiar filtros" resetea los filtros (CA-08)', async () => {
    const user = userEvent.setup()
    render(<LibraryPage />)
    await user.selectOptions(screen.getByLabelText(/filtrar por género/i), 'Clasico')
    await user.click(screen.getByRole('button', { name: /limpiar filtros/i }))
    expect(vi.mocked(useBooks)).toHaveBeenLastCalledWith({})
  })
})

describe('LibraryPage — modal crear libro (CA-09 / CA-10)', () => {
  it('botón "Agregar libro" abre el modal de creación', async () => {
    const user = userEvent.setup()
    render(<LibraryPage />)
    await user.click(screen.getByRole('button', { name: /agregar libro/i }))
    expect(useUIStore.getState().bookModalOpen).toBe(true)
  })

  it('guardar el formulario llama a useCreateBook.mutate (CA-09)', async () => {
    const mutateMock = vi.fn()
    vi.mocked(useCreateBook).mockReturnValue({ mutate: mutateMock, isPending: false } as never)
    act(() => useUIStore.setState({ bookModalOpen: true, bookModalBookId: null }))

    const user = userEvent.setup()
    render(<LibraryPage />)

    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/título/i), 'El Aleph')
    await user.type(within(dialog).getByLabelText(/autor/i), 'Borges')
    await user.selectOptions(within(dialog).getByLabelText(/^género \*/i), 'Clasico')
    await user.type(within(dialog).getByLabelText(/país/i), 'Argentina')
    await user.selectOptions(within(dialog).getByLabelText(/energía mental/i), 'Alta')
    await user.selectOptions(within(dialog).getByLabelText(/ánimo/i), 'Aventurero')
    await user.selectOptions(within(dialog).getByLabelText(/categoría de rotación/i), 'Debe')
    await user.click(within(dialog).getByRole('button', { name: /guardar/i }))

    await waitFor(() => expect(mutateMock).toHaveBeenCalledOnce())
    expect(mutateMock).toHaveBeenCalledWith(
      expect.objectContaining({ title: 'El Aleph', genre: 'Clasico' }),
      expect.objectContaining({ onSuccess: expect.any(Function) }),
    )
  })

  it('tras éxito de creación el modal se cierra (CA-10)', async () => {
    const mutateMock = vi.fn((_data, { onSuccess }) => onSuccess())
    vi.mocked(useCreateBook).mockReturnValue({ mutate: mutateMock, isPending: false } as never)
    act(() => useUIStore.setState({ bookModalOpen: true, bookModalBookId: null }))

    const user = userEvent.setup()
    render(<LibraryPage />)

    const dialog = await screen.findByRole('dialog')
    await user.type(within(dialog).getByLabelText(/título/i), 'El Aleph')
    await user.type(within(dialog).getByLabelText(/autor/i), 'Borges')
    await user.selectOptions(within(dialog).getByLabelText(/^género \*/i), 'Clasico')
    await user.type(within(dialog).getByLabelText(/país/i), 'Argentina')
    await user.selectOptions(within(dialog).getByLabelText(/energía mental/i), 'Alta')
    await user.selectOptions(within(dialog).getByLabelText(/ánimo/i), 'Aventurero')
    await user.selectOptions(within(dialog).getByLabelText(/categoría de rotación/i), 'Debe')
    await user.click(within(dialog).getByRole('button', { name: /guardar/i }))

    await waitFor(() => expect(useUIStore.getState().bookModalOpen).toBe(false))
  })
})

describe('LibraryPage — eliminar libro (CA-08)', () => {
  it('confirmar eliminación llama a useDeleteBook.mutate con el id', async () => {
    const mutateMock = vi.fn()
    vi.mocked(useDeleteBook).mockReturnValue({ mutate: mutateMock, isPending: false } as never)

    const user = userEvent.setup()
    render(<LibraryPage />)

    await user.click(screen.getByRole('button', { name: /opciones/i }))
    await user.click(await screen.findByRole('menuitem', { name: /eliminar/i }))
    const alertDialog = await screen.findByRole('alertdialog')
    await user.click(within(alertDialog).getByRole('button', { name: /confirmar/i }))

    expect(mutateMock).toHaveBeenCalledWith(BOOK.id, expect.anything())
  })
})

describe('LibraryPage — estado vacío', () => {
  it('muestra ilustración y botón "Agregar libro" cuando no hay libros', () => {
    setupMocks({ books: [] })
    render(<LibraryPage />)
    expect(screen.getByText(/biblioteca está vacía/i)).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /agregar libro/i }).length).toBeGreaterThan(0)
  })
})
