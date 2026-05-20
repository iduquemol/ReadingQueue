# constitution.md
# Cola Inteligente de Lectura — Principios No Negociables

## 1. Identidad del Proyecto

Esta es una aplicación web full-stack que permite a usuarios con múltiples
libros en su biblioteca personal organizar su cola de lectura de forma
inteligente. El usuario puede registrar su biblioteca, marcar libros como
leídos, aplicar filtros personalizados (género, energía mental, ánimo,
país, prioridad) y recibir sugerencias automáticas generadas por Claude
(Anthropic) que aprende de los hábitos de lectura del usuario.

El sistema soporta múltiples usuarios con autenticación. Cada usuario tiene
su propia biblioteca, cola y estadísticas completamente aisladas.

---

## 2. Stack Tecnológico Obligatorio

### Backend

| Capa | Tecnología | Versión mínima |
|---|---|---|
| Runtime | .NET | 9.0 |
| Framework web | ASP.NET Core Minimal API | 9.0 |
| ORM / Data Access | Dapper | 2.x |
| Base de datos | SQL Server | 2022 |
| Migraciones | DbUp | Latest |
| Autenticación | ASP.NET Core Identity + JWT Bearer | Nativo |
| LLM Client | Anthropic SDK for .NET (`Anthropic.SDK`) | Latest |
| Cache | IMemoryCache (in-process) | Nativo .NET 9 |
| Testing | xUnit + Moq + FluentAssertions | Latest |
| Contenedores | Docker + Docker Compose | Latest |

### Frontend

| Capa | Tecnología | Versión mínima |
|---|---|---|
| Framework | React | 18.x |
| Lenguaje | TypeScript | 5.x |
| Bundler | Vite | 5.x |
| Estilos | Tailwind CSS | 3.x |
| Componentes UI | shadcn/ui | Latest |
| Cliente HTTP | Axios | Latest |
| Estado global | Zustand | Latest |
| Formularios | React Hook Form + Zod | Latest |
| Testing | Vitest + React Testing Library | Latest |

Desviaciones de este stack requieren justificación documentada en el spec
correspondiente.

---

## 3. Modelo de Datos — Reglas No Negociables

### 3.1 Campos Canónicos de un Libro

Las columnas del Excel `cola_inteligente_lectura_v2.xlsx` son la fuente de
verdad del modelo. La tabla `Books` DEBE reflejar exactamente estos campos:

| Campo en Excel | Columna SQL | Tipo SQL | Nullable |
|---|---|---|---|
| # | `Id` | `INT IDENTITY PK` | No |
| Título | `Title` | `NVARCHAR(500)` | No |
| Autor | `Author` | `NVARCHAR(300)` | No |
| Género | `Genre` | `NVARCHAR(100)` | No |
| País | `Country` | `NVARCHAR(100)` | No |
| Por qué leerlo | `WhyRead` | `NVARCHAR(1000)` | Sí |
| Prioridad (1-5) | `Priority` | `TINYINT` | No (default 3) |
| Energía mental | `MentalEnergy` | `NVARCHAR(100)` | No |
| Ánimo recomendado | `RecommendedMood` | `NVARCHAR(200)` | No |
| Rotación | `RotationCategory` | `NVARCHAR(100)` | No |
| Leído | `IsRead` | `BIT` | No (default 0) |
| — | `ReadAt` | `DATETIME2` | Sí |
| Notas | `Notes` | `NVARCHAR(2000)` | Sí |

### 3.2 Valores Canónicos de Enumeraciones

Estos valores son fijos en base de datos. Ningún código debe asumir strings
distintos a los definidos aquí.

**Géneros** (tabla `Genres`):
- `No ficción / ensayo`
- `Clásico`
- `Novela contemporánea`
- `Novela latinoamericana`
- `Cuentos`
- `Novela clásica`
- `Poesía`

**Energía mental** (tabla `MentalEnergyLevels`):
- `🟢 Baja – cualquier momento`
- `🔵 Media – tarde tranquila`
- `🟡 Media-alta – fin de semana`
- `🟠 Alta – concentración`
- `🔴 Máxima – modo lector`

**Ánimo recomendado** (tabla `Moods`):
- `Analítico / quiero aprender algo`
- `Solemne / quiero leer algo grande`
- `Curioso / quiero algo fresco`
- `Identidad / quiero leer en español`
- `Cansado / quiero entrar y salir`
- `Contemplativo / quiero algo que dure`
- `Sensible / quiero pocos palabras`

**Categoría de rotación** (tabla `RotationCategories`):
- `Ensayo / no ficción`
- `Libro corto o cuentos`
- `Clásico`
- `Novela grande`
- `Contemporáneo latinoamericano o raro`

Estas tablas de referencia son de solo lectura desde la API. Su modificación
requiere una migración DbUp nueva.

### 3.3 Esquema Completo de Tablas

```sql
-- 001_initial_schema.sql

CREATE TABLE Users (
    Id          INT IDENTITY PRIMARY KEY,
    Email       NVARCHAR(256) NOT NULL UNIQUE,
    PasswordHash NVARCHAR(512) NOT NULL,
    DisplayName NVARCHAR(200) NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsActive    BIT NOT NULL DEFAULT 1
);

CREATE TABLE Books (
    Id                 INT IDENTITY PRIMARY KEY,
    UserId             INT NOT NULL REFERENCES Users(Id),
    Title              NVARCHAR(500) NOT NULL,
    Author             NVARCHAR(300) NOT NULL,
    Genre              NVARCHAR(100) NOT NULL,
    Country            NVARCHAR(100) NOT NULL,
    WhyRead            NVARCHAR(1000) NULL,
    Priority           TINYINT NOT NULL DEFAULT 3
                           CHECK (Priority BETWEEN 1 AND 5),
    MentalEnergy       NVARCHAR(100) NOT NULL,
    RecommendedMood    NVARCHAR(200) NOT NULL,
    RotationCategory   NVARCHAR(100) NOT NULL,
    IsRead             BIT NOT NULL DEFAULT 0,
    ReadAt             DATETIME2 NULL,
    Notes              NVARCHAR(2000) NULL,
    CreatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt          DATETIME2 NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_Books_UserId ON Books(UserId);
CREATE INDEX IX_Books_UserId_Genre ON Books(UserId, Genre);
CREATE INDEX IX_Books_UserId_IsRead ON Books(UserId, IsRead);

CREATE TABLE ReadingQueue (
    Id         INT IDENTITY PRIMARY KEY,
    UserId     INT NOT NULL REFERENCES Users(Id),
    BookId     INT NOT NULL REFERENCES Books(Id),
    Position   INT NOT NULL,
    AddedAt    DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    Source     NVARCHAR(50) NOT NULL DEFAULT 'Manual',
                -- Valores: 'Manual', 'AI', 'Filter'
    CONSTRAINT UQ_Queue_UserBook UNIQUE (UserId, BookId)
);
CREATE INDEX IX_Queue_UserId ON ReadingQueue(UserId, Position);

CREATE TABLE AISuggestions (
    Id           INT IDENTITY PRIMARY KEY,
    UserId       INT NOT NULL REFERENCES Users(Id),
    BookId       INT NOT NULL REFERENCES Books(Id),
    Reasoning    NVARCHAR(2000) NOT NULL,
    Score        DECIMAL(5,2) NOT NULL,
    GeneratedAt  DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    WasAccepted  BIT NULL
);

CREATE TABLE RefreshTokens (
    Id          INT IDENTITY PRIMARY KEY,
    UserId      INT NOT NULL REFERENCES Users(Id),
    Token       NVARCHAR(512) NOT NULL UNIQUE,
    ExpiresAt   DATETIME2 NOT NULL,
    CreatedAt   DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    IsRevoked   BIT NOT NULL DEFAULT 0
);
```

---

## 4. Principios de Arquitectura

### 4.1 Separación Estricta de Responsabilidades
- El dominio (entidades, interfaces) no tiene dependencias de frameworks.
- La lógica de negocio (ordenamiento de cola, filtros, evaluación de
  sugerencias) vive en la capa Application, no en los endpoints.
- Los endpoints son thin: reciben, delegan al use case, devuelven resultado.

### 4.2 Inversión de Dependencias
- Toda dependencia externa (DB, Claude, cache) se accede vía interfaz.
- Las implementaciones concretas se registran en el contenedor de DI.
- Nunca instanciar servicios con `new` fuera del contexto de DI o tests.

### 4.3 Aislamiento por Usuario
- Todo query a `Books`, `ReadingQueue` y `AISuggestions` filtra por
  `UserId` extraído del JWT — nunca del request body.
- El `UserId` se extrae del claim en un método de extensión
  `HttpContext.GetUserId()` centralizado.
- Nunca confiar en un `userId` que venga del cliente en el body.

### 4.4 Inmutabilidad del Dominio
- Las entidades de dominio son inmutables después de crearse.
- Las actualizaciones se expresan como nuevos comandos que crean nuevas
  instancias — no mutación in-place de objetos de dominio.

---

## 5. Acceso a Datos con Dapper — Reglas No Negociables

1. **Dapper es el único mecanismo de acceso a datos.** No se permite Entity
   Framework Core, NHibernate ni ningún ORM con migrations automáticas.

2. **Todo SQL es explícito y versionado.** Cada query vive como constante
   privada en su repositorio o en un archivo `.sql` dentro de
   `src/ReadingQueue.Infrastructure/Sql/`. Nunca SQL inline en use cases.

3. **Migraciones con DbUp.** Scripts `.sql` numerados secuencialmente en
   `src/ReadingQueue.Infrastructure/Migrations/`. Nunca modificar un script
   ya ejecutado — siempre crear uno nuevo.

4. **Parámetros nombrados siempre.** Todo query usa parámetros nombrados
   (`@Param`) — cero interpolación de strings en SQL.

5. **Transacciones explícitas.** Operaciones que afectan múltiples tablas
   (ej: marcar leído + actualizar cola + guardar sugerencia) usan
   `IDbTransaction` pasado explícitamente a Dapper.

6. **`IDbConnection` nunca se inyecta directamente.** Se inyecta
   `IDbConnectionFactory` que crea y cierra conexiones correctamente.

```csharp
// Patrón obligatorio de repositorio
public class SqlBookRepository : IBookRepository
{
    private readonly IDbConnectionFactory _factory;

    public async Task<IEnumerable<Book>> GetByUserAsync(
        int userId, BookFilter filter, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(
            BookQueries.GetByUserFiltered,
            new { UserId = userId, Genre = filter.Genre, IsRead = filter.IsRead }
        );
    }
}
```

---

## 6. Autenticación y Autorización — Reglas No Negociables

1. **JWT Bearer es el único mecanismo de autenticación.** Se usa
   `Microsoft.AspNetCore.Authentication.JwtBearer`. No se admiten cookies
   de sesión ni API keys fijas.

2. **Refresh tokens en base de datos.** La tabla `RefreshTokens` almacena
   tokens de renovación. El access token tiene TTL de 15 minutos. El
   refresh token tiene TTL de 7 días.

3. **Passwords con BCrypt.** Nunca SHA o MD5. La librería obligatoria es
   `BCrypt.Net-Next`.

4. **Configuración JWT en `appsettings.json` — nunca en código:**

```json
{
  "Jwt": {
    "SecretKey": "{{from-env-var-never-in-repo}}",
    "Issuer": "readingqueue-api",
    "Audience": "readingqueue-client",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

5. **El `SecretKey` NUNCA se commitea al repositorio.** Vive en variable
   de entorno `JWT__SecretKey` en local y en Azure App Service / Key Vault
   en producción.

6. **Todos los endpoints excepto `/api/auth/login` y `/api/auth/register`
   requieren autenticación.** Se aplica globalmente con
   `.RequireAuthorization()` en el grupo raíz.

---

## 7. Integración con Claude (Anthropic) — Reglas No Negociables

1. **El cliente Claude se comunica exclusivamente vía el SDK oficial
   `Anthropic.SDK` para .NET.** La API key se configura desde variable
   de entorno — nunca hardcodeada.

2. **Interfaz obligatoria.** Todo acceso a Claude pasa por `ILLMClient`.
   La implementación concreta es `ClaudeClient` en Infrastructure.

3. **El prompt del sistema está hardcodeado como constante privada** en
   la clase `SuggestionPromptBuilder` dentro de
   `src/ReadingQueue.Infrastructure/LLM/`. Es una constante bien nombrada
   — no está dispersa en el código. No se necesita base de datos para
   este prompt porque el comportamiento del LLM aquí es fijo y simple:
   dado un historial y una lista, devolver JSON de sugerencias. Si el
   prompt necesitara cambiar, un redespliegue es suficiente.

```csharp
// src/ReadingQueue.Infrastructure/LLM/SuggestionPromptBuilder.cs
internal static class SuggestionPromptBuilder
{
    private const string SystemPrompt = """
        Eres un asistente experto en recomendación de libros.
        Tu única función es analizar el historial de lectura de un usuario
        y sugerir libros de una lista de pendientes, priorizando variedad
        de géneros y afinidad con los libros leídos recientemente.
        Responde ÚNICAMENTE con JSON válido, sin texto adicional,
        sin bloques de código markdown, sin explicaciones fuera del JSON.
        """;

    public static string Build(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks) { ... }
}
```

4. **Configuración del modelo en `appsettings.json`:**

```json
{
  "Claude": {
    "ApiKey": "{{from-env-var-never-in-repo}}",
    "Model": "claude-sonnet-4-5",
    "MaxTokens": 1024,
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

5. **El `ApiKey` NUNCA se commitea.** Vive en variable de entorno
   `Claude__ApiKey` en local y en Azure Key Vault en producción.

6. **Contrato de respuesta del LLM.** El use case `GenerateQueueSuggestions`
   envía a Claude el historial de libros leídos y la lista de pendientes.
   Claude responde ÚNICAMENTE con este JSON — el prompt lo exige
   explícitamente:

```json
{
  "suggestions": [
    {
      "bookId": 42,
      "score": 9.2,
      "reasoning": "Razón en 1-2 oraciones"
    }
  ]
}
```

   Cualquier respuesta que no sea JSON parseable se descarta, se loggea
   el error y se activa el algoritmo de fallback — nunca se expone raw
   al cliente.

7. **Resiliencia con Polly.** El `ClaudeClient` implementa retry con
   backoff exponencial y circuit breaker. Si la API de Anthropic no está
   disponible, la cola se genera con el algoritmo determinístico de
   fallback del backend — nunca lanza excepción no controlada al usuario.

```csharp
public interface ILLMClient
{
    Task<IEnumerable<BookSuggestion>> GenerateSuggestionsAsync(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks,
        CancellationToken ct = default);
}
```

---

## 8. Lógica de Cola Inteligente — Reglas No Negociables

### 8.1 Algoritmo de Generación de Cola

La cola inteligente se genera siguiendo esta prioridad de factores,
en este orden exacto. Ninguna implementación puede alterar el orden:

1. **Prioridad del libro** (campo `Priority` 1-5): peso 40%
2. **Variedad de `RotationCategory`**: se evita repetir la misma categoría
   en posiciones consecutivas. Peso 30%.
3. **Sugerencia del LLM** (campo `Score` de `AISuggestions`): peso 20%.
4. **Antigüedad en la biblioteca** (campo `CreatedAt`): peso 10%.

### 8.2 Algoritmo de Fallback (sin LLM)

Cuando la API de Anthropic no está disponible, la cola se genera con este algoritmo
determinístico:
- Ordenar por `Priority DESC`, luego por `RotationCategory` (round-robin
  entre las 5 categorías), luego por `CreatedAt ASC`.
- Máximo 20 libros en la cola activa.

### 8.3 Filtros Disponibles

El endpoint de consulta de biblioteca acepta todos estos filtros de forma
independiente y combinable:

| Filtro | Parámetro Query | Tipo |
|---|---|---|
| Género | `genre` | string |
| País | `country` | string |
| Energía mental | `mentalEnergy` | string |
| Ánimo | `mood` | string |
| Categoría rotación | `rotation` | string |
| Prioridad mínima | `minPriority` | int (1-5) |
| Solo leídos | `isRead` | bool |
| Solo no leídos | `isUnread` | bool |
| Búsqueda libre | `q` | string (título o autor) |

### 8.4 Listas Especiales

Las listas especiales del Excel se generan dinámicamente con queries
específicas — no se almacenan como listas precalculadas:

- **⭐ Próximos 5**: Top 5 no leídos por prioridad + variedad de rotación.
- **😴 Cuando estoy cansado**: No leídos con `MentalEnergy` =
  `'🟢 Baja – cualquier momento'`, ordenados por prioridad.
- **🏛️ Deuda histórica**: No leídos con `Genre` = `'Clásico'`, por prioridad.

---

## 9. Minimal API — Convenciones Obligatorias

1. **Sin Controllers.** Solo Minimal API de ASP.NET Core. Ningún archivo
   `*Controller.cs` es aceptable.

2. **Endpoints agrupados por feature** usando `RouteGroupBuilder`:

```csharp
var auth  = app.MapGroup("/api/auth");
var books = app.MapGroup("/api/books").RequireAuthorization();
var queue = app.MapGroup("/api/queue").RequireAuthorization();
var stats = app.MapGroup("/api/stats").RequireAuthorization();

auth.MapPost("/register",      AuthEndpoints.Register);
auth.MapPost("/login",         AuthEndpoints.Login);
auth.MapPost("/refresh",       AuthEndpoints.Refresh);
auth.MapPost("/logout",        AuthEndpoints.Logout);

books.MapGet("/",              BookEndpoints.GetAll);       // con filtros
books.MapGet("/{id}",          BookEndpoints.GetById);
books.MapPost("/",             BookEndpoints.Create);
books.MapPut("/{id}",          BookEndpoints.Update);
books.MapDelete("/{id}",       BookEndpoints.Delete);
books.MapPost("/{id}/read",    BookEndpoints.MarkAsRead);
books.MapPost("/{id}/unread",  BookEndpoints.MarkAsUnread);

queue.MapGet("/",              QueueEndpoints.GetQueue);
queue.MapPost("/generate",     QueueEndpoints.GenerateAI);
queue.MapPut("/reorder",       QueueEndpoints.Reorder);
queue.MapDelete("/{bookId}",   QueueEndpoints.RemoveFromQueue);

stats.MapGet("/dashboard",     StatsEndpoints.GetDashboard);
stats.MapGet("/special-lists", StatsEndpoints.GetSpecialLists);
```

3. **Los handlers son métodos estáticos** en clases `*Endpoints` dentro
   de `src/ReadingQueue.Api/Endpoints/`.

4. **Validación con FluentValidation.** Todo request body tiene su
   `AbstractValidator<T>`. La validación se ejecuta como filtro antes del
   handler.

5. **Tipado fuerte.** Todos los endpoints devuelven `Results<T1, T2>` con
   tipos explícitos — nunca `IResult` genérico sin tipar.

6. **OpenAPI obligatorio.** Cada endpoint tiene `.WithName()`,
   `.WithSummary()` y `.WithTags()`.

---

## 10. Frontend React + TypeScript — Reglas No Negociables

### 10.1 Estructura de Carpetas

```
src/
  api/           # Clientes Axios tipados por dominio (booksApi.ts, authApi.ts)
  components/
    ui/           # Re-exports de shadcn/ui — nunca modificar directamente
    library/      # BookCard, BookFilters, BookForm
    queue/        # QueueList, QueueItem, SuggestionBadge
    stats/        # DashboardStats, GenreChart, SpecialLists
    layout/       # AppShell, Sidebar, Header
  pages/          # LibraryPage, QueuePage, StatsPage, LoginPage
  stores/         # Zustand: useAuthStore, useBooksStore, useQueueStore
  hooks/          # useBooks, useQueue, useStats (wrappean react-query)
  types/          # Book, User, QueueItem, Filters (espejo del dominio)
  lib/            # axios instance, queryClient, zod schemas
```

### 10.2 Tipado

- **Cero `any`.** Está prohibido el uso de `any` en todo el frontend.
  Se usa `unknown` cuando el tipo no se conoce y se hace type guard.
- **Los tipos del dominio** (`Book`, `User`, `QueueItem`) se definen en
  `src/types/` y se usan en toda la aplicación — nunca tipos inline en
  componentes.
- **Los schemas de Zod** validan todo dato que viene de la API antes de
  usarse en el estado.

### 10.3 Estado y Data Fetching

- **TanStack Query (React Query)** para todo server state (fetch, cache,
  invalidación). Zustand solo para estado de UI y sesión del usuario.
- Nunca usar `useEffect` para fetching — siempre `useQuery` / `useMutation`.
- Al marcar un libro como leído, se invalida automáticamente el cache de
  `books`, `queue` y `stats`.

### 10.4 Componentes shadcn/ui

- Los componentes de `src/components/ui/` son generados por shadcn CLI —
  nunca se editan directamente.
- Las customizaciones van en componentes wrapper dentro de
  `src/components/library/`, `queue/`, `stats/`.
- Tailwind se usa exclusivamente con clases utilitarias — nunca `style={}`
  inline salvo para valores dinámicos imposibles de lograr con clases.

### 10.5 Formularios

- **React Hook Form + Zod** para todos los formularios (login, registro,
  crear/editar libro).
- El schema Zod de validación del formulario es el mismo que valida la
  respuesta de la API — definido en `src/lib/schemas/`.

---

## 11. Estructura de Proyecto

```
/
├── src/
│   ├── ReadingQueue.Domain/
│   │   ├── Entities/          # Book, User, QueueItem, AISuggestion
│   │   ├── Interfaces/        # IBookRepository, IQueueRepository,
│   │   │                      # ILLMClient, IAuthService
│   │   ├── ValueObjects/      # BookFilter, QueuePosition
│   │   └── Exceptions/        # BookNotFoundException, UnauthorizedException
│   ├── ReadingQueue.Application/
│   │   ├── UseCases/          # GetFilteredBooks, MarkBookAsRead,
│   │   │                      # GenerateQueue, GetDashboardStats
│   │   └── Services/          # QueueScoringService, SuggestionParser
│   ├── ReadingQueue.Infrastructure/
│   │   ├── Data/              # SqlBookRepository, SqlQueueRepository
│   │   ├── Sql/               # BookQueries.cs, QueueQueries.cs (constantes)
│   │   ├── Migrations/        # 001_initial_schema.sql, 002_seed_data.sql
│   │   ├── Auth/              # JwtService, PasswordHasher
│   │   └── LLM/               # ClaudeClient, SuggestionPromptBuilder
│   └── ReadingQueue.Api/
│       ├── Endpoints/         # AuthEndpoints, BookEndpoints, QueueEndpoints,
│       │                      # StatsEndpoints
│       └── Program.cs         # Composición raíz, DI, Middleware
│
├── frontend/
│   ├── src/                   # Estructura detallada en sección 10.1
│   ├── index.html
│   ├── vite.config.ts
│   ├── tailwind.config.ts
│   └── tsconfig.json
│
├── tests/
│   ├── ReadingQueue.Domain.Tests/
│   ├── ReadingQueue.Application.Tests/
│   ├── ReadingQueue.Infrastructure.Tests/   # Testcontainers (SQL Server real)
│   └── ReadingQueue.Api.Tests/              # TestServer + cliente HTTP real
│
├── docker-compose.yml          # Local dev: API + Frontend + SQL Server
├── docker-compose.override.yml # Overrides locales (puertos, volúmenes)
└── .env.example                # Template de variables — NUNCA .env en repo
```

Máximo 4 proyectos en `src/`. Proyectos adicionales requieren justificación.

---

## 12. Docker y Despliegue — Reglas No Negociables

### 12.1 Docker Compose Local

```yaml
# docker-compose.yml — servicios obligatorios
services:
  api:
    build: ./src
    ports: ["5000:8080"]
    environment:
      - ConnectionStrings__DefaultConnection=...
      - Claude__ApiKey=${CLAUDE_API_KEY}
      - Jwt__SecretKey=${JWT_SECRET_KEY}
    depends_on: [sqlserver]

  frontend:
    build: ./frontend
    ports: ["3000:80"]
    depends_on: [api]

  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      - ACCEPT_EULA=Y
      - SA_PASSWORD=${SA_PASSWORD}
    volumes:
      - sqldata:/var/opt/mssql

volumes:
  sqldata:
```

### 12.2 Dockerfiles

- **API**: Imagen base `mcr.microsoft.com/dotnet/aspnet:9.0`. Build en
  `mcr.microsoft.com/dotnet/sdk:9.0`. Multi-stage obligatorio.
- **Frontend**: Build en `node:20-alpine`, servir con `nginx:alpine`.
  El `nginx.conf` incluye la regla de fallback a `index.html` para SPA.

### 12.3 Variables de Entorno — Obligatorio

Estas variables DEBEN estar en `.env.example` documentadas y NUNCA en el
repositorio con valores reales:

```
CLAUDE_API_KEY=sk-ant-...
JWT_SECRET_KEY=...
SA_PASSWORD=...
VITE_API_BASE_URL=http://localhost:5000
```

### 12.4 Azure (Post-MVP)

- **API**: Azure App Service (Linux, .NET 9).
- **Frontend**: Azure Static Web Apps.
- **Base de datos**: Azure SQL Database.
- **Secretos**: Azure Key Vault + Managed Identity — nunca variables de
  entorno con valores reales en Azure Portal directamente.
- La cadena de conexión y el API key de Claude se obtienen de Key Vault
  en startup via `Azure.Extensions.AspNetCore.Configuration.Secrets`.

---

## 13. Testing — Reglas No Negociables

1. **TDD**: Tests antes que implementación.
2. **Cobertura mínima del 80%** en Domain y Application.
3. **Tests de integración con Testcontainers** para SQL Server real —
   nunca mocks de base de datos en tests de repositorio.
4. **Cada `IBookRepository` e `IQueueRepository` tienen test de contrato**
   que validan comportamiento independientemente de la implementación.
5. **Tests de API** usan `TestServer` con cliente HTTP real y JWT válido.
6. **Tests del `ClaudeClient`** usan `WireMock.Net` para simular el
   servidor Anthropic — nunca depender de la API real en tests.
7. **Tests del algoritmo de cola** cubren los casos: biblioteca vacía,
   solo libros leídos, LLM disponible, LLM no disponible (fallback).

---

## 14. Seguridad y Observabilidad

- Nunca loggear el API Key de Claude (Anthropic) ni el JWT SecretKey.
- Nunca loggear el contenido del prompt del sistema en producción.
- El `UserId` del JWT nunca se expone en respuestas de API que no sean
  el propio perfil del usuario.
- Los errores de la API de Anthropic nunca se exponen raw al cliente — siempre se
  mapean a mensajes genéricos.
- Toda llamada al LLM incluye el `UserId` en los metadatos de trace de
  OpenTelemetry (sin PII en el valor).
- El tiempo de respuesta del LLM se registra como métrica (`llm_latency_ms`).
- Los endpoints de libros validan que el `BookId` pertenezca al usuario
  autenticado antes de cualquier operación (nunca asumir por el ID solo).

---

## 15. Lo que el Agente de IA NO debe hacer

- ❌ NO usar Entity Framework Core, NHibernate ni ORM con migrations automáticas
- ❌ NO usar Controllers — solo Minimal API
- ❌ NO hardcodear el API Key de Claude (Anthropic) ni el JWT SecretKey en código
- ❌ NO hardcodear el prompt del sistema en archivos `.cs`
- ❌ NO usar `.Result` o `.Wait()` en código async
- ❌ NO interpolar variables directamente en strings SQL
- ❌ NO inyectar `IDbConnection` directamente — siempre via factory
- ❌ NO crear más de 4 proyectos en `src/` sin justificación
- ❌ NO omitir `CancellationToken` en métodos que llaman a Claude
- ❌ NO confiar en `UserId` que venga del request body — siempre del JWT
- ❌ NO devolver errores de Claude/Anthropic o SQL raw al cliente
- ❌ NO usar `any` en TypeScript — cero excepciones
- ❌ NO usar `useEffect` para fetching en el frontend
- ❌ NO editar directamente los componentes generados por shadcn CLI
- ❌ NO commitear archivos `.env` con valores reales al repositorio
- ❌ NO escribir implementación antes que los tests
- ❌ NO usar `style={{}}` inline en React cuando una clase Tailwind lo resuelve
- ❌ NO exponer el `connectionId` de SQL ni IDs internos de infraestructura al cliente
