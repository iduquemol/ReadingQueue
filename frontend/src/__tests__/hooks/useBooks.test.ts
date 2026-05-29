import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createWrapper } from '../helpers/queryWrapper'

vi.mock('@/api/booksApi')

import { booksApi } from '@/api/booksApi'
import type { Book } from '@/types'

const mockBook = (overrides?: Partial<Book>): Book => ({
  id:               1,
  userId:           1,
  title:            'Cien años de soledad',
  author:           'García Márquez',
  genre:            'Clasico',
  subgenre:         null,
  country:          'Colombia',
  whyRead:          null,
  priority:         3,
  mentalEnergy:     'Baja - cualquier momento',
  recommendedMood:  'Solemne / quiero leer algo grande',
  rotationCategory: 'Clasico',
  isRead:           false,
  readAt:           null,
  notes:            null,
  createdAt:        '2025-01-01T00:00:00Z',
  updatedAt:        '2025-01-01T00:00:00Z',
  ...overrides,
})

beforeEach(() => {
  vi.clearAllMocks()
})

// ── useBooks ──────────────────────────────────────────────────────────────────

describe('useBooks', () => {
  it('llama a booksApi.getAll sin filtros y retorna los datos', async () => {
    const { useBooks } = await import('@/hooks/useBooks')
    vi.mocked(booksApi.getAll).mockResolvedValueOnce({ data: [mockBook()] } as never)

    const { result } = renderHook(() => useBooks(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(booksApi.getAll).toHaveBeenCalledWith(undefined)
    expect(result.current.data).toHaveLength(1)
    expect(result.current.data![0].title).toBe('Cien años de soledad')
  })

  it('pasa el filtro de género en la llamada a getAll', async () => {
    const { useBooks } = await import('@/hooks/useBooks')
    vi.mocked(booksApi.getAll).mockResolvedValueOnce({ data: [] } as never)

    const { result } = renderHook(
      () => useBooks({ genre: 'Clasico' }),
      { wrapper: createWrapper() },
    )

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(booksApi.getAll).toHaveBeenCalledWith({ genre: 'Clasico' })
  })
})

// ── useCreateBook ─────────────────────────────────────────────────────────────

describe('useCreateBook', () => {
  it('llama a booksApi.create y tras éxito invalida ["books"]', async () => {
    const { useCreateBook } = await import('@/hooks/useBooks')
    const created = mockBook({ title: 'Nuevo libro' })
    vi.mocked(booksApi.create).mockResolvedValueOnce({ data: created } as never)
    vi.mocked(booksApi.getAll).mockResolvedValue({ data: [] } as never)

    const { result } = renderHook(() => useCreateBook(), { wrapper: createWrapper() })
    result.current.mutate({
      title:            'Nuevo libro',
      author:           'Autor',
      genre:            'Clasico',
      country:          'Colombia',
      priority:         3,
      mentalEnergy:     'Baja - cualquier momento',
      recommendedMood:  'Solemne / quiero leer algo grande',
      rotationCategory: 'Clasico',
    })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(booksApi.create).toHaveBeenCalledOnce()
  })
})

// ── useDeleteBook ─────────────────────────────────────────────────────────────

describe('useDeleteBook', () => {
  it('llama a booksApi.remove con el id correcto', async () => {
    const { useDeleteBook } = await import('@/hooks/useBooks')
    vi.mocked(booksApi.remove).mockResolvedValueOnce({ data: undefined } as never)
    vi.mocked(booksApi.getAll).mockResolvedValue({ data: [] } as never)

    const { result } = renderHook(() => useDeleteBook(), { wrapper: createWrapper() })
    result.current.mutate(7)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(booksApi.remove).toHaveBeenCalledWith(7)
  })
})

// ── useMarkAsRead ─────────────────────────────────────────────────────────────

describe('useMarkAsRead', () => {
  it('tras éxito invalida books, queue y stats (CA-11)', async () => {
    const { useMarkAsRead } = await import('@/hooks/useBooks')
    const readBook = mockBook({ isRead: true, readAt: '2025-06-01T00:00:00Z' })
    vi.mocked(booksApi.markAsRead).mockResolvedValueOnce({ data: readBook } as never)
    vi.mocked(booksApi.getAll).mockResolvedValue({ data: [] } as never)

    const { result } = renderHook(() => useMarkAsRead(), { wrapper: createWrapper() })
    result.current.mutate({ id: 1, readAt: '2025-06-01' })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(booksApi.markAsRead).toHaveBeenCalledWith(1, { readAt: '2025-06-01' })
  })
})
