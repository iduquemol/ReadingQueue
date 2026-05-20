import { useState } from 'react'
import {
  DndContext,
  closestCenter,
  KeyboardSensor,
  PointerSensor,
  useSensor,
  useSensors,
  type DragEndEvent,
} from '@dnd-kit/core'
import {
  SortableContext,
  sortableKeyboardCoordinates,
  verticalListSortingStrategy,
  arrayMove,
} from '@dnd-kit/sortable'
import { QueueItemCard } from './QueueItemCard'
import type { QueueItem } from '@/types'

interface Props {
  items:     QueueItem[]
  onRemove:  (bookId: number) => void
  onReorder: (positions: { bookId: number; position: number }[]) => void
}

export function QueueDndList({ items: initialItems, onRemove, onReorder }: Props) {
  const [items, setItems] = useState(initialItems)

  const sensors = useSensors(
    useSensor(PointerSensor),
    useSensor(KeyboardSensor, { coordinateGetter: sortableKeyboardCoordinates }),
  )

  function handleDragEnd(event: DragEndEvent) {
    const { active, over } = event
    if (!over || active.id === over.id) return

    const oldIndex = items.findIndex(i => i.book.id === active.id)
    const newIndex = items.findIndex(i => i.book.id === over.id)
    const reordered = arrayMove(items, oldIndex, newIndex).map((item, idx) => ({
      ...item,
      position: idx + 1,
    }))

    setItems(reordered)
    onReorder(reordered.map(i => ({ bookId: i.book.id, position: i.position })))
  }

  return (
    <DndContext
      sensors={sensors}
      collisionDetection={closestCenter}
      onDragEnd={handleDragEnd}
    >
      <SortableContext
        items={items.map(i => i.book.id)}
        strategy={verticalListSortingStrategy}
      >
        <div className="space-y-2">
          {items.map(item => (
            <QueueItemCard
              key={item.book.id}
              item={item}
              onRemove={onRemove}
            />
          ))}
        </div>
      </SortableContext>
    </DndContext>
  )
}
