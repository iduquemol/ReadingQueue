import { useState } from 'react'
import { BookOpen, Plus } from 'lucide-react'
import {
  useBooks, useCreateBook, useUpdateBook,
  useDeleteBook, useMarkAsRead, useMarkAsUnread,
} from '@/hooks/useBooks'
import { useGenres, useMentalEnergy, useMoods, useRotations } from '@/hooks/useReferenceData'
import { useUIStore } from '@/stores/useUIStore'
import { Button }  from '@/components/ui/button'
import { Skeleton } from '@/components/ui/skeleton'
import {
  Dialog, DialogContent, DialogHeader, DialogTitle,
} from '@/components/ui/dialog'
import {
  AlertDialog, AlertDialogAction, AlertDialogCancel,
  AlertDialogContent, AlertDialogDescription,
  AlertDialogFooter, AlertDialogHeader, AlertDialogTitle,
} from '@/components/ui/alert-dialog'
import { BookFilters }   from '@/components/library/BookFilters'
import { BookGrid }      from '@/components/library/BookGrid'
import { BookForm }      from '@/components/library/BookForm'
import { MarkAsReadForm } from '@/components/library/MarkAsReadForm'
import type { Book, BookFilters as BookFiltersType } from '@/types'
import type { CreateBookFormValues, MarkAsReadFormValues } from '@/lib/schemas/book.schemas'

function LibrarySkeleton() {
  return (
    <div data-testid="library-skeleton" className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
      {Array.from({ length: 8 }).map((_, i) => (
        <Skeleton key={i} className="h-48 rounded-lg" />
      ))}
    </div>
  )
}

export function LibraryPage() {
  const [filters, setFilters] = useState<BookFiltersType>({})

  const { data: books = [], isLoading, isError, refetch } = useBooks(filters)
  const createBook  = useCreateBook()
  const updateBook  = useUpdateBook()
  const deleteBook  = useDeleteBook()
  const markAsRead  = useMarkAsRead()
  const markAsUnread = useMarkAsUnread()

  const { data: genres             = [] } = useGenres()
  const { data: mentalEnergyLevels = [] } = useMentalEnergy()
  const { data: moods              = [] } = useMoods()
  const { data: rotations          = [] } = useRotations()

  const {
    bookModalOpen, bookModalBookId, readModalBookId, deleteBookId,
    openCreateModal, openEditModal, openReadModal, openDeleteDialog, closeAll,
  } = useUIStore()

  const editingBook  = books.find(b => b.id === bookModalBookId)
  const readingBook  = books.find(b => b.id === readModalBookId)

  function handleCreate(data: CreateBookFormValues) {
    createBook.mutate(data, { onSuccess: () => closeAll() })
  }

  function handleUpdate(data: CreateBookFormValues) {
    if (!bookModalBookId) return
    updateBook.mutate({ id: bookModalBookId, ...data }, { onSuccess: () => closeAll() })
  }

  function handleDelete() {
    if (deleteBookId == null) return
    deleteBook.mutate(deleteBookId, { onSuccess: () => closeAll() })
  }

  function handleMarkRead(data: MarkAsReadFormValues) {
    if (readModalBookId == null) return
    markAsRead.mutate({ id: readModalBookId, ...data }, { onSuccess: () => closeAll() })
  }

  function handleMarkUnread(book: Book) {
    markAsUnread.mutate(book.id)
  }

  if (isError) {
    return (
      <div className="flex flex-col items-center gap-4 py-20">
        <p className="text-muted-foreground">Error al cargar los libros.</p>
        <Button onClick={() => refetch()}>Reintentar</Button>
      </div>
    )
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Biblioteca</h1>
        <Button onClick={openCreateModal}>
          <Plus className="mr-2 h-4 w-4" />
          Agregar libro
        </Button>
      </div>

      <BookFilters
        filters={filters}
        genres={genres}
        onChange={setFilters}
        onClear={() => setFilters({})}
      />

      {isLoading ? (
        <LibrarySkeleton />
      ) : books.length === 0 ? (
        <div className="flex flex-col items-center gap-4 py-20">
          <BookOpen className="h-16 w-16 text-muted-foreground" />
          <p className="text-muted-foreground">Tu biblioteca está vacía.</p>
          <Button onClick={openCreateModal}>Agregar libro</Button>
        </div>
      ) : (
        <BookGrid
          books={books}
          onEdit={b => openEditModal(b.id)}
          onMarkRead={b => openReadModal(b.id)}
          onMarkUnread={handleMarkUnread}
          onDelete={openDeleteDialog}
        />
      )}

      {/* Create / Edit dialog */}
      <Dialog open={bookModalOpen} onOpenChange={open => !open && closeAll()}>
        <DialogContent className="max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>{bookModalBookId ? 'Editar libro' : 'Agregar libro'}</DialogTitle>
          </DialogHeader>
          <BookForm
            key={bookModalBookId ?? 'create'}
            defaultValues={editingBook ? {
              ...editingBook,
              subgenre: editingBook.subgenre ?? undefined,
              whyRead:  editingBook.whyRead  ?? undefined,
              notes:    editingBook.notes    ?? undefined,
            } : undefined}
            onSubmit={bookModalBookId ? handleUpdate : handleCreate}
            isPending={createBook.isPending || updateBook.isPending}
            genres={genres}
            mentalEnergyLevels={mentalEnergyLevels}
            moods={moods}
            rotationCategories={rotations}
          />
        </DialogContent>
      </Dialog>

      {/* Mark as read dialog */}
      <Dialog open={readModalBookId !== null} onOpenChange={open => !open && closeAll()}>
        <DialogContent>
          <DialogHeader>
            <DialogTitle>Marcar como leído: {readingBook?.title}</DialogTitle>
          </DialogHeader>
          <MarkAsReadForm
            onSubmit={handleMarkRead}
            isPending={markAsRead.isPending}
          />
        </DialogContent>
      </Dialog>

      {/* Delete alert dialog */}
      <AlertDialog open={deleteBookId !== null} onOpenChange={open => !open && closeAll()}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>¿Eliminar libro?</AlertDialogTitle>
            <AlertDialogDescription>
              Esta acción no se puede deshacer.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel onClick={closeAll}>Cancelar</AlertDialogCancel>
            <AlertDialogAction onClick={handleDelete}>Confirmar</AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  )
}
