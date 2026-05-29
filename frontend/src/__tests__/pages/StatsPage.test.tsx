import { describe, it, expect, vi, beforeEach } from 'vitest'
import { render, screen } from '@testing-library/react'

vi.mock('@/hooks/useStats')
vi.mock('@/components/stats/GenreBarChart', () => ({
  GenreBarChart: ({ data }: { data: unknown[] }) => (
    <div data-testid="genre-chart">GenreChart ({data.length} géneros)</div>
  ),
}))

import { useDashboard } from '@/hooks/useStats'
import { StatsPage }    from '@/pages/StatsPage'
import type { DashboardStats, Book } from '@/types'

const BOOK: Book = {
  id: 1, userId: 1,
  title: 'Cien años de soledad', author: 'García Márquez',
  genre: 'Clasico', subgenre: null, country: 'Colombia',
  whyRead: null, priority: 3,
  mentalEnergy: 'Media', recommendedMood: 'Aventurero',
  rotationCategory: 'Debe', isRead: true,
  readAt: '2024-06-15T12:00:00Z', notes: null,
  createdAt: '2024-01-01T00:00:00Z', updatedAt: '2024-01-01T00:00:00Z',
}

const STATS: DashboardStats = {
  totalBooks:         10,
  readBooks:          4,
  unreadBooks:        6,
  readPercentage:     40,
  byGenre:            [{ genre: 'Clasico', total: 5, read: 2, unread: 3 }],
  byRotationCategory: [],
  byMentalEnergy:     [],
  byCountry:          [{ country: 'Colombia', total: 5 }, { country: 'Argentina', total: 3 }],
  topUnreadPriority:  [],
  recentlyRead:       [BOOK],
}

beforeEach(() => {
  vi.clearAllMocks()
  vi.mocked(useDashboard).mockReturnValue(
    { data: STATS, isLoading: false, isError: false, refetch: vi.fn() } as never,
  )
})

describe('StatsPage — carga', () => {
  it('muestra skeletons mientras carga', () => {
    vi.mocked(useDashboard).mockReturnValue(
      { data: undefined, isLoading: true, isError: false, refetch: vi.fn() } as never,
    )
    render(<StatsPage />)
    expect(screen.getByTestId('stats-skeleton')).toBeInTheDocument()
  })
})

describe('StatsPage — cards de resumen (CA-17)', () => {
  it('muestra totalBooks, readBooks, unreadBooks y readPercentage', () => {
    render(<StatsPage />)
    expect(screen.getByText('10')).toBeInTheDocument()
    expect(screen.getByText('4')).toBeInTheDocument()
    expect(screen.getByText('6')).toBeInTheDocument()
    expect(screen.getAllByText('40%').length).toBeGreaterThan(0)
  })

  it('la barra de progreso refleja el readPercentage', () => {
    render(<StatsPage />)
    const bar = screen.getByRole('progressbar')
    expect(bar).toBeInTheDocument()
  })
})

describe('StatsPage — gráficos y listas', () => {
  it('renderiza GenreBarChart con los datos de byGenre', () => {
    render(<StatsPage />)
    expect(screen.getByTestId('genre-chart')).toBeInTheDocument()
    expect(screen.getByText(/1 géneros/i)).toBeInTheDocument()
  })

  it('lista los países del mock en "Top países"', () => {
    render(<StatsPage />)
    expect(screen.getByText('Colombia')).toBeInTheDocument()
    expect(screen.getByText('Argentina')).toBeInTheDocument()
  })

  it('"Últimas lecturas" muestra título y fecha de recentlyRead', () => {
    render(<StatsPage />)
    expect(screen.getByText('Cien años de soledad')).toBeInTheDocument()
    expect(screen.getByText('15/06/2024')).toBeInTheDocument()
  })
})
