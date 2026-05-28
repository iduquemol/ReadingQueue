# Preparación Avanzada: Preguntas Técnicas Profundas

## NIVEL EXPERTO: Preguntas que te Pueden Hacer

### 1. CONCURRENCIA Y RACE CONDITIONS

**P: ¿Qué pasa si dos usuarios simultáneamente crean un libro con el mismo subgénero que luego se elimina?**

```
T0: Usuario A: POST /books con subgenre="Realismo moderno" ✅ Validación OK
T1: Usuario B: DELETE subgenre="Realismo moderno" desde admin
T2: Usuario A: Aún enviando request (T0 no terminó)
T3: Usuario A: ¿Se crea el libro? ¿Se rechaza?
```

**Respuesta:**

En tu arquitectura actual:
1. **Validación ocurre en CreateBook.cs** antes de insert
2. **Validación lee de tabla Subgenres** en ese momento
3. **Si se elimina ENTRE validación e insert:** 

   Opción 1: El DELETE funciona (sin transacción explícita)
   - Validación pasó ✅
   - DELETE aconteció
   - INSERT de libro funciona ✅
   - El libro tiene `subgenre='Realismo moderno'` pero referencia ya no existe (válido por diseño)

**Mitigación actual:**
- La tabla `Books` NO tiene FK a Subgenres (decisión deliberada)
- Esto permite exactamente este escenario
- El libro es "huérfano" pero válido

**Si quisieras ser más estricto:**
```csharp
// Transacción explícita
using var txn = conn.BeginTransaction();

// 1. Validar (dentro de transacción)
var exists = await conn.ExecuteScalarAsync(
    "SELECT 1 FROM Subgenres WHERE GenreName=@genre AND Name=@subgenre",
    new { genre = cmd.Genre, subgenre = cmd.Subgenre },
    txn);

// 2. Insert (dentro de la misma transacción)
await conn.ExecuteAsync(
    "INSERT INTO Books (Subgenre, ...) VALUES (@subgenre, ...)",
    new { subgenre = cmd.Subgenre },
    txn);

await txn.CommitAsync();
```

**Pero:** Añade complejidad. Tu diseño actual es pragmático.

---

### 2. MIGRACIONES Y ESTADO INCONSISTENTE

**P: ¿Qué pasa si alguien está creando un libro mientras corre la migración 005?**

```sql
-- Corriendo migración 005_update_rotation_categories.sql
DELETE FROM RotationCategories WHERE Name NOT IN (...10 nuevas...);

-- En paralelo, usuario hace:
POST /books con RotationCategory="Ensayo / no ficcion"
```

**Respuesta:**

**Escenario:**
1. Validación en CreateBook lee RotationCategories
2. Ve "Ensayo / no ficcion" ✅
3. Migración corre DELETE
4. CreateBook intenta INSERT con categoría que ya no existe
5. ¿Error?

**Análisis:**

En SQL Server:
- DELETE es READ COMMITTED por defecto
- Si transaction 1 ya "saw" el valor, puede insertarlo
- No hay constraint FK en Books → RotationCategories
- **Resultado: El libro se crea con valor "huérfano"** ✅ (válido)

**En producción (recomendación):**

```sql
-- Migración más segura
BEGIN TRANSACTION;

-- Limpiar libros con categorías obsoletas (si lo deseas)
UPDATE Books
SET RotationCategory = 'Ficción literaria y narrativa general'
WHERE RotationCategory IN ('Novela grande', 'Clasico', ...);

-- Ahora sí, delete de referencias
DELETE FROM RotationCategories
WHERE Name NOT IN (...10 nuevas...);

COMMIT TRANSACTION;
```

---

### 3. PERFORMANCE: N+1 QUERIES

**P: ¿Cuántas queries hace el endpoint GET /books/reference/subgenres?**

**Respuesta:**

```csharp
public async Task<IEnumerable<string>> GetSubgenresAsync(string genre)
{
    var conn = _factory.CreateConnection();
    return await conn.QueryAsync<string>(
        "SELECT Name FROM Subgenres WHERE GenreName = @genre ORDER BY Name",
        new { genre });
}
```

**Queries:** 1️⃣ query única

**Frontend (con TanStack Query):**
```typescript
// Primer render: 1 query
const { data: subgenres } = useSubgenres("Novela contemporanea");

// Re-render con mismo genre: 0 queries (cache)

// Cambiar género: 1 query
setGenre("Poesia");  // ← Automáticamente refetch
```

**Total por sesión:** 
- Cambios de género (típicamente 2-3 máximo)
- **No hay N+1**: Una query por género, no una por cada libro

---

### 4. VALIDACIÓN EN MÚLTIPLES NIVELES

**P: ¿Qué sucede si alguien bypasea el frontend y envía directamente un JSON inválido?**

```json
POST /api/books
{
  "title": "Test",
  "genre": "Novela contemporanea",
  "subgenre": "XYZABC123",  // ← No existe
  ...
}
```

**Respuesta:**

Capas de defensa:

```
1. API Validator (CreateBookRequestValidator)
   ↓
   ✅ Check: Subgenre no vacío
   ✅ Passes: Max length 100 chars
   ✅ Paso 1: Básico JSON schema

2. CreateBook Use Case
   ↓
   ✅ Check: Subgenre pertenece a Subgenres table
   ✅ Check: Subgenre matches Genre
   ✗ Falla: "XYZABC123" no existe
   
   Throws ValidationException("subgenre", "'XYZABC123' no es válido...")
   
   API responde:
   {
     "errors": {
       "subgenre": "'XYZABC123' no es válido para 'Novela contemporanea'"
     }
   }
```

**¿Se crea el libro?** No. ❌

---

### 5. TESTS: COBERTURA DE EDGE CASES

**P: ¿Qué casos edge testiaste?**

**Respuesta:**

```csharp
// ✅ Subgenre válido para el género
[Fact]
public async Task CreateBook_ValidSubgenre_Success()
{
    // Arrange
    var cmd = new CreateBookCommand(
        "Title", "Author", 
        "Novela contemporanea", "Realismo moderno",  // ✅ Match
        ...);
    
    // Act
    var result = await _sut.ExecuteAsync(cmd);
    
    // Assert
    result.Subgenre.Should().Be("Realismo moderno");
}

// ❌ Subgenre no pertenece al género
[Fact]
public async Task CreateBook_SubgenreWrongGenre_Fails()
{
    var cmd = new CreateBookCommand(
        ..., 
        "Poesia",              // Género
        "Realismo moderno",    // ❌ No pertenece a Poesia
        ...);
    
    var act = () => _sut.ExecuteAsync(cmd);
    
    act.Should().ThrowAsync<ValidationException>()
        .Where(e => e.Errors.ContainsKey("subgenre"));
}

// ❌ Subgenre no existe
[Fact]
public async Task CreateBook_NonexistentSubgenre_Fails()
{
    var cmd = new CreateBookCommand(
        ...,
        "Novela contemporanea",
        "XYZABC",  // ❌ No existe
        ...);
    
    var act = () => _sut.ExecuteAsync(cmd);
    
    act.Should().ThrowAsync<ValidationException>();
}

// ❌ Subgenre vacío
[Fact]
public async Task CreateBook_EmptySubgenre_Fails()
{
    var cmd = new CreateBookCommand(
        ...,
        "Novela contemporanea",
        "",  // ❌ Vacío
        ...);
    
    var act = () => _sut.ExecuteAsync(cmd);
    
    act.Should().ThrowAsync<ValidationException>();
}

// ⚠️ Null subgenre
[Fact]
public async Task CreateBook_NullSubgenre_Fails()
{
    var cmd = new CreateBookCommand(
        ...,
        "Novela contemporanea",
        null!,  // ❌ Null
        ...);
    
    var act = () => _sut.ExecuteAsync(cmd);
    
    act.Should().ThrowAsync<ValidationException>();
}
```

---

### 6. ARQUITECTURA: ¿POR QUÉ NO CACHE EN BACKEND?

**P: ¿Cacheaste el resultado de GetSubgenresAsync en el backend?**

**Respuesta:**

Decisión: **No, en este caso**

**Razones:**

1. **Datos raramente cambian**
   - Subgéneros se agregan tal vez 1x por semana
   - No justifica complejidad de cache invalidation

2. **Frontend cachea**
   - TanStack Query: 5 min (configurable)
   - Si algo cambia, max 5 min para verlo
   - Suficiente para UX

3. **BD es rápida**
   - Query simple: `SELECT Name FROM Subgenres WHERE GenreId=@id`
   - ~1-2ms en SSD
   - 1000 requests/min = no problema

**Si TUVIERA que cachear (casos con millones de libros):**

```csharp
// Option 1: Memory Cache
public async Task<IEnumerable<string>> GetSubgenresAsync(string genre)
{
    var cacheKey = $"subgenres:{genre}";
    
    if (_cache.TryGetValue(cacheKey, out var cached))
        return (IEnumerable<string>)cached;
    
    var result = await _conn.QueryAsync<string>(
        "SELECT Name FROM Subgenres WHERE GenreName=@genre",
        new { genre });
    
    _cache.Set(cacheKey, result, TimeSpan.FromHours(1));
    return result;
}

// Option 2: Distributed Cache (Redis)
public async Task<IEnumerable<string>> GetSubgenresAsync(string genre)
{
    var cacheKey = $"subgenres:{genre}";
    var cached = await _redis.GetStringAsync(cacheKey);
    
    if (cached != null)
        return JsonConvert.DeserializeObject<List<string>>(cached);
    
    var result = await _conn.QueryAsync<string>(...);
    
    await _redis.SetStringAsync(cacheKey, 
        JsonConvert.SerializeObject(result), 
        TimeSpan.FromHours(1));
    
    return result;
}
```

---

### 7. AUDITORÍA: ¿GUARDAMOS CAMBIOS?

**P: Si alguien edita el subgénero de un libro, ¿queda un registro?**

```
T0: Creo libro con Subgenre = "Realismo moderno"
T1: Lo edito a Subgenre = "Ficción especulativa"

¿Hay forma de saber cuál fue el valor anterior?
```

**Respuesta:**

Actual:
- ❌ No. UpdatedAt marca cuándo se editó, pero no qué cambió
- Tabla Books no tiene auditoría

**Soluciones (si fuera necesario):**

**Opción 1: Tabla de Auditoría**
```sql
CREATE TABLE BookAuditLog (
    Id INT IDENTITY,
    BookId INT,
    FieldName NVARCHAR(50),
    OldValue NVARCHAR(500),
    NewValue NVARCHAR(500),
    ChangedBy INT,
    ChangedAt DATETIME2,
);

-- Update trigger
CREATE TRIGGER tr_Books_Audit
ON Books
AFTER UPDATE
AS
BEGIN
    INSERT INTO BookAuditLog (BookId, FieldName, OldValue, NewValue, ChangedAt)
    SELECT 
        i.Id, 'Subgenre', 
        d.Subgenre, i.Subgenre, 
        GETUTCDATE()
    FROM inserted i
    INNER JOIN deleted d ON i.Id = d.Id
    WHERE i.Subgenre <> d.Subgenre;
END
```

**Opción 2: Event Sourcing** (arquitectura más grande)

---

### 8. MIGRACIÓN BACKWARDS COMPATIBLE

**P: ¿Qué pasa si necesitamos hacer rollback rápido?**

```
Produción falla
Necesitamos revertir a versión anterior
¿Se pierde todo?
```

**Respuesta:**

Script de rollback seguro:

```sql
-- Rollback: 006_revert_rotation_categories.sql

-- 1. Restaurar valores antiguos
UPDATE Books
SET RotationCategory = CASE
    WHEN RotationCategory = 'Ficción literaria y narrativa general' 
        THEN 'Novela grande'  -- O el que tenía
    WHEN RotationCategory = 'No ficción'
        THEN 'Ensayo / no ficcion'
    ELSE RotationCategory
END;

-- 2. Re-insertar antiguas categorías
INSERT INTO RotationCategories (Name) 
VALUES 
    ('Ensayo / no ficcion'),
    ('Libro corto o cuentos'),
    ('Clasico'),
    ('Novela grande'),
    ('Contemporaneo latinoamericano o raro')
ON CONFLICT DO NOTHING;

-- 3. Eliminar nueva tabla Subgenres (opcional)
-- DROP TABLE Subgenres;
-- ALTER TABLE Books DROP COLUMN Subgenre;
```

**Riesgo:** Datos intermedios se pierden (libros creados con nuevas categorías)

**Mejor: Feature flag**

```csharp
// appsettings.json
{
  "Features": {
    "UseNewRotationCategories": true
  }
}

// CreateBook.cs
if (_config["Features:UseNewRotationCategories"] == "true")
{
    // Validar contra 10 nuevas
}
else
{
    // Validar contra 5 antiguas
}
```

---

### 9. TESTING: ¿CÓMO TESTIASTE LA MIGRACIÓN?

**P: ¿Cómo validaste que la migración actualiza correctamente los datos antiguos?**

**Respuesta:**

```csharp
[Fact]
public async Task Migration_UpdatesOldRotationCategories()
{
    // Arrange: Crear DB vieja (sin migración)
    using var conn = new SqlConnection(ConnectionString);
    conn.Open();
    
    // Simular DB antigua
    await conn.ExecuteAsync(@"
        CREATE TABLE Books (RotationCategory NVARCHAR(100));
        INSERT INTO Books (RotationCategory) VALUES 
            ('Novela grande'),
            ('Clasico'),
            ('Ensayo / no ficcion'),
            ('Libro corto o cuentos');
    ");
    
    // Act: Ejecutar migración
    MigrationRunner.Run(ConnectionString);
    
    // Assert: Verificar actualización
    var results = await conn.QueryAsync<string>(
        "SELECT DISTINCT RotationCategory FROM Books ORDER BY RotationCategory");
    
    results.Should().ContainInOrder(
        "Ficción literaria y narrativa general",
        "Ficción literaria y narrativa general",  // Todos los antiguos → aquí
        "No ficción"  // Solo este cambió específicamente
    );
    
    // Verificar que no quedan valores antiguos
    var old = await conn.QueryScalarAsync<int>(
        "SELECT COUNT(*) FROM Books WHERE RotationCategory IN ('Novela grande', 'Clasico', 'Libro corto o cuentos')");
    
    old.Should().Be(0);
}

[Fact]
public async Task Migration_IsIdempotent()
{
    // Run twice
    MigrationRunner.Run(ConnectionString);
    MigrationRunner.Run(ConnectionString);
    
    var count = await conn.QueryScalarAsync<int>(
        "SELECT COUNT(*) FROM RotationCategories");
    
    count.Should().Be(10);  // Exactamente 10, no 20
}
```

---

### 10. FRONTEND: MANEJO DE ERRORES

**P: ¿Qué pasa si el endpoint `/subgenres` devuelve 500?**

**Respuesta:**

```tsx
// useSubgenres.ts
export const useSubgenres = (genreName?: string) => {
  return useQuery({
    queryKey: ['subgenres', genreName],
    queryFn: async () => {
      try {
        if (!genreName) return [];
        return await booksApi.getSubgenres(genreName);
      } catch (error) {
        console.error('Failed to load subgenres:', error);
        throw error;  // TanStack Query lo maneja
      }
    },
    enabled: !!genreName,
  });
};

// BookForm.tsx
const { data: subgenres = [], isError, error, isPending } = useSubgenres(selectedGenre);

return (
  <>
    {isPending && <span>Cargando subgéneros...</span>}
    
    {isError && (
      <p className="text-destructive">
        Error cargando subgéneros: {error?.message || 'Desconocido'}
      </p>
    )}
    
    <select {...register('subgenre')} disabled={!selectedGenre || isPending}>
      <option value="">Selecciona...</option>
      {subgenres.map(s => <option key={s}>{s}</option>)}
    </select>
  </>
);
```

**Comportamiento:**
- ✅ Muestra mensaje de error
- ✅ Disable select (evita enviar formulario)
- ✅ Permite reintentar (cambiar género vuelve a intentar)
- ✅ No rompe página

---

## QUICK REFERENCE: RESPUESTAS DEFENSIVAS

### Si te dicen...

**"Esto es muy complicado"**
→ "Es simple desde el usuario: elige género → se cargan subgéneros → elige. Backend lo valida. La complejidad es robustez, no feature-count."

**"¿Por qué 10 categorías y no 20?"**
→ "10 cubre todos los tipos de libros sistemáticamente. 20 sería redundancia. Las antiguas 5 eran ambiguas (¿'Novela grande' = qué? Páginas? Complejidad?)"

**"¿Cómo sé que la migración no rompe datos?"**
→ "Tests de integración contra BD real. Mitigation: la migración es reversible y puede correr N veces sin efectos colaterales."

**"¿Y si alguien delete un subgénero que está en uso?"**
→ "Razón por la que NO usamos FK. El libro mantiene el valor histórico. No puedes crear NUEVOS con ese valor. Es feature, no bug."

**"¿Impacta performance?"**
→ "1 query por género + TanStack Query cache (5 min). Típicamente 2-3 queries por sesión. Negligible."

**"¿Se puede atacar esto?"**
→ "Backend valida SIEMPRE. Frontend valida para UX. Alguien podrría enviar JSON inválido, pero use case lo rechaza. Base de datos protegida."

---

## PRESENTACIÓN EN VIVO (SCRIPT)

```
"Hoy les presento dos mejoras coordinadas al sistema de libros:

1. SUBGÉNEROS
   - Problema: 'Novela' es demasiado genérico
   - Solución: Dentro de 'Novela contemporánea' tenemos 
     'Realismo moderno', 'Ficción especulativa', etc.
   - Implementación: Nueva tabla Subgenres, endpoint dinámico,
     validación en backend
   - Beneficio: Usuarios pueden clasificar más precisamente

2. CATEGORÍAS DE ROTACIÓN
   - Problema: Las 5 antiguas eran ambiguas y se sobreponían
   - Solución: 10 categorías semánticas claras
   - Ejemplo: 'Novela grande' ahora es 'Ficción literaria general'
   - Beneficio: Estadísticas más confiables, datos limpios

Preguntas?"
```

