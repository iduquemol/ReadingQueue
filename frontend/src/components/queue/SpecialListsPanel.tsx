import {
  Collapsible,
  CollapsibleContent,
  CollapsibleTrigger,
} from '@/components/ui/collapsible'
import { ChevronDown } from 'lucide-react'
import type { SpecialLists, Book } from '@/types'

function SpecialSection({ title, books }: { title: string; books: Book[] }) {
  return (
    <Collapsible>
      <CollapsibleTrigger className="flex w-full items-center justify-between rounded-md border px-4 py-3 text-sm font-medium hover:bg-accent">
        <span>{title}</span>
        <ChevronDown className="h-4 w-4 transition-transform [[data-state=open]>&]:rotate-180" />
      </CollapsibleTrigger>
      <CollapsibleContent>
        <ul className="mt-1 space-y-1 rounded-md border p-2">
          {books.length === 0 ? (
            <li className="text-sm text-muted-foreground px-2 py-1">Sin libros.</li>
          ) : (
            books.map(b => (
              <li key={b.id} className="flex items-start gap-2 px-2 py-1 text-sm">
                <span className="font-medium">{b.title}</span>
                <span className="text-muted-foreground">— {b.author}</span>
              </li>
            ))
          )}
        </ul>
      </CollapsibleContent>
    </Collapsible>
  )
}

export function SpecialListsPanel({ lists }: { lists: SpecialLists }) {
  return (
    <div className="space-y-2">
      <h2 className="text-lg font-semibold">Listas especiales</h2>
      <SpecialSection title="Próximos 5"            books={lists.next5} />
      <SpecialSection title="Cuando estoy cansado"  books={lists.whenTired} />
      <SpecialSection title="Deuda histórica"        books={lists.historicalDebt} />
    </div>
  )
}
