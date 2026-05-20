# SPEC-03 · Tasks — CRUD de Biblioteca y Gestión de Libros
> Proyecto: `ReadingQueue` (solución .NET 9 + React/Vite)
> Regla: **Tests primero, implementación después** (constitution §13)
> Cobertura mínima: **80%** en Domain y Application · Integración con Testcontainers en Infrastructure

---

## Nota previa: esquema ya existe

Las tablas `Books`, `ReadingQueue`, `AISuggestions`, `Genres`, `MentalEnergyLevels`, `Moods` y `RotationCategories` ya fueron creadas en la migración `001_initial_schema.sql` del Spec-01. El seed de datos de referencia está en `002_seed_reference_data.sql` con valores en texto plano (sin emojis ni tildes). **Los tests deben usar exactamente los valores de la BD**, por ejemplo `"Clasico"` en lugar de `"Clásico"` o `"Baja - cualquier momento"` en lugar de `"🟢 Baja – cualquier momento"`.

---

## Bloque A — Paquetes NuGet nuevos

> Sin tests — preparación del entorno de compilación.

### TASK-03-A1 · Agregar `IMemoryCache` a la capa Application

- **Acción:** `GetReferenceData` usa `IMemoryCache` directamente en el use case. La capa Application necesita la abstracción.

```powershell
dotnet add src/ReadingQueue.Application package Microsoft.Extensions.Caching.Abstractions
```

- **Completado cuando:** `dotnet build ReadingQueue.sln` → `0 Error(s)  0 Warning(s)`.

---

## Bloque B — Domain: Entidades, Value Objects, Excepciones e Interfaces

### TASK-03-B1 · Test: entidad `Book`

- **Archivo test:** `tests/ReadingQueue.Domain.Tests/BookEntityTests.cs`
- **Casos:**
  - [ ] Constructor de `Book` asigna correctamente las 16 propiedades.
  - [ ] `Book` con `IsRead = false` y `ReadAt = null` puede construirse sin excepción.
  - [ ] `Book` con `IsRead = true` y `ReadAt` con fecha puede construirse sin excepción.
  - [ ] `WhyRead` y `Notes` pueden ser `null` sin excepción.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-B2 · Implementar `Book`, `BookFilter`, `CreateBookData`, `UpdateBookData`

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Entities/
    Book.cs
  ValueObjects/
    BookFilter.cs
    CreateBookData.cs
    UpdateBookData.cs
```

```csharp
// src/ReadingQueue.Domain/Entities/Book.cs
namespace ReadingQueue.Domain.Entities;

public sealed class Book
{
    public int      Id               { get; }
    public int      UserId           { get; }
    public string   Title            { get; }
    public string   Author           { get; }
    public string   Genre            { get; }
    public string   Country          { get; }
    public string?  WhyRead          { get; }
    public int      Priority         { get; }
    public string   MentalEnergy     { get; }
    public string   RecommendedMood  { get; }
    public string   RotationCategory { get; }
    public bool     IsRead           { get; }
    public DateTime? ReadAt          { get; }
    public string?  Notes            { get; }
    public DateTime CreatedAt        { get; }
    public DateTime UpdatedAt        { get; }

    public Book(int id, int userId, string title, string author,
                string genre, string country, string? whyRead,
                int priority, string mentalEnergy, string recommendedMood,
                string rotationCategory, bool isRead, DateTime? readAt,
                string? notes, DateTime createdAt, DateTime updatedAt)
    {
        Id               = id;
        UserId           = userId;
        Title            = title;
        Author           = author;
        Genre            = genre;
        Country          = country;
        WhyRead          = whyRead;
        Priority         = priority;
        MentalEnergy     = mentalEnergy;
        RecommendedMood  = recommendedMood;
        RotationCategory = rotationCategory;
        IsRead           = isRead;
        ReadAt           = readAt;
        Notes            = notes;
        CreatedAt        = createdAt;
        UpdatedAt        = updatedAt;
    }
}
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/BookFilter.cs
namespace ReadingQueue.Domain.ValueObjects;

public sealed record BookFilter(
    string?  Genre          = null,
    string?  Country        = null,
    string?  MentalEnergy   = null,
    string?  Mood           = null,
    string?  Rotation       = null,
    int?     MinPriority    = null,
    bool?    IsRead         = null,
    string?  SearchQuery    = null
);
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/CreateBookData.cs
namespace ReadingQueue.Domain.ValueObjects;

public sealed record CreateBookData(
    string  Title,
    string  Author,
    string  Genre,
    string  Country,
    string? WhyRead,
    int     Priority,
    string  MentalEnergy,
    string  RecommendedMood,
    string  RotationCategory,
    string? Notes
);
```

```csharp
// src/ReadingQueue.Domain/ValueObjects/UpdateBookData.cs
namespace ReadingQueue.Domain.ValueObjects;

public sealed record UpdateBookData(
    string  Title,
    string  Author,
    string  Genre,
    string  Country,
    string? WhyRead,
    int     Priority,
    string  MentalEnergy,
    string  RecommendedMood,
    string  RotationCategory,
    string? Notes
);
```

- **Completado cuando:** tests de TASK-03-B1 pasan (verde).

### TASK-03-B3 · Implementar excepciones de dominio

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Exceptions/
    BookNotFoundException.cs
    ValidationException.cs
```

```csharp
// src/ReadingQueue.Domain/Exceptions/BookNotFoundException.cs
namespace ReadingQueue.Domain.Exceptions;

public sealed class BookNotFoundException : Exception
{
    public BookNotFoundException(int bookId)
        : base($"Libro {bookId} no encontrado.") { }
}
```

```csharp
// src/ReadingQueue.Domain/Exceptions/ValidationException.cs
namespace ReadingQueue.Domain.Exceptions;

public sealed class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]> { [field] = [message] };
    }
}
```

- **Completado cuando:** `dotnet build ReadingQueue.sln` → `0 Error(s)`.

### TASK-03-B4 · Crear interfaces de dominio

> Sin tests previos — los contratos se verifican implícitamente en los tests de use cases (Bloque D).

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Interfaces/
    IBookRepository.cs
    IReferenceDataRepository.cs
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IBookRepository.cs
using System.Data;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface IBookRepository
{
    Task<IEnumerable<Book>> GetByUserAsync(
        int userId, BookFilter filter, CancellationToken ct = default);

    Task<Book?> GetByIdAsync(
        int bookId, int userId, CancellationToken ct = default);

    Task<int> CreateAsync(
        int userId, CreateBookData data, CancellationToken ct = default);

    Task UpdateAsync(
        int bookId, int userId, UpdateBookData data, CancellationToken ct = default);

    Task DeleteAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task DeleteFromQueueAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task DeleteFromSuggestionsAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task MarkAsReadAsync(
        int bookId, int userId, DateTime readAt, string? notes,
        IDbTransaction tx, CancellationToken ct = default);

    Task RemoveFromQueueIfPresentAsync(
        int bookId, int userId, IDbTransaction tx, CancellationToken ct = default);

    Task MarkAsUnreadAsync(
        int bookId, int userId, CancellationToken ct = default);

    Task<IEnumerable<Book>> GetUnreadByUserAsync(
        int userId, CancellationToken ct = default);

    Task<IEnumerable<Book>> GetReadByUserAsync(
        int userId, CancellationToken ct = default);
}
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IReferenceDataRepository.cs
namespace ReadingQueue.Domain.Interfaces;

public interface IReferenceDataRepository
{
    Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct = default);
}
```

- **Completado cuando:** `dotnet build ReadingQueue.sln` → `0 Error(s)`.

---

## Bloque C — Infrastructure: Repositorios SQL

> Usa Testcontainers con SQL Server real. Se reutiliza o amplía la fixture existente.

### TASK-03-C1 · Test: `SqlBookRepository` contra SQL Server real

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlBookRepositoryTests.cs`
- **Fixture:** `IClassFixture<BookRepositoryFixture>` — inicia contenedor, ejecuta migraciones (incluye seed de referencia), expone `ConnectionString` y un `UserId` de usuario de prueba pre-creado.
- **Nota:** Usar los valores exactos del seed de BD (`"Clasico"`, `"Baja - cualquier momento"`, etc.), no los del spec con emojis.

- **Casos:**
  - [ ] `GetByUserAsync` con biblioteca vacía → retorna lista vacía.
  - [ ] `CreateAsync` inserta un libro y retorna `Id > 0`.
  - [ ] `GetByIdAsync` con `bookId` y `userId` correctos → retorna el libro.
  - [ ] `GetByIdAsync` con `userId` incorrecto → retorna `null` (aislamiento de usuario).
  - [ ] `GetByIdAsync` con `bookId` inexistente → retorna `null`.
  - [ ] `GetByUserAsync` con filtro `Genre` → retorna solo libros de ese género.
  - [ ] `GetByUserAsync` con filtro `IsRead = false` → retorna solo libros no leídos.
  - [ ] `GetByUserAsync` con filtro `MinPriority = 4` → retorna libros con prioridad 4 y 5.
  - [ ] `GetByUserAsync` con `SearchQuery` → encuentra libro por subcadena en `Title` (case-insensitive).
  - [ ] `GetByUserAsync` con `SearchQuery` → encuentra libro por subcadena en `Author`.
  - [ ] `GetByUserAsync` sin filtros → retorna libros ordenados por `Priority DESC, CreatedAt ASC`.
  - [ ] `UpdateAsync` modifica los campos y el `UpdatedAt` cambia.
  - [ ] `MarkAsUnreadAsync` establece `IsRead = false` y `ReadAt = null`.
  - [ ] `GetUnreadByUserAsync` retorna solo libros con `IsRead = false`.
  - [ ] `GetReadByUserAsync` retorna solo libros con `IsRead = true`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-C2 · Implementar `SqlBookRepository` y `BookQueries`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Data/
    SqlBookRepository.cs
  Sql/
    BookQueries.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/BookQueries.cs
namespace ReadingQueue.Infrastructure.Sql;

internal static class BookQueries
{
    internal const string GetByUserFiltered = """
        SELECT
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId
          AND (@Genre        IS NULL OR Genre            = @Genre)
          AND (@Country      IS NULL OR Country          = @Country)
          AND (@MentalEnergy IS NULL OR MentalEnergy     = @MentalEnergy)
          AND (@Mood         IS NULL OR RecommendedMood  = @Mood)
          AND (@Rotation     IS NULL OR RotationCategory = @Rotation)
          AND (@MinPriority  IS NULL OR Priority        >= @MinPriority)
          AND (@IsRead       IS NULL OR IsRead           = @IsRead)
          AND (@SearchQuery  IS NULL OR
               Title  LIKE '%' + @SearchQuery + '%' OR
               Author LIKE '%' + @SearchQuery + '%')
        ORDER BY Priority DESC, CreatedAt ASC;
        """;

    internal const string GetById = """
        SELECT
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO Books
            (UserId, Title, Author, Genre, Country, WhyRead,
             Priority, MentalEnergy, RecommendedMood, RotationCategory, Notes)
        OUTPUT INSERTED.Id
        VALUES
            (@UserId, @Title, @Author, @Genre, @Country, @WhyRead,
             @Priority, @MentalEnergy, @RecommendedMood, @RotationCategory, @Notes);
        """;

    internal const string Update = """
        UPDATE Books SET
            Title            = @Title,
            Author           = @Author,
            Genre            = @Genre,
            Country          = @Country,
            WhyRead          = @WhyRead,
            Priority         = @Priority,
            MentalEnergy     = @MentalEnergy,
            RecommendedMood  = @RecommendedMood,
            RotationCategory = @RotationCategory,
            Notes            = @Notes,
            UpdatedAt        = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string Delete = """
        DELETE FROM Books WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string DeleteFromQueue = """
        DELETE FROM ReadingQueue WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string DeleteFromSuggestions = """
        DELETE FROM AISuggestions WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string MarkAsRead = """
        UPDATE Books SET
            IsRead    = 1,
            ReadAt    = @ReadAt,
            Notes     = COALESCE(@Notes, Notes),
            UpdatedAt = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string RemoveFromQueueIfPresent = """
        DELETE FROM ReadingQueue WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string MarkAsUnread = """
        UPDATE Books SET
            IsRead    = 0,
            ReadAt    = NULL,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string GetUnreadByUser = """
        SELECT
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 0
        ORDER BY Priority DESC, CreatedAt ASC;
        """;

    internal const string GetReadByUser = """
        SELECT
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 1
        ORDER BY ReadAt DESC;
        """;
}
```

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlBookRepository.cs
using System.Data;
using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlBookRepository : IBookRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlBookRepository(IDbConnectionFactory factory)
        => _factory = factory;

    public async Task<IEnumerable<Book>> GetByUserAsync(
        int userId, BookFilter filter, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(BookQueries.GetByUserFiltered, new
        {
            UserId        = userId,
            Genre         = filter.Genre,
            Country       = filter.Country,
            MentalEnergy  = filter.MentalEnergy,
            Mood          = filter.Mood,
            Rotation      = filter.Rotation,
            MinPriority   = filter.MinPriority,
            IsRead        = filter.IsRead.HasValue ? (object)(filter.IsRead.Value ? 1 : 0) : null,
            SearchQuery   = filter.SearchQuery
        });
    }

    public async Task<Book?> GetByIdAsync(int bookId, int userId, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<Book>(
            BookQueries.GetById, new { BookId = bookId, UserId = userId });
    }

    public async Task<int> CreateAsync(int userId, CreateBookData data, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(BookQueries.Insert, new
        {
            UserId           = userId,
            data.Title,
            data.Author,
            data.Genre,
            data.Country,
            data.WhyRead,
            data.Priority,
            data.MentalEnergy,
            data.RecommendedMood,
            data.RotationCategory,
            data.Notes
        });
    }

    public async Task UpdateAsync(int bookId, int userId, UpdateBookData data, CancellationToken ct)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(BookQueries.Update, new
        {
            BookId           = bookId,
            UserId           = userId,
            data.Title,
            data.Author,
            data.Genre,
            data.Country,
            data.WhyRead,
            data.Priority,
            data.MentalEnergy,
            data.RecommendedMood,
            data.RotationCategory,
            data.Notes
        });
    }

    public async Task DeleteAsync(int bookId, int userId, IDbTransaction tx, CancellationToken ct)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.Delete, new { BookId = bookId, UserId = userId }, tx);

    public async Task DeleteFromQueueAsync(int bookId, int userId, IDbTransaction tx, CancellationToken ct)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.DeleteFromQueue, new { BookId = bookId, UserId = userId }, tx);

    public async Task DeleteFromSuggestionsAsync(int bookId, int userId, IDbTransaction tx, CancellationToken ct)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.DeleteFromSuggestions, new { BookId = bookId, UserId = userId }, tx);

    public async Task MarkAsReadAsync(int bookId, int userId, DateTime readAt,
        string? notes, IDbTransaction tx, CancellationToken ct)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.MarkAsRead,
            new { BookId = bookId, UserId = userId, ReadAt = readAt, Notes = notes }, tx);

    public async Task RemoveFromQueueIfPresentAsync(int bookId, int userId,
        IDbTransaction tx, CancellationToken ct)
        => await tx.Connection!.ExecuteAsync(
            BookQueries.RemoveFromQueueIfPresent,
            new { BookId = bookId, UserId = userId }, tx);

    public async Task MarkAsUnreadAsync(int bookId, int userId, CancellationToken ct)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(
            BookQueries.MarkAsUnread, new { BookId = bookId, UserId = userId });
    }

    public async Task<IEnumerable<Book>> GetUnreadByUserAsync(int userId, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(BookQueries.GetUnreadByUser, new { UserId = userId });
    }

    public async Task<IEnumerable<Book>> GetReadByUserAsync(int userId, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<Book>(BookQueries.GetReadByUser, new { UserId = userId });
    }
}
```

- **Completado cuando:** tests de TASK-03-C1 pasan (verde).

### TASK-03-C3 · Test: `SqlReferenceDataRepository` contra SQL Server real

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlReferenceDataRepositoryTests.cs`
- **Fixture:** misma `BookRepositoryFixture` (ya ejecutó las migraciones con seed data).

- **Casos:**
  - [ ] `GetGenresAsync` retorna exactamente 7 géneros.
  - [ ] `GetGenresAsync` incluye `"Clasico"` (valor exacto del seed).
  - [ ] `GetMentalEnergyLevelsAsync` retorna exactamente 5 niveles.
  - [ ] `GetMentalEnergyLevelsAsync` el primer elemento es `"Baja - cualquier momento"` (SortOrder = 1).
  - [ ] `GetMentalEnergyLevelsAsync` el último elemento es `"Maxima - modo lector"` (SortOrder = 5).
  - [ ] `GetMoodsAsync` retorna exactamente 7 ánimos.
  - [ ] `GetRotationCategoriesAsync` retorna exactamente 5 categorías.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-C4 · Implementar `SqlReferenceDataRepository` y `ReferenceQueries`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Data/
    SqlReferenceDataRepository.cs
  Sql/
    ReferenceQueries.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/ReferenceQueries.cs
namespace ReadingQueue.Infrastructure.Sql;

internal static class ReferenceQueries
{
    internal const string GetGenres = """
        SELECT Name FROM Genres ORDER BY Name;
        """;

    internal const string GetMentalEnergyLevels = """
        SELECT Name FROM MentalEnergyLevels ORDER BY SortOrder ASC;
        """;

    internal const string GetMoods = """
        SELECT Name FROM Moods ORDER BY Name;
        """;

    internal const string GetRotationCategories = """
        SELECT Name FROM RotationCategories ORDER BY Name;
        """;
}
```

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlReferenceDataRepository.cs
using Dapper;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlReferenceDataRepository : IReferenceDataRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlReferenceDataRepository(IDbConnectionFactory factory)
        => _factory = factory;

    public async Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetGenres);
    }

    public async Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetMentalEnergyLevels);
    }

    public async Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetMoods);
    }

    public async Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetRotationCategories);
    }
}
```

- **Completado cuando:** tests de TASK-03-C3 pasan (verde).

---

## Bloque D — Application: Use Cases

> Todos los tests usan Moq para `IBookRepository`, `IReferenceDataRepository` e `IDbConnectionFactory`. Sin Testcontainers en este bloque.

### TASK-03-D1 · Test: `GetFilteredBooks` y `GetBookById`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetFilteredBooksTests.cs`
- **Casos:**
  - [ ] `ExecuteAsync` delega a `IBookRepository.GetByUserAsync` con el `UserId` y `BookFilter` correctos.
  - [ ] Retorna exactamente lo que retorna el repositorio.

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetBookByIdTests.cs`
- **Casos:**
  - [ ] Repositorio retorna un `Book` → use case retorna ese libro.
  - [ ] Repositorio retorna `null` → lanza `BookNotFoundException` con el `bookId` en el mensaje.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-D2 · Implementar `GetFilteredBooks` y `GetBookById`

**Archivos a crear:**

```
src/ReadingQueue.Application/UseCases/
  GetFilteredBooks.cs
  GetBookById.cs
```

```csharp
// src/ReadingQueue.Application/UseCases/GetFilteredBooks.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GetFilteredBooks
{
    private readonly IBookRepository _books;

    public GetFilteredBooks(IBookRepository books) => _books = books;

    public record Query(int UserId, BookFilter Filter);

    public async Task<IEnumerable<Book>> ExecuteAsync(Query q, CancellationToken ct = default)
        => await _books.GetByUserAsync(q.UserId, q.Filter, ct);
}
```

```csharp
// src/ReadingQueue.Application/UseCases/GetBookById.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetBookById
{
    private readonly IBookRepository _books;

    public GetBookById(IBookRepository books) => _books = books;

    public record Query(int BookId, int UserId);

    public async Task<Book> ExecuteAsync(Query q, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(q.BookId, q.UserId, ct);
        return book ?? throw new BookNotFoundException(q.BookId);
    }
}
```

- **Completado cuando:** tests de TASK-03-D1 pasan (verde).

### TASK-03-D3 · Test: `CreateBook` y `UpdateBook`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/CreateBookTests.cs`
- **Casos:**
  - [ ] `Genre` no está en la lista de géneros → lanza `ValidationException` con campo `"genre"`.
  - [ ] `MentalEnergy` inválido → lanza `ValidationException` con campo `"mentalEnergy"`.
  - [ ] `RecommendedMood` inválido → lanza `ValidationException` con campo `"recommendedMood"`.
  - [ ] `RotationCategory` inválido → lanza `ValidationException` con campo `"rotationCategory"`.
  - [ ] Todos los valores de referencia válidos → llama a `IBookRepository.CreateAsync` con los datos correctos.
  - [ ] `UserId` del comando se pasa al repositorio, nunca viene del body.
  - [ ] Retorna el `Book` completo recuperado tras la creación (repositorio devuelve el nuevo `Id`, luego se llama `GetByIdAsync`).

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/UpdateBookTests.cs`
- **Casos:**
  - [ ] Libro no existe (`GetByIdAsync` retorna `null`) → lanza `BookNotFoundException`.
  - [ ] `Genre` inválido → lanza `ValidationException`.
  - [ ] Todos los valores válidos → llama a `IBookRepository.UpdateAsync` con los datos correctos.
  - [ ] Retorna el libro actualizado.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-D4 · Implementar `CreateBook` y `UpdateBook`

**Archivos a crear:**

```
src/ReadingQueue.Application/UseCases/
  CreateBook.cs
  UpdateBook.cs
```

```csharp
// src/ReadingQueue.Application/UseCases/CreateBook.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class CreateBook
{
    private readonly IBookRepository _books;
    private readonly IReferenceDataRepository _refs;

    public CreateBook(IBookRepository books, IReferenceDataRepository refs)
    {
        _books = books;
        _refs  = refs;
    }

    public record Command(
        int     UserId,
        string  Title,
        string  Author,
        string  Genre,
        string  Country,
        string? WhyRead,
        int     Priority,
        string  MentalEnergy,
        string  RecommendedMood,
        string  RotationCategory,
        string? Notes
    );

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var genres     = (await _refs.GetGenresAsync(ct)).ToHashSet();
        var energies   = (await _refs.GetMentalEnergyLevelsAsync(ct)).ToHashSet();
        var moods      = (await _refs.GetMoodsAsync(ct)).ToHashSet();
        var rotations  = (await _refs.GetRotationCategoriesAsync(ct)).ToHashSet();

        if (!genres.Contains(cmd.Genre))
            throw new ValidationException("genre", $"'{cmd.Genre}' no es un género válido.");
        if (!energies.Contains(cmd.MentalEnergy))
            throw new ValidationException("mentalEnergy", $"'{cmd.MentalEnergy}' no es un nivel de energía válido.");
        if (!moods.Contains(cmd.RecommendedMood))
            throw new ValidationException("recommendedMood", $"'{cmd.RecommendedMood}' no es un ánimo válido.");
        if (!rotations.Contains(cmd.RotationCategory))
            throw new ValidationException("rotationCategory", $"'{cmd.RotationCategory}' no es una categoría de rotación válida.");

        var data = new CreateBookData(cmd.Title, cmd.Author, cmd.Genre, cmd.Country,
            cmd.WhyRead, cmd.Priority, cmd.MentalEnergy, cmd.RecommendedMood,
            cmd.RotationCategory, cmd.Notes);

        var newId = await _books.CreateAsync(cmd.UserId, data, ct);
        return (await _books.GetByIdAsync(newId, cmd.UserId, ct))!;
    }
}
```

```csharp
// src/ReadingQueue.Application/UseCases/UpdateBook.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class UpdateBook
{
    private readonly IBookRepository _books;
    private readonly IReferenceDataRepository _refs;

    public UpdateBook(IBookRepository books, IReferenceDataRepository refs)
    {
        _books = books;
        _refs  = refs;
    }

    public record Command(
        int     BookId,
        int     UserId,
        string  Title,
        string  Author,
        string  Genre,
        string  Country,
        string? WhyRead,
        int     Priority,
        string  MentalEnergy,
        string  RecommendedMood,
        string  RotationCategory,
        string? Notes
    );

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var existing = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        var genres    = (await _refs.GetGenresAsync(ct)).ToHashSet();
        var energies  = (await _refs.GetMentalEnergyLevelsAsync(ct)).ToHashSet();
        var moods     = (await _refs.GetMoodsAsync(ct)).ToHashSet();
        var rotations = (await _refs.GetRotationCategoriesAsync(ct)).ToHashSet();

        if (!genres.Contains(cmd.Genre))
            throw new ValidationException("genre", $"'{cmd.Genre}' no es un género válido.");
        if (!energies.Contains(cmd.MentalEnergy))
            throw new ValidationException("mentalEnergy", $"'{cmd.MentalEnergy}' no es un nivel de energía válido.");
        if (!moods.Contains(cmd.RecommendedMood))
            throw new ValidationException("recommendedMood", $"'{cmd.RecommendedMood}' no es un ánimo válido.");
        if (!rotations.Contains(cmd.RotationCategory))
            throw new ValidationException("rotationCategory", $"'{cmd.RotationCategory}' no es una categoría de rotación válida.");

        var data = new UpdateBookData(cmd.Title, cmd.Author, cmd.Genre, cmd.Country,
            cmd.WhyRead, cmd.Priority, cmd.MentalEnergy, cmd.RecommendedMood,
            cmd.RotationCategory, cmd.Notes);

        await _books.UpdateAsync(cmd.BookId, cmd.UserId, data, ct);
        return (await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct))!;
    }
}
```

- **Completado cuando:** tests de TASK-03-D3 pasan (verde).

### TASK-03-D5 · Test: `DeleteBook`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/DeleteBookTests.cs`
- **Casos:**
  - [ ] Libro no existe (`GetByIdAsync` retorna `null`) → lanza `BookNotFoundException`.
  - [ ] Libro existe → llama a `DeleteFromQueueAsync` antes de `DeleteAsync`.
  - [ ] Libro existe → llama a `DeleteFromSuggestionsAsync` antes de `DeleteAsync`.
  - [ ] Libro existe → llama a `DeleteAsync` en la misma transacción.
  - [ ] Si `DeleteAsync` lanza excepción → la transacción hace rollback (no se traga el error).

- **Nota de implementación:** usar `Mock<IDbConnectionFactory>` que retorne un `Mock<IDbConnection>` que retorne un `Mock<IDbTransaction>`. Verificar el orden de las llamadas con `MockSequence`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-D6 · Implementar `DeleteBook`

- **Archivo:** `src/ReadingQueue.Application/UseCases/DeleteBook.cs`

```csharp
// src/ReadingQueue.Application/UseCases/DeleteBook.cs
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class DeleteBook
{
    private readonly IBookRepository _books;
    private readonly IDbConnectionFactory _factory;

    public DeleteBook(IBookRepository books, IDbConnectionFactory factory)
    {
        _books   = books;
        _factory = factory;
    }

    public record Command(int BookId, int UserId);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var existing = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await _books.DeleteFromQueueAsync(cmd.BookId, cmd.UserId, tx, ct);
            await _books.DeleteFromSuggestionsAsync(cmd.BookId, cmd.UserId, tx, ct);
            await _books.DeleteAsync(cmd.BookId, cmd.UserId, tx, ct);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
```

- **Completado cuando:** tests de TASK-03-D5 pasan (verde).

### TASK-03-D7 · Test: `MarkBookAsRead` y `MarkBookAsUnread`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/MarkBookAsReadTests.cs`
- **Casos:**
  - [ ] Libro no existe (`GetByIdAsync` retorna `null`) → lanza `BookNotFoundException`.
  - [ ] `Command.ReadAt = null` → se llama `MarkAsReadAsync` con fecha `≈ DateTime.UtcNow` (tolerancia ±5 s).
  - [ ] `Command.ReadAt` con fecha explícita → se pasa esa fecha a `MarkAsReadAsync`.
  - [ ] Se llama a `RemoveFromQueueIfPresentAsync` en la misma transacción que `MarkAsReadAsync`.
  - [ ] Libro ya leído + llamada nueva → operación es idempotente (no lanza excepción).
  - [ ] Retorna el libro actualizado con `IsRead = true`.

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/MarkBookAsUnreadTests.cs`
- **Casos:**
  - [ ] Libro no existe → lanza `BookNotFoundException`.
  - [ ] Libro existe → llama a `IBookRepository.MarkAsUnreadAsync`.
  - [ ] Libro ya no leído → idempotente (no lanza excepción).
  - [ ] Retorna el libro actualizado con `IsRead = false`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-D8 · Implementar `MarkBookAsRead` y `MarkBookAsUnread`

**Archivos a crear:**

```
src/ReadingQueue.Application/UseCases/
  MarkBookAsRead.cs
  MarkBookAsUnread.cs
```

```csharp
// src/ReadingQueue.Application/UseCases/MarkBookAsRead.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class MarkBookAsRead
{
    private readonly IBookRepository _books;
    private readonly IDbConnectionFactory _factory;

    public MarkBookAsRead(IBookRepository books, IDbConnectionFactory factory)
    {
        _books   = books;
        _factory = factory;
    }

    public record Command(int BookId, int UserId, DateTime? ReadAt, string? Notes);

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        var readAt = cmd.ReadAt ?? DateTime.UtcNow;

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await _books.MarkAsReadAsync(cmd.BookId, cmd.UserId, readAt, cmd.Notes, tx, ct);
            await _books.RemoveFromQueueIfPresentAsync(cmd.BookId, cmd.UserId, tx, ct);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return (await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct))!;
    }
}
```

```csharp
// src/ReadingQueue.Application/UseCases/MarkBookAsUnread.cs
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class MarkBookAsUnread
{
    private readonly IBookRepository _books;

    public MarkBookAsUnread(IBookRepository books) => _books = books;

    public record Command(int BookId, int UserId);

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        await _books.MarkAsUnreadAsync(cmd.BookId, cmd.UserId, ct);
        return (await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct))!;
    }
}
```

- **Completado cuando:** tests de TASK-03-D7 pasan (verde).

### TASK-03-D9 · Test: `GetReferenceData`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetReferenceDataTests.cs`
- **Setup:** `Mock<IReferenceDataRepository>` + `IMemoryCache` real (`new MemoryCache(new MemoryCacheOptions())`).

- **Casos:**
  - [ ] Primera llamada a `GetGenresAsync` → llama al repositorio exactamente una vez.
  - [ ] Segunda llamada a `GetGenresAsync` (misma instancia) → no llama de nuevo al repositorio (sirve desde cache).
  - [ ] `GetMentalEnergyLevelsAsync`, `GetMoodsAsync`, `GetRotationCategoriesAsync` tienen el mismo comportamiento de cache.
  - [ ] Retorna los mismos valores que el repositorio.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-D10 · Implementar `GetReferenceData`

- **Archivo:** `src/ReadingQueue.Application/UseCases/GetReferenceData.cs`

```csharp
// src/ReadingQueue.Application/UseCases/GetReferenceData.cs
using Microsoft.Extensions.Caching.Abstractions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetReferenceData
{
    private readonly IReferenceDataRepository _refs;
    private readonly IMemoryCache _cache;

    public GetReferenceData(IReferenceDataRepository refs, IMemoryCache cache)
    {
        _refs  = refs;
        _cache = cache;
    }

    public async Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:genres", () => _refs.GetGenresAsync(ct));

    public async Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:energy", () => _refs.GetMentalEnergyLevelsAsync(ct));

    public async Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:moods", () => _refs.GetMoodsAsync(ct));

    public async Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:rotations", () => _refs.GetRotationCategoriesAsync(ct));

    private async Task<IEnumerable<string>> GetOrCreateAsync(
        string key, Func<Task<IEnumerable<string>>> factory)
    {
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await factory();
        }) ?? [];
    }
}
```

- **Nota:** el `using` correcto es `Microsoft.Extensions.Caching.Memory` para `IMemoryCache` y `GetOrCreateAsync`.
- **Completado cuando:** tests de TASK-03-D9 pasan (verde).

---

## Bloque E — API: Validators, Requests y Responses

### TASK-03-E1 · Test: `CreateBookRequestValidator`

- **Archivo test:** `tests/ReadingQueue.Api.Tests/Validators/CreateBookRequestValidatorTests.cs`
- **Casos:**
  - [ ] Request con todos los campos válidos → `IsValid` retorna `true`.
  - [ ] `Title` vacío → `IsValid` retorna `false` con error en `title`.
  - [ ] `Author` vacío → `IsValid` retorna `false` con error en `author`.
  - [ ] `Genre` vacío → `IsValid` retorna `false` con error en `genre`.
  - [ ] `Country` vacío → `IsValid` retorna `false` con error en `country`.
  - [ ] `MentalEnergy` vacío → `IsValid` retorna `false` con error en `mentalEnergy`.
  - [ ] `RecommendedMood` vacío → `IsValid` retorna `false` con error en `recommendedMood`.
  - [ ] `RotationCategory` vacía → `IsValid` retorna `false` con error en `rotationCategory`.
  - [ ] `Priority = 0` → `IsValid` retorna `false` (fuera del rango 1-5).
  - [ ] `Priority = 6` → `IsValid` retorna `false`.
  - [ ] `Priority = 3` → `IsValid` retorna `true`.
  - [ ] `WhyRead` con 1001 caracteres → `IsValid` retorna `false`.
  - [ ] `Notes` con 2001 caracteres → `IsValid` retorna `false`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-E2 · Implementar validators, requests y responses

**Archivos a crear:**

```
src/ReadingQueue.Api/
  Requests/
    CreateBookRequest.cs
    UpdateBookRequest.cs
    MarkAsReadRequest.cs
  Responses/
    BookResponse.cs
  Validators/
    CreateBookRequestValidator.cs
    UpdateBookRequestValidator.cs
    MarkAsReadRequestValidator.cs
```

```csharp
// src/ReadingQueue.Api/Requests/CreateBookRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record CreateBookRequest(
    string  Title,
    string  Author,
    string  Genre,
    string  Country,
    string? WhyRead,
    int     Priority,
    string  MentalEnergy,
    string  RecommendedMood,
    string  RotationCategory,
    string? Notes
);

// src/ReadingQueue.Api/Requests/UpdateBookRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record UpdateBookRequest(
    string  Title,
    string  Author,
    string  Genre,
    string  Country,
    string? WhyRead,
    int     Priority,
    string  MentalEnergy,
    string  RecommendedMood,
    string  RotationCategory,
    string? Notes
);

// src/ReadingQueue.Api/Requests/MarkAsReadRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record MarkAsReadRequest(DateTime? ReadAt, string? Notes);
```

```csharp
// src/ReadingQueue.Api/Responses/BookResponse.cs
namespace ReadingQueue.Api.Responses;

public sealed record BookResponse(
    int      Id,
    int      UserId,
    string   Title,
    string   Author,
    string   Genre,
    string   Country,
    string?  WhyRead,
    int      Priority,
    string   MentalEnergy,
    string   RecommendedMood,
    string   RotationCategory,
    bool     IsRead,
    DateTime? ReadAt,
    string?  Notes,
    DateTime CreatedAt,
    DateTime UpdatedAt
);
```

```csharp
// src/ReadingQueue.Api/Validators/CreateBookRequestValidator.cs
using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class CreateBookRequestValidator : AbstractValidator<CreateBookRequest>
{
    public CreateBookRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(500);

        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("El autor es obligatorio.")
            .MaximumLength(300);

        RuleFor(x => x.Genre)
            .NotEmpty().WithMessage("El género es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("El país es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 5)
            .WithMessage("La prioridad debe estar entre 1 y 5.");

        RuleFor(x => x.MentalEnergy)
            .NotEmpty().WithMessage("El nivel de energía mental es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.RecommendedMood)
            .NotEmpty().WithMessage("El ánimo recomendado es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.RotationCategory)
            .NotEmpty().WithMessage("La categoría de rotación es obligatoria.")
            .MaximumLength(100);

        RuleFor(x => x.WhyRead)
            .MaximumLength(1000).When(x => x.WhyRead is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes is not null);
    }
}
```

```csharp
// src/ReadingQueue.Api/Validators/UpdateBookRequestValidator.cs
using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class UpdateBookRequestValidator : AbstractValidator<UpdateBookRequest>
{
    public UpdateBookRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("El título es obligatorio.")
            .MaximumLength(500);

        RuleFor(x => x.Author)
            .NotEmpty().WithMessage("El autor es obligatorio.")
            .MaximumLength(300);

        RuleFor(x => x.Genre)
            .NotEmpty().WithMessage("El género es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage("El país es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.Priority)
            .InclusiveBetween(1, 5)
            .WithMessage("La prioridad debe estar entre 1 y 5.");

        RuleFor(x => x.MentalEnergy)
            .NotEmpty().WithMessage("El nivel de energía mental es obligatorio.")
            .MaximumLength(100);

        RuleFor(x => x.RecommendedMood)
            .NotEmpty().WithMessage("El ánimo recomendado es obligatorio.")
            .MaximumLength(200);

        RuleFor(x => x.RotationCategory)
            .NotEmpty().WithMessage("La categoría de rotación es obligatoria.")
            .MaximumLength(100);

        RuleFor(x => x.WhyRead)
            .MaximumLength(1000).When(x => x.WhyRead is not null);

        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes is not null);
    }
}
```

```csharp
// src/ReadingQueue.Api/Validators/MarkAsReadRequestValidator.cs
using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class MarkAsReadRequestValidator : AbstractValidator<MarkAsReadRequest>
{
    public MarkAsReadRequestValidator()
    {
        RuleFor(x => x.Notes)
            .MaximumLength(2000).When(x => x.Notes is not null);
    }
}
```

- **Completado cuando:** tests de TASK-03-E1 pasan (verde).

---

## Bloque F — API: BookEndpoints y Program.cs

### TASK-03-F1 · Test de integración: `BookEndpoints`

- **Archivos test:**
  - `tests/ReadingQueue.Api.Tests/Endpoints/BookEndpointsTests.cs`
  - `tests/ReadingQueue.Api.Tests/Endpoints/BookEndpointsIsolationTests.cs`

- **Fixture:** `IClassFixture<BookEndpointsFixture>` — misma estructura que `AuthEndpointsFixture`:
  - Inicia Testcontainers SQL Server.
  - `WebApplicationFactory<Program>` con `ConnectionStrings:DefaultConnection` y `Jwt:SecretKey` de test.
  - Helper `RegisterAndLoginAsync(email, password, displayName)` → retorna `(HttpClient autenticado, AuthResponse)`.
  - Helper `CreateBookAsync(client, book)` → retorna `BookResponse`.

- **Nota:** usar valores de referencia exactos del seed: `"Clasico"`, `"Baja - cualquier momento"`, etc.

- **Casos — GET /api/books (CA-01, CA-02):**
  - [ ] Sin filtros → `200 OK` con todos los libros del usuario (CA-01).
  - [ ] Dos usuarios distintos → cada uno solo ve sus propios libros (CA-02).

- **Casos — GET /api/books con filtros (CA-03, CA-04, CA-05, CA-06, CA-07):**
  - [ ] `?genre=Clasico` → retorna solo libros de ese género (CA-03).
  - [ ] `?isRead=false` → retorna solo libros no leídos (CA-04).
  - [ ] `?q=marquez` → retorna libros con "marquez" en título o autor, case-insensitive (CA-05).
  - [ ] `?minPriority=4` → retorna libros con prioridad 4 y 5 (CA-06).
  - [ ] `?genre=Clasico&isRead=false` → aplica ambos filtros (AND) (CA-07).

- **Casos — GET /api/books/{id} (CA-08):**
  - [ ] `GET /api/books/{id}` de libro de otro usuario → `404 Not Found` (CA-08).

- **Casos — POST /api/books (CA-09, CA-10, CA-11, CA-12):**
  - [ ] Datos válidos → `201 Created` con el objeto completo (CA-09).
  - [ ] `genre` inválido → `422` con mensaje en `errors.genre` (CA-10).
  - [ ] `UserId` del JWT, no del body (CA-11) — el body no acepta `userId`.
  - [ ] Enviar `isRead: true` en el body → se ignora, libro se crea con `isRead: false` (CA-12).

- **Casos — PUT /api/books/{id} (CA-13, CA-14):**
  - [ ] Datos válidos → `200 OK` con el objeto actualizado y `updatedAt` posterior al `createdAt` (CA-13).
  - [ ] Libro de otro usuario → `404 Not Found` (CA-14).

- **Casos — DELETE /api/books/{id} (CA-15, CA-16, CA-17, CA-18):**
  - [ ] Libro propio → `204 No Content` (CA-15).
  - [ ] Tras DELETE, query SQL confirma que no hay fila en `ReadingQueue` para ese `BookId` (CA-16).
  - [ ] Tras DELETE, query SQL confirma que no hay fila en `AISuggestions` para ese `BookId` (CA-17).
  - [ ] Libro de otro usuario → `404 Not Found` (CA-18).

- **Casos — POST /api/books/{id}/read (CA-19, CA-20, CA-21, CA-22):**
  - [ ] Con body `{ readAt, notes }` → `200 OK`, query SQL confirma `IsRead=1` y `ReadAt` correcto (CA-19).
  - [ ] Sin body → `200 OK` con `IsRead=true` y `ReadAt ≈ UtcNow` (CA-20).
  - [ ] Tras marcar leído, query SQL confirma que no hay fila en `ReadingQueue` (CA-21).
  - [ ] Libro ya leído → `200 OK` igualmente (CA-22).

- **Casos — POST /api/books/{id}/unread (CA-23, CA-24):**
  - [ ] Libro leído → `200 OK`, query SQL confirma `IsRead=0` y `ReadAt=null` (CA-23).
  - [ ] `Notes` no cambia al marcar como no leído (CA-24).

- **Casos — GET /api/books/reference/* (CA-25, CA-26, CA-27):**
  - [ ] `GET /api/books/reference/genres` → `200 OK` con exactamente 7 géneros (CA-25).
  - [ ] `GET /api/books/reference/mental-energy` → `200 OK` con 5 niveles, primer elemento `SortOrder=1` (CA-26).
  - [ ] Segunda llamada a `/reference/genres` → misma respuesta (cache — no verifica internamente, basta `200 OK`) (CA-27).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-03-F2 · Implementar `BookEndpoints` y actualizar `Program.cs`

**Archivos a crear / actualizar:**

```
src/ReadingQueue.Api/
  Endpoints/
    BookEndpoints.cs       ← nuevo
  Program.cs               ← actualizar (agregar DI y endpoints)
```

```csharp
// src/ReadingQueue.Api/Endpoints/BookEndpoints.cs
using ReadingQueue.Api.Extensions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;
using ReadingQueue.Api.Validators;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Api.Endpoints;

public static class BookEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/api/books").RequireAuthorization().WithTags("Books");

        books.MapGet("/",                   GetAll);
        books.MapGet("/{id:int}",           GetById);
        books.MapPost("/",                  Create);
        books.MapPut("/{id:int}",           Update);
        books.MapDelete("/{id:int}",        Delete);
        books.MapPost("/{id:int}/read",     MarkAsRead);
        books.MapPost("/{id:int}/unread",   MarkAsUnread);

        var reference = books.MapGroup("/reference").WithTags("Reference");
        reference.MapGet("/genres",               GetGenres);
        reference.MapGet("/mental-energy",        GetMentalEnergy);
        reference.MapGet("/moods",                GetMoods);
        reference.MapGet("/rotation-categories",  GetRotationCategories);
    }

    private static async Task<IResult> GetAll(
        HttpContext ctx,
        GetFilteredBooks useCase,
        string? genre = null, string? country = null, string? mentalEnergy = null,
        string? mood = null, string? rotation = null, int? minPriority = null,
        bool? isRead = null, string? q = null)
    {
        var userId = ctx.GetUserId();
        var filter = new BookFilter(genre, country, mentalEnergy, mood, rotation,
                                    minPriority, isRead, q);
        var books = await useCase.ExecuteAsync(new GetFilteredBooks.Query(userId, filter));
        return Results.Ok(books.Select(ToResponse));
    }

    private static async Task<IResult> GetById(
        int id, HttpContext ctx, GetBookById useCase)
    {
        var book = await useCase.ExecuteAsync(
            new GetBookById.Query(id, ctx.GetUserId()));
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> Create(
        CreateBookRequest req,
        HttpContext ctx,
        CreateBook useCase,
        CreateBookRequestValidator validator)
    {
        var validation = await validator.ValidateAsync(req);
        if (!validation.IsValid)
            return Results.UnprocessableEntity(new
            {
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName.ToLower())
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });

        var cmd = new CreateBook.Command(ctx.GetUserId(), req.Title, req.Author,
            req.Genre, req.Country, req.WhyRead, req.Priority, req.MentalEnergy,
            req.RecommendedMood, req.RotationCategory, req.Notes);

        var book = await useCase.ExecuteAsync(cmd);
        return Results.Created($"/api/books/{book.Id}", ToResponse(book));
    }

    private static async Task<IResult> Update(
        int id,
        UpdateBookRequest req,
        HttpContext ctx,
        UpdateBook useCase,
        UpdateBookRequestValidator validator)
    {
        var validation = await validator.ValidateAsync(req);
        if (!validation.IsValid)
            return Results.UnprocessableEntity(new
            {
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName.ToLower())
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });

        var cmd = new UpdateBook.Command(id, ctx.GetUserId(), req.Title, req.Author,
            req.Genre, req.Country, req.WhyRead, req.Priority, req.MentalEnergy,
            req.RecommendedMood, req.RotationCategory, req.Notes);

        var book = await useCase.ExecuteAsync(cmd);
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> Delete(
        int id, HttpContext ctx, DeleteBook useCase)
    {
        await useCase.ExecuteAsync(new DeleteBook.Command(id, ctx.GetUserId()));
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAsRead(
        int id,
        HttpContext ctx,
        MarkBookAsRead useCase,
        MarkAsReadRequest? req = null)
    {
        var cmd = new MarkBookAsRead.Command(id, ctx.GetUserId(),
            req?.ReadAt, req?.Notes);
        var book = await useCase.ExecuteAsync(cmd);
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> MarkAsUnread(
        int id, HttpContext ctx, MarkBookAsUnread useCase)
    {
        var book = await useCase.ExecuteAsync(
            new MarkBookAsUnread.Command(id, ctx.GetUserId()));
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> GetGenres(GetReferenceData useCase)
        => Results.Ok(await useCase.GetGenresAsync());

    private static async Task<IResult> GetMentalEnergy(GetReferenceData useCase)
        => Results.Ok(await useCase.GetMentalEnergyLevelsAsync());

    private static async Task<IResult> GetMoods(GetReferenceData useCase)
        => Results.Ok(await useCase.GetMoodsAsync());

    private static async Task<IResult> GetRotationCategories(GetReferenceData useCase)
        => Results.Ok(await useCase.GetRotationCategoriesAsync());

    private static BookResponse ToResponse(Book b) => new(
        b.Id, b.UserId, b.Title, b.Author, b.Genre, b.Country, b.WhyRead,
        b.Priority, b.MentalEnergy, b.RecommendedMood, b.RotationCategory,
        b.IsRead, b.ReadAt, b.Notes, b.CreatedAt, b.UpdatedAt);
}
```

**Actualizar `Program.cs`** — agregar después de los registros del Spec-02:

```csharp
// ── Repositorios (Spec-03) ────────────────────────────────────────────────────
builder.Services.AddScoped<IBookRepository,          SqlBookRepository>();
builder.Services.AddScoped<IReferenceDataRepository, SqlReferenceDataRepository>();

// ── Use cases (Spec-03) ──────────────────────────────────────────────────────
builder.Services.AddScoped<GetFilteredBooks>();
builder.Services.AddScoped<GetBookById>();
builder.Services.AddScoped<CreateBook>();
builder.Services.AddScoped<UpdateBook>();
builder.Services.AddScoped<DeleteBook>();
builder.Services.AddScoped<MarkBookAsRead>();
builder.Services.AddScoped<MarkBookAsUnread>();
builder.Services.AddScoped<GetReferenceData>();

// ── Validadores (Spec-03) ─────────────────────────────────────────────────────
builder.Services.AddScoped<CreateBookRequestValidator>();
builder.Services.AddScoped<UpdateBookRequestValidator>();
builder.Services.AddScoped<MarkAsReadRequestValidator>();

// ── Cache (Spec-03) ───────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
```

Agregar al switch del `ExceptionHandler` en `Program.cs`:

```csharp
BookNotFoundException e => (404, (object)new { error = e.Message }),
ValidationException   e => (422, (object)new { errors = e.Errors }),
```

Agregar al final de los endpoints (antes de `app.Run()`):

```csharp
BookEndpoints.Map(app);
```

- **Completado cuando:** tests de TASK-03-F1 pasan (verde).

---

## Bloque G — Verificación Final

### TASK-03-G1 · Build .NET sin errores ni warnings

```powershell
dotnet build ReadingQueue.sln
```

- **Criterio:** `0 Error(s)  0 Warning(s)`.

### TASK-03-G2 · Tests de Domain y Application pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Domain.Tests/ReadingQueue.Domain.Tests.csproj --no-build -v normal
dotnet test tests/ReadingQueue.Application.Tests/ReadingQueue.Application.Tests.csproj --no-build -v normal
```

- **Criterio:** todos los tests unitarios pasan.

### TASK-03-G3 · Tests de Infrastructure pasan (verde, incluyendo nuevos)

```powershell
dotnet test tests/ReadingQueue.Infrastructure.Tests/ReadingQueue.Infrastructure.Tests.csproj --no-build -v normal
```

- **Criterio:** `SqlBookRepositoryTests` y `SqlReferenceDataRepositoryTests` pasan junto con los tests de Spec-01 y Spec-02.

### TASK-03-G4 · Tests de API pasan (verde, incluyendo nuevos)

```powershell
dotnet test tests/ReadingQueue.Api.Tests/ReadingQueue.Api.Tests.csproj --no-build -v normal
```

- **Criterio:** `BookEndpointsTests`, `BookEndpointsIsolationTests` y `CreateBookRequestValidatorTests` pasan junto con todos los tests anteriores.

---

## Resumen de archivos que genera SPEC-03

| # | Archivo | Bloque |
|---|---|---|
| 1  | `src/ReadingQueue.Domain/Entities/Book.cs` | B |
| 2  | `src/ReadingQueue.Domain/ValueObjects/BookFilter.cs` | B |
| 3  | `src/ReadingQueue.Domain/ValueObjects/CreateBookData.cs` | B |
| 4  | `src/ReadingQueue.Domain/ValueObjects/UpdateBookData.cs` | B |
| 5  | `src/ReadingQueue.Domain/Exceptions/BookNotFoundException.cs` | B |
| 6  | `src/ReadingQueue.Domain/Exceptions/ValidationException.cs` | B |
| 7  | `src/ReadingQueue.Domain/Interfaces/IBookRepository.cs` | B |
| 8  | `src/ReadingQueue.Domain/Interfaces/IReferenceDataRepository.cs` | B |
| 9  | `src/ReadingQueue.Infrastructure/Data/SqlBookRepository.cs` | C |
| 10 | `src/ReadingQueue.Infrastructure/Sql/BookQueries.cs` | C |
| 11 | `src/ReadingQueue.Infrastructure/Data/SqlReferenceDataRepository.cs` | C |
| 12 | `src/ReadingQueue.Infrastructure/Sql/ReferenceQueries.cs` | C |
| 13 | `src/ReadingQueue.Application/UseCases/GetFilteredBooks.cs` | D |
| 14 | `src/ReadingQueue.Application/UseCases/GetBookById.cs` | D |
| 15 | `src/ReadingQueue.Application/UseCases/CreateBook.cs` | D |
| 16 | `src/ReadingQueue.Application/UseCases/UpdateBook.cs` | D |
| 17 | `src/ReadingQueue.Application/UseCases/DeleteBook.cs` | D |
| 18 | `src/ReadingQueue.Application/UseCases/MarkBookAsRead.cs` | D |
| 19 | `src/ReadingQueue.Application/UseCases/MarkBookAsUnread.cs` | D |
| 20 | `src/ReadingQueue.Application/UseCases/GetReferenceData.cs` | D |
| 21 | `src/ReadingQueue.Api/Requests/CreateBookRequest.cs` | E |
| 22 | `src/ReadingQueue.Api/Requests/UpdateBookRequest.cs` | E |
| 23 | `src/ReadingQueue.Api/Requests/MarkAsReadRequest.cs` | E |
| 24 | `src/ReadingQueue.Api/Responses/BookResponse.cs` | E |
| 25 | `src/ReadingQueue.Api/Validators/CreateBookRequestValidator.cs` | E |
| 26 | `src/ReadingQueue.Api/Validators/UpdateBookRequestValidator.cs` | E |
| 27 | `src/ReadingQueue.Api/Validators/MarkAsReadRequestValidator.cs` | E |
| 28 | `src/ReadingQueue.Api/Endpoints/BookEndpoints.cs` | F |
| 29 | `src/ReadingQueue.Api/Program.cs` (actualizado) | F |
| 30 | `tests/ReadingQueue.Domain.Tests/BookEntityTests.cs` | B |
| 31 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlBookRepositoryTests.cs` | C |
| 32 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlReferenceDataRepositoryTests.cs` | C |
| 33 | `tests/ReadingQueue.Application.Tests/UseCases/GetFilteredBooksTests.cs` | D |
| 34 | `tests/ReadingQueue.Application.Tests/UseCases/GetBookByIdTests.cs` | D |
| 35 | `tests/ReadingQueue.Application.Tests/UseCases/CreateBookTests.cs` | D |
| 36 | `tests/ReadingQueue.Application.Tests/UseCases/UpdateBookTests.cs` | D |
| 37 | `tests/ReadingQueue.Application.Tests/UseCases/DeleteBookTests.cs` | D |
| 38 | `tests/ReadingQueue.Application.Tests/UseCases/MarkBookAsReadTests.cs` | D |
| 39 | `tests/ReadingQueue.Application.Tests/UseCases/MarkBookAsUnreadTests.cs` | D |
| 40 | `tests/ReadingQueue.Application.Tests/UseCases/GetReferenceDataTests.cs` | D |
| 41 | `tests/ReadingQueue.Api.Tests/Validators/CreateBookRequestValidatorTests.cs` | E |
| 42 | `tests/ReadingQueue.Api.Tests/Endpoints/BookEndpointsTests.cs` | F |
| 43 | `tests/ReadingQueue.Api.Tests/Endpoints/BookEndpointsIsolationTests.cs` | F |

---

## Checklist SPEC-03

### Bloque A — NuGet
- [x] TASK-03-A1 · `Microsoft.Extensions.Caching.Abstractions` en Application

### Bloque B — Domain
- [x] TASK-03-B1 · Tests de entidad `Book` (rojo)
- [x] TASK-03-B2 · Impl `Book` + value objects `BookFilter`, `CreateBookData`, `UpdateBookData` (verde)
- [x] TASK-03-B3 · Impl excepciones `BookNotFoundException` + `ValidationException`
- [x] TASK-03-B4 · Interfaces `IBookRepository` + `IReferenceDataRepository` creadas

### Bloque C — Infrastructure
- [x] TASK-03-C1 · Tests `SqlBookRepository` con Testcontainers (rojo)
- [x] TASK-03-C2 · Impl `SqlBookRepository` + `BookQueries` (verde)
- [x] TASK-03-C3 · Tests `SqlReferenceDataRepository` con Testcontainers (rojo)
- [x] TASK-03-C4 · Impl `SqlReferenceDataRepository` + `ReferenceQueries` (verde)

### Bloque D — Application
- [x] TASK-03-D1 · Tests `GetFilteredBooks` + `GetBookById` (rojo)
- [x] TASK-03-D2 · Impl `GetFilteredBooks` + `GetBookById` (verde)
- [x] TASK-03-D3 · Tests `CreateBook` + `UpdateBook` (rojo)
- [x] TASK-03-D4 · Impl `CreateBook` + `UpdateBook` (verde)
- [x] TASK-03-D5 · Tests `DeleteBook` (rojo)
- [x] TASK-03-D6 · Impl `DeleteBook` (verde)
- [x] TASK-03-D7 · Tests `MarkBookAsRead` + `MarkBookAsUnread` (rojo)
- [x] TASK-03-D8 · Impl `MarkBookAsRead` + `MarkBookAsUnread` (verde)
- [x] TASK-03-D9 · Tests `GetReferenceData` (rojo)
- [x] TASK-03-D10 · Impl `GetReferenceData` (verde)

### Bloque E — API: Validators, Requests, Responses
- [x] TASK-03-E1 · Tests `CreateBookRequestValidator` (rojo)
- [x] TASK-03-E2 · Impl validators + requests + responses (verde)

### Bloque F — BookEndpoints + Program.cs
- [x] TASK-03-F1 · Tests de integración `BookEndpoints` + isolation con Testcontainers (rojo)
- [x] TASK-03-F2 · Impl `BookEndpoints` + `Program.cs` actualizado (verde)

### Bloque G — Verificación Final
- [x] TASK-03-G1 · `dotnet build` → 0 errores, 0 warnings
- [x] TASK-03-G2 · Tests de Domain y Application pasan (verde)
- [x] TASK-03-G3 · Tests de Infrastructure pasan (verde, incluye nuevos)
- [x] TASK-03-G4 · Tests de Api pasan (verde, incluye nuevos)
