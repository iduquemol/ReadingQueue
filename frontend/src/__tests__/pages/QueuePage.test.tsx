import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'

vi.mock('@/hooks/useQueue')
vi.mock('@/hooks/useStats')

import {
  useQueue, useGenerateQueue, useReorderQueue, useRemoveFromQueue,
} from '@/hooks/useQueue'
import { useSpecialLists } from '@/hooks/useStats'
import { QueuePage } from '@/pages/QueuePage'
import type { QueueItem, Book } from '@/types'

const BOOK: Book = {
  id: 1, userId: 1,
  title: 'Cien años de soledad', author: 'García Márquez',
  genre: 'Clasico', country: 'Colombia',
  whyRead: null, priority: 3,
  mentalEnergy: 'Media', recommendedMood: 'Aventurero',
  rotationCategory: 'Debe', isRead: false,
  readAt: null, notes: null,
  createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
}

const ITEM: QueueItem = {
  position: 1, addedAt: '2024-01-01T00:00:00Z',
  source: 'Manual', aiReasoning: null, book: BOOK,
}

const SPECIAL_LISTS = {
  next5:          [BOOK],
  whenTired:      [BOOK],
  historicalDebt: [BOOK],
}

function setupMocks({
  items     = [ITEM] as QueueItem[],
  isLoading = false,
  isPending = false,
  mutate    = vi.fn(),
} = {}) {
  vi.mocked(useQueue).mockReturnValue(
    { data: items, isLoading, isError: false, refetch: vi.fn() } as never,
  )
  vi.mocked(useGenerateQueue).mockReturnValue({ mutate, isPending } as never)
  vi.mocked(useReorderQueue).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useRemoveFromQueue).mockReturnValue({ mutate: vi.fn(), isPending: false } as never)
  vi.mocked(useSpecialLists).mockReturnValue({ data: SPECIAL_LISTS } as never)
}

beforeEach(() => {
  vi.clearAllMocks()
  setupMocks()
})

describe('QueuePage — carga', () => {
  it('muestra QueueSkeleton mientras carga', () => {
    setupMocks({ isLoading: true, items: [] })
    render(<QueuePage />)
    expect(screen.getByTestId('queue-skeleton')).toBeInTheDocument()
  })
})

describe('QueuePage — estado vacío', () => {
  it('muestra mensaje y botón "Generar cola" cuando la cola está vacía', () => {
    setupMocks({ items: [] })
    render(<QueuePage />)
    expect(screen.getByText(/cola está vacía/i)).toBeInTheDocument()
    expect(screen.getAllByRole('button', { name: /generar cola/i }).length).toBeGreaterThan(0)
  })
})

describe('QueuePage — generar cola (CA-12)', () => {
  it('el botón "Generar cola" llama a useGenerateQueue.mutate()', async () => {
    const mutate = vi.fn()
    setupMocks({ items: [], mutate })
    const user = userEvent.setup()
    render(<QueuePage />)
    await user.click(screen.getAllByRole('button', { name: /generar cola/i })[0])
    expect(mutate).toHaveBeenCalledOnce()
  })

  it('durante la mutación muestra "Claude está analizando tu biblioteca…" (CA-12)', () => {
    setupMocks({ isPending: true })
    render(<QueuePage />)
    expect(screen.getByText(/Claude está analizando tu biblioteca/i)).toBeInTheDocument()
  })
})

describe('QueuePage — badge IA (CA-13 / CA-14)', () => {
  it('muestra "✨ Generada con IA" cuando aiContributed=true (CA-13)', async () => {
    const mutate = vi.fn((_args, { onSuccess }) =>
      onSuccess({ aiContributed: true, queue: [] }),
    )
    setupMocks({ items: [], mutate })
    const user = userEvent.setup()
    render(<QueuePage />)
    await user.click(screen.getAllByRole('button', { name: /generar cola/i })[0])
    expect(await screen.findByText(/✨ Generada con IA/i)).toBeInTheDocument()
  })

  it('muestra "Generada con algoritmo" cuando aiContributed=false (CA-14)', async () => {
    const mutate = vi.fn((_args, { onSuccess }) =>
      onSuccess({ aiContributed: false, queue: [] }),
    )
    setupMocks({ items: [], mutate })
    const user = userEvent.setup()
    render(<QueuePage />)
    await user.click(screen.getAllByRole('button', { name: /generar cola/i })[0])
    expect(await screen.findByText(/Generada con algoritmo/i)).toBeInTheDocument()
  })
})

describe('QueuePage — listas especiales', () => {
  it('renderiza las tres secciones de listas especiales', () => {
    render(<QueuePage />)
    expect(screen.getByText(/Próximos 5/i)).toBeInTheDocument()
    expect(screen.getByText(/Cuando.*cansado/i)).toBeInTheDocument()
    expect(screen.getByText(/Deuda histórica/i)).toBeInTheDocument()
  })
})
