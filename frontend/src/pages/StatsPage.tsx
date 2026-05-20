import { useDashboard } from '@/hooks/useStats'
import { Skeleton }     from '@/components/ui/skeleton'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Button }                  from '@/components/ui/button'
import { DashboardSummaryCards }   from '@/components/stats/DashboardSummaryCards'
import { GenreBarChart }           from '@/components/stats/GenreBarChart'
import { MentalEnergyChart }       from '@/components/stats/MentalEnergyChart'
import { CountryList }             from '@/components/stats/CountryList'
import { RecentlyReadList }        from '@/components/stats/RecentlyReadList'

function StatsSkeleton() {
  return (
    <div data-testid="stats-skeleton" className="space-y-6">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => <Skeleton key={i} className="h-28 rounded-lg" />)}
      </div>
      <Skeleton className="h-72 rounded-lg" />
      <div className="grid gap-4 md:grid-cols-2">
        <Skeleton className="h-48 rounded-lg" />
        <Skeleton className="h-48 rounded-lg" />
      </div>
    </div>
  )
}

export function StatsPage() {
  const { data: stats, isLoading, isError, refetch } = useDashboard()

  if (isLoading) return <StatsSkeleton />

  if (isError || !stats) {
    return (
      <div className="flex flex-col items-center gap-4 py-20">
        <p className="text-muted-foreground">Error al cargar las estadísticas.</p>
        <Button onClick={() => refetch()}>Reintentar</Button>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <h1 className="text-2xl font-bold">Estadísticas</h1>

      <DashboardSummaryCards stats={stats} />

      <Card>
        <CardHeader>
          <CardTitle>Distribución por género</CardTitle>
        </CardHeader>
        <CardContent>
          <GenreBarChart data={stats.byGenre} />
        </CardContent>
      </Card>

      <div className="grid gap-6 md:grid-cols-2">
        <Card>
          <CardHeader>
            <CardTitle>Energía mental</CardTitle>
          </CardHeader>
          <CardContent>
            <MentalEnergyChart data={stats.byMentalEnergy} />
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Top países</CardTitle>
          </CardHeader>
          <CardContent>
            <CountryList data={stats.byCountry} />
          </CardContent>
        </Card>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Últimas lecturas</CardTitle>
        </CardHeader>
        <CardContent>
          <RecentlyReadList books={stats.recentlyRead} />
        </CardContent>
      </Card>
    </div>
  )
}
