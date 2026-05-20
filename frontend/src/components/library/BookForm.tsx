import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { createBookSchema, type CreateBookFormValues } from '@/lib/schemas/book.schemas'
import { Button } from '@/components/ui/button'
import { Input }  from '@/components/ui/input'
import { Label }  from '@/components/ui/label'

interface Props {
  defaultValues?:     Partial<CreateBookFormValues>
  onSubmit:           (data: CreateBookFormValues) => void
  isPending?:         boolean
  genres:             string[]
  mentalEnergyLevels: string[]
  moods:              string[]
  rotationCategories: string[]
}

export function BookForm({
  defaultValues, onSubmit, isPending = false,
  genres, mentalEnergyLevels, moods, rotationCategories,
}: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<CreateBookFormValues>({
    resolver:      zodResolver(createBookSchema),
    defaultValues: { priority: 3, ...defaultValues },
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} noValidate className="space-y-4">
      <div className="space-y-1">
        <Label htmlFor="book-title">Título *</Label>
        <Input id="book-title" {...register('title')} />
        {errors.title && <p className="text-sm text-destructive">{errors.title.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-author">Autor *</Label>
        <Input id="book-author" {...register('author')} />
        {errors.author && <p className="text-sm text-destructive">{errors.author.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-genre">Género *</Label>
        <select id="book-genre" {...register('genre')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {genres.map(g => <option key={g} value={g}>{g}</option>)}
        </select>
        {errors.genre && <p className="text-sm text-destructive">{errors.genre.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-country">País *</Label>
        <Input id="book-country" {...register('country')} />
        {errors.country && <p className="text-sm text-destructive">{errors.country.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-priority">Prioridad (1–5) *</Label>
        <input id="book-priority" type="number" min={1} max={5}
          {...register('priority', { valueAsNumber: true })}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background"
        />
        {errors.priority && <p className="text-sm text-destructive">{errors.priority.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-energy">Energía mental *</Label>
        <select id="book-energy" {...register('mentalEnergy')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {mentalEnergyLevels.map(m => <option key={m} value={m}>{m}</option>)}
        </select>
        {errors.mentalEnergy && <p className="text-sm text-destructive">{errors.mentalEnergy.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-mood">Ánimo *</Label>
        <select id="book-mood" {...register('recommendedMood')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {moods.map(m => <option key={m} value={m}>{m}</option>)}
        </select>
        {errors.recommendedMood && <p className="text-sm text-destructive">{errors.recommendedMood.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="book-rotation">Categoría de rotación *</Label>
        <select id="book-rotation" {...register('rotationCategory')}
          className="w-full rounded-md border px-2 py-1.5 text-sm bg-background">
          <option value="">Selecciona...</option>
          {rotationCategories.map(r => <option key={r} value={r}>{r}</option>)}
        </select>
        {errors.rotationCategory && <p className="text-sm text-destructive">{errors.rotationCategory.message}</p>}
      </div>

      <Button type="submit" className="w-full" disabled={isPending}>
        {isPending ? 'Guardando…' : 'Guardar'}
      </Button>
    </form>
  )
}
