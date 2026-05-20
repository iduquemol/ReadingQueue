# SPEC-04 · Tasks — Cola Inteligente y Listas Especiales
> Proyecto: `ReadingQueue` (solución .NET 9 + React/Vite)
> Regla: **Tests primero, implementación después** (constitution §13)
> Cobertura mínima: **80%** en Domain y Application · Integración con Testcontainers en Infrastructure

---

## Nota previa: valores de referencia del seed

Los valores de la base de datos real (en `002_seed_reference_data.sql`) son **sin emojis ni tildes**. El spec-04 en varios lugares los muestra con emojis (tomados del spec original), pero los tests deben usar los valores exactos de la BD:

| Concepto | Valor en spec-04 (incorrecto para tests) | Valor real en BD (correcto) |
|---|---|---|
| MentalEnergy baja | `"🟢 Baja – cualquier momento"` | `"Baja - cualquier momento"` |
| Género clásico | `"Clásico"` | `"Clasico"` |
| Género novela clásica | `"Novela clásica"` | `"Novela clasica"` |
| Rotación clásico | `"Clásico"` | `"Clasico"` |
| Rotación ensayo | `"Ensayo / no ficción"` | `"Ensayo / no ficcion"` |
| Rotación latinoam. | `"Contemporáneo latinoamericano o raro"` | `"Contemporaneo latinoamericano o raro"` |

**El `QueueScoringService.RotationOrder[]` debe usar los valores reales de BD**, no los del spec-04 literal.

## Nota previa: Dapper y entidades con JOIN

`QueueItem` contiene una propiedad `Book` (navegación). Para que Dapper pueda materializar `QueueItem` sin errores (como el que apareció en Spec-03 con `Book.Priority: TINYINT → byte`), **todas las entidades nuevas deben diseñarse con `{ get; init; }` + constructor sin parámetros desde el inicio**. La columna `Priority` de SQL Server es `TINYINT` — Dapper la mapea como `byte`; los setters `init` manejan la conversión automáticamente.

Para el query de cola (JOIN entre `ReadingQueue` y `Books`), el repositorio usará Dapper multi-map con `splitOn: "BookId"` — dos tipos: `QueueItem` y `Book`, combinados en la función de mapeo.

---

## Bloque A — Domain: Entidades, Value Objects e Interfaces

### TASK-04-A1 · Test: entidad `QueueItem`

- **Archivo test:** `tests/ReadingQueue.Domain.Tests/QueueItemTests.cs`
- **Casos:**
  - [ ] Constructor de `QueueItem` asigna correctamente las 7 propiedades.
  - [ ] `Source` puede ser `"Manual"`, `"AI"` o `"Filter"` sin excepción.
  - [ ] `Book` se asigna correctamente en el constructor.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-A2 · Test: value object `ScoredBook`

- **Archivo test:** `tests/ReadingQueue.Domain.Tests/ScoredBookTests.cs`
- **Casos:**
  - [ ] `ScoredBook` con `AiScore = 0.0` se construye sin excepción.
  - [ ] `CompositeScore` está dentro del rango `[0.0, 1.0]` para valores de entrada válidos.
  - [ ] Es un record — `with` expression crea una copia modificada sin afectar el original.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-A3 · Implementar entidades, value objects e interfaces de dominio

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Entities/
    QueueItem.cs
  ValueObjects/
    ScoredBook.cs
    DashboardStats.cs        ← incluye GenreStat, RotationStat, MentalEnergyStat, CountryStat
    SpecialLists.cs
  Interfaces/
    IQueueRepository.cs
    IStatsRepository.cs
```

```csharp
// src/ReadingQueue.Domain/Entities/QueueItem.cs
namespace ReadingQueue.Domain.Entities;

public sealed class QueueItem
{
    public int      Id       { get; init; }
    public int      UserId   { get; init; }
    public int      BookId   { get; init; }
    public int      Position { get; init; }
    public DateTime AddedAt  { get; init; }
    public string   Source   { get; init; } = null!;
    public Book     Book     { get; init; } = null!;

    public QueueItem() { }

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
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/ScoredBook.cs
namespace ReadingQueue.Domain.ValueObjects;

public sealed record ScoredBook(
    Book   Book,
    double NormalizedPriority,
    double VarietyBonus,
    double AiScore,
    double NormalizedAge,
    double CompositeScore
);
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/DashboardStats.cs
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.ValueObjects;

public sealed record DashboardStats(
    int    TotalBooks,
    int    ReadBooks,
    int    UnreadBooks,
    double ReadPercentage,
    IReadOnlyList<GenreStat>        ByGenre,
    IReadOnlyList<RotationStat>     ByRotationCategory,
    IReadOnlyList<MentalEnergyStat> ByMentalEnergy,
    IReadOnlyList<CountryStat>      ByCountry,
    IReadOnlyList<Book>             TopUnreadPriority,
    IReadOnlyList<Book>             RecentlyRead
);

public sealed record GenreStat(string Genre, int Total, int Read, int Unread);
public sealed record RotationStat(string Category, int Total, int Read, int Unread);
public sealed record MentalEnergyStat(string Level, int Total, int Unread);
public sealed record CountryStat(string Country, int Total);
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/SpecialLists.cs
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.ValueObjects;

public sealed record SpecialLists(
    IReadOnlyList<Book> Next5,
    IReadOnlyList<Book> WhenTired,
    IReadOnlyList<Book> HistoricalDebt
);
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IQueueRepository.cs
using System.Data;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

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
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IStatsRepository.cs
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface IStatsRepository
{
    Task<DashboardStats> GetDashboardAsync(
        int userId, CancellationToken ct = default);
}
```

- **Completado cuando:** tests de TASK-04-A1 y TASK-04-A2 pasan (verde) y `dotnet build` → `0 Error(s)`.

---

## Bloque B — Application: `QueueScoringService`

> Servicio puro — sin dependencias de infraestructura. Testeable 100% en memoria.

### TASK-04-B1 · Test: `QueueScoringService`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/Services/QueueScoringServiceTests.cs`
- **Nota:** Usar los valores reales del seed para `RotationCategory` (sin tildes ni emojis).

- **Casos:**
  - [ ] Lista vacía de libros → retorna lista vacía (CA-04).
  - [ ] Un libro con `Priority=5` aparece antes que uno con `Priority=3`, con igualdad de otras condiciones (CA-07).
  - [ ] Un libro creado hace 10 días aparece antes que uno creado hoy, con igualdad de prioridad y categoría (CA-08).
  - [ ] No repite la misma `RotationCategory` en posiciones consecutivas cuando hay variedad disponible (CA-06).
  - [ ] Con 30 libros no leídos → retorna exactamente 20 elementos (CA-05).
  - [ ] `AiScore` es `0.0` en todos los `ScoredBook` cuando `aiScores = null` (CA-24).
  - [ ] `aiScores = null` produce el mismo resultado que `aiScores = {}` vacío (CA-22).
  - [ ] `GetNext5` retorna exactamente 5 libros si hay al menos 5 no leídos.
  - [ ] `GetNext5` retorna menos de 5 si hay menos de 5 libros disponibles.
  - [ ] Con un solo libro → `NormalizedAge = 1.0` (caso de único elemento).
  - [ ] Todos los libros creados el mismo día → `NormalizedAge = 1.0` para todos (`maxDias = 0` tratado como `1`).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-B2 · Implementar `QueueScoringService`

- **Archivo:** `src/ReadingQueue.Application/Services/QueueScoringService.cs`

```csharp
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Services;

public sealed class QueueScoringService
{
    // Valores reales del seed (sin tildes ni emojis)
    private static readonly string[] RotationOrder =
    [
        "Ensayo / no ficcion",
        "Libro corto o cuentos",
        "Clasico",
        "Novela grande",
        "Contemporaneo latinoamericano o raro"
    ];

    public IReadOnlyList<ScoredBook> Score(
        IEnumerable<Book> unreadBooks,
        IReadOnlyDictionary<int, double>? aiScores = null)
    {
        var books = unreadBooks.ToList();
        if (books.Count == 0) return [];

        aiScores ??= new Dictionary<int, double>();

        var maxDays = books.Max(b => (DateTime.UtcNow - b.CreatedAt).TotalDays);
        if (maxDays == 0) maxDays = 1;

        var preliminary = books.Select(b =>
        {
            var normalizedPriority = (b.Priority - 1) / 4.0;
            var normalizedAge      = 1.0 - (DateTime.UtcNow - b.CreatedAt).TotalDays / maxDays;
            var aiScore = aiScores.TryGetValue(b.Id, out var s) ? s / 10.0 : 0.0;

            return new ScoredBook(b, normalizedPriority, 0.0, aiScore, normalizedAge, 0.0);
        })
        .OrderByDescending(sb =>
              sb.NormalizedPriority * 0.40
            + sb.AiScore            * 0.20
            + sb.NormalizedAge      * 0.10)
        .ToList();

        var result        = new List<ScoredBook>(Math.Min(books.Count, 20));
        var remaining     = preliminary.ToList();
        string? lastCat   = null;

        while (result.Count < 20 && remaining.Count > 0)
        {
            var candidate = remaining.FirstOrDefault(sb => sb.Book.RotationCategory != lastCat)
                         ?? remaining.First();

            var hasBonus = candidate.Book.RotationCategory != lastCat;
            var withBonus = candidate with
            {
                VarietyBonus   = hasBonus ? 1.0 : 0.0,
                CompositeScore = candidate.NormalizedPriority * 0.40
                               + (hasBonus ? 1.0 : 0.0) * 0.30
                               + candidate.AiScore * 0.20
                               + candidate.NormalizedAge * 0.10
            };

            result.Add(withBonus);
            lastCat = candidate.Book.RotationCategory;
            remaining.Remove(candidate);
        }

        return result;
    }

    public IReadOnlyList<Book> GetNext5(IEnumerable<Book> unreadBooks)
        => Score(unreadBooks).Take(5).Select(sb => sb.Book).ToList();
}
```

- **Completado cuando:** tests de TASK-04-B1 pasan (verde).

---

## Bloque C — Application: Use Cases

### TASK-04-C1 · Test: `GenerateQueue`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GenerateQueueTests.cs`
- **Setup:** Moq para `IBookRepository`, `IQueueRepository` e `IDbConnectionFactory`.

- **Casos:**
  - [ ] Sin libros no leídos → llama a `ReplaceQueueAsync` con lista vacía y retorna lista vacía.
  - [ ] Con libros → llama a `GetUnreadByUserAsync` con el `UserId` correcto.
  - [ ] Con libros → llama a `ReplaceQueueAsync` con la transacción activa.
  - [ ] Con 30 libros → persiste máximo 20 ítems (CA-05).
  - [ ] Si `ReplaceQueueAsync` lanza excepción → la transacción hace rollback (CA-09).
  - [ ] Source de los ítems generados es `"Filter"`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-C2 · Implementar `GenerateQueue`

- **Archivo:** `src/ReadingQueue.Application/UseCases/GenerateQueue.cs`

```csharp
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Application.Services;

namespace ReadingQueue.Application.UseCases;

public sealed class GenerateQueue
{
    private readonly IBookRepository      _books;
    private readonly IQueueRepository     _queue;
    private readonly QueueScoringService  _scoring;
    private readonly IDbConnectionFactory _factory;

    public GenerateQueue(IBookRepository books, IQueueRepository queue,
        QueueScoringService scoring, IDbConnectionFactory factory)
    {
        _books   = books;
        _queue   = queue;
        _scoring = scoring;
        _factory = factory;
    }

    public record Command(int UserId);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Command cmd, CancellationToken ct = default)
    {
        var unread = (await _books.GetUnreadByUserAsync(cmd.UserId, ct)).ToList();
        var scored = _scoring.Score(unread, aiScores: null);

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var items = scored.Select((sb, idx) => (sb.Book.Id, idx + 1, "Filter"));
            await _queue.ReplaceQueueAsync(cmd.UserId, items, tx, ct);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        return (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
    }
}
```

- **Completado cuando:** tests de TASK-04-C1 pasan (verde).

### TASK-04-C3 · Test: `GetQueue`, `RemoveFromQueue`, `ReorderQueue`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetQueueTests.cs`
- **Casos `GetQueue`:**
  - [ ] Delega a `IQueueRepository.GetByUserAsync` con el `UserId` correcto.
  - [ ] Retorna exactamente lo que retorna el repositorio.

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/RemoveFromQueueTests.cs`
- **Casos `RemoveFromQueue`:**
  - [ ] `ContainsBookAsync` retorna `false` → lanza `BookNotFoundException`.
  - [ ] `ContainsBookAsync` retorna `true` → llama a `RemoveItemAsync`.

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/ReorderQueueTests.cs`
- **Casos `ReorderQueue`:**
  - [ ] Posiciones duplicadas → lanza `ValidationException("positions", ...)` (CA-12).
  - [ ] `bookId` no está en la cola del usuario → lanza `ValidationException("bookIds", ...)` (CA-11).
  - [ ] Posiciones válidas y bookIds correctos → llama a `UpdatePositionsAsync` con la transacción.
  - [ ] Si `UpdatePositionsAsync` lanza excepción → rollback.
  - [ ] Retorna la cola actualizada tras el reordenamiento.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-C4 · Implementar `GetQueue`, `RemoveFromQueue`, `ReorderQueue`

**Archivos a crear:**

```
src/ReadingQueue.Application/UseCases/
  GetQueue.cs
  RemoveFromQueue.cs
  ReorderQueue.cs
```

```csharp
// src/ReadingQueue.Application/UseCases/GetQueue.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetQueue
{
    private readonly IQueueRepository _queue;

    public GetQueue(IQueueRepository queue) => _queue = queue;

    public record Query(int UserId);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Query q, CancellationToken ct = default)
        => (await _queue.GetByUserAsync(q.UserId, ct)).ToList();
}
```

```csharp
// src/ReadingQueue.Application/UseCases/RemoveFromQueue.cs
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class RemoveFromQueue
{
    private readonly IQueueRepository _queue;

    public RemoveFromQueue(IQueueRepository queue) => _queue = queue;

    public record Command(int UserId, int BookId);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var exists = await _queue.ContainsBookAsync(cmd.UserId, cmd.BookId, ct);
        if (!exists) throw new BookNotFoundException(cmd.BookId);

        await _queue.RemoveItemAsync(cmd.UserId, cmd.BookId, ct);
    }
}
```

```csharp
// src/ReadingQueue.Application/UseCases/ReorderQueue.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class ReorderQueue
{
    private readonly IQueueRepository     _queue;
    private readonly IDbConnectionFactory _factory;

    public ReorderQueue(IQueueRepository queue, IDbConnectionFactory factory)
    {
        _queue   = queue;
        _factory = factory;
    }

    public record Command(int UserId, IReadOnlyList<QueueItemPosition> Positions);
    public record QueueItemPosition(int BookId, int Position);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Command cmd, CancellationToken ct = default)
    {
        var positions = cmd.Positions.ToList();

        if (positions.Select(p => p.Position).Distinct().Count() != positions.Count)
            throw new ValidationException("positions", "Hay posiciones duplicadas.");

        var currentQueue    = (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
        var currentBookIds  = currentQueue.Select(q => q.BookId).ToHashSet();
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
```

- **Completado cuando:** tests de TASK-04-C3 pasan (verde).

### TASK-04-C5 · Test: `GetSpecialLists` y `GetDashboardStats`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetSpecialListsTests.cs`
- **Setup:** Moq para `IBookRepository` y `QueueScoringService` real (o Moq).
- **Nota:** Usar valores exactos del seed para MentalEnergy y Genre.

- **Casos `GetSpecialLists`:**
  - [ ] `next5` contiene máximo 5 libros.
  - [ ] `whenTired` solo incluye libros con `MentalEnergy = "Baja - cualquier momento"` (valor real, sin emoji).
  - [ ] `historicalDebt` solo incluye libros con `Genre = "Clasico"` o `Genre = "Novela clasica"` (sin tildes).
  - [ ] Sin libros no leídos → las tres listas están vacías.
  - [ ] `whenTired` está ordenado por `Priority DESC`, luego `CreatedAt ASC`.

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetDashboardStatsTests.cs`
- **Casos `GetDashboardStats`:**
  - [ ] Delega a `IStatsRepository.GetDashboardAsync` con el `UserId` correcto.
  - [ ] Retorna exactamente lo que retorna el repositorio.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-C6 · Implementar `GetSpecialLists` y `GetDashboardStats`

**Archivos a crear:**

```
src/ReadingQueue.Application/UseCases/
  GetSpecialLists.cs
  GetDashboardStats.cs
```

```csharp
// src/ReadingQueue.Application/UseCases/GetSpecialLists.cs
using ReadingQueue.Application.Services;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GetSpecialLists
{
    private readonly IBookRepository    _books;
    private readonly QueueScoringService _scoring;

    public GetSpecialLists(IBookRepository books, QueueScoringService scoring)
    {
        _books   = books;
        _scoring = scoring;
    }

    public record Query(int UserId);

    public async Task<SpecialLists> ExecuteAsync(Query q, CancellationToken ct = default)
    {
        var allUnread = (await _books.GetUnreadByUserAsync(q.UserId, ct)).ToList();

        var next5 = _scoring.GetNext5(allUnread);

        // Usar valor real del seed: "Baja - cualquier momento" (sin emoji)
        var whenTired = allUnread
            .Where(b => b.MentalEnergy == "Baja - cualquier momento")
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        // Usar valores reales del seed: "Clasico" y "Novela clasica" (sin tildes)
        var historicalDebt = allUnread
            .Where(b => b.Genre is "Clasico" or "Novela clasica")
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        return new SpecialLists(next5, whenTired, historicalDebt);
    }
}
```

```csharp
// src/ReadingQueue.Application/UseCases/GetDashboardStats.cs
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GetDashboardStats
{
    private readonly IStatsRepository _stats;

    public GetDashboardStats(IStatsRepository stats) => _stats = stats;

    public record Query(int UserId);

    public async Task<DashboardStats> ExecuteAsync(Query q, CancellationToken ct = default)
        => await _stats.GetDashboardAsync(q.UserId, ct);
}
```

- **Completado cuando:** tests de TASK-04-C5 pasan (verde).

---

## Bloque D — Infrastructure: SQL Queries y Repositorios

### TASK-04-D1 · Test de integración: `SqlQueueRepository`

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlQueueRepositoryTests.cs`
- **Fixture:** reutilizar o extender `BookRepositoryFixture` (ya crea usuario de prueba y ejecuta migraciones).

- **Casos:**
  - [ ] `GetByUserAsync` con cola vacía → retorna lista vacía.
  - [ ] `ReplaceQueueAsync` inserta ítems y `GetByUserAsync` los retorna ordenados por `Position ASC`.
  - [ ] `GetByUserAsync` solo retorna libros con `IsRead = 0` (filtro defensivo).
  - [ ] `ReplaceQueueAsync` borra la cola previa antes de insertar la nueva.
  - [ ] `GetByUserAsync` no retorna ítems de otro usuario (aislamiento).
  - [ ] `UpdatePositionsAsync` modifica las posiciones correctamente.
  - [ ] `ContainsBookAsync` retorna `true` si el libro está en la cola.
  - [ ] `ContainsBookAsync` retorna `false` si el libro no está en la cola.
  - [ ] `RemoveItemAsync` elimina el ítem específico sin afectar el resto de la cola.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-D2 · Implementar `QueueQueries` y `SqlQueueRepository`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Sql/
    QueueQueries.cs
  Data/
    SqlQueueRepository.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/QueueQueries.cs
namespace ReadingQueue.Infrastructure.Sql;

internal static class QueueQueries
{
    // Nota: splitOn = "BookId2" — el segundo BookId (del JOIN con Books)
    // Los alias "BookId2, UserId2, ..." permiten a Dapper multi-map separar
    // las columnas de QueueItem y Book sin colisión de nombres.
    internal const string GetByUser = """
        SELECT
            rq.Id,
            rq.UserId,
            rq.BookId,
            rq.Position,
            rq.AddedAt,
            rq.Source,
            b.Id           AS BookId2,
            b.UserId       AS UserId2,
            b.Title,
            b.Author,
            b.Genre,
            b.Country,
            b.WhyRead,
            b.Priority,
            b.MentalEnergy,
            b.RecommendedMood,
            b.RotationCategory,
            b.IsRead,
            b.ReadAt,
            b.Notes,
            b.CreatedAt    AS BookCreatedAt,
            b.UpdatedAt    AS BookUpdatedAt
        FROM ReadingQueue rq
        INNER JOIN Books b ON rq.BookId = b.Id
        WHERE rq.UserId = @UserId
          AND b.IsRead  = 0
        ORDER BY rq.Position ASC;
        """;

    internal const string DeleteByUser = """
        DELETE FROM ReadingQueue WHERE UserId = @UserId;
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
```

**Nota de implementación para `GetByUserAsync`:** la query retorna columnas con alias (`BookId2`, `UserId2`, `BookCreatedAt`, `BookUpdatedAt`) para evitar colisiones. Usar Dapper con una función de mapeo manual:

```csharp
// En SqlQueueRepository.GetByUserAsync:
using var conn = _factory.Create();
var result = await conn.QueryAsync<QueueItem, Book, QueueItem>(
    QueueQueries.GetByUser,
    (qi, book) => qi with { Book = book },
    new { UserId = userId },
    splitOn: "BookId2");
return result;
```

Para que esto funcione, Dapper necesita que `Book` tenga propiedades que coincidan con los alias de la query:
- `BookId2` → `Id` — no coincide por nombre, por lo que hay que mapear manualmente.

**Alternativa más simple**: usar una query flat y hacer el mapeo a mano:

```csharp
var rows = await conn.QueryAsync<dynamic>(QueueQueries.GetByUser, new { UserId = userId });
return rows.Select(r => new QueueItem
{
    Id       = (int)r.Id,
    UserId   = (int)r.UserId,
    BookId   = (int)r.BookId,
    Position = (int)r.Position,
    AddedAt  = (DateTime)r.AddedAt,
    Source   = (string)r.Source,
    Book     = new Book
    {
        Id               = (int)r.BookId2,
        UserId           = (int)r.UserId2,
        Title            = (string)r.Title,
        Author           = (string)r.Author,
        Genre            = (string)r.Genre,
        Country          = (string)r.Country,
        WhyRead          = (string?)r.WhyRead,
        Priority         = (int)(byte)r.Priority,
        MentalEnergy     = (string)r.MentalEnergy,
        RecommendedMood  = (string)r.RecommendedMood,
        RotationCategory = (string)r.RotationCategory,
        IsRead           = (bool)r.IsRead,
        ReadAt           = (DateTime?)r.ReadAt,
        Notes            = (string?)r.Notes,
        CreatedAt        = (DateTime)r.BookCreatedAt,
        UpdatedAt        = (DateTime)r.BookUpdatedAt
    }
}).ToList();
```

> Nota: `(int)(byte)r.Priority` porque Dapper con `dynamic` devuelve `byte` para `TINYINT`.

- **Completado cuando:** tests de TASK-04-D1 pasan (verde).

### TASK-04-D3 · Test de integración: `SqlStatsRepository`

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlStatsRepositoryTests.cs`
- **Fixture:** misma que D1 (ya tiene migraciones + seed data).

- **Casos:**
  - [ ] `GetDashboardAsync` con biblioteca vacía → `TotalBooks = 0`, listas vacías.
  - [ ] `TotalBooks`, `ReadBooks`, `UnreadBooks` son correctos tras insertar libros (CA-15).
  - [ ] `ReadPercentage` se calcula correctamente: 1 leído de 4 → `25.0` (CA-16).
  - [ ] `ByMentalEnergy` está ordenado por `SortOrder ASC` (CA-17).
  - [ ] `ByCountry` retorna máximo 10 países con 12 distintos (CA-18).
  - [ ] `TopUnreadPriority` retorna máximo 3 libros no leídos con mayor prioridad.
  - [ ] `RecentlyRead` retorna máximo 5 libros leídos más recientes ordenados por `ReadAt DESC`.
  - [ ] `ByGenre` agrupa correctamente por género.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-D4 · Implementar `StatsQueries` y `SqlStatsRepository`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Sql/
    StatsQueries.cs
  Data/
    SqlStatsRepository.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/StatsQueries.cs
namespace ReadingQueue.Infrastructure.Sql;

internal static class StatsQueries
{
    internal const string GetCounts = """
        SELECT
            COUNT(*)                                           AS TotalBooks,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END)       AS ReadBooks,
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END)       AS UnreadBooks
        FROM Books
        WHERE UserId = @UserId;
        """;

    internal const string GetByGenre = """
        SELECT
            Genre,
            COUNT(*)                                           AS Total,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END)       AS Read,
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END)       AS Unread
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

**Nota sobre `SqlStatsRepository.GetDashboardAsync`:** ejecutar todas las queries en paralelo (`Task.WhenAll`) o en secuencia, luego construir el `DashboardStats`. El `ReadPercentage` se calcula en C#: `totalBooks == 0 ? 0.0 : Math.Round((double)readBooks / totalBooks * 100, 1)`.

- **Completado cuando:** tests de TASK-04-D3 pasan (verde).

---

## Bloque E — API: Requests, Responses, Endpoints y Program.cs

### TASK-04-E1 · Test de integración: `QueueEndpoints`

- **Archivo test:** `tests/ReadingQueue.Api.Tests/QueueEndpointsTests.cs`
- **Fixture:** `IClassFixture<QueueEndpointsFixture>` — nueva fixture con Testcontainers + JWT + helper para crear libros y usuarios.

- **Casos:**
  - [ ] `GET /api/queue` sin libros en cola → `200 OK` con array vacío.
  - [ ] `GET /api/queue` retorna ítems ordenados por `Position ASC` (CA-01).
  - [ ] `GET /api/queue` no retorna libros leídos aunque estén en la tabla (CA-02).
  - [ ] `POST /api/queue/generate` sin libros no leídos → `200 OK` con `[]` (CA-04).
  - [ ] `POST /api/queue/generate` con libros → retorna la cola generada y persiste en BD.
  - [ ] `POST /api/queue/generate` con 30 libros → persiste máximo 20 (CA-05).
  - [ ] `PUT /api/queue/reorder` con nuevo orden → `200 OK` y BD confirma posiciones actualizadas (CA-10).
  - [ ] `PUT /api/queue/reorder` con `bookId` ajeno a la cola → `422` (CA-11).
  - [ ] `PUT /api/queue/reorder` con posiciones duplicadas → `422` (CA-12).
  - [ ] `DELETE /api/queue/{bookId}` → `204 No Content` y BD confirma eliminación (CA-13).
  - [ ] `DELETE /api/queue/{bookId}` para libro no en cola → `404` (CA-14).
  - [ ] `GET /api/queue` no retorna ítems de otro usuario (CA-03).
  - [ ] Todos los endpoints requieren JWT (sin token → `401`).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-E2 · Test de integración: `StatsEndpoints`

- **Archivo test:** `tests/ReadingQueue.Api.Tests/StatsEndpointsTests.cs`
- **Fixture:** misma `QueueEndpointsFixture`.

- **Casos:**
  - [ ] `GET /api/stats/dashboard` → `200 OK` con `totalBooks` correcto (CA-15).
  - [ ] `GET /api/stats/dashboard` → `readPercentage` correcto: 1 leído de 4 → `25.0` (CA-16).
  - [ ] `GET /api/stats/dashboard` → `byMentalEnergy` ordenado por `SortOrder ASC` (CA-17).
  - [ ] `GET /api/stats/dashboard` → `byCountry` retorna máximo 10 (CA-18).
  - [ ] `GET /api/stats/special-lists` → `next5` tiene máximo 5 elementos (CA-19).
  - [ ] `GET /api/stats/special-lists` → `whenTired` solo contiene libros con `MentalEnergy = "Baja - cualquier momento"` (CA-20).
  - [ ] `GET /api/stats/special-lists` → `historicalDebt` solo contiene `"Clasico"` o `"Novela clasica"` (CA-21).
  - [ ] Ambos endpoints requieren JWT (sin token → `401`).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-04-E3 · Implementar Requests, Responses, Endpoints y actualizar Program.cs

**Archivos a crear / actualizar:**

```
src/ReadingQueue.Api/
  Requests/
    ReorderQueueRequest.cs
  Responses/
    QueueItemResponse.cs
    DashboardStatsResponse.cs
    SpecialListsResponse.cs
  Endpoints/
    QueueEndpoints.cs
    StatsEndpoints.cs
  Program.cs                  ← actualizar
```

```csharp
// src/ReadingQueue.Api/Requests/ReorderQueueRequest.cs
namespace ReadingQueue.Api.Requests;

public sealed record ReorderQueueRequest(
    IReadOnlyList<QueuePositionItem> Positions);

public sealed record QueuePositionItem(int BookId, int Position);
```

```csharp
// src/ReadingQueue.Api/Responses/QueueItemResponse.cs
using ReadingQueue.Api.Responses;

namespace ReadingQueue.Api.Responses;

public sealed record QueueItemResponse(
    int      Position,
    DateTime AddedAt,
    string   Source,
    BookResponse Book
);
```

```csharp
// src/ReadingQueue.Api/Responses/DashboardStatsResponse.cs
namespace ReadingQueue.Api.Responses;

public sealed record DashboardStatsResponse(
    int    TotalBooks,
    int    ReadBooks,
    int    UnreadBooks,
    double ReadPercentage,
    IReadOnlyList<GenreStatResponse>        ByGenre,
    IReadOnlyList<RotationStatResponse>     ByRotationCategory,
    IReadOnlyList<MentalEnergyStatResponse> ByMentalEnergy,
    IReadOnlyList<CountryStatResponse>      ByCountry,
    IReadOnlyList<BookResponse>             TopUnreadPriority,
    IReadOnlyList<BookResponse>             RecentlyRead
);

public sealed record GenreStatResponse(string Genre, int Total, int Read, int Unread);
public sealed record RotationStatResponse(string Category, int Total, int Read, int Unread);
public sealed record MentalEnergyStatResponse(string Level, int Total, int Unread);
public sealed record CountryStatResponse(string Country, int Total);
```

```csharp
// src/ReadingQueue.Api/Responses/SpecialListsResponse.cs
namespace ReadingQueue.Api.Responses;

public sealed record SpecialListsResponse(
    IReadOnlyList<BookResponse> Next5,
    IReadOnlyList<BookResponse> WhenTired,
    IReadOnlyList<BookResponse> HistoricalDebt
);
```

**`QueueEndpoints.cs`** — mapea los 4 endpoints de cola usando `MapGroup("/api/queue").RequireAuthorization()`:
- `GET /` → `GetQueue`
- `POST /generate` → `GenerateQueue` (sin body)
- `PUT /reorder` → `ReorderQueue` (valida `ReorderQueueRequest`)
- `DELETE /{bookId:int}` → `RemoveFromQueue`

**`StatsEndpoints.cs`** — mapea los 2 endpoints de estadísticas usando `MapGroup("/api/stats").RequireAuthorization()`:
- `GET /dashboard` → `GetDashboardStats`
- `GET /special-lists` → `GetSpecialLists`

**Actualizar `Program.cs`** — agregar después de los registros del Spec-03:

```csharp
// ── Repositorios (Spec-04) ────────────────────────────────────────────────────
builder.Services.AddScoped<IQueueRepository, SqlQueueRepository>();
builder.Services.AddScoped<IStatsRepository, SqlStatsRepository>();

// ── Servicios de Application (Spec-04) ────────────────────────────────────────
builder.Services.AddScoped<QueueScoringService>();

// ── Use cases (Spec-04) ───────────────────────────────────────────────────────
builder.Services.AddScoped<GenerateQueue>();
builder.Services.AddScoped<GetQueue>();
builder.Services.AddScoped<ReorderQueue>();
builder.Services.AddScoped<RemoveFromQueue>();
builder.Services.AddScoped<GetSpecialLists>();
builder.Services.AddScoped<GetDashboardStats>();
```

Agregar al final de los endpoints (antes de `app.Run()`):

```csharp
QueueEndpoints.Map(app);
StatsEndpoints.Map(app);
```

- **Completado cuando:** tests de TASK-04-E1 y TASK-04-E2 pasan (verde).

---

## Bloque F — Verificación Final

### TASK-04-F1 · Build .NET sin errores ni warnings

```powershell
dotnet build ReadingQueue.sln
```

- **Criterio:** `0 Error(s)  0 Warning(s)`.

### TASK-04-F2 · Tests de Domain y Application pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Domain.Tests/ReadingQueue.Domain.Tests.csproj --no-build -v minimal
dotnet test tests/ReadingQueue.Application.Tests/ReadingQueue.Application.Tests.csproj --no-build -v minimal
```

- **Criterio:** todos los tests unitarios de los nuevos servicios y use cases pasan.

### TASK-04-F3 · Tests de Infrastructure pasan (verde, incluyendo nuevos)

```powershell
dotnet test tests/ReadingQueue.Infrastructure.Tests/ReadingQueue.Infrastructure.Tests.csproj --no-build -v minimal
```

- **Criterio:** `SqlQueueRepositoryTests` y `SqlStatsRepositoryTests` pasan junto con los tests anteriores.

### TASK-04-F4 · Tests de API pasan (verde, incluyendo nuevos)

```powershell
dotnet test tests/ReadingQueue.Api.Tests/ReadingQueue.Api.Tests.csproj --no-build -v minimal
```

- **Criterio:** `QueueEndpointsTests` y `StatsEndpointsTests` pasan junto con todos los tests anteriores.

---

## Resumen de archivos que genera SPEC-04

| # | Archivo | Bloque |
|---|---|---|
| 1  | `src/ReadingQueue.Domain/Entities/QueueItem.cs` | A |
| 2  | `src/ReadingQueue.Domain/ValueObjects/ScoredBook.cs` | A |
| 3  | `src/ReadingQueue.Domain/ValueObjects/DashboardStats.cs` | A |
| 4  | `src/ReadingQueue.Domain/ValueObjects/SpecialLists.cs` | A |
| 5  | `src/ReadingQueue.Domain/Interfaces/IQueueRepository.cs` | A |
| 6  | `src/ReadingQueue.Domain/Interfaces/IStatsRepository.cs` | A |
| 7  | `src/ReadingQueue.Application/Services/QueueScoringService.cs` | B |
| 8  | `src/ReadingQueue.Application/UseCases/GenerateQueue.cs` | C |
| 9  | `src/ReadingQueue.Application/UseCases/GetQueue.cs` | C |
| 10 | `src/ReadingQueue.Application/UseCases/RemoveFromQueue.cs` | C |
| 11 | `src/ReadingQueue.Application/UseCases/ReorderQueue.cs` | C |
| 12 | `src/ReadingQueue.Application/UseCases/GetSpecialLists.cs` | C |
| 13 | `src/ReadingQueue.Application/UseCases/GetDashboardStats.cs` | C |
| 14 | `src/ReadingQueue.Infrastructure/Sql/QueueQueries.cs` | D |
| 15 | `src/ReadingQueue.Infrastructure/Data/SqlQueueRepository.cs` | D |
| 16 | `src/ReadingQueue.Infrastructure/Sql/StatsQueries.cs` | D |
| 17 | `src/ReadingQueue.Infrastructure/Data/SqlStatsRepository.cs` | D |
| 18 | `src/ReadingQueue.Api/Requests/ReorderQueueRequest.cs` | E |
| 19 | `src/ReadingQueue.Api/Responses/QueueItemResponse.cs` | E |
| 20 | `src/ReadingQueue.Api/Responses/DashboardStatsResponse.cs` | E |
| 21 | `src/ReadingQueue.Api/Responses/SpecialListsResponse.cs` | E |
| 22 | `src/ReadingQueue.Api/Endpoints/QueueEndpoints.cs` | E |
| 23 | `src/ReadingQueue.Api/Endpoints/StatsEndpoints.cs` | E |
| 24 | `src/ReadingQueue.Api/Program.cs` (actualizado) | E |
| 25 | `tests/ReadingQueue.Domain.Tests/QueueItemTests.cs` | A |
| 26 | `tests/ReadingQueue.Domain.Tests/ScoredBookTests.cs` | A |
| 27 | `tests/ReadingQueue.Application.Tests/Services/QueueScoringServiceTests.cs` | B |
| 28 | `tests/ReadingQueue.Application.Tests/UseCases/GenerateQueueTests.cs` | C |
| 29 | `tests/ReadingQueue.Application.Tests/UseCases/GetQueueTests.cs` | C |
| 30 | `tests/ReadingQueue.Application.Tests/UseCases/RemoveFromQueueTests.cs` | C |
| 31 | `tests/ReadingQueue.Application.Tests/UseCases/ReorderQueueTests.cs` | C |
| 32 | `tests/ReadingQueue.Application.Tests/UseCases/GetSpecialListsTests.cs` | C |
| 33 | `tests/ReadingQueue.Application.Tests/UseCases/GetDashboardStatsTests.cs` | C |
| 34 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlQueueRepositoryTests.cs` | D |
| 35 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlStatsRepositoryTests.cs` | D |
| 36 | `tests/ReadingQueue.Api.Tests/QueueEndpointsTests.cs` | E |
| 37 | `tests/ReadingQueue.Api.Tests/StatsEndpointsTests.cs` | E |

---

## Checklist SPEC-04

### Bloque A — Domain
- [x] TASK-04-A1 · Tests entidad `QueueItem` (rojo)
- [x] TASK-04-A2 · Tests value object `ScoredBook` (rojo)
- [x] TASK-04-A3 · Impl `QueueItem`, `ScoredBook`, `DashboardStats`, `SpecialLists`, `IQueueRepository`, `IStatsRepository` (verde)

### Bloque B — Application: QueueScoringService
- [x] TASK-04-B1 · Tests `QueueScoringService` (rojo)
- [x] TASK-04-B2 · Impl `QueueScoringService` (verde)

### Bloque C — Application: Use Cases
- [x] TASK-04-C1 · Tests `GenerateQueue` (rojo)
- [x] TASK-04-C2 · Impl `GenerateQueue` (verde)
- [x] TASK-04-C3 · Tests `GetQueue` + `RemoveFromQueue` + `ReorderQueue` (rojo)
- [x] TASK-04-C4 · Impl `GetQueue` + `RemoveFromQueue` + `ReorderQueue` (verde)
- [x] TASK-04-C5 · Tests `GetSpecialLists` + `GetDashboardStats` (rojo)
- [x] TASK-04-C6 · Impl `GetSpecialLists` + `GetDashboardStats` (verde)

### Bloque D — Infrastructure
- [x] TASK-04-D1 · Tests `SqlQueueRepository` con Testcontainers (rojo)
- [x] TASK-04-D2 · Impl `QueueQueries` + `SqlQueueRepository` (verde)
- [x] TASK-04-D3 · Tests `SqlStatsRepository` con Testcontainers (rojo)
- [x] TASK-04-D4 · Impl `StatsQueries` + `SqlStatsRepository` (verde)

### Bloque E — API
- [x] TASK-04-E1 · Tests integración `QueueEndpoints` (rojo)
- [x] TASK-04-E2 · Tests integración `StatsEndpoints` (rojo)
- [x] TASK-04-E3 · Impl Requests + Responses + `QueueEndpoints` + `StatsEndpoints` + `Program.cs` actualizado (verde)

### Bloque F — Verificación Final
- [x] TASK-04-F1 · `dotnet build` → 0 errores, 0 warnings
- [x] TASK-04-F2 · Tests de Domain y Application pasan (verde)
- [x] TASK-04-F3 · Tests de Infrastructure pasan (verde, incluye nuevos)
- [x] TASK-04-F4 · Tests de Api pasan (verde, incluye nuevos)
