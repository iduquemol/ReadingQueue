import { apiClient } from '@/lib/axios'
import type { DashboardStats, SpecialLists } from '@/types'

export const statsApi = {
  getDashboard:    () => apiClient.get<DashboardStats>('/stats/dashboard'),
  getSpecialLists: () => apiClient.get<SpecialLists>('/stats/special-lists'),
}
