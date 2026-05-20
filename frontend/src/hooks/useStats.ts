import { useQuery } from '@tanstack/react-query'
import { statsApi } from '@/api/statsApi'

export function useDashboard() {
  return useQuery({
    queryKey: ['stats', 'dashboard'],
    queryFn:  () => statsApi.getDashboard().then(r => r.data),
  })
}

export function useSpecialLists() {
  return useQuery({
    queryKey: ['stats', 'special-lists'],
    queryFn:  () => statsApi.getSpecialLists().then(r => r.data),
  })
}
