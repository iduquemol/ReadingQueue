import {
  ResponsiveContainer, BarChart, Bar,
  XAxis, YAxis, Tooltip, Legend, CartesianGrid,
} from 'recharts'
import type { GenreStat } from '@/types'

interface Props {
  data: GenreStat[]
}

export function GenreBarChart({ data }: Props) {
  const angle = data.length > 4 ? -45 : 0

  return (
    <div role="img" aria-label="Gráfico de géneros por estado de lectura" className="w-full">
      <ResponsiveContainer width="100%" height={300}>
        <BarChart data={data} margin={{ top: 5, right: 20, left: 0, bottom: angle ? 60 : 5 }}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="genre" angle={angle} textAnchor={angle ? 'end' : 'middle'} interval={0} />
          <YAxis allowDecimals={false} />
          <Tooltip />
          <Legend />
          <Bar dataKey="read"   name="Leídos"    fill="#22c55e" stackId="a" />
          <Bar dataKey="unread" name="Sin leer"  fill="#d1d5db" stackId="a" />
        </BarChart>
      </ResponsiveContainer>
    </div>
  )
}
