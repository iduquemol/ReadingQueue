# spec-03-crud-biblioteca.md
# Feature: CRUD de Biblioteca y Gestión de Libros

## 1. Resumen

Implementar la gestión completa de la biblioteca personal de libros de cada
usuario: crear, leer, actualizar y eliminar libros; aplicar filtros
combinables sobre la colección; marcar libros como leídos o no leídos; y
exponer los endpoints de valores de referencia (géneros, energías, ánimos,
categorías de rotación) que el frontend necesita para poblar los selectores
del formulario. Este spec es el núcleo de datos de la aplicación — todo lo
que hacen los Specs 4 y 5 (cola e IA) opera sobre los libros que este spec
gestiona.

---

## 2. Motivación

Sin la biblioteca no existe cola. El Spec 4 necesita poder leer todos los
libros no leídos de un usuario para construir la cola. El Spec 5 necesita
el historial de libros leídos para alimentar a Claude. La acción de marcar
un libro como leído es el evento central que dispara la regeneración de la
cola — ese flujo empieza aquí, en el repositorio de libros, aunque su
efecto cascada se implementa en los specs posteriores. Este spec deja el
evento preparado y documentado para que los specs 4 y 5 lo consuman.

---

## 3. Usuarios y Casos de Uso

| Actor | Caso de uso |
|---|---|
| Usuario autenticado | Ver todos sus libros con filtros combinables |
| Usuario autenticado | Ver el detalle de un libro específico |
| Usuario autenticado | Agregar un libro nuevo a su biblioteca (12 campos) |
| Usuario autenticado | Editar cualquier campo de un libro existente |
| Usuario autenticado | Eliminar un libro de su biblioteca |
| Usuario autenticado | Marcar un libro como leído (registra fecha y notas opcionales) |
| Usuario autenticado | Marcar un libro como no leído (revertir) |
| Usuario autenticado | Consultar los valores válidos de géneros, energías, ánimos y rotaciones |
| Sistema (Spec 4) | Leer todos los libros no leídos de un usuario para generar la cola |
| Sistema (Spec 5) | Leer todos los libros leídos de un usuario para alimentar a Claude |

---

## 4. Requisitos Funcionales

### RF-01 — Listar libros con filtros
- Retorna todos los libros del usuario autenticado.
- Acepta todos los filtros definidos en la constitution (ver sección 8),
  combinables entre sí. Un request sin filtros retorna todos los libros.
- Los filtros `isRead=true` e `isUnread=true` son mutuamente excluyentes:
  si se envían ambos, se ignora `isUnread` y se aplica `isRead`.
- El filtro `q` busca por subcadena case-insensitive en `Title` y `Author`.
- El filtro `minPriority` retorna libros con `Priority >= minPriority`.
- El resultado se ordena por defecto por `Priority DESC`, luego `CreatedAt ASC`.
- Un usuario nunca puede ver libros de otro usuario — el `UserId` siempre
  viene del JWT, nunca de un parámetro de query.

### RF-02 — Obtener libro por ID
- Retorna el detalle completo de un libro.
- Si el libro no existe o no pertenece al usuario autenticado, retorna `404`.
- Nunca retorna `403` — la ausencia de pertenencia se expresa como `404`
  para no revelar que el libro existe pero pertenece a otro usuario.

### RF-03 — Crear libro
- Acepta los 12 campos canónicos definidos en la constitution.
- `Title`, `Author`, `Genre`, `MentalEnergy`, `RecommendedMood` y
  `RotationCategory` son obligatorios.
- `Country` es obligatorio.
- `Priority` es obligatorio, debe estar entre 1 y 5. Si no se envía,
  el backend lo defaultea a 3.
- `WhyRead` y `Notes` son opcionales.
- `Genre`, `MentalEnergy`, `RecommendedMood` y `RotationCategory` deben
  ser uno de los valores canónicos registrados en las tablas de referencia.
  Si se envía un valor no reconocido, retorna `422` con mensaje descriptivo.
- Al crear, `IsRead = false` y `ReadAt = null` siempre — el cliente no puede
  crear un libro ya marcado como leído.
- El `UserId` se asigna desde el JWT, nunca desde el body.
- Retorna `201 Created` con el libro completo recién creado.

### RF-04 — Editar libro
- Acepta los mismos campos que RF-03, todos opcionales en el PUT.
- Solo se actualizan los campos que se envían en el body (patch semántico
  implementado como PUT — el frontend siempre envía el objeto completo).
- `IsRead` y `ReadAt` no son editables vía este endpoint — solo se
  modifican a través de `POST /{id}/read` y `POST /{id}/unread`.
- Si el libro no existe o no pertenece al usuario autenticado, retorna `404`.
- Actualiza `UpdatedAt = GETUTCDATE()` en SQL.
- Los valores de referencia se validan igual que en RF-03.

### RF-05 — Eliminar libro
- Elimina físicamente el libro de la base de datos.
- Si el libro está en la cola activa (`ReadingQueue`), se elimina también
  de la cola (cascade delete o delete explícito antes del libro).
- Si el libro tiene sugerencias en `AISuggestions`, se eliminan también.
- Si el libro no existe o no pertenece al usuario autenticado, retorna `404`.
- Retorna `204 No Content` al eliminar correctamente.
- Esta operación usa transacción explícita: primero elimina de
  `ReadingQueue`, luego de `AISuggestions`, luego de `Books`.

### RF-06 — Marcar libro como leído
- Acepta `readAt` (fecha ISO 8601, opcional — si no se envía usa `GETUTCDATE()`)
  y `notes` (string opcional, reemplaza el valor previo).
- Establece `IsRead = true`, `ReadAt = @ReadAt`, `UpdatedAt = GETUTCDATE()`.
- Si el libro ya está marcado como leído, la operación es idempotente:
  actualiza `ReadAt` y `Notes` con los nuevos valores y retorna `200`.
- Si el libro no existe o no pertenece al usuario, retorna `404`.
- Retorna `200 OK` con el libro actualizado completo.
- **Nota para Spec 4**: este endpoint debe eliminar el libro de
  `ReadingQueue` si está en ella, porque un libro leído no debe aparecer
  en la cola. Esta limpieza se hace en la misma transacción.

### RF-07 — Marcar libro como no leído
- Establece `IsRead = false`, `ReadAt = null`, `UpdatedAt = GETUTCDATE()`.
- `Notes` no se modifica al desmarcar como leído.
- Si el libro no existe o no pertenece al usuario, retorna `404`.
- Si el libro ya está como no leído, la operación es idempotente.
- Retorna `200 OK` con el libro actualizado completo.

### RF-08 — Endpoints de valores de referencia
- `GET /api/books/reference/genres` retorna la lista de géneros válidos.
- `GET /api/books/reference/mental-energy` retorna los niveles de energía
  ordenados por `SortOrder ASC`.
- `GET /api/books/reference/moods` retorna los ánimos válidos.
- `GET /api/books/reference/rotation-categories` retorna las categorías
  de rotación válidas.
- Estos endpoints requieren autenticación pero no tienen lógica de usuario
  — todos los usuarios ven los mismos valores.
- Las respuestas de referencia se cachean en `IMemoryCache` con TTL de
  24 horas — son datos que nunca cambian sin una migración.

---

## 5. Requisitos No Funcionales

- **Aislamiento estricto**: Ningún query a `Books` puede retornar filas
  de otro usuario. El `WHERE UserId = @UserId` es obligatorio en todo
  SELECT, UPDATE y DELETE de esta tabla.
- **Validación de referencia en BD**: Los valores de `Genre`,
  `MentalEnergy`, `RecommendedMood` y `RotationCategory` se validan
  contra las tablas de referencia en la capa Application antes de
  persistir — no solo con anotaciones de validación.
- **Transacciones en eliminación**: El DELETE de un libro usa
  `IDbTransaction` explícita para garantizar que la eliminación en cascada
  de `ReadingQueue` y `AISuggestions` sea atómica.
- **Sin SQL dinámico**: Los filtros combinables se implementan con SQL
  que usa `WHERE 1=1` y condiciones `AND @Param IS NULL OR Campo = @Param`,
  no con concatenación de strings SQL.

---

## 6. Modelo de Dominio

```csharp
// src/ReadingQueue.Domain/Entities/Book.cs
public sealed class Book
{
    public int Id { get; }
    public int UserId { get; }
    public string Title { get; }
    public string Author { get; }
    public string Genre { get; }
    public string Country { get; }
    public string? WhyRead { get; }
    public int Priority { get; }
    public string MentalEnergy { get; }
    public string RecommendedMood { get; }
    public string RotationCategory { get; }
    public bool IsRead { get; }
    public DateTime? ReadAt { get; }
    public string? Notes { get; }
    public DateTime CreatedAt { get; }
    public DateTime UpdatedAt { get; }

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

// src/ReadingQueue.Domain/ValueObjects/BookFilter.cs
public sealed record BookFilter(
    string?  Genre          = null,
    string?  Country        = null,
    string?  MentalEnergy   = null,
    string?  Mood           = null,
    string?  Rotation       = null,
    int?     MinPriority    = null,
    bool?    IsRead         = null,   // null = todos, true = leídos, false = no leídos
    string?  SearchQuery    = null    // busca en Title y Author
);

// src/ReadingQueue.Domain/Interfaces/IBookRepository.cs
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

    Task MarkAsReadAsync(
        int bookId, int userId, DateTime readAt, string? notes,
        CancellationToken ct = default);

    Task MarkAsUnreadAsync(
        int bookId, int userId, CancellationToken ct = default);

    // Usados por Spec 4 y 5
    Task<IEnumerable<Book>> GetUnreadByUserAsync(
        int userId, CancellationToken ct = default);

    Task<IEnumerable<Book>> GetReadByUserAsync(
        int userId, CancellationToken ct = default);
}

// src/ReadingQueue.Domain/Interfaces/IReferenceDataRepository.cs
public interface IReferenceDataRepository
{
    Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct = default);
}

// src/ReadingQueue.Domain/Exceptions/BookNotFoundException.cs
public sealed class BookNotFoundException : Exception
{
    public BookNotFoundException(int bookId)
        : base($"Libro {bookId} no encontrado.") { }
}

// src/ReadingQueue.Domain/Exceptions/ValidationException.cs
public sealed class ValidationException : Exception
{
    public IReadOnlyDictionary<string, string[]> Errors { get; }

    public ValidationException(string field, string message)
        : base(message)
    {
        Errors = new Dictionary<string, string[]>
        {
            [field] = [message]
        };
    }
}
```

---

## 7. Modelo de Application (Use Cases)

```csharp
// src/ReadingQueue.Application/UseCases/GetFilteredBooks.cs
public sealed class GetFilteredBooks
{
    private readonly IBookRepository _books;

    public record Query(int UserId, BookFilter Filter);

    public async Task<IEnumerable<Book>> ExecuteAsync(Query q, CancellationToken ct)
        => await _books.GetByUserAsync(q.UserId, q.Filter, ct);
}

// src/ReadingQueue.Application/UseCases/GetBookById.cs
public sealed class GetBookById
{
    public record Query(int BookId, int UserId);
    // Lanza BookNotFoundException si no existe o no pertenece al usuario
}

// src/ReadingQueue.Application/UseCases/CreateBook.cs
public sealed class CreateBook
{
    private readonly IBookRepository _books;
    private readonly IReferenceDataRepository _refs;

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

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct)
    {
        // 1. Validar Genre contra tabla de referencia
        // 2. Validar MentalEnergy contra tabla de referencia
        // 3. Validar RecommendedMood contra tabla de referencia
        // 4. Validar RotationCategory contra tabla de referencia
        // 5. Crear el libro y retornar la entidad completa
    }
}

// src/ReadingQueue.Application/UseCases/UpdateBook.cs
public sealed class UpdateBook
{
    // Misma lógica de validación de referencia que CreateBook
    // Lanza BookNotFoundException si no existe o no es del usuario
}

// src/ReadingQueue.Application/UseCases/DeleteBook.cs
public sealed class DeleteBook
{
    private readonly IBookRepository _books;
    private readonly IDbConnectionFactory _factory;

    public record Command(int BookId, int UserId);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct)
    {
        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            // 1. Eliminar de ReadingQueue (si existe)
            // 2. Eliminar de AISuggestions (si existen)
            // 3. Eliminar de Books
            // 4. tx.Commit()
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}

// src/ReadingQueue.Application/UseCases/MarkBookAsRead.cs
public sealed class MarkBookAsRead
{
    public record Command(int BookId, int UserId, DateTime? ReadAt, string? Notes);
    // 1. Verificar que el libro existe y es del usuario
    // 2. Actualizar IsRead, ReadAt, Notes
    // 3. Eliminar de ReadingQueue si estaba en ella (misma transacción)
    // 4. Retornar libro actualizado
}

// src/ReadingQueue.Application/UseCases/MarkBookAsUnread.cs
public sealed class MarkBookAsUnread
{
    public record Command(int BookId, int UserId);
    // 1. Verificar que el libro existe y es del usuario
    // 2. Actualizar IsRead = false, ReadAt = null
    // 3. Retornar libro actualizado
}

// src/ReadingQueue.Application/UseCases/GetReferenceData.cs
public sealed class GetReferenceData
{
    private readonly IReferenceDataRepository _refs;
    private readonly IMemoryCache _cache;

    // TTL de 24h — los datos de referencia nunca cambian sin migración
    public async Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct)
    {
        return await _cache.GetOrCreateAsync("ref:genres", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await _refs.GetGenresAsync(ct);
        }) ?? [];
    }
    // Ídem para GetMentalEnergyLevelsAsync, GetMoodsAsync, GetRotationCategoriesAsync
}
```

---

## 8. Contrato de API

### GET `/api/books`
Lista todos los libros del usuario con filtros opcionales combinables.

**Query params (todos opcionales):**

| Parámetro | Tipo | Descripción |
|---|---|---|
| `genre` | string | Filtra por género exacto |
| `country` | string | Filtra por país exacto |
| `mentalEnergy` | string | Filtra por nivel de energía exacto |
| `mood` | string | Filtra por ánimo exacto |
| `rotation` | string | Filtra por categoría de rotación exacta |
| `minPriority` | int (1-5) | Retorna libros con prioridad ≥ valor |
| `isRead` | bool | `true` = solo leídos, `false` = solo no leídos |
| `q` | string | Búsqueda en título y autor (case-insensitive) |

**Response `200 OK`:**
```json
[
  {
    "id": 1,
    "userId": 42,
    "title": "Cien años de soledad",
    "author": "Gabriel García Márquez",
    "genre": "Novela latinoamericana",
    "country": "Colombia",
    "whyRead": "El clásico latinoamericano que siempre postergué",
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
]
```

---

### GET `/api/books/{id}`
Retorna el detalle de un libro del usuario.

**Response `200 OK`:** mismo shape que el objeto de la lista.

**Responses de error:**
- `404 Not Found` — libro no existe o no pertenece al usuario autenticado

---

### POST `/api/books`
Crea un libro nuevo en la biblioteca del usuario.

**Request:**
```json
{
  "title": "Cien años de soledad",
  "author": "Gabriel García Márquez",
  "genre": "Novela latinoamericana",
  "country": "Colombia",
  "whyRead": "El clásico latinoamericano que siempre postergué",
  "priority": 5,
  "mentalEnergy": "🔴 Máxima – modo lector",
  "recommendedMood": "Solemne / quiero leer algo grande",
  "rotationCategory": "Novela grande",
  "notes": null
}
```

**Response `201 Created`:** objeto `Book` completo con `id`, `userId`,
`createdAt` y `updatedAt` asignados por el servidor.

**Responses de error:**
- `422 Unprocessable Entity` — campo obligatorio faltante o valor de
  referencia inválido
  ```json
  {
    "errors": {
      "genre": ["'Novela histórica' no es un género válido."],
      "title": ["El título es obligatorio."]
    }
  }
  ```

---

### PUT `/api/books/{id}`
Actualiza un libro existente del usuario.

**Request:** mismos campos que POST, todos opcionales. El frontend siempre
envía el objeto completo.

**Response `200 OK`:** objeto `Book` completo actualizado.

**Responses de error:**
- `404 Not Found` — libro no existe o no pertenece al usuario
- `422 Unprocessable Entity` — valor de referencia inválido

---

### DELETE `/api/books/{id}`
Elimina un libro y lo remueve de la cola y sugerencias en la misma
transacción.

**Response `204 No Content`**

**Responses de error:**
- `404 Not Found` — libro no existe o no pertenece al usuario

---

### POST `/api/books/{id}/read`
Marca un libro como leído.

**Request (body opcional):**
```json
{
  "readAt": "2026-05-01T00:00:00Z",
  "notes": "Impresionante. La mejor novela que he leído."
}
```

Si no se envía body o `readAt` es null, se usa la fecha UTC actual.

**Response `200 OK`:** objeto `Book` completo con `isRead: true` y
`readAt` establecido.

**Responses de error:**
- `404 Not Found` — libro no existe o no pertenece al usuario

---

### POST `/api/books/{id}/unread`
Revierte un libro a no leído.

**Request:** sin body.

**Response `200 OK`:** objeto `Book` completo con `isRead: false` y
`readAt: null`.

**Responses de error:**
- `404 Not Found` — libro no existe o no pertenece al usuario

---

### GET `/api/books/reference/genres`
Retorna la lista de géneros válidos.

**Response `200 OK`:**
```json
[
  "No ficción / ensayo",
  "Clásico",
  "Novela contemporánea",
  "Novela latinoamericana",
  "Cuentos",
  "Novela clásica",
  "Poesía"
]
```

---

### GET `/api/books/reference/mental-energy`
Retorna los niveles de energía mental ordenados de menor a mayor.

**Response `200 OK`:**
```json
[
  "🟢 Baja – cualquier momento",
  "🔵 Media – tarde tranquila",
  "🟡 Media-alta – fin de semana",
  "🟠 Alta – concentración",
  "🔴 Máxima – modo lector"
]
```

---

### GET `/api/books/reference/moods`
Retorna los ánimos recomendados válidos.

**Response `200 OK`:**
```json
[
  "Analítico / quiero aprender algo",
  "Solemne / quiero leer algo grande",
  "Curioso / quiero algo fresco",
  "Identidad / quiero leer en español",
  "Cansado / quiero entrar y salir",
  "Contemplativo / quiero algo que dure",
  "Sensible / quiero pocos palabras"
]
```

---

### GET `/api/books/reference/rotation-categories`
Retorna las categorías de rotación válidas.

**Response `200 OK`:**
```json
[
  "Ensayo / no ficción",
  "Libro corto o cuentos",
  "Clásico",
  "Novela grande",
  "Contemporáneo latinoamericano o raro"
]
```

---

## 9. Implementación de Infraestructura

### Queries SQL

```csharp
// src/ReadingQueue.Infrastructure/Sql/BookQueries.cs
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
        DELETE FROM Books
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string MarkAsRead = """
        UPDATE Books SET
            IsRead    = 1,
            ReadAt    = @ReadAt,
            Notes     = COALESCE(@Notes, Notes),
            UpdatedAt = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
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

    // Queries de cascada para DELETE — se ejecutan en transacción
    internal const string DeleteFromQueue = """
        DELETE FROM ReadingQueue
        WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string DeleteFromSuggestions = """
        DELETE FROM AISuggestions
        WHERE BookId = @BookId AND UserId = @UserId;
        """;

    // Se usa en MarkAsRead para limpiar la cola
    internal const string RemoveFromQueueIfPresent = """
        DELETE FROM ReadingQueue
        WHERE BookId = @BookId AND UserId = @UserId;
        """;
}

// src/ReadingQueue.Infrastructure/Sql/ReferenceQueries.cs
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

### Registro en Program.cs

```csharp
// Fragmento a agregar en src/ReadingQueue.Api/Program.cs

// ── Repositorios ────────────────────────────────────────────────────────────
builder.Services.AddScoped<IBookRepository,          SqlBookRepository>();
builder.Services.AddScoped<IReferenceDataRepository, SqlReferenceDataRepository>();

// ── Use cases ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<GetFilteredBooks>();
builder.Services.AddScoped<GetBookById>();
builder.Services.AddScoped<CreateBook>();
builder.Services.AddScoped<UpdateBook>();
builder.Services.AddScoped<DeleteBook>();
builder.Services.AddScoped<MarkBookAsRead>();
builder.Services.AddScoped<MarkBookAsUnread>();
builder.Services.AddScoped<GetReferenceData>();

// IMemoryCache ya debe estar registrado (agregar si no está):
builder.Services.AddMemoryCache();

// ── Endpoints ───────────────────────────────────────────────────────────────
var books = app.MapGroup("/api/books").RequireAuthorization();

books.MapGet("/",              BookEndpoints.GetAll)
     .WithName("GetBooks").WithSummary("Lista libros con filtros").WithTags("Books");

books.MapGet("/{id:int}",     BookEndpoints.GetById)
     .WithName("GetBookById").WithSummary("Obtiene un libro por ID").WithTags("Books");

books.MapPost("/",             BookEndpoints.Create)
     .WithName("CreateBook").WithSummary("Crea un libro nuevo").WithTags("Books");

books.MapPut("/{id:int}",     BookEndpoints.Update)
     .WithName("UpdateBook").WithSummary("Actualiza un libro").WithTags("Books");

books.MapDelete("/{id:int}",  BookEndpoints.Delete)
     .WithName("DeleteBook").WithSummary("Elimina un libro").WithTags("Books");

books.MapPost("/{id:int}/read",   BookEndpoints.MarkAsRead)
     .WithName("MarkAsRead").WithSummary("Marca como leído").WithTags("Books");

books.MapPost("/{id:int}/unread", BookEndpoints.MarkAsUnread)
     .WithName("MarkAsUnread").WithSummary("Marca como no leído").WithTags("Books");

// Referencia — sin lógica de usuario, misma autenticación
books.MapGet("/reference/genres",              BookEndpoints.GetGenres)
     .WithName("GetGenres").WithSummary("Lista géneros válidos").WithTags("Reference");

books.MapGet("/reference/mental-energy",       BookEndpoints.GetMentalEnergy)
     .WithName("GetMentalEnergy").WithSummary("Lista niveles de energía").WithTags("Reference");

books.MapGet("/reference/moods",               BookEndpoints.GetMoods)
     .WithName("GetMoods").WithSummary("Lista ánimos válidos").WithTags("Reference");

books.MapGet("/reference/rotation-categories", BookEndpoints.GetRotationCategories)
     .WithName("GetRotationCategories").WithSummary("Lista categorías de rotación").WithTags("Reference");
```

### Manejo de excepciones (agregar al middleware del Spec 2)

```csharp
// Agregar al switch del ExceptionHandler en Program.cs:
BookNotFoundException e => (404, e.Message),
ValidationException   e => (422, new { errors = e.Errors }),
```

---

## 10. Validaciones con FluentValidation

```csharp
// src/ReadingQueue.Api/Validators/CreateBookRequestValidator.cs
public sealed class CreateBookRequestValidator
    : AbstractValidator<CreateBookRequest>
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

---

## 11. Criterios de Aceptación

| ID | Criterio | Verificación |
|---|---|---|
| CA-01 | `GET /api/books` sin filtros retorna todos los libros del usuario autenticado | Test integration |
| CA-02 | `GET /api/books` nunca retorna libros de otro usuario | Test integration con 2 usuarios |
| CA-03 | `GET /api/books?genre=Clásico` retorna solo libros de ese género | Test integration |
| CA-04 | `GET /api/books?isRead=false` retorna solo libros no leídos | Test integration |
| CA-05 | `GET /api/books?q=márquez` retorna libros con "márquez" en título o autor (case-insensitive) | Test integration |
| CA-06 | `GET /api/books?minPriority=4` retorna libros con prioridad 4 y 5 | Test integration |
| CA-07 | Filtros combinados aplican AND entre sí | Test integration con `genre` + `isRead` |
| CA-08 | `GET /api/books/{id}` retorna `404` para libro de otro usuario | Test integration |
| CA-09 | `POST /api/books` crea un libro y retorna `201` con el objeto completo | Test integration |
| CA-10 | `POST /api/books` con `genre` inválido retorna `422` con mensaje descriptivo | Test integration |
| CA-11 | `POST /api/books` asigna `UserId` del JWT, no del body | Test unitario use case |
| CA-12 | `POST /api/books` no permite enviar `isRead = true` (se ignora o retorna 422) | Test integration |
| CA-13 | `PUT /api/books/{id}` actualiza los campos y el `UpdatedAt` | Test integration con query SQL directa |
| CA-14 | `PUT /api/books/{id}` retorna `404` para libro de otro usuario | Test integration |
| CA-15 | `DELETE /api/books/{id}` elimina el libro y retorna `204` | Test integration |
| CA-16 | `DELETE /api/books/{id}` también elimina el libro de `ReadingQueue` en la misma tx | Test integration con SQL directo |
| CA-17 | `DELETE /api/books/{id}` también elimina de `AISuggestions` en la misma tx | Test integration con SQL directo |
| CA-18 | `DELETE /api/books/{id}` retorna `404` para libro de otro usuario | Test integration |
| CA-19 | `POST /api/books/{id}/read` establece `IsRead=true` y `ReadAt` en la BD | Test integration con SQL directo |
| CA-20 | `POST /api/books/{id}/read` sin body usa la fecha UTC del servidor | Test integration |
| CA-21 | `POST /api/books/{id}/read` también elimina el libro de `ReadingQueue` | Test integration con SQL directo |
| CA-22 | `POST /api/books/{id}/read` en libro ya leído es idempotente (retorna `200`) | Test integration |
| CA-23 | `POST /api/books/{id}/unread` establece `IsRead=false` y `ReadAt=null` | Test integration con SQL directo |
| CA-24 | `POST /api/books/{id}/unread` no modifica `Notes` | Test integration |
| CA-25 | `GET /api/books/reference/genres` retorna exactamente 7 géneros | Test integration |
| CA-26 | `GET /api/books/reference/mental-energy` retorna 5 niveles en orden ascendente | Test integration |
| CA-27 | Los datos de referencia se sirven desde cache en la segunda llamada | Test unitario con mock de repositorio |
| CA-28 | El SQL de filtros nunca usa concatenación de strings — solo parámetros nombrados | Code review / test unitario repositorio |

---

## 12. Archivos que este spec genera

```
src/
  ReadingQueue.Domain/
    Entities/
      Book.cs
    ValueObjects/
      BookFilter.cs
      CreateBookData.cs
      UpdateBookData.cs
    Interfaces/
      IBookRepository.cs
      IReferenceDataRepository.cs
    Exceptions/
      BookNotFoundException.cs
      ValidationException.cs        ← si no se creó ya en Spec 2

  ReadingQueue.Application/
    UseCases/
      GetFilteredBooks.cs
      GetBookById.cs
      CreateBook.cs
      UpdateBook.cs
      DeleteBook.cs
      MarkBookAsRead.cs
      MarkBookAsUnread.cs
      GetReferenceData.cs

  ReadingQueue.Infrastructure/
    Data/
      SqlBookRepository.cs
      SqlReferenceDataRepository.cs
    Sql/
      BookQueries.cs
      ReferenceQueries.cs

  ReadingQueue.Api/
    Endpoints/
      BookEndpoints.cs
    Validators/
      CreateBookRequestValidator.cs
      UpdateBookRequestValidator.cs
      MarkAsReadRequestValidator.cs
    Requests/
      CreateBookRequest.cs
      UpdateBookRequest.cs
      MarkAsReadRequest.cs
    Responses/
      BookResponse.cs

tests/
  ReadingQueue.Domain.Tests/
    BookEntityTests.cs
    BookFilterTests.cs

  ReadingQueue.Application.Tests/
    GetFilteredBooksTests.cs
    CreateBookTests.cs
    UpdateBookTests.cs
    DeleteBookTests.cs
    MarkBookAsReadTests.cs
    MarkBookAsUnreadTests.cs
    GetReferenceDataTests.cs

  ReadingQueue.Infrastructure.Tests/
    Data/
      SqlBookRepositoryTests.cs          ← Testcontainers
      SqlReferenceDataRepositoryTests.cs ← Testcontainers

  ReadingQueue.Api.Tests/
    BookEndpointsTests.cs                ← TestServer + JWT real
    BookEndpointsIsolationTests.cs       ← verifica aislamiento entre usuarios
```
