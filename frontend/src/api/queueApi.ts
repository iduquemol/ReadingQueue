import { apiClient } from '@/lib/axios'
import type { QueueItem, GenerateQueueResponse, AISuggestion } from '@/types'

export const queueApi = {
  getQueue:       () => apiClient.get<QueueItem[]>('/queue'),
  generate:       () => apiClient.post<GenerateQueueResponse>('/queue/generate'),
  reorder:        (positions: { bookId: number; position: number }[]) =>
                    apiClient.put<QueueItem[]>('/queue/reorder', { positions }),
  remove:         (bookId: number) => apiClient.delete(`/queue/${bookId}`),
  getSuggestions: () => apiClient.get<AISuggestion[]>('/queue/suggestions'),
}
