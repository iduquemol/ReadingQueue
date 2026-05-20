import { Menu } from 'lucide-react'
import { useUIStore } from '@/stores/useUIStore'
import { Button }    from '@/components/ui/button'

export function Header() {
  const toggleSidebar = useUIStore(s => s.toggleSidebar)

  return (
    <header className="flex items-center border-b px-4 py-3 md:hidden">
      <Button variant="ghost" size="icon" onClick={toggleSidebar} aria-label="Abrir menú">
        <Menu className="h-5 w-5" />
      </Button>
    </header>
  )
}
