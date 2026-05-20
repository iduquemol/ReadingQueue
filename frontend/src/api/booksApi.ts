import { apiClient } from '@/lib/axios'
import type {
  Book, BookFilters, CreateBookPayload,
  UpdateBookPayload, MarkAsReadPayload,
} from '@/types'

export const booksApi = {
  getAll:       (filters?: BookFilters) =>
                  apiClient.get<Book[]>('/books', { params: filters }),
  getById:      (id: number)            => apiClient.get<Book>(`/books/${id}`),
  create:       (p: CreateBookPayload)  => apiClient.post<Book>('/books', p),
  update:       (id: number, p: UpdateBookPayload) =>
                  apiClient.put<Book>(`/books/${id}`, p),
  remove:       (id: number)            => apiClient.delete(`/books/${id}`),
  markAsRead:   (id: number, p: MarkAsReadPayload) =>
                  apiClient.post<Book>(`/books/${id}/read`, p),
  markAsUnread: (id: number)            => apiClient.post<Book>(`/books/${id}/unread`),

  getGenres:       () => apiClient.get<string[]>('/books/reference/genres'),
  getMentalEnergy: () => apiClient.get<string[]>('/books/reference/mental-energy'),
  getMoods:        () => apiClient.get<string[]>('/books/reference/moods'),
  getRotations:    () => apiClient.get<string[]>('/books/reference/rotation-categories'),
}
