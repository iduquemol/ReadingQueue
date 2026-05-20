import { z } from 'zod'

export const loginSchema = z.object({
  email:    z.string().email('Email inválido.'),
  password: z.string().min(1, 'La contraseña es obligatoria.'),
})

export const registerSchema = z.object({
  email:           z.string().email('Email inválido.'),
  displayName:     z.string().min(2, 'Mínimo 2 caracteres.').max(200),
  password:        z.string()
                    .min(8,  'Mínimo 8 caracteres.')
                    .regex(/[A-Z]/, 'Debe incluir una mayúscula.')
                    .regex(/[0-9]/, 'Debe incluir un número.'),
  confirmPassword: z.string(),
}).refine(d => d.password === d.confirmPassword, {
  message: 'Las contraseñas no coinciden.',
  path:    ['confirmPassword'],
})

export type LoginFormValues    = z.infer<typeof loginSchema>
export type RegisterFormValues = z.infer<typeof registerSchema>
