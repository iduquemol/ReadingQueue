import { CSS } from '@dnd-kit/utilities'
import { useSortable } from '@dnd-kit/sortable'
import { GripVertical, X } from 'lucide-react'
import { Badge }           from '@/components/ui/badge'
import { Button }          from '@/components/ui/button'
import { getGenreColor }   from '@/lib/genreColors'
import { SuggestionBadge } from './SuggestionBadge'
import type { QueueItem }  from '@/types'

interface Props {
  item:     QueueItem
  onRemove: (bookId: number) => void
}

export function QueueItemCard({ item, onRemove }: Props) {
  const {
    attributes, listeners, setNodeRef,
    transform, transition, isDragging,
  } = useSortable({ id: item.book.id })

  const style = {
    transform:  CSS.Transform.toString(transform),
    transition,
    opacity:    isDragging ? 0.5 : 1,
    zIndex:     isDragging ? 10 : undefined,
  }

  return (
    <div
      ref={setNodeRef}
      style={style}
      className="flex items-start gap-3 rounded-lg border bg-card p-3 shadow-sm"
    >
      <button
        className="mt-1 cursor-grab text-muted-foreground active:cursor-grabbing"
        aria-label="Arrastrar para reordenar"
        {...attributes}
        {...listeners}
      >
        <GripVertical className="h-4 w-4" />
      </button>

      <span className="mt-1 w-5 shrink-0 text-center text-sm font-bold text-muted-foreground">
        {item.position}
      </span>

      <div className="flex-1 min-w-0 space-y-1">
        <p className="truncate font-semibold text-sm">{item.book.title}</p>
        <p className="text-xs text-muted-foreground">{item.book.author}</p>
        <Badge className={getGenreColor(item.book.genre)}>{item.book.genre}</Badge>
        <SuggestionBadge source={item.source} reasoning={item.aiReasoning} />
      </div>

      <Button
        variant="ghost"
        size="icon"
        className="shrink-0 text-muted-foreground hover:text-destructive"
        aria-label="Eliminar de la cola"
        onClick={() => onRemove(item.book.id)}
      >
        <X className="h-4 w-4" />
      </Button>
    </div>
  )
}
