import { NavLink, useNavigate } from 'react-router-dom'
import { BookOpen, List, BarChart2, LogOut } from 'lucide-react'
import { authApi }      from '@/api/authApi'
import { useAuthStore } from '@/stores/useAuthStore'
import { Button }       from '@/components/ui/button'
import { cn }           from '@/lib/utils'

const NAV_ITEMS = [
  { to: '/library', label: 'Biblioteca',    Icon: BookOpen  },
  { to: '/queue',   label: 'Cola',          Icon: List      },
  { to: '/stats',   label: 'Estadísticas',  Icon: BarChart2 },
]

export function Sidebar() {
  const navigate     = useNavigate()
  const displayName  = useAuthStore(s => s.displayName)
  const refreshToken = useAuthStore(s => s.refreshToken)
  const logout       = useAuthStore(s => s.logout)

  async function handleLogout() {
    try { await authApi.logout(refreshToken ?? '') } catch { /* fire-and-forget */ }
    logout()
    navigate('/login', { replace: true })
  }

  return (
    <aside className="flex w-60 shrink-0 flex-col border-r bg-background">
      <div className="flex flex-col gap-1 p-4 flex-1">
        <p className="mb-4 truncate text-sm font-medium text-foreground">{displayName}</p>

        {NAV_ITEMS.map(({ to, label, Icon }) => (
          <NavLink
            key={to}
            to={to}
            className={({ isActive }) =>
              cn(
                'flex items-center gap-2 rounded-md px-3 py-2 text-sm transition-colors',
                isActive
                  ? 'bg-primary/10 font-semibold text-primary'
                  : 'text-muted-foreground hover:bg-accent hover:text-accent-foreground',
              )
            }
          >
            <Icon className="h-4 w-4" />
            {label}
          </NavLink>
        ))}
      </div>

      <div className="p-4">
        <Button
          variant="ghost"
          className="w-full justify-start gap-2 text-muted-foreground"
          onClick={handleLogout}
        >
          <LogOut className="h-4 w-4" />
          Cerrar sesión
        </Button>
      </div>
    </aside>
  )
}
