import type { CountryStat } from '@/types'

interface Props {
  data: CountryStat[]
}

export function CountryList({ data }: Props) {
  const top = data.slice(0, 10)
  const max = top[0]?.total ?? 1

  return (
    <div className="space-y-2">
      {top.map(({ country, total }) => (
        <div key={country} className="space-y-0.5">
          <div className="flex items-center justify-between text-sm">
            <span className="font-medium">{country}</span>
            <span className="text-muted-foreground">{total}</span>
          </div>
          <div className="h-1.5 rounded-full bg-muted">
            <div
              className="h-full rounded-full bg-primary"
              style={{ width: `${(total / max) * 100}%` }}
            />
          </div>
        </div>
      ))}
    </div>
  )
}
