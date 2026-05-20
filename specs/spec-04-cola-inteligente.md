# spec-04-cola-inteligente.md
# Feature: Cola Inteligente y Listas Especiales

## 1. Resumen

Implementar la cola de lectura activa del usuario: generación automática
mediante el algoritmo determinístico de scoring (prioridad 40% + variedad
de rotación 30% + antigüedad 10%), persistencia en `ReadingQueue`,
reordenamiento manual, y las tres listas especiales generadas con queries
dedicadas (Próximos 5, Cuando estoy cansado, Deuda histórica). Este spec
implementa también el endpoint `POST /api/queue/generate` como el punto
de entrada que el Spec 5 extenderá con el score de Claude (20%) —
dejando un hueco explícito y documentado para ese factor.

El tablero de estadísticas (`GET /api/stats/dashboard` y
`GET /api/stats/special-lists`) se incluye aquí porque sus queries
operan puramente sobre `Books` y `ReadingQueue`, sin dependencia de la
capa LLM.

---

## 2. Motivación

Con el Spec 3 el usuario tiene libros en su biblioteca. Este spec responde
la pregunta central de la app: **¿cuál debería leer primero?** El algoritmo
determinístico garantiza que siempre haya una cola válida, incluso antes
de que Claude entre en escena en el Spec 5. El Spec 5 no reemplaza este
algoritmo — lo complementa añadiendo el 20% del score IA al cálculo.
Construir este spec primero es la estrategia correcta: si Claude falla,
el sistema ya tiene una respuesta razonada que ofrecer.

---

## 3. Usuarios y Casos de Uso

| Actor | Caso de uso |
|---|---|
| Usuario autenticado | Ver su cola de lectura activa (top 20 libros ordenados) |
| Usuario autenticado | Generar o regenerar la cola con el algoritmo |
| Usuario autenticado | Reordenar manualmente la posición de los libros en la cola |
| Usuario autenticado | Eliminar un libro específico de la cola sin marcarlo como leído |
| Usuario autenticado | Ver las listas especiales (próximos 5, cansado, deuda histórica) |
| Usuario autenticado | Ver el tablero de estadísticas de su biblioteca |
| Sistema (Spec 3) | Eliminar un libro de la cola cuando se marca como leído |
| Sistema (Spec 5) | Leer la cola generada para mezclar con el score de Claude |

---

## 4. Requisitos Funcionales

### RF-01 — Ver cola activa
- Retorna los libros actualmente en la cola del usuario, ordenados por
  `Position ASC`.
- Cada ítem de la cola incluye los datos completos del libro más los
  metadatos de cola: `position`, `addedAt`, `source` (`'Manual'`,
  `'AI'`, `'Filter'`).
- Solo retorna libros no leídos — si por alguna razón un libro leído
  quedara en cola, se omite de la respuesta (filtro defensivo).
- Máximo 20 ítems.

### RF-02 — Generar cola (algoritmo determinístico)
- `POST /api/queue/generate` elimina la cola actual del usuario y
  construye una nueva con los libros no leídos aplicando el algoritmo
  de scoring definido en la constitution.
- El algoritmo opera en memoria sobre los libros traídos de BD —
  no con SQL ORDER BY — para permitir la lógica de round-robin de
  categorías, que no es expresable en SQL puro.
- El algoritmo asigna un `CompositeScore` a cada libro y ordena
  descendentemente. Los primeros 20 se persisten en `ReadingQueue`
  con `Source = 'Filter'`.
- Si el usuario no tiene libros no leídos, retorna la cola vacía
  sin error.
- El Spec 5 extenderá este endpoint para mezclar el score de Claude.
  Este spec deja el campo `aiScore` en `0.0` por ahora.
- Retorna la cola generada completa.

### RF-03 — Algoritmo de scoring (implementación exacta)

El score compuesto de cada libro se calcula así, en este orden:

```
normalizedPriority  = (Priority - 1) / 4.0          // mapea 1-5 → 0.0-1.0
normalizedAge       = 1 - (diasDesdeCreacion / maxDias) // libros más viejos → score mayor
varietyBonus        = 0.0 o 1.0                       // ver regla de rotación
aiScore             = 0.0                             // Spec 5 lo rellena

compositeScore = (normalizedPriority * 0.40)
               + (varietyBonus       * 0.30)
               + (aiScore            * 0.20)
               + (normalizedAge      * 0.10)
```

**Regla de variedad (`varietyBonus`):**
- Al ordenar la lista final, se aplica un algoritmo round-robin sobre
  `RotationCategory`.
- Se mantiene un contador de la última categoría colocada en la cola.
- Si el libro candidato tiene la misma categoría que el libro
  inmediatamente anterior en la cola, su `varietyBonus = 0.0`.
- Si tiene categoría diferente, su `varietyBonus = 1.0`.
- El round-robin se resuelve iterando por score descendente y
  reordenando para maximizar la alternancia de categorías.

**Normalización de antigüedad:**
- `maxDias` es el máximo de `(hoy - CreatedAt).TotalDays` entre todos
  los libros no leídos del usuario.
- Si solo hay un libro, `normalizedAge = 1.0`.
- Si `maxDias = 0` (todos creados hoy), `normalizedAge = 1.0` para todos.

### RF-04 — Reordenar cola manualmente
- `PUT /api/queue/reorder` acepta un array de `{ bookId, position }`
  que representa el nuevo orden deseado.
- Valida que todos los `bookId` del array pertenezcan a la cola actual
  del usuario. Si alguno no pertenece, retorna `422`.
- Las posiciones deben ser números enteros positivos sin repetidos.
  Si hay duplicados, retorna `422`.
- Actualiza las posiciones en `ReadingQueue` en una sola transacción.
- Retorna la cola completa con el nuevo orden.

### RF-05 — Eliminar libro de la cola
- `DELETE /api/queue/{bookId}` elimina el libro de `ReadingQueue` sin
  modificar el libro en `Books` (no lo marca como leído).
- Si el libro no está en la cola del usuario, retorna `404`.
- Retorna `204 No Content`.

### RF-06 — Listas especiales (queries dinámicas)
Las tres listas se calculan en el momento de la petición — no se
almacenan. Todas filtran `UserId = @UserId` e `IsRead = 0`.

**⭐ Próximos 5:**
- Los 5 libros no leídos con mayor `Priority`, con variedad de
  `RotationCategory` — si hay empate en prioridad, se elige el de
  categoría más diferente a los ya seleccionados.
- Implementado en memoria sobre los resultados de `GetUnreadByUserAsync`.

**😴 Cuando estoy cansado:**
- Libros no leídos con `MentalEnergy = '🟢 Baja – cualquier momento'`,
  ordenados por `Priority DESC`, luego `CreatedAt ASC`.
- Sin límite de cantidad — retorna todos los que cumplan el filtro.

**🏛️ Deuda histórica:**
- Libros no leídos con `Genre = 'Clásico'` o `Genre = 'Novela clásica'`,
  ordenados por `Priority DESC`, luego `CreatedAt ASC`.
- Sin límite de cantidad — retorna todos los que cumplan el filtro.

### RF-07 — Tablero de estadísticas
`GET /api/stats/dashboard` retorna métricas calculadas con queries SQL
directas sobre `Books` del usuario:

- `totalBooks`: total de libros en la biblioteca.
- `readBooks`: total de libros leídos.
- `unreadBooks`: total de libros no leídos.
- `readPercentage`: porcentaje leído (0-100, redondeado a 1 decimal).
- `byGenre`: array de `{ genre, total, read, unread }` para cada género.
- `byRotationCategory`: array de `{ category, total, read, unread }`.
- `byMentalEnergy`: array de `{ level, total, unread }` ordenado por
  `SortOrder ASC` de la tabla `MentalEnergyLevels`.
- `byCountry`: array de `{ country, total }` ordenado por `total DESC`,
  top 10.
- `topUnreadPriority`: los 3 libros no leídos con mayor prioridad
  (mismos campos que `Book`).
- `recentlyRead`: los 5 libros marcados como leídos más recientemente
  ordenados por `ReadAt DESC`.

### RF-08 — Listas especiales (endpoint)
`GET /api/stats/special-lists` retorna las tres listas del RF-06 en un
solo response para que el frontend haga una sola llamada:

```json
{
  "next5": [ ...books ],
  "whenTired": [ ...books ],
  "historicalDebt": [ ...books ]
}
```

---

## 5. Requisitos No Funcionales

- **El algoritmo vive en Application, no en Infrastructure.** El
  `QueueScoringService` es un servicio de dominio puro en
  `ReadingQueue.Application/Services/` — sin dependencias de Dapper,
  SQL Server ni HTTP. Es completamente testeable sin contenedores.
- **Sin SQL de ordenamiento para el algoritmo.** Los libros se traen de
  BD sin ORDER BY relevante y se ordenan en memoria por el
  `QueueScoringService`. SQL solo se usa para persistir el resultado.
- **Transacción en generación de cola.** El `POST /api/queue/generate`
  primero elimina todos los ítems actuales de la cola del usuario, luego
  inserta los nuevos — todo en una sola `IDbTransaction`.
- **Aislamiento por usuario.** Todo query a `ReadingQueue` filtra por
  `UserId`. El reordenamiento valida que los `bookId` pertenezcan al
  usuario antes de actualizar.
- **El score de Claude es 0.0 en este spec.** El campo existe en el
  modelo y en la interfaz del servicio de scoring desde ya, para que
  el Spec 5 solo necesite rellenarlo — sin tocar la estructura.

---

## 6. Modelo de Dominio

```csharp
// src/ReadingQueue.Domain/Entities/QueueItem.cs
public sealed class QueueItem
{
    public int Id { get; }
    public int UserId { get; }
    public int BookId { get; }
    public int Position { get; }
    public DateTime AddedAt { get; }
    public string Source { get; }   // 'Manual' | 'AI' | 'Filter'
    public Book Book { get; }       // navegación — poblada por el repositorio

    public QueueItem(int id, int userId, int bookId, int position,
                     DateTime addedAt, string source, Book book)
    {
        Id       = id;
        UserId   = userId;
        BookId   = bookId;
        Position = position;
        AddedAt  = addedAt;
        Source   = source;
        Book     = book;
    }
}

// src/ReadingQueue.Domain/ValueObjects/ScoredBook.cs
// Resultado intermedio del algoritmo — nunca se persiste directamente
public sealed record ScoredBook(
    Book   Book,
    double NormalizedPriority,  // 0.0 – 1.0
    double VarietyBonus,        // 0.0 o 1.0
    double AiScore,             // 0.0 en Spec 4; Spec 5 lo rellena
    double NormalizedAge,       // 0.0 – 1.0
    double CompositeScore       // suma ponderada final
);

// src/ReadingQueue.Domain/ValueObjects/DashboardStats.cs
public sealed record DashboardStats(
    int   TotalBooks,
    int   ReadBooks,
    int   UnreadBooks,
    double ReadPercentage,
    IReadOnlyList<GenreStat>          ByGenre,
    IReadOnlyList<RotationStat>       ByRotationCategory,
    IReadOnlyList<MentalEnergyStat>   ByMentalEnergy,
    IReadOnlyList<CountryStat>        ByCountry,
    IReadOnlyList<Book>               TopUnreadPriority,
    IReadOnlyList<Book>               RecentlyRead
);

public sealed record GenreStat(string Genre, int Total, int Read, int Unread);
public sealed record RotationStat(string Category, int Total, int Read, int Unread);
public sealed record MentalEnergyStat(string Level, int Total, int Unread);
public sealed record CountryStat(string Country, int Total);

// src/ReadingQueue.Domain/ValueObjects/SpecialLists.cs
public sealed record SpecialLists(
    IReadOnlyList<Book> Next5,
    IReadOnlyList<Book> WhenTired,
    IReadOnlyList<Book> HistoricalDebt
);

// src/ReadingQueue.Domain/Interfaces/IQueueRepository.cs
public interface IQueueRepository
{
    Task<IEnumerable<QueueItem>> GetByUserAsync(
        int userId, CancellationToken ct = default);

    Task ReplaceQueueAsync(
        int userId,
        IEnumerable<(int bookId, int position, string source)> items,
        IDbTransaction tx,
        CancellationToken ct = default);

    Task UpdatePositionsAsync(
        int userId,
        IEnumerable<(int bookId, int position)> positions,
        IDbTransaction tx,
        CancellationToken ct = default);

    Task RemoveItemAsync(
        int userId, int bookId, CancellationToken ct = default);

    Task<bool> ContainsBookAsync(
        int userId, int bookId, CancellationToken ct = default);
}

// src/ReadingQueue.Domain/Interfaces/IStatsRepository.cs
public interface IStatsRepository
{
    Task<DashboardStats> GetDashboardAsync(
        int userId, CancellationToken ct = default);
}
```

---

## 7. Servicio de Scoring (Application)

```csharp
// src/ReadingQueue.Application/Services/QueueScoringService.cs

/// <summary>
/// Algoritmo determinístico de scoring de la cola de lectura.
/// Sin dependencias de infraestructura — 100% testeable en memoria.
/// El Spec 5 extiende este servicio pasando aiScores poblados por Claude.
/// </summary>
public sealed class QueueScoringService
{
    private static readonly string[] RotationOrder =
    [
        "Ensayo / no ficción",
        "Libro corto o cuentos",
        "Clásico",
        "Novela grande",
        "Contemporáneo latinoamericano o raro"
    ];

    /// <param name="unreadBooks">Libros no leídos del usuario.</param>
    /// <param name="aiScores">
    ///   Dic bookId → score IA (0.0–10.0). En Spec 4 siempre vacío.
    ///   El Spec 5 lo rellena antes de llamar a este método.
    /// </param>
    /// <returns>Lista ordenada de hasta 20 ScoredBook, position 1..N.</returns>
    public IReadOnlyList<ScoredBook> Score(
        IEnumerable<Book> unreadBooks,
        IReadOnlyDictionary<int, double>? aiScores = null)
    {
        var books = unreadBooks.ToList();
        if (books.Count == 0) return [];

        aiScores ??= new Dictionary<int, double>();

        // 1. Normalizar antigüedad
        var maxDays = books
            .Max(b => (DateTime.UtcNow - b.CreatedAt).TotalDays);
        if (maxDays == 0) maxDays = 1;

        // 2. Calcular score preliminar (sin varietyBonus aún)
        var preliminary = books.Select(b =>
        {
            var normalizedPriority = (b.Priority - 1) / 4.0;
            var normalizedAge      = 1.0 - (DateTime.UtcNow - b.CreatedAt)
                                               .TotalDays / maxDays;
            var aiScore = aiScores.TryGetValue(b.Id, out var s)
                          ? s / 10.0   // normaliza 0-10 → 0-1
                          : 0.0;

            // varietyBonus se asigna en el paso siguiente
            return new ScoredBook(b, normalizedPriority, 0.0, aiScore,
                                  normalizedAge, 0.0);
        })
        .OrderByDescending(sb =>
              sb.NormalizedPriority * 0.40
            + sb.AiScore            * 0.20
            + sb.NormalizedAge      * 0.10)
        .ToList();

        // 3. Aplicar round-robin de categorías (varietyBonus)
        var result          = new List<ScoredBook>(Math.Min(books.Count, 20));
        var remaining       = preliminary.ToList();
        string? lastCategory = null;

        while (result.Count < 20 && remaining.Count > 0)
        {
            // Buscar el mejor candidato con categoría distinta a la última
            var candidate = remaining.FirstOrDefault(
                                sb => sb.Book.RotationCategory != lastCategory)
                         ?? remaining.First(); // si todos son igual categoría

            var withBonus = candidate with
            {
                VarietyBonus   = candidate.Book.RotationCategory != lastCategory
                                 ? 1.0 : 0.0,
                CompositeScore = candidate.NormalizedPriority * 0.40
                               + (candidate.Book.RotationCategory != lastCategory
                                  ? 1.0 : 0.0) * 0.30
                               + candidate.AiScore * 0.20
                               + candidate.NormalizedAge * 0.10
            };

            result.Add(withBonus);
            lastCategory = candidate.Book.RotationCategory;
            remaining.Remove(candidate);
        }

        return result;
    }

    /// <summary>
    /// Genera los Próximos 5 con variedad de categorías.
    /// Reutiliza el mismo algoritmo de round-robin sobre todos los no leídos.
    /// </summary>
    public IReadOnlyList<Book> GetNext5(IEnumerable<Book> unreadBooks)
        => Score(unreadBooks).Take(5).Select(sb => sb.Book).ToList();
}
```

---

## 8. Use Cases (Application)

```csharp
// src/ReadingQueue.Application/UseCases/GenerateQueue.cs
public sealed class GenerateQueue
{
    private readonly IBookRepository    _books;
    private readonly IQueueRepository   _queue;
    private readonly QueueScoringService _scoring;
    private readonly IDbConnectionFactory _factory;

    public record Command(int UserId);
    // El Spec 5 extenderá este use case pasando aiScores poblados por Claude.
    // Para ese propósito existe el parámetro opcional en QueueScoringService.Score().

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Command cmd, CancellationToken ct)
    {
        var unread = (await _books.GetUnreadByUserAsync(cmd.UserId, ct)).ToList();

        // Score en memoria — aiScores vacío en Spec 4
        var scored = _scoring.Score(unread, aiScores: null);

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var items = scored.Select((sb, idx) =>
                (sb.Book.Id, idx + 1, "Filter"));

            await _queue.ReplaceQueueAsync(cmd.UserId, items, tx, ct);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        // Retornar la cola recién creada
        return (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
    }
}

// src/ReadingQueue.Application/UseCases/GetQueue.cs
public sealed class GetQueue
{
    private readonly IQueueRepository _queue;

    public record Query(int UserId);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Query q, CancellationToken ct)
        => (await _queue.GetByUserAsync(q.UserId, ct)).ToList();
}

// src/ReadingQueue.Application/UseCases/ReorderQueue.cs
public sealed class ReorderQueue
{
    private readonly IQueueRepository   _queue;
    private readonly IDbConnectionFactory _factory;

    public record Command(int UserId, IReadOnlyList<QueueItemPosition> Positions);
    public record QueueItemPosition(int BookId, int Position);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Command cmd, CancellationToken ct)
    {
        // 1. Validar que no hay posiciones duplicadas
        var positions = cmd.Positions.ToList();
        if (positions.Select(p => p.Position).Distinct().Count() != positions.Count)
            throw new ValidationException("positions", "Hay posiciones duplicadas.");

        // 2. Validar que todos los bookIds están en la cola del usuario
        var currentQueue = (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
        var currentBookIds = currentQueue.Select(q => q.BookId).ToHashSet();
        var invalidIds = positions
            .Select(p => p.BookId)
            .Where(id => !currentBookIds.Contains(id))
            .ToList();

        if (invalidIds.Count > 0)
            throw new ValidationException("bookIds",
                $"Los libros {string.Join(", ", invalidIds)} no están en la cola.");

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await _queue.UpdatePositionsAsync(
                cmd.UserId,
                positions.Select(p => (p.BookId, p.Position)),
                tx, ct);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        return (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
    }
}

// src/ReadingQueue.Application/UseCases/RemoveFromQueue.cs
public sealed class RemoveFromQueue
{
    private readonly IQueueRepository _queue;

    public record Command(int UserId, int BookId);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct)
    {
        var exists = await _queue.ContainsBookAsync(cmd.UserId, cmd.BookId, ct);
        if (!exists) throw new BookNotFoundException(cmd.BookId);

        await _queue.RemoveItemAsync(cmd.UserId, cmd.BookId, ct);
    }
}

// src/ReadingQueue.Application/UseCases/GetSpecialLists.cs
public sealed class GetSpecialLists
{
    private readonly IBookRepository    _books;
    private readonly QueueScoringService _scoring;

    public record Query(int UserId);

    public async Task<SpecialLists> ExecuteAsync(Query q, CancellationToken ct)
    {
        var allUnread = (await _books.GetUnreadByUserAsync(q.UserId, ct)).ToList();

        var next5 = _scoring.GetNext5(allUnread);

        var whenTired = allUnread
            .Where(b => b.MentalEnergy == "🟢 Baja – cualquier momento")
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        var historicalDebt = allUnread
            .Where(b => b.Genre is "Clásico" or "Novela clásica")
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        return new SpecialLists(next5, whenTired, historicalDebt);
    }
}

// src/ReadingQueue.Application/UseCases/GetDashboardStats.cs
public sealed class GetDashboardStats
{
    private readonly IStatsRepository _stats;

    public record Query(int UserId);

    public async Task<DashboardStats> ExecuteAsync(Query q, CancellationToken ct)
        => await _stats.GetDashboardAsync(q.UserId, ct);
}
```

---

## 9. Contrato de API

### GET `/api/queue`
Retorna la cola activa del usuario ordenada por posición.

**Response `200 OK`:**
```json
[
  {
    "position": 1,
    "addedAt": "2026-05-04T10:00:00Z",
    "source": "Filter",
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
```

---

### POST `/api/queue/generate`
Genera o regenera la cola con el algoritmo de scoring.

**Request:** sin body.

**Response `200 OK`:** mismo shape que `GET /api/queue` con la cola
recién generada.

**Notas:**
- Si el usuario no tiene libros no leídos, retorna array vacío `[]`.
- En Spec 4, el campo `source` de todos los ítems es `'Filter'`.
  En Spec 5 pasará a `'AI'` cuando Claude contribuya al scoring.

---

### PUT `/api/queue/reorder`
Reordena manualmente la cola.

**Request:**
```json
{
  "positions": [
    { "bookId": 12, "position": 1 },
    { "bookId": 7,  "position": 2 },
    { "bookId": 3,  "position": 3 }
  ]
}
```

**Response `200 OK`:** cola completa con el nuevo orden.

**Responses de error:**
- `422 Unprocessable Entity` — posiciones duplicadas o `bookId` no
  pertenece a la cola del usuario

---

### DELETE `/api/queue/{bookId}`
Elimina un libro de la cola sin marcarlo como leído.

**Response `204 No Content`**

**Responses de error:**
- `404 Not Found` — el libro no está en la cola del usuario

---

### GET `/api/stats/dashboard`
Retorna las estadísticas completas de la biblioteca.

**Response `200 OK`:**
```json
{
  "totalBooks": 313,
  "readBooks": 28,
  "unreadBooks": 285,
  "readPercentage": 8.9,
  "byGenre": [
    { "genre": "No ficción / ensayo", "total": 87, "read": 10, "unread": 77 },
    { "genre": "Clásico",             "total": 52, "read": 5,  "unread": 47 }
  ],
  "byRotationCategory": [
    { "category": "Ensayo / no ficción", "total": 87, "read": 10, "unread": 77 }
  ],
  "byMentalEnergy": [
    { "level": "🟢 Baja – cualquier momento", "total": 45, "unread": 38 }
  ],
  "byCountry": [
    { "country": "Colombia", "total": 42 },
    { "country": "Argentina", "total": 38 }
  ],
  "topUnreadPriority": [
    { "id": 7, "title": "El otoño del patriarca", "priority": 5, "..." : "..." }
  ],
  "recentlyRead": [
    { "id": 12, "title": "Cien años de soledad", "readAt": "2026-04-28T00:00:00Z", "...": "..." }
  ]
}
```

---

### GET `/api/stats/special-lists`
Retorna las tres listas especiales en una sola llamada.

**Response `200 OK`:**
```json
{
  "next5": [
    { "id": 7, "title": "El otoño del patriarca", "priority": 5, "rotationCategory": "Novela grande", "...": "..." }
  ],
  "whenTired": [
    { "id": 22, "title": "Ficciones", "priority": 4, "mentalEnergy": "🟢 Baja – cualquier momento", "...": "..." }
  ],
  "historicalDebt": [
    { "id": 31, "title": "La Odisea", "priority": 3, "genre": "Clásico", "...": "..." }
  ]
}
```

---

## 10. Implementación de Infraestructura

### Queries SQL

```csharp
// src/ReadingQueue.Infrastructure/Sql/QueueQueries.cs
internal static class QueueQueries
{
    internal const string GetByUser = """
        SELECT
            rq.Id, rq.UserId, rq.BookId, rq.Position, rq.AddedAt, rq.Source,
            b.Id        AS Book_Id,
            b.UserId    AS Book_UserId,
            b.Title     AS Book_Title,
            b.Author    AS Book_Author,
            b.Genre     AS Book_Genre,
            b.Country   AS Book_Country,
            b.WhyRead   AS Book_WhyRead,
            b.Priority  AS Book_Priority,
            b.MentalEnergy     AS Book_MentalEnergy,
            b.RecommendedMood  AS Book_RecommendedMood,
            b.RotationCategory AS Book_RotationCategory,
            b.IsRead    AS Book_IsRead,
            b.ReadAt    AS Book_ReadAt,
            b.Notes     AS Book_Notes,
            b.CreatedAt AS Book_CreatedAt,
            b.UpdatedAt AS Book_UpdatedAt
        FROM ReadingQueue rq
        INNER JOIN Books b ON rq.BookId = b.Id
        WHERE rq.UserId = @UserId
          AND b.IsRead  = 0
        ORDER BY rq.Position ASC;
        """;

    internal const string DeleteByUser = """
        DELETE FROM ReadingQueue
        WHERE UserId = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO ReadingQueue (UserId, BookId, Position, Source)
        VALUES (@UserId, @BookId, @Position, @Source);
        """;

    internal const string UpdatePosition = """
        UPDATE ReadingQueue
        SET Position = @Position
        WHERE UserId = @UserId AND BookId = @BookId;
        """;

    internal const string DeleteByBook = """
        DELETE FROM ReadingQueue
        WHERE UserId = @UserId AND BookId = @BookId;
        """;

    internal const string ExistsBook = """
        SELECT COUNT(1)
        FROM ReadingQueue
        WHERE UserId = @UserId AND BookId = @BookId;
        """;
}

// src/ReadingQueue.Infrastructure/Sql/StatsQueries.cs
internal static class StatsQueries
{
    internal const string GetCounts = """
        SELECT
            COUNT(*)                              AS TotalBooks,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END) AS ReadBooks,
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END) AS UnreadBooks
        FROM Books
        WHERE UserId = @UserId;
        """;

    internal const string GetByGenre = """
        SELECT
            Genre,
            COUNT(*)                              AS Total,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END) AS Read,
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END) AS Unread
        FROM Books
        WHERE UserId = @UserId
        GROUP BY Genre
        ORDER BY Total DESC;
        """;

    internal const string GetByRotationCategory = """
        SELECT
            RotationCategory AS Category,
            COUNT(*)         AS Total,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END) AS Read,
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END) AS Unread
        FROM Books
        WHERE UserId = @UserId
        GROUP BY RotationCategory
        ORDER BY Total DESC;
        """;

    internal const string GetByMentalEnergy = """
        SELECT
            b.MentalEnergy AS Level,
            COUNT(*)       AS Total,
            SUM(CASE WHEN b.IsRead = 0 THEN 1 ELSE 0 END) AS Unread
        FROM Books b
        INNER JOIN MentalEnergyLevels mel ON b.MentalEnergy = mel.Name
        WHERE b.UserId = @UserId
        GROUP BY b.MentalEnergy, mel.SortOrder
        ORDER BY mel.SortOrder ASC;
        """;

    internal const string GetByCountryTop10 = """
        SELECT TOP 10
            Country,
            COUNT(*) AS Total
        FROM Books
        WHERE UserId = @UserId
        GROUP BY Country
        ORDER BY Total DESC;
        """;

    internal const string GetTopUnreadPriority = """
        SELECT TOP 3
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 0
        ORDER BY Priority DESC, CreatedAt ASC;
        """;

    internal const string GetRecentlyRead = """
        SELECT TOP 5
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 1
        ORDER BY ReadAt DESC;
        """;
}
```

### Repositorio de cola con multi-insert eficiente

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlQueueRepository.cs
public sealed class SqlQueueRepository : IQueueRepository
{
    private readonly IDbConnectionFactory _factory;

    public async Task ReplaceQueueAsync(
        int userId,
        IEnumerable<(int bookId, int position, string source)> items,
        IDbTransaction tx,
        CancellationToken ct)
    {
        var conn = tx.Connection!;

        // 1. Borrar cola actual del usuario
        await conn.ExecuteAsync(
            QueueQueries.DeleteByUser,
            new { UserId = userId },
            transaction: tx);

        // 2. Insertar nuevos ítems — uno por uno con Dapper
        //    (SQL Server no tiene INSERT INTO ... VALUES (...), (...) nativo en Dapper
        //    sin TVP; para ≤20 filas el loop es aceptable)
        foreach (var (bookId, position, source) in items)
        {
            await conn.ExecuteAsync(
                QueueQueries.Insert,
                new { UserId = userId, BookId = bookId,
                      Position = position, Source = source },
                transaction: tx);
        }
    }
}
```

### Registro en Program.cs

```csharp
// Fragmento a agregar en src/ReadingQueue.Api/Program.cs

// ── Repositorios ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IQueueRepository, SqlQueueRepository>();
builder.Services.AddScoped<IStatsRepository, SqlStatsRepository>();

// ── Servicios de Application ─────────────────────────────────────────────────
builder.Services.AddScoped<QueueScoringService>();

// ── Use cases ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<GenerateQueue>();
builder.Services.AddScoped<GetQueue>();
builder.Services.AddScoped<ReorderQueue>();
builder.Services.AddScoped<RemoveFromQueue>();
builder.Services.AddScoped<GetSpecialLists>();
builder.Services.AddScoped<GetDashboardStats>();

// ── Endpoints ───────────────────────────────────────────────────────────────
var queue = app.MapGroup("/api/queue").RequireAuthorization();

queue.MapGet("/",              QueueEndpoints.GetQueue)
     .WithName("GetQueue").WithSummary("Ver cola activa").WithTags("Queue");

queue.MapPost("/generate",     QueueEndpoints.Generate)
     .WithName("GenerateQueue").WithSummary("Generar cola").WithTags("Queue");

queue.MapPut("/reorder",       QueueEndpoints.Reorder)
     .WithName("ReorderQueue").WithSummary("Reordenar cola").WithTags("Queue");

queue.MapDelete("/{bookId:int}", QueueEndpoints.Remove)
     .WithName("RemoveFromQueue").WithSummary("Eliminar de la cola").WithTags("Queue");

var stats = app.MapGroup("/api/stats").RequireAuthorization();

stats.MapGet("/dashboard",     StatsEndpoints.GetDashboard)
     .WithName("GetDashboard").WithSummary("Tablero de estadísticas").WithTags("Stats");

stats.MapGet("/special-lists", StatsEndpoints.GetSpecialLists)
     .WithName("GetSpecialLists").WithSummary("Listas especiales").WithTags("Stats");
```

---

## 11. Criterios de Aceptación

| ID | Criterio | Verificación |
|---|---|---|
| CA-01 | `GET /api/queue` retorna los ítems ordenados por `Position ASC` | Test integration |
| CA-02 | `GET /api/queue` nunca retorna libros leídos aunque estén en la tabla | Test integration: marcar leído un libro en cola y consultar |
| CA-03 | `GET /api/queue` nunca retorna ítems de otro usuario | Test integration con 2 usuarios |
| CA-04 | `POST /api/queue/generate` con biblioteca vacía retorna `[]` sin error | Test integration |
| CA-05 | `POST /api/queue/generate` persiste máximo 20 ítems en `ReadingQueue` | Test integration: usuario con 30 libros no leídos |
| CA-06 | `POST /api/queue/generate` no repite la misma `RotationCategory` en posiciones consecutivas cuando hay variedad disponible | Test unitario `QueueScoringService` |
| CA-07 | Un libro con `Priority=5` aparece antes que uno con `Priority=3` en igualdad de otras condiciones | Test unitario `QueueScoringService` |
| CA-08 | Un libro más antiguo en la biblioteca aparece antes que uno reciente en igualdad de prioridad y categoría | Test unitario `QueueScoringService` |
| CA-09 | `POST /api/queue/generate` es atómico: si falla el insert, no queda la cola vacía | Test unitario con mock que lanza en el segundo insert |
| CA-10 | `PUT /api/queue/reorder` actualiza las posiciones correctamente | Test integration con SQL directo |
| CA-11 | `PUT /api/queue/reorder` con `bookId` ajeno a la cola retorna `422` | Test integration |
| CA-12 | `PUT /api/queue/reorder` con posiciones duplicadas retorna `422` | Test unitario use case |
| CA-13 | `DELETE /api/queue/{bookId}` elimina el ítem y retorna `204` | Test integration |
| CA-14 | `DELETE /api/queue/{bookId}` para libro no en cola retorna `404` | Test integration |
| CA-15 | `GET /api/stats/dashboard` retorna `totalBooks` correcto | Test integration |
| CA-16 | `GET /api/stats/dashboard` `readPercentage` se calcula correctamente | Test integration: 1 leído de 4 → 25.0 |
| CA-17 | `GET /api/stats/dashboard` `byMentalEnergy` está ordenado por `SortOrder ASC` | Test integration |
| CA-18 | `GET /api/stats/dashboard` `byCountry` retorna máximo 10 países | Test integration con 12 países distintos |
| CA-19 | `GET /api/stats/special-lists` `next5` retorna exactamente 5 libros (o menos si no hay suficientes) | Test integration |
| CA-20 | `GET /api/stats/special-lists` `whenTired` solo contiene libros con `MentalEnergy = '🟢 Baja – cualquier momento'` | Test integration |
| CA-21 | `GET /api/stats/special-lists` `historicalDebt` solo contiene libros de género `'Clásico'` o `'Novela clásica'` | Test integration |
| CA-22 | `QueueScoringService.Score()` con `aiScores` vacío produce el mismo resultado que con `aiScores = null` | Test unitario |
| CA-23 | `QueueScoringService` es instanciable sin ninguna dependencia de infraestructura | Verificación en compilación |
| CA-24 | El campo `aiScore = 0.0` en todos los `ScoredBook` cuando no se pasan scores | Test unitario |

---

## 12. Archivos que este spec genera

```
src/
  ReadingQueue.Domain/
    Entities/
      QueueItem.cs
    ValueObjects/
      ScoredBook.cs
      DashboardStats.cs
      GenreStat.cs
      RotationStat.cs
      MentalEnergyStat.cs
      CountryStat.cs
      SpecialLists.cs
    Interfaces/
      IQueueRepository.cs
      IStatsRepository.cs

  ReadingQueue.Application/
    Services/
      QueueScoringService.cs
    UseCases/
      GenerateQueue.cs
      GetQueue.cs
      ReorderQueue.cs
      RemoveFromQueue.cs
      GetSpecialLists.cs
      GetDashboardStats.cs

  ReadingQueue.Infrastructure/
    Data/
      SqlQueueRepository.cs
      SqlStatsRepository.cs
    Sql/
      QueueQueries.cs
      StatsQueries.cs

  ReadingQueue.Api/
    Endpoints/
      QueueEndpoints.cs
      StatsEndpoints.cs
    Requests/
      ReorderQueueRequest.cs
    Responses/
      QueueItemResponse.cs
      DashboardStatsResponse.cs
      SpecialListsResponse.cs

tests/
  ReadingQueue.Domain.Tests/
    QueueItemTests.cs
    ScoredBookTests.cs

  ReadingQueue.Application.Tests/
    Services/
      QueueScoringServiceTests.cs      ← sin Testcontainers, 100% en memoria
    UseCases/
      GenerateQueueTests.cs
      ReorderQueueTests.cs
      RemoveFromQueueTests.cs
      GetSpecialListsTests.cs
      GetDashboardStatsTests.cs

  ReadingQueue.Infrastructure.Tests/
    Data/
      SqlQueueRepositoryTests.cs       ← Testcontainers
      SqlStatsRepositoryTests.cs       ← Testcontainers

  ReadingQueue.Api.Tests/
    QueueEndpointsTests.cs             ← TestServer + JWT real
    StatsEndpointsTests.cs             ← TestServer + JWT real
```
