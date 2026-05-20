import { useState } from 'react'
import { Loader2, Sparkles, ListOrdered } from 'lucide-react'
import {
  useQueue, useGenerateQueue, useReorderQueue, useRemoveFromQueue,
} from '@/hooks/useQueue'
import { useSpecialLists } from '@/hooks/useStats'
import { Button }            from '@/components/ui/button'
import { Badge }             from '@/components/ui/badge'
import { Skeleton }          from '@/components/ui/skeleton'
import { QueueDndList }      from '@/components/queue/QueueDndList'
import { SpecialListsPanel } from '@/components/queue/SpecialListsPanel'
import type { GenerateQueueResponse } from '@/types'

function QueueSkeleton() {
  return (
    <div data-testid="queue-skeleton" className="space-y-3">
      {Array.from({ length: 5 }).map((_, i) => (
        <Skeleton key={i} className="h-20 rounded-lg" />
      ))}
    </div>
  )
}

export function QueuePage() {
  const [aiContributed, setAiContributed] = useState<boolean | null>(null)

  const { data: items = [], isLoading } = useQueue()
  const generateQueue   = useGenerateQueue()
  const reorderQueue    = useReorderQueue()
  const removeFromQueue = useRemoveFromQueue()
  const { data: specialLists } = useSpecialLists()

  function handleGenerate() {
    generateQueue.mutate(undefined, {
      onSuccess: (data: GenerateQueueResponse) => setAiContributed(data.aiContributed),
    })
  }

  function handleReorder(positions: { bookId: number; position: number }[]) {
    reorderQueue.mutate(positions)
  }

  function handleRemove(bookId: number) {
    removeFromQueue.mutate(bookId)
  }

  if (isLoading) return <QueueSkeleton />

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Cola de lectura</h1>

        <Button onClick={handleGenerate} disabled={generateQueue.isPending}>
          {generateQueue.isPending ? (
            <>
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
              Claude está analizando tu biblioteca…
            </>
          ) : (
            <>
              <Sparkles className="mr-2 h-4 w-4" />
              Generar cola
            </>
          )}
        </Button>
      </div>

      {aiContributed === true && (
        <Badge className="bg-purple-100 text-purple-800 text-sm px-3 py-1">
          ✨ Generada con IA
        </Badge>
      )}
      {aiContributed === false && (
        <p className="text-sm text-muted-foreground">Generada con algoritmo</p>
      )}

      {items.length === 0 ? (
        <div className="flex flex-col items-center gap-4 py-20">
          <ListOrdered className="h-16 w-16 text-muted-foreground" />
          <p className="text-muted-foreground">Tu cola está vacía.</p>
          <Button onClick={handleGenerate} disabled={generateQueue.isPending}>
            Generar cola
          </Button>
        </div>
      ) : (
        <QueueDndList
          items={items}
          onRemove={handleRemove}
          onReorder={handleReorder}
        />
      )}

      {specialLists && <SpecialListsPanel lists={specialLists} />}
    </div>
  )
}
