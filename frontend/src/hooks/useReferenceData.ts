import { useQuery } from '@tanstack/react-query'
import { booksApi } from '@/api/booksApi'

const DAY = 24 * 60 * 60 * 1000

export function useGenres() {
  return useQuery({
    queryKey:  ['ref', 'genres'],
    queryFn:   () => booksApi.getGenres().then(r => r.data),
    staleTime: DAY,
  })
}

export function useMentalEnergy() {
  return useQuery({
    queryKey:  ['ref', 'mental-energy'],
    queryFn:   () => booksApi.getMentalEnergy().then(r => r.data),
    staleTime: DAY,
  })
}

export function useMoods() {
  return useQuery({
    queryKey:  ['ref', 'moods'],
    queryFn:   () => booksApi.getMoods().then(r => r.data),
    staleTime: DAY,
  })
}

export function useRotations() {
  return useQuery({
    queryKey:  ['ref', 'rotations'],
    queryFn:   () => booksApi.getRotations().then(r => r.data),
    staleTime: DAY,
  })
}
