import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { queueApi } from '@/api/queueApi'

export const QUEUE_KEY = ['queue'] as const

export function useQueue() {
  return useQuery({
    queryKey: QUEUE_KEY,
    queryFn:  () => queueApi.getQueue().then(r => r.data),
  })
}

export function useGenerateQueue() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => queueApi.generate().then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: QUEUE_KEY }),
  })
}

export function useReorderQueue() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (positions: { bookId: number; position: number }[]) =>
                  queueApi.reorder(positions).then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: QUEUE_KEY }),
  })
}

export function useRemoveFromQueue() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (bookId: number) => queueApi.remove(bookId),
    onSuccess:  () => qc.invalidateQueries({ queryKey: QUEUE_KEY }),
  })
}
