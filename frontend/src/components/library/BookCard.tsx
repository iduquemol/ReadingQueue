import { MoreHorizontal } from 'lucide-react'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from '@/components/ui/dropdown-menu'
import { StarRating }    from './StarRating'
import { getGenreColor } from '@/lib/genreColors'
import type { Book }     from '@/types'

function formatDate(iso: string) {
  const d = new Date(iso)
  return `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}/${d.getUTCFullYear()}`
}

interface Props {
  book:        Book
  onEdit:      (book: Book) => void
  onMarkRead:  (book: Book) => void
  onMarkUnread:(book: Book) => void
  onDelete:    (id: number) => void
}

export function BookCard({ book, onEdit, onMarkRead, onMarkUnread, onDelete }: Props) {
  return (
    <div className="rounded-lg border bg-card p-4 shadow-sm flex flex-col gap-3">
      <div className="flex items-start justify-between gap-2">
        <div className="min-w-0 flex-1">
          <h3 className="truncate font-semibold leading-tight">{book.title}</h3>
          <p className="text-sm text-muted-foreground">{book.author}</p>
          <p className="text-xs text-muted-foreground">{book.country}</p>
        </div>

        <DropdownMenu>
          <DropdownMenuTrigger asChild>
            <Button variant="ghost" size="icon" aria-label="Opciones del libro">
              <MoreHorizontal className="h-4 w-4" />
            </Button>
          </DropdownMenuTrigger>
          <DropdownMenuContent align="end">
            <DropdownMenuItem onClick={() => onEdit(book)}>Editar</DropdownMenuItem>
            {book.isRead
              ? <DropdownMenuItem onClick={() => onMarkUnread(book)}>Marcar como no leído</DropdownMenuItem>
              : <DropdownMenuItem onClick={() => onMarkRead(book)}>Marcar como leído</DropdownMenuItem>
            }
            <DropdownMenuItem
              className="text-destructive focus:text-destructive"
              onClick={() => onDelete(book.id)}
            >
              Eliminar
            </DropdownMenuItem>
          </DropdownMenuContent>
        </DropdownMenu>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Badge className={getGenreColor(book.genre)}>{book.genre}</Badge>
        {book.isRead && <Badge variant="secondary">✓ Leído</Badge>}
      </div>

      {book.isRead && book.readAt && (
        <p className="text-xs text-muted-foreground">{formatDate(book.readAt)}</p>
      )}

      <StarRating value={book.priority} />
    </div>
  )
}
