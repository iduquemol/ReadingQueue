# spec-06-frontend-mvp.md
# Feature: Frontend Completo MVP (React + TypeScript + Vite + Tailwind + shadcn/ui)

## 1. Resumen

Implementar la totalidad del frontend de la aplicación: autenticación,
gestión completa de la biblioteca de libros, cola inteligente con
sugerencias de Claude, listas especiales y tablero de estadísticas. Al
terminar este spec el MVP es funcional de punta a punta: un usuario puede
registrarse, cargar su biblioteca, generar su cola con IA y ver sus
estadísticas desde el navegador. Este spec consume todos los endpoints
implementados en los Specs 2 al 5.

---

## 2. Motivación

Los Specs 1–5 construyeron un backend completo y testeado. Este spec es
la capa que el usuario realmente toca. Sin él, la app existe pero nadie
puede usarla. Al terminar este spec el ciclo MVP está cerrado: el usuario
puede registrarse, cargar los 313 libros de su biblioteca, generar su cola
inteligente con Claude y ver el tablero de estadísticas desde el navegador.

---

## 3. Usuarios y Casos de Uso

| Actor | Caso de uso |
|---|---|
| Usuario anónimo | Ver pantalla de login y registrarse con email y password |
| Usuario autenticado | Ver su biblioteca con búsqueda y filtros combinables |
| Usuario autenticado | Agregar, editar y eliminar libros con formulario de 12 campos |
| Usuario autenticado | Marcar un libro como leído con fecha y notas opcionales |
| Usuario autenticado | Ver su cola de lectura ordenada con razonamiento de Claude |
| Usuario autenticado | Regenerar la cola con un botón |
| Usuario autenticado | Reordenar la cola manualmente con drag & drop |
| Usuario autenticado | Ver las tres listas especiales |
| Usuario autenticado | Ver el tablero de estadísticas con gráficos por género |
| Usuario autenticado | Cerrar sesión desde la barra lateral |

---

## 4. Páginas y Navegación

La app usa React Router v6 con las siguientes rutas:

| Ruta | Componente | Protegida |
|---|---|---|
| `/login` | `LoginPage` | No |
| `/register` | `RegisterPage` | No |
| `/` | Redirect a `/library` | Sí |
| `/library` | `LibraryPage` | Sí |
| `/library/new` | `LibraryPage` con modal de creación abierto | Sí |
| `/library/:id/edit` | `LibraryPage` con modal de edición abierto | Sí |
| `/queue` | `QueuePage` | Sí |
| `/stats` | `StatsPage` | Sí |
| `*` | `NotFoundPage` | No |

**Rutas protegidas:** si el usuario no está autenticado (no hay `accessToken`
en `useAuthStore`), redirige a `/login`. Si está autenticado y navega a
`/login` o `/register`, redirige a `/library`.

---

## 5. Requisitos Funcionales por Página

### RF-01 — LoginPage y RegisterPage

**LoginPage (`/login`):**
- Formulario con campos `email` y `password`.
- Validación con Zod: email válido, password no vacío.
- Al enviar llama a `POST /api/auth/login`.
- Si éxito: guarda `accessToken`, `refreshToken`, `userId` y `displayName`
  en `useAuthStore` y en `localStorage`. Redirige a `/library`.
- Si error `401`: muestra mensaje "Credenciales inválidas." debajo del
  formulario.
- Link a `/register` al pie del formulario.

**RegisterPage (`/register`):**
- Formulario con campos `email`, `password`, `confirmPassword` y
  `displayName`.
- Validación Zod: email válido, password ≥ 8 chars con mayúscula y número,
  `confirmPassword` debe coincidir con `password`.
- Al enviar llama a `POST /api/auth/register`.
- Si éxito: misma lógica que login — guarda tokens y redirige a `/library`.
- Si error `409`: muestra "Este email ya está registrado."
- Link a `/login` al pie del formulario.

### RF-02 — AppShell (Layout)

Componente raíz de todas las rutas protegidas. Contiene:
- **Sidebar** fijo en desktop (240px), colapsable en móvil:
  - Logo / nombre "Cola Inteligente" en la cabecera.
  - Links de navegación: 📚 Biblioteca, 📋 Cola, 📊 Estadísticas.
  - Link activo resaltado con color primario.
  - Nombre del usuario autenticado (`displayName`) en el pie.
  - Botón "Cerrar sesión" que llama a `POST /api/auth/logout`, limpia
    `useAuthStore` y `localStorage`, y redirige a `/login`.
- **Header** en móvil con botón hamburguesa para abrir el sidebar.
- **Área de contenido** que renderiza el outlet de React Router.

### RF-03 — LibraryPage (`/library`)

**Vista principal:**
- Grid de `BookCard` componentes (3 columnas en desktop, 2 en tablet,
  1 en móvil).
- Panel de filtros lateral (desktop) o drawer inferior (móvil) con:
  - Campo de búsqueda `q` (texto libre).
  - Select de `genre` (opciones desde `GET /api/books/reference/genres`).
  - Select de `country` (opciones derivadas de los libros del usuario).
  - Select de `mentalEnergy` (desde referencia).
  - Select de `mood` (desde referencia).
  - Select de `rotation` (desde referencia).
  - Slider o select de `minPriority` (1–5).
  - Toggle "Solo no leídos" / "Solo leídos" / "Todos".
  - Botón "Limpiar filtros".
- Contador de resultados: "Mostrando X de Y libros".
- Botón "Agregar libro" (abre modal de creación).
- Estado vacío si no hay libros: ilustración + mensaje + botón de agregar.

**BookCard:**
- Muestra: título, autor, género (badge de color por género), país,
  prioridad (estrellas ★), energía mental (emoji), ánimo.
- Si está leído: badge "✓ Leído" + fecha de lectura.
- Menú de acciones (icono ⋯): Editar, Marcar como leído/no leído, Eliminar.
- Click en la card abre el modal de detalle/edición.

**Modal de crear/editar libro:**
- Formulario con todos los campos: `title`, `author`, `genre` (select),
  `country`, `whyRead`, `priority` (1–5 con estrellas interactivas),
  `mentalEnergy` (select), `recommendedMood` (select),
  `rotationCategory` (select), `notes`.
- Los selects de `genre`, `mentalEnergy`, `recommendedMood` y
  `rotationCategory` se pueblan con los endpoints de referencia del Spec 3.
- Validación con React Hook Form + Zod antes de enviar.
- En creación: llama a `POST /api/books`. En edición: `PUT /api/books/:id`.
- Al guardar correctamente: cierra el modal e invalida el query
  `['books']` en React Query.

**Modal de marcar como leído:**
- Se abre desde el menú de acciones de la `BookCard`.
- Campos opcionales: `readAt` (date picker, default hoy) y `notes`
  (textarea).
- Al confirmar: llama a `POST /api/books/:id/read`.
- Al guardar: invalida `['books']`, `['queue']` y `['stats']` en React Query.

**Confirmación de eliminar:**
- Dialog de confirmación (shadcn `AlertDialog`) antes de llamar a
  `DELETE /api/books/:id`.
- Al confirmar: invalida `['books']` y `['queue']`.

### RF-04 — QueuePage (`/queue`)

**Vista principal:**
- Lista ordenada de hasta 20 `QueueItem` con su posición numérica.
- Botón "Generar cola" prominente que llama a `POST /api/queue/generate`.
  Durante la llamada muestra spinner con texto "Claude está analizando
  tu biblioteca…".
- Badge "✨ Generada con IA" visible si `aiContributed: true` en la
  respuesta. Si `aiContributed: false` muestra "Generada con algoritmo"
  con tooltip explicativo.
- Drag & drop para reordenar (usando `@dnd-kit/core`). Al soltar llama
  a `PUT /api/queue/reorder` con el nuevo orden.
- Botón de eliminar (×) en cada ítem que llama a
  `DELETE /api/queue/:bookId`.
- Estado vacío si la cola está vacía: mensaje + botón "Generar cola".

**QueueItem:**
- Número de posición destacado.
- Título y autor del libro.
- Badge de género con color.
- Prioridad (estrellas).
- Si `aiReasoning` no es null: caja colapsable con icono Claude ✨ y
  el texto del razonamiento en itálica.
- Si `source = 'Filter'`: icono de algoritmo en lugar de Claude.
- Botón de eliminar de la cola (no marca como leído).

**Sección de Listas Especiales** (debajo de la cola o en tab separado):
- Tres tarjetas expandibles:
  - ⭐ **Próximos 5**: muestra los 5 libros en formato compacto.
  - 😴 **Cuando estoy cansado**: lista con badge de energía baja.
  - 🏛️ **Deuda histórica**: lista de clásicos pendientes.
- Datos desde `GET /api/stats/special-lists`.

### RF-05 — StatsPage (`/stats`)

**Vista principal:**
- Cards de resumen en la parte superior:
  - Total de libros.
  - Libros leídos (número + porcentaje en badge verde).
  - Libros pendientes.
  - Barra de progreso general (% leído).
- **Gráfico de barras por género**: usando `recharts` (BarChart).
  Eje X: géneros. Eje Y: cantidad. Barras apiladas leídos/no leídos.
  Colores distintos por estado.
- **Gráfico de energía mental**: barras horizontales con emoji de nivel.
  Solo muestra libros no leídos (cuántos puede leer según su energía).
- **Top 10 países**: lista simple con nombre de país y cantidad.
- **Top 3 sin leer por prioridad**: tres `BookCard` compactas.
- **Últimas 5 lecturas**: lista con título, autor y fecha de lectura.
- Datos desde `GET /api/stats/dashboard`.

---

## 6. Tipos TypeScript

```typescript
// src/types/index.ts — fuente de verdad de tipos del frontend

export interface User {
  id: number
  email: string
  displayName: string
}

export interface Book {
  id: number
  userId: number
  title: string
  author: string
  genre: string
  country: string
  whyRead: string | null
  priority: number           // 1–5
  mentalEnergy: string
  recommendedMood: string
  rotationCategory: string
  isRead: boolean
  readAt: string | null      // ISO 8601
  notes: string | null
  createdAt: string
  updatedAt: string
}

export interface QueueItem {
  position: number
  addedAt: string
  source: 'Manual' | 'AI' | 'Filter'
  aiReasoning: string | null
  book: Book
}

export interface GenerateQueueResponse {
  aiContributed: boolean
  queue: QueueItem[]
}

export interface AISuggestion {
  bookId: number
  bookTitle: string
  score: number
  reasoning: string
  generatedAt: string
  wasAccepted: boolean | null
}

export interface DashboardStats {
  totalBooks: number
  readBooks: number
  unreadBooks: number
  readPercentage: number
  byGenre: GenreStat[]
  byRotationCategory: RotationStat[]
  byMentalEnergy: MentalEnergyStat[]
  byCountry: CountryStat[]
  topUnreadPriority: Book[]
  recentlyRead: Book[]
}

export interface GenreStat {
  genre: string
  total: number
  read: number
  unread: number
}

export interface RotationStat {
  category: string
  total: number
  read: number
  unread: number
}

export interface MentalEnergyStat {
  level: string
  total: number
  unread: number
}

export interface CountryStat {
  country: string
  total: number
}

export interface SpecialLists {
  next5: Book[]
  whenTired: Book[]
  historicalDebt: Book[]
}

export interface BookFilters {
  genre?: string
  country?: string
  mentalEnergy?: string
  mood?: string
  rotation?: string
  minPriority?: number
  isRead?: boolean
  q?: string
}

// Payloads de formularios
export interface CreateBookPayload {
  title: string
  author: string
  genre: string
  country: string
  whyRead?: string
  priority: number
  mentalEnergy: string
  recommendedMood: string
  rotationCategory: string
  notes?: string
}

export type UpdateBookPayload = CreateBookPayload

export interface MarkAsReadPayload {
  readAt?: string
  notes?: string
}

export interface LoginPayload {
  email: string
  password: string
}

export interface RegisterPayload {
  email: string
  password: string
  displayName: string
}

export interface AuthResponse {
  accessToken: string
  refreshToken: string
  userId: number
  displayName: string
}
```

---

## 7. Schemas Zod

```typescript
// src/lib/schemas/auth.schemas.ts
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

// src/lib/schemas/book.schemas.ts
export const createBookSchema = z.object({
  title:            z.string().min(1, 'Obligatorio.').max(500),
  author:           z.string().min(1, 'Obligatorio.').max(300),
  genre:            z.string().min(1, 'Selecciona un género.'),
  country:          z.string().min(1, 'Obligatorio.').max(100),
  whyRead:          z.string().max(1000).optional(),
  priority:         z.number().int().min(1).max(5).default(3),
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
```

---

## 8. Clientes API (Axios)

```typescript
// src/lib/axios.ts
import axios from 'axios'
import { useAuthStore } from '@/stores/useAuthStore'

export const apiClient = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api',
  headers: { 'Content-Type': 'application/json' },
})

// Interceptor de request: adjunta el accessToken a cada petición
apiClient.interceptors.request.use(config => {
  const token = useAuthStore.getState().accessToken
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

// Interceptor de response: si 401, intenta refresh; si falla, logout
apiClient.interceptors.response.use(
  res => res,
  async error => {
    const original = error.config
    if (error.response?.status === 401 && !original._retry) {
      original._retry = true
      try {
        const refreshToken = useAuthStore.getState().refreshToken
        const res = await axios.post('/api/auth/refresh', { refreshToken })
        useAuthStore.getState().setTokens(
          res.data.accessToken,
          res.data.refreshToken
        )
        original.headers.Authorization = `Bearer ${res.data.accessToken}`
        return apiClient(original)
      } catch {
        useAuthStore.getState().logout()
        window.location.href = '/login'
      }
    }
    return Promise.reject(error)
  }
)

// src/api/authApi.ts
import { apiClient } from '@/lib/axios'
import type { AuthResponse, LoginPayload, RegisterPayload } from '@/types'

export const authApi = {
  login:    (p: LoginPayload)    => apiClient.post<AuthResponse>('/auth/login',    p),
  register: (p: RegisterPayload) => apiClient.post<AuthResponse>('/auth/register', p),
  refresh:  (token: string)      => apiClient.post<{ accessToken: string; refreshToken: string }>(
                                      '/auth/refresh', { refreshToken: token }),
  logout:   (token: string)      => apiClient.post('/auth/logout', { refreshToken: token }),
  me:       ()                   => apiClient.get<{ id: number; email: string; displayName: string }>('/auth/me'),
}

// src/api/booksApi.ts
import { apiClient } from '@/lib/axios'
import type {
  Book, BookFilters, CreateBookPayload,
  UpdateBookPayload, MarkAsReadPayload
} from '@/types'

export const booksApi = {
  getAll:      (filters?: BookFilters) =>
                 apiClient.get<Book[]>('/books', { params: filters }),
  getById:     (id: number)            => apiClient.get<Book>(`/books/${id}`),
  create:      (p: CreateBookPayload)  => apiClient.post<Book>('/books', p),
  update:      (id: number, p: UpdateBookPayload) =>
                 apiClient.put<Book>(`/books/${id}`, p),
  remove:      (id: number)            => apiClient.delete(`/books/${id}`),
  markAsRead:  (id: number, p: MarkAsReadPayload) =>
                 apiClient.post<Book>(`/books/${id}/read`, p),
  markAsUnread:(id: number)            => apiClient.post<Book>(`/books/${id}/unread`),

  // Referencia
  getGenres:       () => apiClient.get<string[]>('/books/reference/genres'),
  getMentalEnergy: () => apiClient.get<string[]>('/books/reference/mental-energy'),
  getMoods:        () => apiClient.get<string[]>('/books/reference/moods'),
  getRotations:    () => apiClient.get<string[]>('/books/reference/rotation-categories'),
}

// src/api/queueApi.ts
import { apiClient } from '@/lib/axios'
import type { QueueItem, GenerateQueueResponse, AISuggestion } from '@/types'

export const queueApi = {
  getQueue:      () => apiClient.get<QueueItem[]>('/queue'),
  generate:      () => apiClient.post<GenerateQueueResponse>('/queue/generate'),
  reorder:       (positions: { bookId: number; position: number }[]) =>
                   apiClient.put<QueueItem[]>('/queue/reorder', { positions }),
  remove:        (bookId: number) => apiClient.delete(`/queue/${bookId}`),
  getSuggestions:() => apiClient.get<AISuggestion[]>('/queue/suggestions'),
}

// src/api/statsApi.ts
import { apiClient } from '@/lib/axios'
import type { DashboardStats, SpecialLists } from '@/types'

export const statsApi = {
  getDashboard:    () => apiClient.get<DashboardStats>('/stats/dashboard'),
  getSpecialLists: () => apiClient.get<SpecialLists>('/stats/special-lists'),
}
```

---

## 9. Stores Zustand

```typescript
// src/stores/useAuthStore.ts
import { create } from 'zustand'
import { persist } from 'zustand/middleware'

interface AuthState {
  accessToken:  string | null
  refreshToken: string | null
  userId:       number | null
  displayName:  string | null
  isAuthenticated: boolean

  setSession: (data: {
    accessToken: string
    refreshToken: string
    userId: number
    displayName: string
  }) => void
  setTokens:  (accessToken: string, refreshToken: string) => void
  logout:     () => void
}

export const useAuthStore = create<AuthState>()(
  persist(
    (set) => ({
      accessToken:     null,
      refreshToken:    null,
      userId:          null,
      displayName:     null,
      isAuthenticated: false,

      setSession: (data) => set({
        accessToken:     data.accessToken,
        refreshToken:    data.refreshToken,
        userId:          data.userId,
        displayName:     data.displayName,
        isAuthenticated: true,
      }),

      setTokens: (accessToken, refreshToken) =>
        set({ accessToken, refreshToken }),

      logout: () => set({
        accessToken:     null,
        refreshToken:    null,
        userId:          null,
        displayName:     null,
        isAuthenticated: false,
      }),
    }),
    {
      name:    'auth-storage',  // clave en localStorage
      partialize: (s) => ({     // solo persistir tokens y perfil, no funciones
        accessToken:     s.accessToken,
        refreshToken:    s.refreshToken,
        userId:          s.userId,
        displayName:     s.displayName,
        isAuthenticated: s.isAuthenticated,
      }),
    }
  )
)

// src/stores/useUIStore.ts
// Estado de UI puro: modales abiertos, sidebar abierto, etc.
interface UIState {
  sidebarOpen:     boolean
  bookModalOpen:   boolean
  bookModalBookId: number | null  // null = modo creación
  readModalBookId: number | null
  deleteBookId:    number | null

  openCreateModal:  () => void
  openEditModal:    (id: number) => void
  openReadModal:    (id: number) => void
  openDeleteDialog: (id: number) => void
  closeAll:         () => void
  toggleSidebar:    () => void
}
```

---

## 10. Hooks React Query

```typescript
// src/hooks/useBooks.ts
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import { booksApi } from '@/api/booksApi'
import type { BookFilters, CreateBookPayload, UpdateBookPayload,
              MarkAsReadPayload } from '@/types'

export const BOOKS_KEY = ['books'] as const

export function useBooks(filters?: BookFilters) {
  return useQuery({
    queryKey: [...BOOKS_KEY, filters],
    queryFn:  () => booksApi.getAll(filters).then(r => r.data),
  })
}

export function useCreateBook() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (p: CreateBookPayload) => booksApi.create(p).then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: BOOKS_KEY }),
  })
}

export function useUpdateBook() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...p }: UpdateBookPayload & { id: number }) =>
                  booksApi.update(id, p).then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: BOOKS_KEY }),
  })
}

export function useDeleteBook() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => booksApi.remove(id),
    onSuccess:  () => {
      qc.invalidateQueries({ queryKey: BOOKS_KEY })
      qc.invalidateQueries({ queryKey: ['queue'] })
    },
  })
}

export function useMarkAsRead() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: ({ id, ...p }: MarkAsReadPayload & { id: number }) =>
                  booksApi.markAsRead(id, p).then(r => r.data),
    onSuccess: () => {
      // Invalida los tres caches afectados por marcar como leído
      qc.invalidateQueries({ queryKey: BOOKS_KEY })
      qc.invalidateQueries({ queryKey: ['queue'] })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

export function useMarkAsUnread() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (id: number) => booksApi.markAsUnread(id).then(r => r.data),
    onSuccess:  () => {
      qc.invalidateQueries({ queryKey: BOOKS_KEY })
      qc.invalidateQueries({ queryKey: ['stats'] })
    },
  })
}

// src/hooks/useQueue.ts
export function useQueue() {
  return useQuery({
    queryKey: ['queue'],
    queryFn:  () => queueApi.getQueue().then(r => r.data),
  })
}

export function useGenerateQueue() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: () => queueApi.generate().then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: ['queue'] }),
  })
}

export function useReorderQueue() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (positions: { bookId: number; position: number }[]) =>
                  queueApi.reorder(positions).then(r => r.data),
    onSuccess:  () => qc.invalidateQueries({ queryKey: ['queue'] }),
  })
}

export function useRemoveFromQueue() {
  const qc = useQueryClient()
  return useMutation({
    mutationFn: (bookId: number) => queueApi.remove(bookId),
    onSuccess:  () => qc.invalidateQueries({ queryKey: ['queue'] }),
  })
}

// src/hooks/useStats.ts
export function useDashboard() {
  return useQuery({
    queryKey: ['stats', 'dashboard'],
    queryFn:  () => statsApi.getDashboard().then(r => r.data),
  })
}

export function useSpecialLists() {
  return useQuery({
    queryKey: ['stats', 'special-lists'],
    queryFn:  () => statsApi.getSpecialLists().then(r => r.data),
  })
}

// src/hooks/useReferenceData.ts
// Datos de referencia con staleTime largo (no cambian sin redespliegue)
export function useGenres() {
  return useQuery({
    queryKey: ['ref', 'genres'],
    queryFn:  () => booksApi.getGenres().then(r => r.data),
    staleTime: 24 * 60 * 60 * 1000,   // 24 horas
  })
}
// Ídem para useMentalEnergy, useMoods, useRotations
```

---

## 11. Componentes Clave

### ProtectedRoute

```typescript
// src/components/layout/ProtectedRoute.tsx
import { Navigate, Outlet } from 'react-router-dom'
import { useAuthStore } from '@/stores/useAuthStore'

export function ProtectedRoute() {
  const isAuthenticated = useAuthStore(s => s.isAuthenticated)
  return isAuthenticated ? <Outlet /> : <Navigate to="/login" replace />
}
```

### BookCard

```typescript
// src/components/library/BookCard.tsx
// Props: book: Book, onEdit, onMarkRead, onMarkUnread, onDelete
// — Badge de género con color determinístico por string (hash → color Tailwind)
// — Estrellas de prioridad: ★★★☆☆ para priority=3
// — Menú ⋯ con DropdownMenu de shadcn/ui
// — Si isRead: badge verde "✓ Leído" + fecha formateada
// — Sin lógica de fetching — recibe datos por props
```

### GenreColorMap

```typescript
// src/lib/genreColors.ts
// Mapeo determinístico de género → color Tailwind para badges
export const GENRE_COLORS: Record<string, string> = {
  'No ficción / ensayo':          'bg-blue-100 text-blue-800',
  'Clásico':                      'bg-amber-100 text-amber-800',
  'Novela contemporánea':         'bg-purple-100 text-purple-800',
  'Novela latinoamericana':       'bg-green-100 text-green-800',
  'Cuentos':                      'bg-pink-100 text-pink-800',
  'Novela clásica':               'bg-orange-100 text-orange-800',
  'Poesía':                       'bg-rose-100 text-rose-800',
}

export function getGenreColor(genre: string): string {
  return GENRE_COLORS[genre] ?? 'bg-gray-100 text-gray-800'
}
```

### SuggestionBadge

```typescript
// src/components/queue/SuggestionBadge.tsx
// Muestra el razonamiento de Claude en una caja colapsable
// Props: reasoning: string | null, source: 'AI' | 'Filter' | 'Manual'
// — Si source='AI' y reasoning no null: icono ✨ + texto en itálica
// — Si source='Filter': icono ⚙️ + texto "Generado por algoritmo"
// — Colapsable con Collapsible de shadcn/ui
```

### StarRating

```typescript
// src/components/library/StarRating.tsx
// Renderiza 5 estrellas para prioridad 1-5
// Props: value: number, onChange?: (v: number) => void, readonly?: boolean
// — Si readonly=true: solo muestra estrellas llenas/vacías
// — Si tiene onChange: interactivo con hover y click
// — Usa ★ y ☆ (Unicode, no SVG)
```

### QueueDndList

```typescript
// src/components/queue/QueueDndList.tsx
// Lista drag & drop usando @dnd-kit/core y @dnd-kit/sortable
// Props: items: QueueItem[], onReorder: (newOrder) => void
// — DndContext + SortableContext + useSortable por ítem
// — Al soltar llama a onReorder con el nuevo array
// — El hook useReorderQueue transforma el array a { bookId, position }[]
// — Animación de reordenamiento con CSS transition
```

### GenreBarChart

```typescript
// src/components/stats/GenreBarChart.tsx
// Gráfico de barras apiladas por género usando recharts
// Props: data: GenreStat[]
// — BarChart con dos series: read (verde) y unread (gris)
// — ResponsiveContainer al 100% de ancho
// — Eje X: nombres de género (rotados 45° si hay más de 4)
// — Tooltip con valores absolutos
// — Leyenda al pie
```

---

## 12. Manejo de Estado de Carga y Errores

**Regla general:** todo componente que depende de datos remotos maneja
tres estados explícitamente: cargando, error y datos.

```typescript
// Patrón obligatorio en páginas
function LibraryPage() {
  const { data: books, isLoading, isError, error } = useBooks(filters)

  if (isLoading) return <LibrarySkeleton />   // skeleton cards, no spinner global
  if (isError)   return <ErrorMessage error={error} onRetry={refetch} />

  return <BookGrid books={books} />
}
```

- **Skeletons**: usar `Skeleton` de shadcn/ui para estados de carga.
  `LibrarySkeleton` muestra 6 `BookCard` con skeleton. `QueueSkeleton`
  muestra 5 ítems con skeleton.
- **Toasts**: usar `Sonner` (shadcn/ui) para notificaciones de éxito y
  error de mutaciones. Éxito → verde. Error → rojo con mensaje.
- **Errores de mutación**: mostrar debajo del formulario en modales
  (no toast) para errores de validación (`422`). Toast para errores
  inesperados.

---

## 13. Configuración de React Query y Router

```typescript
// src/main.tsx
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { BrowserRouter } from 'react-router-dom'
import { Toaster } from '@/components/ui/sonner'
import App from './App'
import './index.css'

const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime:        5 * 60 * 1000,  // 5 minutos
      retry:            1,
      refetchOnWindowFocus: false,
    },
  },
})

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <BrowserRouter>
        <App />
        <Toaster richColors position="bottom-right" />
      </BrowserRouter>
    </QueryClientProvider>
  </StrictMode>
)

// src/App.tsx
import { Routes, Route, Navigate } from 'react-router-dom'
import { ProtectedRoute }  from '@/components/layout/ProtectedRoute'
import { AppShell }        from '@/components/layout/AppShell'
import { LoginPage }       from '@/pages/LoginPage'
import { RegisterPage }    from '@/pages/RegisterPage'
import { LibraryPage }     from '@/pages/LibraryPage'
import { QueuePage }       from '@/pages/QueuePage'
import { StatsPage }       from '@/pages/StatsPage'
import { NotFoundPage }    from '@/pages/NotFoundPage'

export default function App() {
  return (
    <Routes>
      {/* Rutas públicas */}
      <Route path="/login"    element={<LoginPage />} />
      <Route path="/register" element={<RegisterPage />} />

      {/* Rutas protegidas bajo AppShell */}
      <Route element={<ProtectedRoute />}>
        <Route element={<AppShell />}>
          <Route index element={<Navigate to="/library" replace />} />
          <Route path="/library"          element={<LibraryPage />} />
          <Route path="/library/new"      element={<LibraryPage />} />
          <Route path="/library/:id/edit" element={<LibraryPage />} />
          <Route path="/queue"            element={<QueuePage />} />
          <Route path="/stats"            element={<StatsPage />} />
        </Route>
      </Route>

      <Route path="*" element={<NotFoundPage />} />
    </Routes>
  )
}
```

---

## 14. Dependencias del Frontend

```json
// Agregar a package.json — dependencies
{
  "@dnd-kit/core":      "latest",
  "@dnd-kit/sortable":  "latest",
  "@dnd-kit/utilities": "latest",
  "@tanstack/react-query": "^5.0.0",
  "axios":              "latest",
  "react-hook-form":    "latest",
  "react-router-dom":   "^6.0.0",
  "recharts":           "latest",
  "sonner":             "latest",
  "zod":                "latest",
  "zustand":            "latest"
}
```

```bash
# Componentes shadcn/ui a instalar con el CLI
npx shadcn@latest add button
npx shadcn@latest add input
npx shadcn@latest add label
npx shadcn@latest add select
npx shadcn@latest add textarea
npx shadcn@latest add dialog
npx shadcn@latest add alert-dialog
npx shadcn@latest add dropdown-menu
npx shadcn@latest add badge
npx shadcn@latest add card
npx shadcn@latest add separator
npx shadcn@latest add skeleton
npx shadcn@latest add collapsible
npx shadcn@latest add tooltip
npx shadcn@latest add sheet          # sidebar móvil
npx shadcn@latest add progress       # barra de progreso en stats
npx shadcn@latest add sonner         # toasts
```

---

## 15. Criterios de Aceptación

| ID | Criterio | Verificación |
|---|---|---|
| CA-01 | Usuario anónimo que navega a `/library` es redirigido a `/login` | Test Vitest + RTL |
| CA-02 | Usuario autenticado que navega a `/login` es redirigido a `/library` | Test Vitest + RTL |
| CA-03 | Login exitoso guarda tokens en localStorage y redirige a `/library` | Test Vitest + RTL |
| CA-04 | Login fallido muestra "Credenciales inválidas." debajo del formulario | Test Vitest + RTL |
| CA-05 | Register con passwords que no coinciden muestra error en `confirmPassword` | Test Vitest + RTL |
| CA-06 | La biblioteca muestra todos los libros al cargar sin filtros | Test Vitest + RTL |
| CA-07 | Aplicar filtro de género reduce la lista correctamente | Test Vitest + RTL con datos mock |
| CA-08 | Limpiar filtros restaura la lista completa | Test Vitest + RTL |
| CA-09 | Crear un libro dispara `POST /api/books` y cierra el modal | Test Vitest + RTL + mock Axios |
| CA-10 | Después de crear un libro, la lista se actualiza (React Query invalida cache) | Test Vitest con QueryClient real |
| CA-11 | Marcar como leído invalida los queries de `books`, `queue` y `stats` | Test Vitest con QueryClient real |
| CA-12 | El botón "Generar cola" muestra spinner durante la llamada | Test Vitest + RTL |
| CA-13 | Si `aiContributed: true`, se muestra el badge "✨ Generada con IA" | Test Vitest + RTL |
| CA-14 | Si `aiContributed: false`, se muestra "Generada con algoritmo" | Test Vitest + RTL |
| CA-15 | Los razonamientos de Claude se muestran en caja colapsable por ítem | Test Vitest + RTL |
| CA-16 | El drag & drop reordena los ítems visualmente antes de llamar a la API | Test Vitest + RTL con dnd-kit |
| CA-17 | El tablero muestra el porcentaje leído correcto | Test Vitest con datos mock |
| CA-18 | El gráfico de géneros renderiza sin errores con datos reales | Test Vitest + RTL |
| CA-19 | Cerrar sesión limpia localStorage y redirige a `/login` | Test Vitest + RTL |
| CA-20 | El interceptor de Axios reintenta con refresh token ante un `401` | Test Vitest con mock de axios |
| CA-21 | Si el refresh token falla, el usuario es redirigido a `/login` | Test Vitest con mock de axios |
| CA-22 | No existe ningún `any` en el código TypeScript — `tsc --noEmit` pasa sin errores | `npm run build` sin errores de tipo |
| CA-23 | No existe ningún `useEffect` para fetching — solo `useQuery`/`useMutation` | Code review / ESLint custom rule |
| CA-24 | Los componentes de `src/components/ui/` no fueron editados directamente | `git diff` solo muestra adiciones en `ui/` |
| CA-25 | La app carga correctamente desde Docker (`http://localhost:3000`) | Verificación manual en navegador |

---

## 16. Archivos que este spec genera

```
frontend/src/
  types/
    index.ts

  lib/
    axios.ts
    queryClient.ts
    genreColors.ts
    schemas/
      auth.schemas.ts
      book.schemas.ts

  api/
    authApi.ts
    booksApi.ts
    queueApi.ts
    statsApi.ts

  stores/
    useAuthStore.ts
    useUIStore.ts

  hooks/
    useBooks.ts
    useQueue.ts
    useStats.ts
    useReferenceData.ts

  components/
    layout/
      ProtectedRoute.tsx
      AppShell.tsx
      Sidebar.tsx
      Header.tsx
    library/
      BookCard.tsx
      BookFilters.tsx
      BookForm.tsx
      BookGrid.tsx
      MarkAsReadForm.tsx
      StarRating.tsx
    queue/
      QueueDndList.tsx
      QueueItemCard.tsx
      SuggestionBadge.tsx
      SpecialListsPanel.tsx
    stats/
      DashboardSummaryCards.tsx
      GenreBarChart.tsx
      MentalEnergyChart.tsx
      CountryList.tsx
      RecentlyReadList.tsx
    ui/                              ← generado por shadcn CLI — no editar

  pages/
    LoginPage.tsx
    RegisterPage.tsx
    LibraryPage.tsx
    QueuePage.tsx
    StatsPage.tsx
    NotFoundPage.tsx

  main.tsx                           ← modificar: agregar providers
  App.tsx                            ← reemplazar con rutas definitivas
  index.css                          ← ya existe del Spec 1

tests/                               ← en frontend/src/__tests__/
  pages/
    LoginPage.test.tsx
    LibraryPage.test.tsx
    QueuePage.test.tsx
    StatsPage.test.tsx
  components/
    BookCard.test.tsx
    BookForm.test.tsx
    QueueDndList.test.tsx
    SuggestionBadge.test.tsx
  hooks/
    useBooks.test.ts
    useQueue.test.ts
  stores/
    useAuthStore.test.ts
```
