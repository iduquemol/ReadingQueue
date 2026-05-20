import type { MentalEnergyStat } from '@/types'

const EMOJI: Record<string, string> = {
  Alta: '⚡',
  Media: '🧠',
  Baja: '😴',
}

interface Props {
  data: MentalEnergyStat[]
}

export function MentalEnergyChart({ data }: Props) {
  const max = Math.max(...data.map(d => d.total), 1)

  return (
    <div className="space-y-3">
      {data.map(({ level, total, unread }) => (
        <div key={level} className="space-y-1">
          <div className="flex items-center justify-between text-sm">
            <span className="flex items-center gap-1">
              <span>{EMOJI[level] ?? '📚'}</span>
              <span className="font-medium">{level}</span>
            </span>
            <span className="text-muted-foreground">{unread} sin leer / {total} total</span>
          </div>
          <div className="h-2 rounded-full bg-muted">
            <div
              className="h-full rounded-full bg-blue-400"
              style={{ width: `${(total / max) * 100}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  )
}
