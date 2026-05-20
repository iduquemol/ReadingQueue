import { describe, it, expect, vi } from 'vitest'
import { render, screen } from '@testing-library/react'
import { GenreBarChart } from '@/components/stats/GenreBarChart'
import type { GenreStat } from '@/types'

vi.mock('recharts', () => ({
  ResponsiveContainer: ({ children }: { children: React.ReactNode }) => <div>{children}</div>,
  BarChart:  ({ children }: { children: React.ReactNode }) => <svg>{children}</svg>,
  Bar:       () => null,
  XAxis:     () => null,
  YAxis:     () => null,
  Tooltip:   () => null,
  Legend:    () => null,
  CartesianGrid: () => null,
}))

const GENRE_DATA: GenreStat[] = [
  { genre: 'Clasico',             total: 5, read: 2, unread: 3 },
  { genre: 'Novela contemporánea', total: 3, read: 1, unread: 2 },
]

describe('GenreBarChart', () => {
  it('renderiza sin lanzar excepciones con datos de GenreStat (CA-18)', () => {
    expect(() => render(<GenreBarChart data={GENRE_DATA} />)).not.toThrow()
  })

  it('renderiza sin lanzar excepciones con array vacío', () => {
    expect(() => render(<GenreBarChart data={[]} />)).not.toThrow()
  })

  it('tiene un contenedor accesible con aria-label', () => {
    render(<GenreBarChart data={GENRE_DATA} />)
    expect(screen.getByRole('img', { name: /géneros/i })).toBeInTheDocument()
  })
})
