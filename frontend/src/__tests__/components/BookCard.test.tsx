import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { BookCard } from '@/components/library/BookCard'
import type { Book } from '@/types'

const BOOK: Book = {
  id: 1, userId: 1,
  title: 'Cien años de soledad', author: 'Gabriel García Márquez',
  genre: 'Clasico', country: 'Colombia',
  whyRead: null, priority: 4,
  mentalEnergy: 'Media', recommendedMood: 'Aventurero',
  rotationCategory: 'Debe', isRead: false,
  readAt: null, notes: null,
  createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
}

function renderCard(book = BOOK, overrides: Record<string, unknown> = {}) {
  const props = {
    book,
    onEdit:      vi.fn(),
    onMarkRead:  vi.fn(),
    onMarkUnread: vi.fn(),
    onDelete:    vi.fn(),
    ...overrides,
  }
  render(<BookCard {...props} />)
  return props
}

describe('BookCard — contenido', () => {
  it('muestra título, autor, género, país y estrellas de prioridad', () => {
    renderCard()
    expect(screen.getByText('Cien años de soledad')).toBeInTheDocument()
    expect(screen.getByText('Gabriel García Márquez')).toBeInTheDocument()
    expect(screen.getByText('Clasico')).toBeInTheDocument()
    expect(screen.getByText('Colombia')).toBeInTheDocument()
    expect(screen.getAllByText('★')).toHaveLength(4)
  })

  it('muestra badge "✓ Leído" y la fecha formateada cuando isRead=true', () => {
    const readBook = { ...BOOK, isRead: true, readAt: '2024-06-15T12:00:00Z' }
    renderCard(readBook)
    expect(screen.getByText(/leído/i)).toBeInTheDocument()
    expect(screen.getByText('15/06/2024')).toBeInTheDocument()
  })
})

describe('BookCard — menú', () => {
  it('el menú contiene opciones Editar, Marcar como leído y Eliminar', async () => {
    const user = userEvent.setup()
    renderCard()
    await user.click(screen.getByRole('button', { name: /opciones/i }))
    expect(await screen.findByRole('menuitem', { name: /editar/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /marcar como leído/i })).toBeInTheDocument()
    expect(screen.getByRole('menuitem', { name: /eliminar/i })).toBeInTheDocument()
  })

  it('click en Eliminar llama a onDelete con book.id', async () => {
    const user = userEvent.setup()
    const { onDelete } = renderCard()
    await user.click(screen.getByRole('button', { name: /opciones/i }))
    await user.click(await screen.findByRole('menuitem', { name: /eliminar/i }))
    expect(onDelete).toHaveBeenCalledWith(BOOK.id)
  })
})
