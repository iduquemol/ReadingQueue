import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { QueueDndList } from '@/components/queue/QueueDndList'
import type { QueueItem } from '@/types'

const makeItem = (position: number, aiReasoning: string | null = null): QueueItem => ({
  position,
  addedAt: '2024-01-01T00:00:00Z',
  source: aiReasoning ? 'AI' : 'Manual',
  aiReasoning,
  book: {
    id: position,
    userId: 1,
    title: `Libro ${position}`,
    author: `Autor ${position}`,
    genre: 'Clasico',
    subgenre: null,
    country: 'Colombia',
    whyRead: null,
    priority: 3,
    mentalEnergy: 'Media',
    recommendedMood: 'Aventurero',
    rotationCategory: 'Debe',
    isRead: false,
    readAt: null,
    notes: null,
    createdAt: '2024-01-01T00:00:00Z',
    updatedAt: '2024-01-01T00:00:00Z',
  },
})

const ITEMS = [makeItem(1), makeItem(2, 'Razonamiento de IA')]

describe('QueueDndList — renderizado', () => {
  it('renderiza un ítem por cada QueueItem del array', () => {
    render(<QueueDndList items={ITEMS} onRemove={vi.fn()} onReorder={vi.fn()} />)
    expect(screen.getByText('Libro 1')).toBeInTheDocument()
    expect(screen.getByText('Libro 2')).toBeInTheDocument()
  })

  it('cada ítem muestra posición, título, autor y badge de género', () => {
    render(<QueueDndList items={[makeItem(1)]} onRemove={vi.fn()} onReorder={vi.fn()} />)
    expect(screen.getByText('1')).toBeInTheDocument()
    expect(screen.getByText('Libro 1')).toBeInTheDocument()
    expect(screen.getByText('Autor 1')).toBeInTheDocument()
    expect(screen.getByText('Clasico')).toBeInTheDocument()
  })

  it('si el ítem tiene aiReasoning renderiza SuggestionBadge con ✨', () => {
    render(<QueueDndList items={ITEMS} onRemove={vi.fn()} onReorder={vi.fn()} />)
    expect(screen.getByText(/✨/)).toBeInTheDocument()
  })
})

describe('QueueDndList — acciones', () => {
  it('el botón × llama a onRemove con el bookId correcto', async () => {
    const user = userEvent.setup()
    const onRemove = vi.fn()
    render(<QueueDndList items={[makeItem(1)]} onRemove={onRemove} onReorder={vi.fn()} />)
    await user.click(screen.getByRole('button', { name: /eliminar de la cola/i }))
    expect(onRemove).toHaveBeenCalledWith(1)
  })
})
