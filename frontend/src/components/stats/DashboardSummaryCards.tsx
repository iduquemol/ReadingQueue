import { BookOpen, BookCheck, BookMarked, TrendingUp } from 'lucide-react'
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card'
import { Progress } from '@/components/ui/progress'
import type { DashboardStats } from '@/types'

interface Props {
  stats: DashboardStats
}

function StatCard({
  title, value, icon: Icon, className = '',
}: {
  title: string
  value: string | number
  icon: React.ElementType
  className?: string
}) {
  return (
    <Card>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-sm font-medium text-muted-foreground">{title}</CardTitle>
        <Icon className={`h-4 w-4 ${className}`} />
      </CardHeader>
      <CardContent>
        <p className="text-3xl font-bold">{value}</p>
      </CardContent>
    </Card>
  )
}

export function DashboardSummaryCards({ stats }: Props) {
  return (
    <div className="space-y-4">
      <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <StatCard title="Total libros"  value={stats.totalBooks}     icon={BookOpen}    />
        <StatCard title="Leídos"        value={stats.readBooks}      icon={BookCheck}   className="text-green-500" />
        <StatCard title="Sin leer"      value={stats.unreadBooks}    icon={BookMarked}  className="text-amber-500" />
        <StatCard title="Completado"    value={`${stats.readPercentage}%`} icon={TrendingUp} className="text-blue-500" />
      </div>

      <div className="space-y-1">
        <div className="flex justify-between text-sm text-muted-foreground">
          <span>Progreso de lectura</span>
          <span>{stats.readPercentage}%</span>
        </div>
        <Progress value={stats.readPercentage} className="h-3" />
      </div>
    </div>
  )
}
