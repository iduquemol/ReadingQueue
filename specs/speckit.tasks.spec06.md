# SPEC-06 · Tasks — Frontend Completo MVP (React + TypeScript + Vite + Tailwind + shadcn/ui)
> Proyecto: `ReadingQueue` (solución .NET 9 + React/Vite)
> Regla: **Tests primero, implementación después** (constitution §13)
> Cobertura mínima: **Vitest + React Testing Library** para páginas, componentes clave, hooks y stores

---

## Nota previa: estructura del proyecto frontend

El frontend vive en `frontend/` dentro del repositorio. Spec-01 ya creó el proyecto con Vite + React + TypeScript + Tailwind. Este spec completa toda la lógica funcional sobre esa base.

El orden de implementación sigue el grafo de dependencias: tipos → stores/axios → hooks → componentes base → páginas. Implementar en otro orden genera imports rotos y tests que no compilan.

## Nota previa: proxy de desarrollo

Para que el frontend llame al backend sin CORS en desarrollo, configurar el proxy de Vite en `vite.config.ts`:

```typescript
server: {
  proxy: {
    '/api': {
      target: 'http://localhost:5000',
      changeOrigin: true,
    },
  },
}
```

En producción, Nginx o el servidor de archivos estáticos manejará el proxy. La variable `VITE_API_BASE_URL` queda vacía → `apiClient` usa `/api` relativo.

## Nota previa: configuración de tests Vitest

Los tests usan Vitest + React Testing Library. Configurar en `vite.config.ts`:

```typescript
test: {
  environment: 'jsdom',
  setupFiles:  ['./src/__tests__/setup.ts'],
  globals:     true,
}
```

El archivo `src/__tests__/setup.ts` importa `@testing-library/jest-dom` para los matchers adicionales (`toBeInTheDocument`, etc.). Mockear `window.matchMedia` para evitar errores de jsdom con shadcn.

## Nota previa: mocking de Axios en tests

Los tests de páginas y hooks **no** hacen llamadas HTTP reales. Usar `vi.mock('@/api/booksApi')` (y similares) para reemplazar los módulos de API con mocks tipados. Los hooks de React Query se testean con un `QueryClient` real con `retry: 0` y `gcTime: 0` para evitar comportamiento asíncrono no determinístico.

## Nota previa: shadcn/ui — no editar `src/components/ui/`

Los archivos en `src/components/ui/` son generados por el CLI de shadcn y **no deben editarse manualmente** (CA-24). Toda la lógica de negocio va en los componentes propios que componen los primitivos de shadcn.

---

## Bloque A — Fundamentos: Tipos, Schemas y Configuración del Proyecto

### TASK-06-A1 · Instalar dependencias y configurar herramientas

**Sin tests — configuración pura.**

**Dependencias a instalar:**
```bash
npm install @dnd-kit/core @dnd-kit/sortable @dnd-kit/utilities \
            @tanstack/react-query axios react-hook-form \
            react-router-dom recharts sonner zod zustand
npm install -D @testing-library/react @testing-library/jest-dom \
               @testing-library/user-event vitest jsdom
```

**Componentes shadcn/ui a instalar:**
```bash
npx shadcn@latest add button input label select textarea dialog \
    alert-dialog dropdown-menu badge card separator skeleton \
    collapsible tooltip sheet progress sonner
```

**Archivos a crear/modificar:**
- `vite.config.ts` → agregar `server.proxy` y `test` (Vitest config)
- `src/__tests__/setup.ts` → `import '@testing-library/jest-dom'` + mock de `window.matchMedia`
- `tsconfig.json` → verificar `paths` con alias `@/` apuntando a `./src`

- **Completado cuando:** `npm run build` termina sin errores de TS · `npm test` encuentra y ejecuta tests (aunque sean 0).

---

### TASK-06-A2 · Tipos TypeScript centrales

**Sin tests — fuente de verdad de tipos.**

- **Archivo:** `frontend/src/types/index.ts`
- Contiene las interfaces: `User`, `Book`, `QueueItem`, `GenerateQueueResponse`, `AISuggestion`, `DashboardStats`, `GenreStat`, `RotationStat`, `MentalEnergyStat`, `CountryStat`, `SpecialLists`, `BookFilters`, `CreateBookPayload`, `UpdateBookPayload` (= `CreateBookPayload`), `MarkAsReadPayload`, `LoginPayload`, `RegisterPayload`, `AuthResponse`.
- Todos los campos `string` de fecha usan tipo `string` (ISO 8601) — **no** `Date`.
- `QueueItem.source` es `'Manual' | 'AI' | 'Filter'` (union literal).
- `Book.priority` es `number` (1–5).

- **Completado cuando:** `tsc --noEmit` pasa sin errores en el proyecto completo tras crear este archivo.

---

### TASK-06-A3 · Schemas Zod y colores de género

**Sin tests unitarios — validados indirectamente por tests de formularios en Bloques D y F.**

**Archivos a crear:**

- `frontend/src/lib/schemas/auth.schemas.ts` → `loginSchema`, `registerSchema` (con `.refine` para `confirmPassword`) y sus tipos exportados `LoginFormValues`, `RegisterFormValues`.
- `frontend/src/lib/schemas/book.schemas.ts` → `createBookSchema`, `markAsReadSchema` y sus tipos exportados.
- `frontend/src/lib/genreColors.ts` → `GENRE_COLORS: Record<string, string>` + `getGenreColor(genre: string): string` (fallback `'bg-gray-100 text-gray-800'`).

- **Completado cuando:** los imports de estos módulos compilan sin errores en los tests de formularios.

---

### TASK-06-A4 · Configuración de React Query, Router y providers (`main.tsx` y `App.tsx`)

**Sin tests directos — verificado por todos los tests de páginas que renderizan `<App />`.**

**Archivos a crear/modificar:**
- `frontend/src/lib/queryClient.ts` → exporta la instancia de `QueryClient` con `staleTime: 5 * 60 * 1000`, `retry: 1`, `refetchOnWindowFocus: false`.
- `frontend/src/main.tsx` → envuelve con `QueryClientProvider`, `BrowserRouter`, `Toaster`.
- `frontend/src/App.tsx` → define todas las rutas descritas en §4 del spec, usando `ProtectedRoute` y `AppShell`.

- **Completado cuando:** la app arranca con `npm run dev` sin errores en consola y navegar a `/` redirige a `/login`.

---

## Bloque B — Capa de Datos: Axios, Clientes API y Stores

### TASK-06-B1 · Test: `useAuthStore`

- **Archivo test:** `frontend/src/__tests__/stores/useAuthStore.test.ts`

- **Casos:**
  - [x] Estado inicial tiene todos los campos en `null`/`false`.
  - [x] `setSession` asigna `accessToken`, `refreshToken`, `userId`, `displayName` e `isAuthenticated=true`.
  - [x] `setTokens` actualiza solo `accessToken` y `refreshToken` sin afectar `displayName` ni `userId`.
  - [x] `logout` resetea todos los campos a `null`/`false`.
  - [x] El store persiste en `localStorage` con clave `'auth-storage'` (mock de `localStorage` con `vi.stubGlobal`).
  - [x] Tras `logout`, `localStorage` no contiene tokens.

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-B2 · Implementar Axios, clientes API y stores

**Archivos a crear:**

```
frontend/src/
  lib/
    axios.ts            ← apiClient + interceptores request/response
  api/
    authApi.ts
    booksApi.ts
    queueApi.ts
    statsApi.ts
  stores/
    useAuthStore.ts     ← Zustand + persist middleware
    useUIStore.ts       ← estado de modales y sidebar
```

**`axios.ts`:**
- `apiClient` con `baseURL: import.meta.env.VITE_API_BASE_URL ?? '/api'`.
- Interceptor de request: adjunta `Bearer {accessToken}` desde `useAuthStore.getState()`.
- Interceptor de response: en `401` intenta refresh con `axios.post('/api/auth/refresh', ...)`. Si el refresh es exitoso actualiza los tokens en el store y reintenta la request original (`_retry = true`). Si el refresh falla, llama a `useAuthStore.getState().logout()` y redirige a `/login`.

**`useUIStore.ts`:**
- Estado: `sidebarOpen`, `bookModalOpen`, `bookModalBookId: number | null`, `readModalBookId: number | null`, `deleteBookId: number | null`.
- Acciones: `openCreateModal`, `openEditModal(id)`, `openReadModal(id)`, `openDeleteDialog(id)`, `closeAll`, `toggleSidebar`.

- **Completado cuando:** tests de TASK-06-B1 pasan (verde) y los imports de los clientes API compilan.

---

### TASK-06-B3 · Test: interceptores de Axios

- **Archivo test:** `frontend/src/__tests__/stores/axiosInterceptors.test.ts`
- **Setup:** usar `vi.mock('axios')` o `axios-mock-adapter` para interceptar llamadas HTTP.

- **Casos:**
  - [x] Una request exitosa adjunta el `Authorization: Bearer {token}` si hay `accessToken` en el store (CA-20 parcial).
  - [x] Una request que recibe `401` con `_retry=false` intenta `POST /api/auth/refresh` antes de rechazar (CA-20).
  - [x] Si el refresh responde con nuevos tokens, la request original se reintenta con el nuevo `accessToken`.
  - [x] Si el refresh falla, se llama a `logout()` y `window.location.href` se establece en `'/login'` (CA-21).
  - [x] Una request que recibe `401` con `_retry=true` no vuelve a intentar el refresh (evita loop infinito).

- **Completado cuando:** tests compilan y fallan (rojo).
- **Nota:** ejecutar `npm test -- axiosInterceptors` para ver el fallo antes de que B2 esté completo.

---

## Bloque C — Hooks React Query

### TASK-06-C1 · Test: `useBooks` y `useQueue`

- **Archivos test:**
  - `frontend/src/__tests__/hooks/useBooks.test.ts`
  - `frontend/src/__tests__/hooks/useQueue.test.ts`
- **Setup:** envolver con `QueryClientProvider` usando un `QueryClient` propio con `retry: 0`.

**Casos `useBooks`:**
  - [x] Llama a `booksApi.getAll()` sin filtros y retorna los datos del mock.
  - [x] Con `filters.genre = 'Clasico'` pasa el parámetro en la query key y en la llamada a `booksApi.getAll`.
  - [x] `useCreateBook` llama a `booksApi.create` y tras éxito invalida el query `['books']`.
  - [x] `useDeleteBook` llama a `booksApi.remove` y tras éxito invalida `['books']` y `['queue']`.
  - [x] `useMarkAsRead` tras éxito invalida `['books']`, `['queue']` y `['stats']` (CA-11).

**Casos `useQueue`:**
  - [x] `useQueue` llama a `queueApi.getQueue` y retorna los ítems del mock.
  - [x] `useGenerateQueue` llama a `queueApi.generate` y tras éxito invalida `['queue']`.
  - [x] `useReorderQueue` llama a `queueApi.reorder` con el array `{ bookId, position }[]` correcto.
  - [x] `useRemoveFromQueue` llama a `queueApi.remove(bookId)` y tras éxito invalida `['queue']`.

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-C2 · Implementar hooks React Query

**Archivos a crear:**

```
frontend/src/hooks/
  useBooks.ts          ← useBooks, useCreateBook, useUpdateBook, useDeleteBook,
                          useMarkAsRead, useMarkAsUnread
  useQueue.ts          ← useQueue, useGenerateQueue, useReorderQueue, useRemoveFromQueue
  useStats.ts          ← useDashboard, useSpecialLists
  useReferenceData.ts  ← useGenres, useMentalEnergy, useMoods, useRotations
                          (staleTime: 24h — datos de referencia sin cambios)
```

- **Completado cuando:** tests de TASK-06-C1 pasan (verde).

---

## Bloque D — Autenticación: LoginPage, RegisterPage y ProtectedRoute

### TASK-06-D1 · Test: flujos de autenticación

- **Archivos test:**
  - `frontend/src/__tests__/pages/LoginPage.test.tsx`
  - `frontend/src/__tests__/components/ProtectedRoute.test.tsx`

**Casos `ProtectedRoute`:**
  - [x] Usuario no autenticado que accede a una ruta protegida es redirigido a `/login` (CA-01).
  - [x] Usuario autenticado que navega a `/login` es redirigido a `/library` (CA-02).
  - [x] Usuario autenticado puede acceder a rutas protegidas sin redirección.

**Casos `LoginPage`:**
  - [x] Renderiza campos `email` y `password` con sus labels.
  - [x] Enviar el formulario vacío muestra errores de validación Zod ("Email inválido.", "La contraseña es obligatoria.").
  - [x] Login exitoso (mock `authApi.login` → 200) guarda tokens en el store y redirige a `/library` (CA-03).
  - [x] Login con `401` (mock `authApi.login` → error 401) muestra "Credenciales inválidas." debajo del formulario (CA-04).
  - [x] El formulario tiene un link a `/register`.

**Casos `RegisterPage`:**
  - [x] `confirmPassword` diferente de `password` muestra "Las contraseñas no coinciden." (CA-05).
  - [x] Password sin mayúscula muestra "Debe incluir una mayúscula.".
  - [x] Registro exitoso guarda tokens y redirige a `/library`.
  - [x] Error `409` muestra "Este email ya está registrado.".

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-D2 · Implementar autenticación

**Archivos a crear:**

```
frontend/src/
  components/layout/
    ProtectedRoute.tsx    ← usa useAuthStore(s => s.isAuthenticated)
  pages/
    LoginPage.tsx         ← react-hook-form + zodResolver(loginSchema)
    RegisterPage.tsx      ← react-hook-form + zodResolver(registerSchema)
    NotFoundPage.tsx      ← mensaje "404 — Página no encontrada" + link a /library
```

**`LoginPage`:**
- Usa `useForm` con `zodResolver(loginSchema)`.
- Al enviar: `authApi.login(data)`, si éxito llama a `useAuthStore.getState().setSession(...)` y navega a `/library`.
- Si el error es `axios.isAxiosError(e) && e.response?.status === 401`: `setError('root', { message: 'Credenciales inválidas.' })`.
- Muestra `errors.root?.message` bajo el botón de submit.

**`RegisterPage`:**
- Misma estructura. Envía `{ email, password, displayName }` (sin `confirmPassword`).
- Error `409` → `setError('root', { message: 'Este email ya está registrado.' })`.

- **Completado cuando:** tests de TASK-06-D1 pasan (verde).

---

## Bloque E — AppShell: Layout, Sidebar y Header

### TASK-06-E1 · Test: AppShell y Sidebar

- **Archivo test:** `frontend/src/__tests__/pages/AppShell.test.tsx`

- **Casos:**
  - [x] El sidebar muestra el nombre del usuario (`displayName`) desde el store.
  - [x] Los links de navegación "Biblioteca", "Cola" y "Estadísticas" apuntan a `/library`, `/queue` y `/stats`.
  - [x] El link activo tiene la clase/atributo que lo distingue visualmente (aria-current o clase activa de React Router).
  - [x] El botón "Cerrar sesión" llama a `authApi.logout(refreshToken)`, luego `useAuthStore.getState().logout()` y redirige a `/login` (CA-19).
  - [x] En viewport móvil (mock de `window.innerWidth`), el sidebar está oculto por defecto y se abre al hacer click en el botón hamburguesa.

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-E2 · Implementar AppShell, Sidebar y Header

**Archivos a crear:**

```
frontend/src/components/layout/
  AppShell.tsx    ← layout raíz con <Sidebar> + <Header> + <Outlet>
  Sidebar.tsx     ← navegación + displayName + botón logout
  Header.tsx      ← header móvil con botón hamburguesa (usa useUIStore)
```

**`Sidebar`:**
- Links con `<NavLink>` de React Router para obtener `isActive`.
- Logout: llama a `authApi.logout(refreshToken)` (fire-and-forget si falla), luego `logout()` del store y `navigate('/login')`.
- En desktop: fijo, 240px. En móvil: `<Sheet>` de shadcn (drawer lateral).

**`AppShell`:**
- En desktop: `flex` con sidebar fijo y contenido a la derecha.
- Renderiza `<Outlet />` en el área de contenido.

- **Completado cuando:** tests de TASK-06-E1 pasan (verde) y la navegación funciona en el navegador.

---

## Bloque F — LibraryPage: Biblioteca Completa

### TASK-06-F1 · Test: LibraryPage y componentes de biblioteca

- **Archivos test:**
  - `frontend/src/__tests__/pages/LibraryPage.test.tsx`
  - `frontend/src/__tests__/components/BookCard.test.tsx`
  - `frontend/src/__tests__/components/BookForm.test.tsx`

**Casos `BookCard`:**
  - [ ] Muestra título, autor, género (badge), país y estrellas de prioridad.
  - [ ] Si `isRead=true`: muestra badge "✓ Leído" y la fecha formateada.
  - [ ] El menú ⋯ contiene las opciones "Editar", "Marcar como leído" (o "Marcar como no leído") y "Eliminar".
  - [ ] Click en "Eliminar" llama a `onDelete` con el `book.id`.

**Casos `BookForm`:**
  - [ ] El formulario tiene inputs para todos los campos obligatorios.
  - [ ] Enviar el formulario vacío muestra los errores de `createBookSchema`.
  - [ ] En modo creación, el submit llama a `onSubmit` con los valores del formulario.
  - [ ] Los selects de género, energía mental, ánimo y categoría de rotación muestran las opciones del mock de referencia.

**Casos `LibraryPage`:**
  - [ ] Muestra `LibrarySkeleton` mientras carga (CA-06 parcial).
  - [ ] Una vez cargada, muestra un `BookCard` por libro del mock (CA-06).
  - [ ] Filtrar por género llama a `useBooks` con `{ genre: 'Clasico' }` en la query key (CA-07).
  - [ ] "Limpiar filtros" resetea todos los filtros a `undefined` y recarga la lista completa (CA-08).
  - [ ] Botón "Agregar libro" abre el modal de creación (`openCreateModal` del store).
  - [ ] Guardar el formulario de creación llama a `useCreateBook.mutate` (CA-09).
  - [ ] Tras éxito de creación, se invalida `['books']` y el modal se cierra (CA-10).
  - [ ] Confirmar eliminación en `AlertDialog` llama a `useDeleteBook.mutate(id)`.
  - [ ] El modal de "Marcar como leído" invalida `['books']`, `['queue']` y `['stats']` (CA-11).
  - [ ] Estado vacío (mock retorna `[]`) muestra ilustración y botón "Agregar libro".

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-F2 · Implementar LibraryPage y componentes de biblioteca

**Archivos a crear:**

```
frontend/src/
  components/library/
    BookCard.tsx          ← props: book, onEdit, onMarkRead, onMarkUnread, onDelete
    BookFilters.tsx       ← panel de filtros (inputs controlados por estado local)
    BookForm.tsx          ← react-hook-form + zodResolver(createBookSchema)
    BookGrid.tsx          ← grid responsive de BookCard
    MarkAsReadForm.tsx    ← campos readAt + notes + botón confirmar
    StarRating.tsx        ← 5 estrellas ★/☆, readonly o interactivo
  pages/
    LibraryPage.tsx       ← orquesta filtros, grid, modales y diálogo de confirmación
```

**`LibraryPage`:**
- Estado local de filtros (`BookFilters`) que se pasa a `useBooks(filters)`.
- Usa `useUIStore` para abrir/cerrar modales.
- El `Dialog` de crear/editar renderiza `BookForm` con `defaultValues` en modo edición.
- El `AlertDialog` para eliminar renderiza texto de confirmación antes de llamar a `useDeleteBook.mutate`.
- El `Dialog` de marcar como leído renderiza `MarkAsReadForm`.
- Maneja `isLoading` con `<LibrarySkeleton />` y `isError` con `<ErrorMessage onRetry={refetch} />`.

**`BookCard`:**
- Badge de género usa `getGenreColor(book.genre)` para la clase Tailwind.
- Menú ⋯ usa `DropdownMenu` de shadcn/ui.

**`StarRating`:**
- 5 `span` con `★` (llena) o `☆` (vacía) según `value`.
- Si `onChange` está definido: hover resalta hasta el cursor y click llama a `onChange(i)`.

- **Completado cuando:** tests de TASK-06-F1 pasan (verde) y la biblioteca es usable en el navegador.

---

## Bloque G — QueuePage: Cola Inteligente

### TASK-06-G1 · Test: QueuePage y componentes de cola

- **Archivos test:**
  - `frontend/src/__tests__/pages/QueuePage.test.tsx`
  - `frontend/src/__tests__/components/SuggestionBadge.test.tsx`
  - `frontend/src/__tests__/components/QueueDndList.test.tsx`

**Casos `SuggestionBadge`:**
  - [x] Si `source='AI'` y `reasoning` no es null: muestra ícono ✨ y el texto del razonamiento (CA-15).
  - [x] Si `source='AI'` y `reasoning` es null: no renderiza nada (o muestra solo el ícono).
  - [x] Si `source='Filter'`: muestra "Generado por algoritmo" con ícono ⚙️.
  - [x] El componente es colapsable — click en el trigger alterna la visibilidad del texto.

**Casos `QueueDndList`:**
  - [x] Renderiza un ítem por cada `QueueItem` del array.
  - [x] Cada ítem muestra posición, título, autor y badge de género.
  - [x] Si el ítem tiene `aiReasoning`, renderiza `SuggestionBadge` con `source='AI'`.
  - [x] El botón × por ítem llama a `onRemove(bookId)`.

**Casos `QueuePage`:**
  - [x] Muestra `QueueSkeleton` mientras carga.
  - [x] Estado vacío muestra mensaje + botón "Generar cola".
  - [x] Botón "Generar cola" llama a `useGenerateQueue.mutate()` (CA-12).
  - [x] Durante la mutación de generación, el botón muestra spinner y texto "Claude está analizando tu biblioteca…" (CA-12).
  - [x] Si `aiContributed: true` en la respuesta: badge "✨ Generada con IA" visible (CA-13).
  - [x] Si `aiContributed: false`: texto "Generada con algoritmo" visible (CA-14).
  - [x] Las listas especiales (Próximos 5, Cuando cansado, Deuda histórica) se renderizan desde `useSpecialLists`.

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-G2 · Implementar QueuePage y componentes de cola

**Archivos a crear:**

```
frontend/src/
  components/queue/
    QueueDndList.tsx      ← DndContext + SortableContext + useSortable por ítem
    QueueItemCard.tsx     ← ítem individual con posición, libro, badge, SuggestionBadge y botón ×
    SuggestionBadge.tsx   ← Collapsible de shadcn con razonamiento de Claude
    SpecialListsPanel.tsx ← tres tarjetas expandibles con datos de useSpecialLists
  pages/
    QueuePage.tsx         ← orquesta generación, lista DnD y listas especiales
```

**`QueueDndList`:**
- `DndContext` con `onDragEnd` que reordena el array local y llama a `onReorder(newOrder)`.
- `SortableContext` con `items={items.map(i => i.book.id)}`.
- Cada `QueueItemCard` usa `useSortable({ id: item.book.id })`.
- CSS `transform` y `transition` para animación de arrastre.
- Tras soltar, `useReorderQueue.mutate` transforma el array a `{ bookId, position }[]`.

**`QueuePage`:**
- Estado local `aiContributed: boolean | null` actualizado en `onSuccess` de `useGenerateQueue`.
- Muestra el badge correspondiente según `aiContributed`.
- Maneja `isLoading` con `<QueueSkeleton />`.

- **Completado cuando:** tests de TASK-06-G1 pasan (verde) y el drag & drop funciona en el navegador.

---

## Bloque H — StatsPage: Tablero de Estadísticas

### TASK-06-H1 · Test: StatsPage y gráficos

- **Archivos test:**
  - `frontend/src/__tests__/pages/StatsPage.test.tsx`
  - `frontend/src/__tests__/components/GenreBarChart.test.tsx`

**Casos `GenreBarChart`:**
  - [x] Renderiza sin lanzar excepciones con un array de `GenreStat` como prop (CA-18).
  - [x] Renderiza sin lanzar excepciones con un array vacío.
  - [x] El componente es accesible (tiene un contenedor con rol o aria-label adecuado).

**Casos `StatsPage`:**
  - [x] Muestra skeletons mientras carga.
  - [x] Las cards de resumen muestran `totalBooks`, `readBooks`, `unreadBooks` y `readPercentage` del mock (CA-17).
  - [x] La barra de progreso refleja el `readPercentage`.
  - [x] Renderiza `GenreBarChart` con los datos de `byGenre`.
  - [x] El "Top 10 países" lista los países del mock de `byCountry`.
  - [x] "Últimas 5 lecturas" lista los libros de `recentlyRead` con título y fecha.

- **Completado cuando:** tests compilan y fallan (rojo).

---

### TASK-06-H2 · Implementar StatsPage y componentes de estadísticas

**Archivos a crear:**

```
frontend/src/
  components/stats/
    DashboardSummaryCards.tsx  ← 4 cards + barra de progreso (Progress de shadcn)
    GenreBarChart.tsx          ← BarChart de recharts, barras apiladas read/unread
    MentalEnergyChart.tsx      ← barras horizontales con emoji por nivel
    CountryList.tsx            ← lista de los top países con su conteo
    RecentlyReadList.tsx       ← últimas lecturas con título, autor y fecha
  pages/
    StatsPage.tsx              ← orquesta todos los componentes con useDashboard
```

**`GenreBarChart`:**
- `<ResponsiveContainer width="100%" height={300}>` envolviendo `<BarChart>`.
- Dos `<Bar>` apiladas: `dataKey="read"` (verde) y `dataKey="unread"` (gris).
- `<XAxis dataKey="genre" />` con `angle={-45}` si `data.length > 4`.
- `<Tooltip />` y `<Legend />`.

**`StatsPage`:**
- Usa `useDashboard()` para todos los datos del tablero.
- Maneja `isLoading` con cards de skeleton y `isError` con `ErrorMessage`.

- **Completado cuando:** tests de TASK-06-H1 pasan (verde) y el tablero es visualmente correcto en el navegador.

---

## Bloque I — Verificación Final

### TASK-06-I1 · TypeScript sin errores ni `any`

```bash
npm run build          # tsc --noEmit + vite build
```

- **Criterio:** `0 errors` de TypeScript · Sin ningún `any` explícito en el código propio (CA-22).
- Verificar que no hay `useEffect` usado para fetching — todo el fetching va en `useQuery`/`useMutation` (CA-23).
- `src/components/ui/` no tiene modificaciones (`git diff --name-only` no muestra esos archivos en cambios) (CA-24).

---

### TASK-06-I2 · Todos los tests Vitest pasan

```bash
npm test -- --run
```

- **Criterio:** todos los tests de Bloques B–H pasan · Cobertura de criterios CA-01 a CA-21 verificada.

---

### TASK-06-I3 · Verificación manual en navegador

Ejecutar `npm run dev` + backend en `http://localhost:5000` y verificar:

- [ ] Registro de usuario nuevo funciona (CA-25).
- [ ] Login y logout funcionan, incluyendo redirecciones.
- [ ] Crear, editar, marcar como leído y eliminar un libro.
- [ ] Generar la cola y ver el badge de IA / algoritmo.
- [ ] Drag & drop reordena la cola.
- [ ] El tablero de estadísticas muestra datos reales.
- [ ] El gráfico de géneros renderiza correctamente.

- **Criterio:** el MVP es funcional de punta a punta sin errores en la consola del navegador.

---

## Resumen de archivos que genera SPEC-06

| # | Archivo | Bloque |
|---|---|---|
| 1  | `frontend/src/types/index.ts` | A |
| 2  | `frontend/src/lib/schemas/auth.schemas.ts` | A |
| 3  | `frontend/src/lib/schemas/book.schemas.ts` | A |
| 4  | `frontend/src/lib/genreColors.ts` | A |
| 5  | `frontend/src/lib/queryClient.ts` | A |
| 6  | `frontend/src/lib/axios.ts` | B |
| 7  | `frontend/src/api/authApi.ts` | B |
| 8  | `frontend/src/api/booksApi.ts` | B |
| 9  | `frontend/src/api/queueApi.ts` | B |
| 10 | `frontend/src/api/statsApi.ts` | B |
| 11 | `frontend/src/stores/useAuthStore.ts` | B |
| 12 | `frontend/src/stores/useUIStore.ts` | B |
| 13 | `frontend/src/hooks/useBooks.ts` | C |
| 14 | `frontend/src/hooks/useQueue.ts` | C |
| 15 | `frontend/src/hooks/useStats.ts` | C |
| 16 | `frontend/src/hooks/useReferenceData.ts` | C |
| 17 | `frontend/src/components/layout/ProtectedRoute.tsx` | D |
| 18 | `frontend/src/pages/LoginPage.tsx` | D |
| 19 | `frontend/src/pages/RegisterPage.tsx` | D |
| 20 | `frontend/src/pages/NotFoundPage.tsx` | D |
| 21 | `frontend/src/components/layout/AppShell.tsx` | E |
| 22 | `frontend/src/components/layout/Sidebar.tsx` | E |
| 23 | `frontend/src/components/layout/Header.tsx` | E |
| 24 | `frontend/src/components/library/BookCard.tsx` | F |
| 25 | `frontend/src/components/library/BookFilters.tsx` | F |
| 26 | `frontend/src/components/library/BookForm.tsx` | F |
| 27 | `frontend/src/components/library/BookGrid.tsx` | F |
| 28 | `frontend/src/components/library/MarkAsReadForm.tsx` | F |
| 29 | `frontend/src/components/library/StarRating.tsx` | F |
| 30 | `frontend/src/pages/LibraryPage.tsx` | F |
| 31 | `frontend/src/components/queue/QueueDndList.tsx` | G |
| 32 | `frontend/src/components/queue/QueueItemCard.tsx` | G |
| 33 | `frontend/src/components/queue/SuggestionBadge.tsx` | G |
| 34 | `frontend/src/components/queue/SpecialListsPanel.tsx` | G |
| 35 | `frontend/src/pages/QueuePage.tsx` | G |
| 36 | `frontend/src/components/stats/DashboardSummaryCards.tsx` | H |
| 37 | `frontend/src/components/stats/GenreBarChart.tsx` | H |
| 38 | `frontend/src/components/stats/MentalEnergyChart.tsx` | H |
| 39 | `frontend/src/components/stats/CountryList.tsx` | H |
| 40 | `frontend/src/components/stats/RecentlyReadList.tsx` | H |
| 41 | `frontend/src/pages/StatsPage.tsx` | H |
| 42 | `frontend/src/main.tsx` (modificado) | A |
| 43 | `frontend/src/App.tsx` (reemplazado) | A |
| 44 | `frontend/vite.config.ts` (modificado) | A |
| 45 | `frontend/src/__tests__/setup.ts` | A |
| 46 | `frontend/src/__tests__/stores/useAuthStore.test.ts` | B |
| 47 | `frontend/src/__tests__/stores/axiosInterceptors.test.ts` | B |
| 48 | `frontend/src/__tests__/hooks/useBooks.test.ts` | C |
| 49 | `frontend/src/__tests__/hooks/useQueue.test.ts` | C |
| 50 | `frontend/src/__tests__/pages/LoginPage.test.tsx` | D |
| 51 | `frontend/src/__tests__/components/ProtectedRoute.test.tsx` | D |
| 52 | `frontend/src/__tests__/pages/AppShell.test.tsx` | E |
| 53 | `frontend/src/__tests__/pages/LibraryPage.test.tsx` | F |
| 54 | `frontend/src/__tests__/components/BookCard.test.tsx` | F |
| 55 | `frontend/src/__tests__/components/BookForm.test.tsx` | F |
| 56 | `frontend/src/__tests__/pages/QueuePage.test.tsx` | G |
| 57 | `frontend/src/__tests__/components/SuggestionBadge.test.tsx` | G |
| 58 | `frontend/src/__tests__/components/QueueDndList.test.tsx` | G |
| 59 | `frontend/src/__tests__/pages/StatsPage.test.tsx` | H |
| 60 | `frontend/src/__tests__/components/GenreBarChart.test.tsx` | H |

---

## Checklist SPEC-06

### Bloque A — Fundamentos
- [x] TASK-06-A1 · Instalar dependencias y configurar herramientas
- [x] TASK-06-A2 · Tipos TypeScript centrales (`src/types/index.ts`)
- [x] TASK-06-A3 · Schemas Zod + `genreColors.ts`
- [x] TASK-06-A4 · `queryClient.ts` + `main.tsx` + `App.tsx` con router y providers

### Bloque B — Capa de Datos
- [x] TASK-06-B1 · Tests `useAuthStore` (rojo)
- [x] TASK-06-B2 · Impl `axios.ts` + clientes API + `useAuthStore` + `useUIStore` (verde)
- [x] TASK-06-B3 · Tests interceptores Axios (rojo → verde en B2)

### Bloque C — Hooks React Query
- [x] TASK-06-C1 · Tests `useBooks` + `useQueue` (rojo)
- [x] TASK-06-C2 · Impl `useBooks`, `useQueue`, `useStats`, `useReferenceData` (verde)

### Bloque D — Autenticación
- [x] TASK-06-D1 · Tests `LoginPage` + `RegisterPage` + `ProtectedRoute` (rojo)
- [x] TASK-06-D2 · Impl `ProtectedRoute`, `LoginPage`, `RegisterPage`, `NotFoundPage` (verde)

### Bloque E — AppShell
- [x] TASK-06-E1 · Tests `AppShell` + `Sidebar` (rojo)
- [x] TASK-06-E2 · Impl `AppShell`, `Sidebar`, `Header` (verde)

### Bloque F — LibraryPage
- [x] TASK-06-F1 · Tests `LibraryPage` + `BookCard` + `BookForm` (rojo)
- [x] TASK-06-F2 · Impl `BookCard`, `StarRating`, `BookFilters`, `BookForm`, `BookGrid`, `MarkAsReadForm`, `LibraryPage` (verde)

### Bloque G — QueuePage
- [x] TASK-06-G1 · Tests `QueuePage` + `SuggestionBadge` + `QueueDndList` (rojo)
- [x] TASK-06-G2 · Impl `SuggestionBadge`, `QueueItemCard`, `QueueDndList`, `SpecialListsPanel`, `QueuePage` (verde)

### Bloque H — StatsPage
- [x] TASK-06-H1 · Tests `StatsPage` + `GenreBarChart` (rojo)
- [x] TASK-06-H2 · Impl `DashboardSummaryCards`, `GenreBarChart`, `MentalEnergyChart`, `CountryList`, `RecentlyReadList`, `StatsPage` (verde)

### Bloque I — Verificación Final ✓
- [x] TASK-06-I1 · `npm run build` → 0 errores TS · sin `any` · sin `useEffect` para fetching · sin ediciones en `ui/`
- [x] TASK-06-I2 · `npm test -- --run` → todos los tests pasan (CA-01 a CA-21)
- [ ] TASK-06-I3 · Verificación manual en navegador — MVP funcional de punta a punta (CA-25)
