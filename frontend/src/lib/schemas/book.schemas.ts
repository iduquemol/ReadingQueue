import { z } from 'zod'

export const createBookSchema = z.object({
  title:            z.string().min(1, 'Obligatorio.').max(500),
  author:           z.string().min(1, 'Obligatorio.').max(300),
  genre:            z.string().min(1, 'Selecciona un género.'),
  subgenre:         z.string().optional(),
  country:          z.string().min(1, 'Obligatorio.').max(100),
  whyRead:          z.string().max(1000).optional(),
  priority:         z.number().int().min(1).max(5),
  mentalEnergy:     z.string().min(1, 'Selecciona un nivel.'),
  recommendedMood:  z.string().min(1, 'Selecciona un ánimo.'),
  rotationCategory: z.string().min(1, 'Selecciona una categoría.'),
  notes:            z.string().max(2000).optional(),
})

export const markAsReadSchema = z.object({
  readAt: z.string().optional(),
  notes:  z.string().max(2000).optional(),
})

export type CreateBookFormValues = z.infer<typeof createBookSchema>
export type MarkAsReadFormValues = z.infer<typeof markAsReadSchema>
