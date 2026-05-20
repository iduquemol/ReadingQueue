import { BookCard } from './BookCard'
import type { Book } from '@/types'

interface Props {
  books:       Book[]
  onEdit:      (book: Book) => void
  onMarkRead:  (book: Book) => void
  onMarkUnread:(book: Book) => void
  onDelete:    (id: number) => void
}

export function BookGrid({ books, onEdit, onMarkRead, onMarkUnread, onDelete }: Props) {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {books.map(book => (
        <BookCard
          key={book.id}
          book={book}
          onEdit={onEdit}
          onMarkRead={onMarkRead}
          onMarkUnread={onMarkUnread}
          onDelete={onDelete}
        />
      ))}
    </div>
  )
}
