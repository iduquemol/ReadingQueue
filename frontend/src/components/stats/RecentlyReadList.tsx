import type { Book } from '@/types'

function formatDate(iso: string) {
  const d = new Date(iso)
  return `${String(d.getUTCDate()).padStart(2, '0')}/${String(d.getUTCMonth() + 1).padStart(2, '0')}/${d.getUTCFullYear()}`
}

interface Props {
  books: Book[]
}

export function RecentlyReadList({ books }: Props) {
  if (books.length === 0) {
    return <p className="text-sm text-muted-foreground">Sin lecturas recientes.</p>
  }

  return (
    <ul className="space-y-3">
      {books.slice(0, 5).map(book => (
        <li key={book.id} className="flex items-start justify-between gap-2">
          <div className="min-w-0">
            <p className="truncate font-medium text-sm">{book.title}</p>
            <p className="text-xs text-muted-foreground">{book.author}</p>
          </div>
          {book.readAt && (
            <span className="shrink-0 text-xs text-muted-foreground">{formatDate(book.readAt)}</span>
          )}
        </li>
      ))}
    </ul>
  )
}
