import type { BookFilters } from '@/types'

interface Props {
  filters:  BookFilters
  genres:   string[]
  onChange: (filters: BookFilters) => void
  onClear:  () => void
}

export function BookFilters({ filters, genres, onChange, onClear }: Props) {
  return (
    <div className="flex flex-wrap items-end gap-4">
      <div className="flex flex-col gap-1">
        <label htmlFor="filter-genre" className="text-sm font-medium">
          Filtrar por género
        </label>
        <select
          id="filter-genre"
          value={filters.genre ?? ''}
          onChange={e => onChange({ ...filters, genre: e.target.value || undefined })}
          className="rounded-md border px-2 py-1.5 text-sm bg-background"
        >
          <option value="">Todos los géneros</option>
          {genres.map(g => <option key={g} value={g}>{g}</option>)}
        </select>
      </div>

      <button
        type="button"
        onClick={onClear}
        className="text-sm text-muted-foreground underline underline-offset-2 hover:text-foreground"
      >
        Limpiar filtros
      </button>
    </div>
  )
}
