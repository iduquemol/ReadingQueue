# spec-05-integracion-claude.md
# Feature: Integración con Claude (Anthropic) — Sugerencias IA

## 1. Resumen

Conectar el sistema con la API de Claude (Anthropic) para enriquecer la
generación de la cola de lectura con inteligencia artificial. Claude analiza
el historial de libros leídos del usuario y los libros pendientes, y devuelve
un score (0–10) con razonamiento en 1-2 oraciones para cada libro sugerido.
Ese score se mezcla como el 20% del `CompositeScore` en el `QueueScoringService`
del Spec 4, completando el algoritmo con los cuatro factores definidos en la
constitution. Las sugerencias se persisten en `AISuggestions` para historial
y auditoría. Si Claude no está disponible, el sistema activa silenciosamente
el fallback determinístico del Spec 4 — el usuario nunca ve un error.

---

## 2. Motivación

El Spec 4 dejó un hueco explícito y documentado: el parámetro `aiScores`
del `QueueScoringService.Score()` siempre llegaba vacío, y el campo
`source` de la cola quedaba en `'Filter'`. Este spec rellena ese hueco.
No toca la estructura del `QueueScoringService` — solo alimenta el
diccionario `aiScores` antes de llamarlo. Esa es la costura diseñada
desde el principio: este spec es una extensión, no una reescritura.

---

## 3. Usuarios y Casos de Uso

| Actor | Caso de uso |
|---|---|
| Usuario autenticado | Generar cola con sugerencias de Claude via `POST /api/queue/generate` |
| Usuario autenticado | Ver el razonamiento de Claude para cada libro en la cola |
| Usuario autenticado | Recibir una cola válida aunque Claude no esté disponible (fallback) |
| Sistema | Persistir las sugerencias generadas en `AISuggestions` |
| Sistema | Activar fallback determinístico si Claude falla o responde JSON inválido |
| Sistema | Registrar latencia y resultado de cada llamada a Claude |

---

## 4. Requisitos Funcionales

### RF-01 — Llamada a Claude en la generación de cola
- `POST /api/queue/generate` ahora llama a Claude antes de invocar al
  `QueueScoringService`.
- Claude recibe el historial de libros leídos del usuario y la lista de
  libros no leídos.
- La respuesta de Claude es un JSON con `suggestions`: array de
  `{ bookId, score, reasoning }`.
- Los scores se pasan como `aiScores` al `QueueScoringService.Score()`,
  completando el 20% del `CompositeScore`.
- El campo `source` de los ítems de cola pasa a `'AI'` cuando Claude
  contribuye exitosamente. Si se activa el fallback, queda `'Filter'`.

### RF-02 — Persistencia de sugerencias
- Después de que Claude responde exitosamente, se persisten las
  sugerencias en la tabla `AISuggestions` con `UserId`, `BookId`,
  `Reasoning`, `Score` y `GeneratedAt`.
- Las sugerencias anteriores del usuario **no se eliminan** — son
  historial inmutable. Solo se añaden las nuevas.
- `WasAccepted` se actualiza a `true` para los libros que efectivamente
  quedaron en la cola generada, `false` para los que Claude sugirió
  pero no entraron en el top 20.

### RF-03 — Fallback silencioso
- Si Claude no responde (timeout, error HTTP, circuit breaker abierto),
  la cola se genera con el algoritmo determinístico puro del Spec 4
  (`aiScores = null`).
- Si Claude responde pero el JSON no es parseable o no cumple el esquema
  esperado, se descarta la respuesta, se loggea el error como `Warning`
  y se activa el fallback.
- En ningún caso se lanza excepción al cliente — el endpoint siempre
  retorna `200 OK` con una cola válida.
- El campo `source` de los ítems indica al cliente si Claude participó
  (`'AI'`) o no (`'Filter'`).

### RF-04 — Resiliencia con Polly
- El `ClaudeClient` usa una pipeline Polly con:
  - **Retry**: 3 intentos, backoff exponencial (1s → 2s → 4s),
    solo para errores HTTP 429, 500, 502, 503 y 504.
  - **Circuit Breaker**: se abre tras 5 fallos consecutivos,
    permanece abierto 60 segundos.
  - **Timeout**: 30 segundos por intento (configurable en
    `appsettings.json`).
- `OperationCanceledException` se propaga sin envolver — no es un
  error de Claude, es una cancelación del usuario.

### RF-05 — Prompt construido dinámicamente
- `SuggestionPromptBuilder.Build()` construye el mensaje de usuario
  (no el system prompt) con la lista de libros leídos y pendientes.
- El system prompt es una constante `private const string` en
  `SuggestionPromptBuilder` — nunca en BD ni en `appsettings`.
- El mensaje de usuario incluye los libros en formato JSON compacto
  para no desperdiciar tokens:
  - **Leídos**: solo `id`, `title`, `author`, `genre`.
  - **Pendientes**: `id`, `title`, `author`, `genre`, `priority`.
- Si el usuario no tiene libros leídos, se envía una sección
  `"readBooks": []` — Claude genera sugerencias basadas solo en
  atributos de los libros pendientes.
- Si hay más de 50 libros no leídos, se envían los 50 de mayor
  prioridad (no todos) para no exceder el contexto.

### RF-06 — Configuración del modelo
- El modelo, `maxTokens` y `timeoutSeconds` se leen de
  `appsettings.json` bajo la clave `Claude:`. Nunca hardcodeados
  en el cliente.
- El `ApiKey` viene exclusivamente de la variable de entorno
  `Claude__ApiKey`. Si no está configurada en runtime, la app
  arranca pero el `ClaudeClient` lanza `InvalidOperationException`
  en la primera llamada — activando el fallback inmediatamente.

### RF-07 — Observabilidad
- Cada llamada a Claude registra con `ILogger`:
  - `Information`: inicio de llamada con `UserId` y cantidad de
    libros enviados.
  - `Information`: resultado exitoso con latencia en ms y cantidad
    de sugerencias recibidas.
  - `Warning`: JSON inválido recibido (con los primeros 200 caracteres
    de la respuesta para debug, nunca la respuesta completa).
  - `Warning`: fallback activado con el motivo.
  - `Error`: excepción no esperada capturada antes del fallback.
- **Nunca se loggea el `ApiKey` ni el contenido del system prompt.**
- **Nunca se loggea el texto completo de la respuesta de Claude** —
  solo los primeros 200 caracteres en caso de error de parseo.

---

## 5. Requisitos No Negociables

- El `ApiKey` de Claude **nunca** aparece en código fuente,
  `appsettings.json`, logs, ni respuestas de la API.
- El `ClaudeClient` **nunca** lanza excepción al llamador — siempre
  retorna `null` o una colección vacía cuando falla, para que
  `GenerateQueueWithAI` active el fallback.
- Los errores de Anthropic **nunca** se exponen raw al cliente.
  El endpoint siempre retorna una cola válida.
- `CancellationToken` **siempre** se propaga hasta la llamada HTTP
  del SDK de Anthropic.
- Las sugerencias en `AISuggestions` **nunca** se eliminan —
  son historial inmutable.

---

## 6. Modelo de Dominio

```csharp
// src/ReadingQueue.Domain/Entities/AISuggestion.cs
public sealed class AISuggestion
{
    public int Id { get; }
    public int UserId { get; }
    public int BookId { get; }
    public string Reasoning { get; }
    public decimal Score { get; }          // 0.00 – 10.00
    public DateTime GeneratedAt { get; }
    public bool? WasAccepted { get; }      // null hasta que se procese

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

// src/ReadingQueue.Domain/ValueObjects/BookSuggestion.cs
// Resultado parseado de la respuesta de Claude — no es una entidad persistida
public sealed record BookSuggestion(
    int     BookId,
    double  Score,       // 0.0 – 10.0
    string  Reasoning    // 1-2 oraciones en el idioma del usuario
);

// src/ReadingQueue.Domain/Interfaces/ILLMClient.cs
public interface ILLMClient
{
    /// <summary>
    /// Solicita sugerencias a Claude. Retorna null si Claude no está
    /// disponible o la respuesta no es parseable. El llamador activa
    /// el fallback cuando recibe null.
    /// </summary>
    Task<IEnumerable<BookSuggestion>?> GenerateSuggestionsAsync(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks,
        CancellationToken ct = default);
}

// src/ReadingQueue.Domain/Interfaces/IAISuggestionRepository.cs
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

---

## 7. Implementación del Cliente Claude

```csharp
// src/ReadingQueue.Infrastructure/LLM/ClaudeOptions.cs
public sealed class ClaudeOptions
{
    public string ApiKey { get; set; } = string.Empty;
    public string Model { get; set; } = "claude-sonnet-4-5";
    public int MaxTokens { get; set; } = 1024;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
}

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
        El JSON debe tener exactamente esta estructura:
        {"suggestions":[{"bookId":1,"score":8.5,"reasoning":"Razón breve."}]}
        """;

    internal static string GetSystemPrompt() => SystemPrompt;

    internal static string BuildUserMessage(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks)
    {
        // Máximo 50 libros no leídos — los de mayor prioridad primero
        var unreadList = unreadBooks
            .OrderByDescending(b => b.Priority)
            .Take(50)
            .Select(b => new
            {
                id       = b.Id,
                title    = b.Title,
                author   = b.Author,
                genre    = b.Genre,
                priority = b.Priority
            });

        var readList = readBooks
            .OrderByDescending(b => b.ReadAt)
            .Take(30)   // últimos 30 leídos — los más recientes son más relevantes
            .Select(b => new
            {
                id     = b.Id,
                title  = b.Title,
                author = b.Author,
                genre  = b.Genre
            });

        return JsonSerializer.Serialize(new
        {
            readBooks   = readList,
            unreadBooks = unreadList
        });
    }
}

// src/ReadingQueue.Infrastructure/LLM/ClaudeClient.cs
public sealed class ClaudeClient : ILLMClient
{
    private readonly ClaudeOptions _options;
    private readonly ILogger<ClaudeClient> _logger;
    private readonly ResiliencePipeline _pipeline;

    public ClaudeClient(
        IOptions<ClaudeOptions> options,
        ILogger<ClaudeClient> logger,
        [FromKeyedServices("claude-pipeline")] ResiliencePipeline pipeline)
    {
        _options  = options.Value;
        _logger   = logger;
        _pipeline = pipeline;
    }

    public async Task<IEnumerable<BookSuggestion>?> GenerateSuggestionsAsync(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks,
        CancellationToken ct = default)
    {
        var unreadList = unreadBooks.ToList();
        var readList   = readBooks.ToList();

        _logger.LogInformation(
            "Llamando a Claude para sugerencias. UserId implícito. " +
            "Libros leídos: {Read}, no leídos: {Unread}.",
            readList.Count, unreadList.Count);

        var sw = Stopwatch.StartNew();

        try
        {
            IEnumerable<BookSuggestion>? result = null;

            await _pipeline.ExecuteAsync(async token =>
            {
                var client = new AnthropicClient(_options.ApiKey);

                var request = new MessageRequest
                {
                    Model      = _options.Model,
                    MaxTokens  = _options.MaxTokens,
                    System     = SuggestionPromptBuilder.GetSystemPrompt(),
                    Messages   =
                    [
                        new Message
                        {
                            Role    = RoleType.User,
                            Content = SuggestionPromptBuilder
                                          .BuildUserMessage(readList, unreadList)
                        }
                    ]
                };

                var response = await client.Messages
                    .GetClaudeMessageAsync(request, token);

                var raw = response.Content.OfType<TextContent>()
                              .FirstOrDefault()?.Text ?? string.Empty;

                result = ParseResponse(raw);
            }, ct);

            sw.Stop();
            _logger.LogInformation(
                "Claude respondió en {Ms}ms. Sugerencias recibidas: {Count}.",
                sw.ElapsedMilliseconds, result?.Count() ?? 0);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;   // propagar cancelación sin envolver
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Error inesperado llamando a Claude tras {Ms}ms. " +
                "Activando fallback determinístico.",
                sw.ElapsedMilliseconds);
            return null;   // fallback
        }
    }

    private IEnumerable<BookSuggestion>? ParseResponse(string raw)
    {
        try
        {
            var doc = JsonDocument.Parse(raw);

            if (!doc.RootElement.TryGetProperty("suggestions", out var arr))
            {
                _logger.LogWarning(
                    "Claude no retornó 'suggestions'. Fragmento: {Frag}",
                    raw[..Math.Min(200, raw.Length)]);
                return null;
            }

            return arr.EnumerateArray().Select(el => new BookSuggestion(
                BookId    : el.GetProperty("bookId").GetInt32(),
                Score     : el.GetProperty("score").GetDouble(),
                Reasoning : el.GetProperty("reasoning").GetString() ?? string.Empty
            )).ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "JSON inválido de Claude. Fragmento: {Frag}",
                raw[..Math.Min(200, raw.Length)]);
            return null;
        }
    }
}
```

---

## 8. Use Case: GenerateQueueWithAI (extiende Spec 4)

El use case `GenerateQueue` del Spec 4 se **reemplaza** por
`GenerateQueueWithAI`. Acepta exactamente la misma entrada y retorna
exactamente la misma salida — el endpoint no cambia.

```csharp
// src/ReadingQueue.Application/UseCases/GenerateQueueWithAI.cs
public sealed class GenerateQueueWithAI
{
    private readonly IBookRepository      _books;
    private readonly IQueueRepository     _queue;
    private readonly IAISuggestionRepository _suggestions;
    private readonly QueueScoringService  _scoring;
    private readonly ILLMClient           _llm;
    private readonly IDbConnectionFactory _factory;
    private readonly ILogger<GenerateQueueWithAI> _logger;

    public record Command(int UserId);
    public record Result(
        IReadOnlyList<QueueItem> Queue,
        bool AiContributed   // true si Claude participó, false si fue fallback
    );

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct)
    {
        // ── 1. Obtener libros del usuario ─────────────────────────────────
        var unread = (await _books.GetUnreadByUserAsync(cmd.UserId, ct)).ToList();
        var read   = (await _books.GetReadByUserAsync(cmd.UserId, ct)).ToList();

        if (unread.Count == 0)
            return new Result([], AiContributed: false);

        // ── 2. Llamar a Claude (puede retornar null → fallback) ───────────
        var suggestions = await _llm.GenerateSuggestionsAsync(unread, read, ct);
        var aiContributed = suggestions is not null;

        if (!aiContributed)
        {
            _logger.LogWarning(
                "Fallback activado para UserId {UserId}. " +
                "Cola generada sin IA.", cmd.UserId);
        }

        // ── 3. Construir diccionario de scores ────────────────────────────
        var aiScores = suggestions?
            .ToDictionary(s => s.BookId, s => s.Score)
            ?? new Dictionary<int, double>();

        // ── 4. Scoring en memoria (mismo QueueScoringService del Spec 4) ──
        var scored = _scoring.Score(unread, aiScores);

        // ── 5. Determinar fuente y libros aceptados ───────────────────────
        var source      = aiContributed ? "AI" : "Filter";
        var acceptedIds = scored.Select(sb => sb.Book.Id).ToHashSet();

        // ── 6. Persistir cola y sugerencias en transacción ────────────────
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // 6a. Reemplazar cola
            var items = scored.Select((sb, idx) =>
                (sb.Book.Id, idx + 1, source));
            await _queue.ReplaceQueueAsync(cmd.UserId, items, tx, ct);

            // 6b. Persistir sugerencias de Claude (si las hubo)
            if (aiContributed && suggestions!.Any())
            {
                await _suggestions.SaveSuggestionsAsync(
                    cmd.UserId, suggestions, acceptedIds, ct);
            }

            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        var queue = (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
        return new Result(queue, aiContributed);
    }
}
```

---

## 9. Contrato de API

### POST `/api/queue/generate` ← mismo endpoint que Spec 4, respuesta extendida

El endpoint no cambia su ruta ni su método. El cambio es en la respuesta:
cada ítem ahora puede incluir el razonamiento de Claude, y el response
raíz incluye un flag `aiContributed`.

**Request:** sin body (igual que Spec 4).

**Response `200 OK`:**
```json
{
  "aiContributed": true,
  "queue": [
    {
      "position": 1,
      "addedAt": "2026-05-04T10:00:00Z",
      "source": "AI",
      "aiReasoning": "Complementa perfectamente tus lecturas recientes de García Márquez con otra voz latinoamericana del boom.",
      "book": {
        "id": 7,
        "title": "El otoño del patriarca",
        "author": "Gabriel García Márquez",
        "genre": "Novela latinoamericana",
        "country": "Colombia",
        "priority": 5,
        "mentalEnergy": "🔴 Máxima – modo lector",
        "recommendedMood": "Solemne / quiero leer algo grande",
        "rotationCategory": "Novela grande",
        "isRead": false,
        "readAt": null,
        "notes": null,
        "createdAt": "2026-01-10T08:00:00Z",
        "updatedAt": "2026-01-10T08:00:00Z"
      }
    }
  ]
}
```

**Cuando el fallback se activa (`aiContributed: false`):**
```json
{
  "aiContributed": false,
  "queue": [
    {
      "position": 1,
      "addedAt": "2026-05-04T10:00:00Z",
      "source": "Filter",
      "aiReasoning": null,
      "book": { "..." : "..." }
    }
  ]
}
```

El cliente frontend usa `aiContributed` para mostrar u ocultar el badge
de Claude en la UI. `aiReasoning: null` con `source: 'Filter'` indica
ítem generado por el algoritmo determinístico.

### GET `/api/queue/suggestions` ← endpoint nuevo
Retorna las últimas sugerencias generadas por Claude para el usuario
(últimas 20, sin paginación en MVP).

**Response `200 OK`:**
```json
[
  {
    "bookId": 7,
    "bookTitle": "El otoño del patriarca",
    "score": 9.2,
    "reasoning": "Complementa perfectamente tus lecturas recientes de García Márquez.",
    "generatedAt": "2026-05-04T10:00:00Z",
    "wasAccepted": true
  }
]
```

---

## 10. Queries SQL

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

---

## 11. Pipeline de Resiliencia Polly

```csharp
// src/ReadingQueue.Infrastructure/LLM/ClaudeResiliencePipeline.cs
public static class ClaudeResiliencePipeline
{
    public static ResiliencePipeline Build(ILogger logger)
    {
        return new ResiliencePipelineBuilder()
            // Timeout por intento
            .AddTimeout(new TimeoutStrategyOptions
            {
                Timeout = TimeSpan.FromSeconds(30)
            })
            // Retry: 3 intentos, backoff exponencial 1s → 2s → 4s
            .AddRetry(new RetryStrategyOptions
            {
                MaxRetryAttempts = 3,
                Delay            = TimeSpan.FromSeconds(1),
                BackoffType      = DelayBackoffType.Exponential,
                ShouldHandle     = new PredicateBuilder()
                    .Handle<HttpRequestException>()
                    .HandleResult<HttpResponseMessage>(r =>
                        r.StatusCode is HttpStatusCode.TooManyRequests
                                     or HttpStatusCode.InternalServerError
                                     or HttpStatusCode.BadGateway
                                     or HttpStatusCode.ServiceUnavailable
                                     or HttpStatusCode.GatewayTimeout),
                OnRetry = args =>
                {
                    logger.LogWarning(
                        "Reintento {Attempt} a Claude. Delay: {Delay}ms.",
                        args.AttemptNumber + 1,
                        args.RetryDelay.TotalMilliseconds);
                    return ValueTask.CompletedTask;
                }
            })
            // Circuit Breaker: abre tras 5 fallos, cierra tras 60s
            .AddCircuitBreaker(new CircuitBreakerStrategyOptions
            {
                FailureRatio            = 1.0,
                MinimumThroughput       = 5,
                SamplingDuration        = TimeSpan.FromSeconds(30),
                BreakDuration           = TimeSpan.FromSeconds(60),
                OnOpened = args =>
                {
                    logger.LogWarning(
                        "Circuit breaker de Claude ABIERTO. " +
                        "Fallback activo por 60s.");
                    return ValueTask.CompletedTask;
                },
                OnClosed = args =>
                {
                    logger.LogInformation(
                        "Circuit breaker de Claude CERRADO. " +
                        "Llamadas normales restablecidas.");
                    return ValueTask.CompletedTask;
                }
            })
            .Build();
    }
}
```

---

## 12. Registro en Program.cs

```csharp
// Fragmento a agregar/modificar en src/ReadingQueue.Api/Program.cs

// ── Opciones de Claude ──────────────────────────────────────────────────────
builder.Services.Configure<ClaudeOptions>(
    builder.Configuration.GetSection("Claude"));

// ── Pipeline de resiliencia (singleton — se comparte entre requests) ────────
builder.Services.AddKeyedSingleton("claude-pipeline", (sp, _) =>
    ClaudeResiliencePipeline.Build(
        sp.GetRequiredService<ILogger<ClaudeClient>>()));

// ── Cliente y repositorio ────────────────────────────────────────────────────
builder.Services.AddScoped<ILLMClient,               ClaudeClient>();
builder.Services.AddScoped<IAISuggestionRepository,  SqlAISuggestionRepository>();

// ── Use case — reemplaza GenerateQueue del Spec 4 ───────────────────────────
// Eliminar: builder.Services.AddScoped<GenerateQueue>();
builder.Services.AddScoped<GenerateQueueWithAI>();

// ── Endpoint nuevo ────────────────────────────────────────────────────────────
// El endpoint POST /api/queue/generate ya existe — actualizar el handler
// para que llame a GenerateQueueWithAI en lugar de GenerateQueue.
//
// Agregar:
queue.MapGet("/suggestions", QueueEndpoints.GetSuggestions)
     .WithName("GetSuggestions")
     .WithSummary("Últimas sugerencias de Claude")
     .WithTags("Queue");
```

---

## 13. Configuración

```json
// src/ReadingQueue.Api/appsettings.json — agregar sección
{
  "Claude": {
    "ApiKey": "",
    "Model": "claude-sonnet-4-5",
    "MaxTokens": 1024,
    "TimeoutSeconds": 30,
    "MaxRetries": 3
  }
}
```

```json
// src/ReadingQueue.Api/appsettings.Development.json — ApiKey desde env var
// No agregar ApiKey aquí — siempre viene de Claude__ApiKey env var.
// Para desarrollo local, configurar en docker-compose.yml o en
// secrets de usuario de .NET: dotnet user-secrets set "Claude:ApiKey" "sk-ant-..."
```

---

## 14. Criterios de Aceptación

| ID | Criterio | Verificación |
|---|---|---|
| CA-01 | `POST /api/queue/generate` retorna `aiContributed: true` cuando Claude responde correctamente | Test integration con WireMock |
| CA-02 | `POST /api/queue/generate` retorna `aiContributed: false` y cola válida cuando Claude no responde | Test integration con WireMock simulando timeout |
| CA-03 | `POST /api/queue/generate` retorna `aiContributed: false` cuando Claude responde JSON inválido | Test integration con WireMock retornando texto plano |
| CA-04 | `POST /api/queue/generate` nunca retorna `500` aunque Claude falle | Test integration con WireMock simulando 503 repetido |
| CA-05 | Los scores de Claude modifican el orden de la cola respecto al fallback puro | Test unitario con scores predefinidos |
| CA-06 | Un libro con `aiScore = 10.0` sube en la cola respecto a uno con `aiScore = 0.0` en igualdad de prioridad | Test unitario `QueueScoringService` con aiScores |
| CA-07 | Las sugerencias se persisten en `AISuggestions` tras una llamada exitosa | Test integration con query SQL directa |
| CA-08 | `WasAccepted = true` para libros que entraron en la cola, `false` para los que no | Test integration con SQL directo |
| CA-09 | Sugerencias anteriores NO se eliminan — son historial acumulativo | Test integration: llamar generate dos veces, contar filas en AISuggestions |
| CA-10 | El circuit breaker se abre tras 5 fallos y activa fallback sin llamar a Claude | Test unitario con pipeline Polly mockeado |
| CA-11 | `OperationCanceledException` se propaga sin envolver hasta el endpoint | Test unitario `ClaudeClient` con token cancelado |
| CA-12 | El `ApiKey` no aparece en ningún log — ni en texto normal ni en excepciones | Revisión de logs en test de error |
| CA-13 | La respuesta de Claude no se loggea completa — máximo 200 caracteres en Warning | Test unitario `ClaudeClient` con respuesta larga inválida |
| CA-14 | Si hay más de 50 libros no leídos, el prompt solo envía los 50 de mayor prioridad | Test unitario `SuggestionPromptBuilder` con 60 libros |
| CA-15 | Si el usuario no tiene libros leídos, el prompt envía `readBooks: []` sin error | Test unitario `SuggestionPromptBuilder` |
| CA-16 | `GET /api/queue/suggestions` retorna las últimas 20 sugerencias ordenadas por `GeneratedAt DESC` | Test integration |
| CA-17 | El campo `source` de los ítems de cola es `'AI'` cuando Claude contribuyó y `'Filter'` en fallback | Test integration con WireMock |
| CA-18 | El `QueueScoringService` del Spec 4 no fue modificado — solo se alimentó con nuevos datos | Verificación: ningún archivo del Spec 4 fue alterado salvo `GenerateQueue` → `GenerateQueueWithAI` en DI |
| CA-19 | El `ClaudeClient` es instanciable con un `ApiKey` vacío — la excepción ocurre en la llamada, no en el constructor | Test unitario |
| CA-20 | Los tests del `ClaudeClient` usan WireMock.Net — nunca llaman a la API real de Anthropic | Verificación: no existe `Claude__ApiKey` real en el entorno de tests |

---

## 15. Archivos que este spec genera

```
src/
  ReadingQueue.Domain/
    Entities/
      AISuggestion.cs
    ValueObjects/
      BookSuggestion.cs
    Interfaces/
      ILLMClient.cs
      IAISuggestionRepository.cs

  ReadingQueue.Application/
    UseCases/
      GenerateQueueWithAI.cs          ← reemplaza GenerateQueue.cs (Spec 4)

  ReadingQueue.Infrastructure/
    LLM/
      ClaudeClient.cs
      ClaudeOptions.cs
      ClaudeResiliencePipeline.cs
      SuggestionPromptBuilder.cs
    Data/
      SqlAISuggestionRepository.cs
    Sql/
      AISuggestionQueries.cs

  ReadingQueue.Api/
    Endpoints/
      QueueEndpoints.cs               ← modificar: GET /suggestions + POST /generate
    Responses/
      GenerateQueueResponse.cs        ← wrapper con aiContributed + queue
      QueueItemWithAIResponse.cs      ← extiende QueueItemResponse con aiReasoning
      AISuggestionResponse.cs

tests/
  ReadingQueue.Domain.Tests/
    AISuggestionTests.cs
    BookSuggestionTests.cs

  ReadingQueue.Application.Tests/
    UseCases/
      GenerateQueueWithAITests.cs     ← mock ILLMClient — sin WireMock
        ├── con Claude exitoso
        ├── con Claude retornando null (fallback)
        └── con biblioteca sin libros leídos

  ReadingQueue.Infrastructure.Tests/
    LLM/
      ClaudeClientTests.cs            ← WireMock.Net
        ├── respuesta JSON válida → sugerencias correctas
        ├── respuesta JSON inválido → null (fallback)
        ├── timeout → null (fallback)
        ├── HTTP 503 → retry → null (fallback)
        └── cancelación → OperationCanceledException
      SuggestionPromptBuilderTests.cs ← sin infraestructura
        ├── con libros leídos
        ├── sin libros leídos
        └── con más de 50 no leídos → trunca a 50
    Data/
      SqlAISuggestionRepositoryTests.cs  ← Testcontainers

  ReadingQueue.Api.Tests/
    QueueEndpointsAITests.cs           ← TestServer + JWT + WireMock
```
