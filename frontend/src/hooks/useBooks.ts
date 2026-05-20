import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { booksApi } from '@/api/booksApi'
import type { BookFilters, CreateBookPayload, UpdateBookPayload, MarkAsReadPayload } from '@/types'

export const BOOKS_KEY = ['books'] as const

export function useBooks(filters?: BookFilters) {
  return useQuery({
    queryKey: [...BOOKS_KEY, filters],
    queryFn:  () => booksApi.getAll(filters).then(r => r.data),
  })
}

export function useCreateBook() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (p: CreateBookPayload) => booksApi.create(p).then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: BOOKS_KEY }),
  })
}

export function useUpdateBook() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...p }: UpdateBookPayload & { id: number }) =>
                  booksApi.update(id, p).then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: BOOKS_KEY }),
  })
}

export function useDeleteBook() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => booksApi.remove(id),
    onSuccess:  () => {
      qc.invalidateQueries({ queryKey: BOOKS_KEY })
      qc.invalidateQueries({ queryKey: ['queue'] })
    },
  })
}

export function useMarkAsRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...p }: MarkAsReadPayload & { id: number }) =>
                  booksApi.markAsRead(id, p).then(r => r.data),
    onSuccess: () => {
      qc.invalidateQueries({ queryKey: BOOKS_KEY })
      qc.invalidateQueries({ queryKey: ['queue'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export function useMarkAsUnread() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => booksApi.markAsUnread(id).then(r => r.data),
    onSuccess:  () => {
      qc.invalidateQueries({ queryKey: BOOKS_KEY })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}
