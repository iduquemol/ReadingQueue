# SPEC-05 · Tasks — Integración con Claude (Anthropic) — Sugerencias IA
> Proyecto: `ReadingQueue` (solución .NET 9 + React/Vite)
> Regla: **Tests primero, implementación después** (constitution §13)
> Cobertura mínima: **80%** en Domain y Application · Integración con Testcontainers en Infrastructure · WireMock.Net en ClaudeClient

---

## Nota previa: estrategia de integración

Este spec **extiende** el Spec-04 sin modificar su estructura interna:
- `QueueScoringService.Score()` no se toca — solo se alimenta con el diccionario `aiScores` poblado.
- `GenerateQueue` (Spec-04) se **reemplaza** en el DI por `GenerateQueueWithAI`. El endpoint `POST /api/queue/generate` no cambia su ruta.
- El campo `source` de los ítems de cola pasa de `"Filter"` (Spec-04) a `"AI"` cuando Claude contribuye exitosamente.
- La respuesta de `POST /api/queue/generate` se extiende con `aiContributed` y `aiReasoning` por ítem.

## Nota previa: IBookRepository — método nuevo

`GenerateQueueWithAI` necesita tanto libros no leídos como leídos del usuario. `IBookRepository` ya tiene `GetUnreadByUserAsync`. Hay que agregar:

```csharp
Task<IEnumerable<Book>> GetReadByUserAsync(int userId, CancellationToken ct = default);
```

Este método debe añadirse a la interfaz y a `SqlBookRepository` antes de implementar el use case (Bloque D).

## Nota previa: WireMock.Net para ClaudeClient

`ClaudeClient` crea internamente un `AnthropicClient`. Para que los tests intercepten las llamadas HTTP sin llegar a la API real de Anthropic, `ClaudeOptions` incluirá una propiedad `BaseUrl` (vacía en producción, apuntando al servidor WireMock en tests). Si `BaseUrl` no está vacío, el `AnthropicClient` se construye pasando un `HttpClient` con esa base URL; de lo contrario, usa el cliente por defecto del SDK.

Agregar a `ClaudeOptions`:
```csharp
public string BaseUrl { get; set; } = string.Empty; // solo para tests
```

## Nota previa: migración de base de datos

Spec-05 necesita una nueva migración para la tabla `AISuggestions`. El `MigrationRunner` ya aplica los archivos `.sql` en orden numérico desde `ReadingQueue.Infrastructure/Migrations/`. Hay que crear `004_ai_suggestions.sql`:

```sql
CREATE TABLE AISuggestions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT            NOT NULL REFERENCES Users(Id),
    BookId      INT            NOT NULL REFERENCES Books(Id),
    Reasoning   NVARCHAR(1000) NOT NULL,
    Score       DECIMAL(4,2)   NOT NULL,
    WasAccepted BIT            NULL,
    GeneratedAt DATETIME2      NOT NULL DEFAULT GETUTCDATE()
);
CREATE INDEX IX_AISuggestions_UserId_GeneratedAt
    ON AISuggestions (UserId, GeneratedAt DESC);
```

---

## Bloque A — Domain: Entidades, Value Objects e Interfaces

### TASK-05-A1 · Test: entidad `AISuggestion` y value object `BookSuggestion`

- **Archivos test:**
  - `tests/ReadingQueue.Domain.Tests/AISuggestionTests.cs`
  - `tests/ReadingQueue.Domain.Tests/BookSuggestionTests.cs`

- **Casos `AISuggestion`:**
  - [ ] Constructor asigna correctamente las 7 propiedades (`Id`, `UserId`, `BookId`, `Reasoning`, `Score`, `GeneratedAt`, `WasAccepted`).
  - [ ] `WasAccepted` puede ser `null`, `true` o `false`.
  - [ ] `Score` acepta valores en el rango `0.00` – `10.00` sin excepción.

- **Casos `BookSuggestion`:**
  - [ ] Es un record — se puede construir con `BookId`, `Score` y `Reasoning`.
  - [ ] `with` expression crea una copia modificada sin afectar el original.
  - [ ] `Score` en `[0.0, 10.0]` no lanza excepción (no hay validación — es un value object simple).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-05-A2 · Implementar entidades, value objects e interfaces de dominio

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Entities/
    AISuggestion.cs
  ValueObjects/
    BookSuggestion.cs
  Interfaces/
    ILLMClient.cs
    IAISuggestionRepository.cs
```

**Actualizar:**
```
src/ReadingQueue.Domain/Interfaces/IBookRepository.cs   ← agregar GetReadByUserAsync
src/ReadingQueue.Infrastructure/Data/SqlBookRepository.cs ← implementar GetReadByUserAsync
```

```csharp
// src/ReadingQueue.Domain/Entities/AISuggestion.cs
namespace ReadingQueue.Domain.Entities;

public sealed class AISuggestion
{
    public int      Id          { get; }
    public int      UserId      { get; }
    public int      BookId      { get; }
    public string   Reasoning   { get; }
    public decimal  Score       { get; }     // 0.00 – 10.00
    public DateTime GeneratedAt { get; }
    public bool?    WasAccepted { get; }     // null hasta que se procese

    public AISuggestion(int id, int userId, int bookId, string reasoning,
                        decimal score, DateTime generatedAt, bool? wasAccepted)
    {
        Id          = id;
        UserId      = userId;
        BookId      = bookId;
        Reasoning   = reasoning;
        Score       = score;
        GeneratedAt = generatedAt;
        WasAccepted = wasAccepted;
    }
}
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/BookSuggestion.cs
namespace ReadingQueue.Domain.ValueObjects;

public sealed record BookSuggestion(
    int    BookId,
    double Score,      // 0.0 – 10.0
    string Reasoning   // 1-2 oraciones
);
```

```csharp
// src/ReadingQueue.Domain/Interfaces/ILLMClient.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface ILLMClient
{
    /// <summary>
    /// Retorna null si Claude no está disponible o la respuesta no es parseable.
    /// El llamador activa el fallback cuando recibe null.
    /// </summary>
    Task<IEnumerable<BookSuggestion>?> GenerateSuggestionsAsync(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks,
        CancellationToken ct = default);
}
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IAISuggestionRepository.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface IAISuggestionRepository
{
    Task SaveSuggestionsAsync(
        int userId,
        IEnumerable<BookSuggestion> suggestions,
        IEnumerable<int> acceptedBookIds,
        CancellationToken ct = default);

    Task<IEnumerable<AISuggestion>> GetLatestByUserAsync(
        int userId,
        int take = 20,
        CancellationToken ct = default);
}
```

```csharp
// Agregar a IBookRepository:
Task<IEnumerable<Book>> GetReadByUserAsync(int userId, CancellationToken ct = default);

// Implementación en SqlBookRepository (query directa):
// SELECT ... FROM Books WHERE UserId = @UserId AND IsRead = 1 ORDER BY ReadAt DESC;
```

- **Completado cuando:** tests de TASK-05-A1 pasan (verde) y `dotnet build` → `0 Error(s)`.

---

## Bloque B — Infrastructure: Cliente LLM

> `SuggestionPromptBuilder` es un helper estático — testeable 100% en memoria.
> `ClaudeClient` usa WireMock.Net para interceptar llamadas HTTP sin llegar a la API real.

### TASK-05-B1 · Test: `SuggestionPromptBuilder`

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/LLM/SuggestionPromptBuilderTests.cs`
- **No requiere Testcontainers ni WireMock — tests unitarios puros.**

- **Casos:**
  - [ ] Con libros leídos → el mensaje de usuario incluye la sección `readBooks` con los campos `id`, `title`, `author`, `genre` (sin `priority`).
  - [ ] Sin libros leídos → el mensaje de usuario incluye `"readBooks":[]` sin error (CA-15).
  - [ ] Con más de 50 libros no leídos → el prompt solo envía los 50 de mayor prioridad (CA-14).
  - [ ] Con más de 30 libros leídos → el prompt solo envía los 30 más recientes (ordenados por `ReadAt DESC`).
  - [ ] Los libros no leídos en el prompt incluyen el campo `priority` además de `id`, `title`, `author`, `genre`.
  - [ ] El JSON del mensaje de usuario es parseable con `JsonDocument.Parse`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-05-B2 · Implementar `ClaudeOptions`, `SuggestionPromptBuilder` y `ClaudeResiliencePipeline`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/LLM/
  ClaudeOptions.cs
  SuggestionPromptBuilder.cs
  ClaudeResiliencePipeline.cs
```

```csharp
// src/ReadingQueue.Infrastructure/LLM/ClaudeOptions.cs
namespace ReadingQueue.Infrastructure.LLM;

public sealed class ClaudeOptions
{
    public string ApiKey        { get; set; } = string.Empty;
    public string Model         { get; set; } = "claude-sonnet-4-5";
    public int    MaxTokens     { get; set; } = 1024;
    public int    TimeoutSeconds { get; set; } = 30;
    public int    MaxRetries    { get; set; } = 3;
    public string BaseUrl       { get; set; } = string.Empty; // solo para tests con WireMock
}
```

```csharp
// src/ReadingQueue.Infrastructure/LLM/SuggestionPromptBuilder.cs
// - SystemPrompt es private const string — nunca en BD ni en appsettings
// - BuildUserMessage toma hasta 50 libros no leídos (mayor prioridad primero)
//   y hasta 30 libros leídos (más recientes primero)
// - Retorna JSON compacto serializado con JsonSerializer.Serialize
// - Nunca se expone el SystemPrompt fuera de la clase
```

```csharp
// src/ReadingQueue.Infrastructure/LLM/ClaudeResiliencePipeline.cs
// Pipeline Polly con:
//   - Timeout: 30s por intento (configurable en ClaudeOptions.TimeoutSeconds)
//   - Retry: 3 intentos, backoff exponencial 1s → 2s → 4s
//     solo para HttpRequestException y HTTP 429/500/502/503/504
//   - CircuitBreaker: abre tras 5 fallos en 30s, permanece abierto 60s
// Loggea Warning en OnRetry, Warning en OnOpened, Information en OnClosed
```

- **Completado cuando:** tests de TASK-05-B1 pasan (verde).

### TASK-05-B3 · Test: `ClaudeClient` con WireMock.Net

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/LLM/ClaudeClientTests.cs`
- **Dependencia de test:** `WireMock.Net` NuGet package.
- **Setup:** WireMock server local; `ClaudeOptions.BaseUrl` apunta al servidor WireMock.

- **Casos:**
  - [ ] Respuesta JSON válida con `suggestions` → retorna lista de `BookSuggestion` con `BookId`, `Score` y `Reasoning` correctos (CA-01).
  - [ ] Respuesta JSON sin campo `suggestions` → retorna `null` y loggea `Warning` con los primeros 200 chars (CA-03, CA-13).
  - [ ] Respuesta JSON malformado (texto plano) → retorna `null` y loggea `Warning` (CA-03).
  - [ ] HTTP 503 → pipeline Polly hace retry → tras agotar reintentos retorna `null` (CA-04).
  - [ ] Timeout del servidor WireMock → retorna `null` y loggea `Error` (CA-02).
  - [ ] `CancellationToken` cancelado → lanza `OperationCanceledException` sin envolver (CA-11).
  - [ ] `ApiKey` vacío → el constructor no lanza; la excepción ocurre en la llamada (CA-19).
  - [ ] El `ApiKey` no aparece en ningún mensaje de log (CA-12).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-05-B4 · Implementar `ClaudeClient`

- **Archivo:** `src/ReadingQueue.Infrastructure/LLM/ClaudeClient.cs`
- Implementa `ILLMClient`.
- Recibe `IOptions<ClaudeOptions>`, `ILogger<ClaudeClient>` y la pipeline Polly vía `[FromKeyedServices("claude-pipeline")]`.
- Si `ClaudeOptions.BaseUrl` no está vacío, construye el `AnthropicClient` pasando un `HttpClient` con esa base URL (para tests). En producción usa el constructor estándar.
- Nunca lanza al llamador excepto `OperationCanceledException` — todo lo demás retorna `null`.
- Loggea `Information` al inicio (UserId implícito, cantidad de libros) y al éxito (latencia ms, cantidad de sugerencias).
- Loggea `Warning` para JSON inválido (máx. 200 chars del raw).
- Loggea `Error` para excepciones no esperadas.
- Nunca loggea el `ApiKey` ni la respuesta completa de Claude.

- **Completado cuando:** tests de TASK-05-B3 pasan (verde).

---

## Bloque C — Infrastructure: Repositorio de Sugerencias

### TASK-05-C1 · Test de integración: `SqlAISuggestionRepository`

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlAISuggestionRepositoryTests.cs`
- **Fixture:** misma `BookRepositoryFixture` (ya crea usuario de prueba y ejecuta migraciones, incluyendo la nueva `004_ai_suggestions.sql`).

- **Casos:**
  - [ ] `GetLatestByUserAsync` con tabla vacía → retorna lista vacía.
  - [ ] `SaveSuggestionsAsync` persiste las sugerencias con `UserId`, `BookId`, `Reasoning`, `Score` correctos.
  - [ ] `SaveSuggestionsAsync` marca `WasAccepted = true` para los `acceptedBookIds` y `false` para los demás.
  - [ ] Llamar `SaveSuggestionsAsync` dos veces no elimina las sugerencias anteriores — son historial acumulativo (CA-09).
  - [ ] `GetLatestByUserAsync` retorna máximo `take` sugerencias (default 20) ordenadas por `GeneratedAt DESC` (CA-16).
  - [ ] `GetLatestByUserAsync` no retorna sugerencias de otro usuario (aislamiento).
  - [ ] El campo `BookTitle` del resultado coincide con el título del libro en la tabla `Books` (JOIN en la query).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-05-C2 · Implementar migración, `AISuggestionQueries` y `SqlAISuggestionRepository`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Migrations/
    004_ai_suggestions.sql
  Sql/
    AISuggestionQueries.cs
  Data/
    SqlAISuggestionRepository.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/AISuggestionQueries.cs
internal static class AISuggestionQueries
{
    internal const string InsertSuggestion = """
        INSERT INTO AISuggestions (UserId, BookId, Reasoning, Score, WasAccepted)
        VALUES (@UserId, @BookId, @Reasoning, @Score, @WasAccepted);
        """;

    internal const string GetLatestByUser = """
        SELECT TOP (@Take)
            s.Id, s.UserId, s.BookId, s.Reasoning, s.Score,
            s.GeneratedAt, s.WasAccepted,
            b.Title AS BookTitle
        FROM AISuggestions s
        INNER JOIN Books b ON s.BookId = b.Id
        WHERE s.UserId = @UserId
        ORDER BY s.GeneratedAt DESC;
        """;
}
```

**`SqlAISuggestionRepository.SaveSuggestionsAsync`:** inserta cada sugerencia individualmente con `InsertSuggestion`, estableciendo `WasAccepted = 1` si `BookId` está en `acceptedBookIds`, `0` si no.

**Nota:** las sugerencias no se eliminan antes de insertar — historial acumulativo.

**`SqlAISuggestionRepository.GetLatestByUserAsync`:** el resultado incluye `BookTitle` del JOIN. Si se usa un DTO intermedio, mapear a `AISuggestion` ignorando ese campo (no forma parte de la entidad de dominio — solo se usa en la response de API).

- **Completado cuando:** tests de TASK-05-C1 pasan (verde).

---

## Bloque D — Application: Use Case `GenerateQueueWithAI`

### TASK-05-D1 · Test: `GenerateQueueWithAI`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GenerateQueueWithAITests.cs`
- **Setup:** Moq para `IBookRepository`, `IQueueRepository`, `IAISuggestionRepository`, `ILLMClient`, `IDbConnectionFactory`. `QueueScoringService` real.

- **Casos:**
  - [ ] Sin libros no leídos → retorna `Result` con `Queue = []` y `AiContributed = false` sin llamar a `ILLMClient` (CA-04 implícito).
  - [ ] `ILLMClient` retorna sugerencias válidas → `AiContributed = true`, scores se pasan al `QueueScoringService` y el source de los ítems es `"AI"` (CA-01, CA-05, CA-17).
  - [ ] `ILLMClient` retorna `null` → `AiContributed = false`, cola generada con `aiScores = null` y source `"Filter"` (CA-02, CA-17).
  - [ ] `ILLMClient` retorna sugerencias → `SaveSuggestionsAsync` es llamado con el `UserId` correcto y los `acceptedBookIds` del top-20 (CA-07, CA-08).
  - [ ] `ILLMClient` retorna `null` → `SaveSuggestionsAsync` NO es llamado.
  - [ ] Si `ReplaceQueueAsync` lanza excepción → la transacción hace rollback.
  - [ ] Con libros leídos → `GetReadByUserAsync` es llamado con el `UserId` correcto.
  - [ ] Un libro con `aiScore = 10.0` aparece antes que uno con `aiScore = 0.0` en igualdad de prioridad (CA-06).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-05-D2 · Implementar `GenerateQueueWithAI`

- **Archivo:** `src/ReadingQueue.Application/UseCases/GenerateQueueWithAI.cs`

```csharp
// Firma del use case (ver spec-05 §8 para implementación completa)
public sealed class GenerateQueueWithAI
{
    public record Command(int UserId);
    public record Result(
        IReadOnlyList<QueueItem> Queue,
        bool AiContributed
    );

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct) { ... }
}
```

**Flujo:**
1. `GetUnreadByUserAsync` + `GetReadByUserAsync` para el usuario.
2. Si no hay libros no leídos → retorna `Result([], false)`.
3. Llama a `ILLMClient.GenerateSuggestionsAsync(readBooks, unreadBooks, ct)`.
4. `aiContributed = suggestions is not null`.
5. Construye `aiScores` desde las sugerencias (o diccionario vacío si null).
6. `_scoring.Score(unread, aiScores)` — igual que Spec-04.
7. `source = aiContributed ? "AI" : "Filter"`.
8. En transacción: `ReplaceQueueAsync` + `SaveSuggestionsAsync` (solo si `aiContributed`).
9. Retorna `Result(queue, aiContributed)`.

**Loggea `Warning`** si `ILLMClient` retorna null: "Fallback activado para UserId {UserId}."

- **Completado cuando:** tests de TASK-05-D1 pasan (verde).

---

## Bloque E — API: Responses, Endpoints y Program.cs

### TASK-05-E1 · Test de integración: `QueueEndpointsAITests`

- **Archivo test:** `tests/ReadingQueue.Api.Tests/Endpoints/QueueEndpointsAITests.cs`
- **Fixture:** `QueueEndpointsFixture` existente (misma de Spec-04) + WireMock.Net para simular la API de Anthropic.
- **Configurar `Claude:BaseUrl`** en `appsettings.json` del test apuntando al servidor WireMock.

- **Casos:**
  - [ ] `POST /api/queue/generate` con WireMock respondiendo JSON válido → `200 OK`, `aiContributed: true`, `source: "AI"`, `aiReasoning` no nulo en los ítems (CA-01, CA-17).
  - [ ] `POST /api/queue/generate` con WireMock simulando timeout → `200 OK`, `aiContributed: false`, `source: "Filter"`, `aiReasoning: null` (CA-02, CA-17).
  - [ ] `POST /api/queue/generate` con WireMock retornando texto plano (JSON inválido) → `200 OK`, `aiContributed: false` (CA-03).
  - [ ] `POST /api/queue/generate` con WireMock retornando HTTP 503 repetido → `200 OK`, `aiContributed: false` — nunca retorna `500` (CA-04).
  - [ ] Sugerencias se persisten en `AISuggestions` tras llamada exitosa de Claude (CA-07).
  - [ ] `WasAccepted = true` para los libros que entraron en la cola (CA-08).
  - [ ] Llamar `generate` dos veces → filas en `AISuggestions` se acumulan, no se reemplazan (CA-09).
  - [ ] `GET /api/queue/suggestions` → `200 OK` con las últimas 20 sugerencias ordenadas por `GeneratedAt DESC` (CA-16).
  - [ ] `GET /api/queue/suggestions` sin token → `401`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-05-E2 · Implementar Responses, actualizar Endpoints y Program.cs

**Archivos a crear:**

```
src/ReadingQueue.Api/
  Responses/
    GenerateQueueResponse.cs     ← { aiContributed, queue: List<QueueItemWithAIResponse> }
    QueueItemWithAIResponse.cs   ← { position, addedAt, source, aiReasoning, book }
    AISuggestionResponse.cs      ← { bookId, bookTitle, score, reasoning, generatedAt, wasAccepted }
```

**Archivos a actualizar:**

```
src/ReadingQueue.Api/Endpoints/QueueEndpoints.cs   ← handler GenerateQueue → GenerateQueueWithAI
                                                      + GET /suggestions
src/ReadingQueue.Api/Program.cs                    ← DI spec-05
src/ReadingQueue.Api/appsettings.json              ← sección Claude:
```

```csharp
// src/ReadingQueue.Api/Responses/GenerateQueueResponse.cs
public sealed record GenerateQueueResponse(
    bool AiContributed,
    IReadOnlyList<QueueItemWithAIResponse> Queue
);
```

```csharp
// src/ReadingQueue.Api/Responses/QueueItemWithAIResponse.cs
public sealed record QueueItemWithAIResponse(
    int          Position,
    DateTime     AddedAt,
    string       Source,
    string?      AiReasoning,   // null cuando source = "Filter"
    BookResponse Book
);
```

```csharp
// src/ReadingQueue.Api/Responses/AISuggestionResponse.cs
public sealed record AISuggestionResponse(
    int       BookId,
    string    BookTitle,
    double    Score,
    string    Reasoning,
    DateTime  GeneratedAt,
    bool?     WasAccepted
);
```

**Cambios en `QueueEndpoints.cs`:**
- Handler de `POST /generate` pasa de `GenerateQueue` a `GenerateQueueWithAI`.
- La respuesta usa `GenerateQueueResponse` con `AiContributed` y lista de `QueueItemWithAIResponse`.
- Agregar `GET /suggestions` → `GetSuggestions` → `IAISuggestionRepository.GetLatestByUserAsync` (o use case si se prefiere).

**Cambios en `Program.cs`:**

```csharp
// ── Opciones de Claude ──────────────────────────────────────────────────────
builder.Services.Configure<ClaudeOptions>(
    builder.Configuration.GetSection("Claude"));

// ── Pipeline de resiliencia (singleton) ────────────────────────────────────
builder.Services.AddKeyedSingleton("claude-pipeline", (sp, _) =>
    ClaudeResiliencePipeline.Build(
        sp.GetRequiredService<ILogger<ClaudeClient>>()));

// ── Cliente LLM y repositorio de sugerencias ────────────────────────────────
builder.Services.AddScoped<ILLMClient,              ClaudeClient>();
builder.Services.AddScoped<IAISuggestionRepository, SqlAISuggestionRepository>();

// ── Use case — reemplaza GenerateQueue del Spec-04 ──────────────────────────
// Eliminar: builder.Services.AddScoped<GenerateQueue>();
builder.Services.AddScoped<GenerateQueueWithAI>();
```

**Cambios en `appsettings.json`** — agregar sección:
```json
"Claude": {
  "ApiKey": "",
  "Model": "claude-sonnet-4-5",
  "MaxTokens": 1024,
  "TimeoutSeconds": 30,
  "MaxRetries": 3,
  "BaseUrl": ""
}
```

**Nota de seguridad:** el `ApiKey` real viene **exclusivamente** de la variable de entorno `Claude__ApiKey`. El campo en `appsettings.json` queda vacío. En tests, usar WireMock — no configurar `ApiKey` real.

- **Completado cuando:** tests de TASK-05-E1 pasan (verde).

---

## Bloque F — Verificación Final

### TASK-05-F1 · Build .NET sin errores ni warnings

```powershell
dotnet build ReadingQueue.sln
```

- **Criterio:** `0 Error(s)  0 Warning(s)`.

### TASK-05-F2 · Tests de Domain y Application pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Domain.Tests/ReadingQueue.Domain.Tests.csproj --no-build -v minimal
dotnet test tests/ReadingQueue.Application.Tests/ReadingQueue.Application.Tests.csproj --no-build -v minimal
```

- **Criterio:** todos los tests existentes + los nuevos de Spec-05 pasan.

### TASK-05-F3 · Tests de Infrastructure pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Infrastructure.Tests/ReadingQueue.Infrastructure.Tests.csproj --no-build -v minimal
```

- **Criterio:** `SuggestionPromptBuilderTests`, `ClaudeClientTests` y `SqlAISuggestionRepositoryTests` pasan junto con los tests anteriores.

### TASK-05-F4 · Tests de API pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Api.Tests/ReadingQueue.Api.Tests.csproj --no-build -v minimal
```

- **Criterio:** `QueueEndpointsAITests` pasa junto con todos los tests anteriores (Spec-03 y Spec-04).

---

## Resumen de archivos que genera SPEC-05

| # | Archivo | Bloque |
|---|---|---|
| 1  | `src/ReadingQueue.Domain/Entities/AISuggestion.cs` | A |
| 2  | `src/ReadingQueue.Domain/ValueObjects/BookSuggestion.cs` | A |
| 3  | `src/ReadingQueue.Domain/Interfaces/ILLMClient.cs` | A |
| 4  | `src/ReadingQueue.Domain/Interfaces/IAISuggestionRepository.cs` | A |
| 5  | `src/ReadingQueue.Domain/Interfaces/IBookRepository.cs` (actualizado: `GetReadByUserAsync`) | A |
| 6  | `src/ReadingQueue.Infrastructure/Data/SqlBookRepository.cs` (actualizado: `GetReadByUserAsync`) | A |
| 7  | `src/ReadingQueue.Infrastructure/LLM/ClaudeOptions.cs` | B |
| 8  | `src/ReadingQueue.Infrastructure/LLM/SuggestionPromptBuilder.cs` | B |
| 9  | `src/ReadingQueue.Infrastructure/LLM/ClaudeResiliencePipeline.cs` | B |
| 10 | `src/ReadingQueue.Infrastructure/LLM/ClaudeClient.cs` | B |
| 11 | `src/ReadingQueue.Infrastructure/Migrations/004_ai_suggestions.sql` | C |
| 12 | `src/ReadingQueue.Infrastructure/Sql/AISuggestionQueries.cs` | C |
| 13 | `src/ReadingQueue.Infrastructure/Data/SqlAISuggestionRepository.cs` | C |
| 14 | `src/ReadingQueue.Application/UseCases/GenerateQueueWithAI.cs` | D |
| 15 | `src/ReadingQueue.Api/Responses/GenerateQueueResponse.cs` | E |
| 16 | `src/ReadingQueue.Api/Responses/QueueItemWithAIResponse.cs` | E |
| 17 | `src/ReadingQueue.Api/Responses/AISuggestionResponse.cs` | E |
| 18 | `src/ReadingQueue.Api/Endpoints/QueueEndpoints.cs` (actualizado) | E |
| 19 | `src/ReadingQueue.Api/Program.cs` (actualizado) | E |
| 20 | `src/ReadingQueue.Api/appsettings.json` (actualizado: sección `Claude:`) | E |
| 21 | `tests/ReadingQueue.Domain.Tests/AISuggestionTests.cs` | A |
| 22 | `tests/ReadingQueue.Domain.Tests/BookSuggestionTests.cs` | A |
| 23 | `tests/ReadingQueue.Infrastructure.Tests/LLM/SuggestionPromptBuilderTests.cs` | B |
| 24 | `tests/ReadingQueue.Infrastructure.Tests/LLM/ClaudeClientTests.cs` | B |
| 25 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlAISuggestionRepositoryTests.cs` | C |
| 26 | `tests/ReadingQueue.Application.Tests/UseCases/GenerateQueueWithAITests.cs` | D |
| 27 | `tests/ReadingQueue.Api.Tests/Endpoints/QueueEndpointsAITests.cs` | E |

---

## Checklist SPEC-05

### Bloque A — Domain
- [x] TASK-05-A1 · Tests `AISuggestion` + `BookSuggestion` (rojo)
- [x] TASK-05-A2 · Impl `AISuggestion`, `BookSuggestion`, `ILLMClient`, `IAISuggestionRepository` + extend `IBookRepository` (verde)

### Bloque B — Infrastructure: LLM
- [x] TASK-05-B1 · Tests `SuggestionPromptBuilder` (rojo)
- [x] TASK-05-B2 · Impl `ClaudeOptions` + `SuggestionPromptBuilder` + `ClaudeResiliencePipeline` (verde)
- [x] TASK-05-B3 · Tests `ClaudeClient` con WireMock.Net (rojo)
- [x] TASK-05-B4 · Impl `ClaudeClient` (verde)

### Bloque C — Infrastructure: Data
- [x] TASK-05-C1 · Tests `SqlAISuggestionRepository` con Testcontainers (rojo)
- [x] TASK-05-C2 · Impl migración `004_ai_suggestions.sql` + `AISuggestionQueries` + `SqlAISuggestionRepository` (verde)

### Bloque D — Application: Use Case
- [x] TASK-05-D1 · Tests `GenerateQueueWithAI` (rojo)
- [x] TASK-05-D2 · Impl `GenerateQueueWithAI` (verde)

### Bloque E — API
- [x] TASK-05-E1 · Tests integración `QueueEndpointsAI` con WireMock.Net (rojo)
- [x] TASK-05-E2 · Impl Responses + update `QueueEndpoints` + `Program.cs` + `appsettings.json` (verde)

### Bloque F — Verificación Final
- [x] TASK-05-F1 · `dotnet build` → 0 errores, 0 warnings
- [x] TASK-05-F2 · Tests de Domain y Application pasan (verde)
- [x] TASK-05-F3 · Tests de Infrastructure pasan (verde, incluye nuevos)
- [x] TASK-05-F4 · Tests de Api pasan (verde, incluye nuevos)
