import { describe, it, expect, vi, beforeEach } from 'vitest'
import { renderHook, waitFor } from '@testing-library/react'
import { createWrapper } from '../helpers/queryWrapper'

vi.mock('@/api/queueApi')

import { queueApi } from '@/api/queueApi'
import type { QueueItem, GenerateQueueResponse } from '@/types'

const mockQueueItem = (pos = 1): QueueItem => ({
  position:    pos,
  addedAt:     '2025-01-01T00:00:00Z',
  source:      'AI',
  aiReasoning: 'Buen libro para empezar.',
  book: {
    id:               pos,
    userId:           1,
    title:            `Libro ${pos}`,
    author:           'Autor',
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
  },
})

beforeEach(() => {
  vi.clearAllMocks()
})

// ── useQueue ──────────────────────────────────────────────────────────────────

describe('useQueue', () => {
  it('llama a queueApi.getQueue y retorna los ítems', async () => {
    const { useQueue } = await import('@/hooks/useQueue')
    vi.mocked(queueApi.getQueue).mockResolvedValueOnce({ data: [mockQueueItem(1), mockQueueItem(2)] } as never)

    const { result } = renderHook(() => useQueue(), { wrapper: createWrapper() })

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queueApi.getQueue).toHaveBeenCalledOnce()
    expect(result.current.data).toHaveLength(2)
  })
})

// ── useGenerateQueue ──────────────────────────────────────────────────────────

describe('useGenerateQueue', () => {
  it('llama a queueApi.generate y tras éxito invalida ["queue"]', async () => {
    const { useGenerateQueue } = await import('@/hooks/useQueue')
    const response: GenerateQueueResponse = {
      aiContributed: true,
      queue:         [mockQueueItem(1)],
    }
    vi.mocked(queueApi.generate).mockResolvedValueOnce({ data: response } as never)
    vi.mocked(queueApi.getQueue).mockResolvedValue({ data: [] } as never)

    const { result } = renderHook(() => useGenerateQueue(), { wrapper: createWrapper() })
    result.current.mutate()

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queueApi.generate).toHaveBeenCalledOnce()
    expect(result.current.data).toEqual(response)
  })
})

// ── useReorderQueue ───────────────────────────────────────────────────────────

describe('useReorderQueue', () => {
  it('llama a queueApi.reorder con el array { bookId, position }[] correcto', async () => {
    const { useReorderQueue } = await import('@/hooks/useQueue')
    vi.mocked(queueApi.reorder).mockResolvedValueOnce({ data: [] } as never)
    vi.mocked(queueApi.getQueue).mockResolvedValue({ data: [] } as never)

    const positions = [
      { bookId: 3, position: 1 },
      { bookId: 1, position: 2 },
    ]
    const { result } = renderHook(() => useReorderQueue(), { wrapper: createWrapper() })
    result.current.mutate(positions)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queueApi.reorder).toHaveBeenCalledWith(positions)
  })
})

// ── useRemoveFromQueue ────────────────────────────────────────────────────────

describe('useRemoveFromQueue', () => {
  it('llama a queueApi.remove con el bookId correcto', async () => {
    const { useRemoveFromQueue } = await import('@/hooks/useQueue')
    vi.mocked(queueApi.remove).mockResolvedValueOnce({ data: undefined } as never)
    vi.mocked(queueApi.getQueue).mockResolvedValue({ data: [] } as never)

    const { result } = renderHook(() => useRemoveFromQueue(), { wrapper: createWrapper() })
    result.current.mutate(5)

    await waitFor(() => expect(result.current.isSuccess).toBe(true))
    expect(queueApi.remove).toHaveBeenCalledWith(5)
  })
})
