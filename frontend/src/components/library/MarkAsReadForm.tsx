import { useForm } from 'react-hook-form'
import { zodResolver } from '@hookform/resolvers/zod'
import { markAsReadSchema, type MarkAsReadFormValues } from '@/lib/schemas/book.schemas'
import { Button }   from '@/components/ui/button'
import { Input }    from '@/components/ui/input'
import { Label }    from '@/components/ui/label'
import { Textarea } from '@/components/ui/textarea'

interface Props {
  onSubmit:   (data: MarkAsReadFormValues) => void
  isPending?: boolean
}

export function MarkAsReadForm({ onSubmit, isPending = false }: Props) {
  const { register, handleSubmit, formState: { errors } } = useForm<MarkAsReadFormValues>({
    resolver: zodResolver(markAsReadSchema),
  })

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-4">
      <div className="space-y-1">
        <Label htmlFor="read-at">Fecha de lectura</Label>
        <Input id="read-at" type="date" {...register('readAt')} />
        {errors.readAt && <p className="text-sm text-destructive">{errors.readAt.message}</p>}
      </div>

      <div className="space-y-1">
        <Label htmlFor="read-notes">Notas</Label>
        <Textarea id="read-notes" rows={3} {...register('notes')} />
        {errors.notes && <p className="text-sm text-destructive">{errors.notes.message}</p>}
      </div>

      <Button type="submit" className="w-full" disabled={isPending}>
        {isPending ? 'Guardando…' : 'Marcar como leído'}
      </Button>
    </form>
  )
}
