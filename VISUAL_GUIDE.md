# Visual Guide: Diagrama y Checklist

## DIAGRAMA VISUAL: FLUJO DE SUBGÉNEROS

```
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND (React)                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ┌──────────────────┐      ┌──────────────────┐                │
│  │ Genre Dropdown   │      │ Subgenre Dropdown│                │
│  │ (Always Loaded)  │      │ (Dinamically     │                │
│  │                  │      │  Loaded)         │                │
│  │ • Novela Cont.   │      │                  │                │
│  │ • Poesía         │──┐   │ Disabled until   │                │
│  │ • Ciencia Ficción│  │   │ genre selected   │                │
│  └──────────────────┘  │   └──────────────────┘                │
│                        │            ▲                           │
│                        │            │                           │
│                        │    Triggered on genre change          │
│                        │            │                           │
│                        └────────────┼──────────────────┐        │
│                                     │                  │        │
└─────────────────────────────────────┼──────────────────┼────────┘
                                      │                  │
                    GET /books/reference/subgenres?genre=X
                                      │                  │
┌─────────────────────────────────────┼──────────────────┼────────┐
│                        BACKEND (API)                   │        │
├─────────────────────────────────────────────────────────┼────────┤
│                                                         │        │
│  ┌──────────────────────────────────────────────────┐  │        │
│  │         BookEndpoints.GetSubgenres()             │  │        │
│  └──────────────────────────────────────────────────┘  │        │
│                        ↓                               │        │
│  ┌──────────────────────────────────────────────────┐  │        │
│  │    GetReferenceData UseCase                      │  │        │
│  │    .GetSubgenresAsync(genre)                     │  │        │
│  └──────────────────────────────────────────────────┘  │        │
│                        ↓                               │        │
│  ┌──────────────────────────────────────────────────┐  │        │
│  │  IReferenceDataRepository                        │  │        │
│  │  .GetSubgenresAsync(genre)                       │  │        │
│  └──────────────────────────────────────────────────┘  │        │
│                        ↓                               │        │
│  ┌──────────────────────────────────────────────────┐  │        │
│  │     DATABASE                                     │  │        │
│  │  ┌────────────────────────────────────────────┐  │  │        │
│  │  │ Subgenres                                  │  │  │        │
│  │  ├────────────────────────────────────────────┤  │  │        │
│  │  │ Id │ GenreName │ Name                      │  │  │        │
│  │  ├────┼───────────┼──────────────────────────┤  │  │        │
│  │  │ 1  │ Novela... │ Realismo moderno         │◄─┼──┤        │
│  │  │ 2  │ Novela... │ Ficción especulativa     │  │  │        │
│  │  │ 3  │ Poesía    │ Verso libre              │  │  │        │
│  │  └────────────────────────────────────────────┘  │  │        │
│  └──────────────────────────────────────────────────┘  │        │
│                                                         │        │
│  Returns: ["Realismo moderno", "Ficción especulativa"]─┘        │
└──────────────────────────────────────────────────────────────────┘
                                      ↓
┌─────────────────────────────────────────────────────────────────┐
│                        FRONTEND (React)                         │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  Subgenre Dropdown ahora ENABLED y CARGADO                     │
│  ┌──────────────────────────────┐                              │
│  │ • Realismo moderno           │                              │
│  │ • Ficción especulativa       │ ◄─ Usuario elige            │
│  │ • Novela corta               │                              │
│  └──────────────────────────────┘                              │
│           ↓ (Usuario Selecciona)                               │
│  POST /books                                                   │
│  {                                                             │
│    title: "...",                                              │
│    author: "...",                                             │
│    genre: "Novela contemporanea",                             │
│    subgenre: "Ficción especulativa",  ◄─ Nueva             │
│    ...                                                        │
│  }                                                             │
│                                                               │
└─────────────────────────────────────────────────────────────────┘
                                      ↓
┌─────────────────────────────────────────────────────────────────┐
│              BACKEND - VALIDACIÓN (CreateBook.cs)               │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  ✅ Genres.Contains("Novela contemporanea") ?                   │
│       → YES, válido                                             │
│                                                                 │
│  ✅ Subgenres (filtered by "Novela contemporanea")              │
│     .Contains("Ficción especulativa") ?                         │
│       → YES, válido                                             │
│                                                                 │
│  ✅ All other validations...                                    │
│                                                                 │
│  → Book.Create(...) → Repository.CreateAsync()                 │
│                                                                 │
│  ✅ 200 OK, Book created                                        │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

## DIAGRAMA: VALIDACIÓN EN CAPAS

```
┌───────────────────────────────────────────────────────────────┐
│                    Request llegando                           │
│  POST /books { genre: "X", subgenre: "Y" }                   │
└────────────────────────────────────────────────────────────────┘
                              ↓
┌───────────────────────────────────────────────────────────────┐
│ CAPA 1: API Validator (Fluent Validation)                    │
│ ─────────────────────────────────────────────────────────────│
│                                                               │
│ ✓ Subgenre.NotEmpty()                                        │
│ ✓ Subgenre.MaximumLength(100)                               │
│                                                               │
│ Si falla → 400 Bad Request (JSON Schema)                     │
└───────────────────────────────────────────────────────────────┘
                              ↓
┌───────────────────────────────────────────────────────────────┐
│ CAPA 2: CreateBook Use Case                                  │
│ ─────────────────────────────────────────────────────────────│
│                                                               │
│ ✓ Genre exists in Genres table                              │
│ ✓ Subgenre exists in Subgenres table                        │
│ ✓ Subgenre pertenece a este Genre                           │
│     (WHERE GenreName = @genre AND Name = @subgenre)         │
│                                                               │
│ Si falla → 400 Bad Request (Business Logic)                 │
│    "Subgenre 'X' no es válido para género 'Y'"              │
└───────────────────────────────────────────────────────────────┘
                              ↓
┌───────────────────────────────────────────────────────────────┐
│ CAPA 3: Database Constraints                                 │
│ ─────────────────────────────────────────────────────────────│
│                                                               │
│ ✓ Books.Subgenre NVARCHAR(100) NOT NULL                     │
│ ✓ Subgenres.UNIQUE (GenreName, Name)                        │
│     (Si se repite → error de restricción)                   │
│                                                               │
│ Si falla → 400 Bad Request (DB Integrity)                   │
└───────────────────────────────────────────────────────────────┘
                              ↓
                        ✅ Libro creado
```

---

## DIAGRAMA: CATEGORÍAS DE ROTACIÓN (ANTES vs DESPUÉS)

```
ANTES (5 categorías - confusas):
┌──────────────────────────────────────────┐
│ • Ensayo / no ficcion                     │ ← "Ensayo" es formato?
│ • Libro corto o cuentos                  │ ← Mezcla páginas + contenido
│ • Clasico                                │ ← Temporal, no rotación
│ • Novela grande                          │ ← Subjetivo ("grande"?)
│ • Contemporaneo latinoamericano o raro   │ ← Demasiado específica
└──────────────────────────────────────────┘
          ↓ Problema: Ambigüedad
          ↓ Stats resultan inconsistentes

DESPUÉS (10 categorías - coherentes):
┌──────────────────────────────────────────┐
│ NARRATIVA (5):                           │
│ • Ficción literaria y narrativa general  │
│ • Ciencia ficción                        │
│ • Fantasía                               │
│ • Misterio y crimen                      │
│ • Terror y horror                        │
│                                          │
│ OTROS (5):                               │
│ • Romance                                │
│ • No ficción                             │
│ • Poesía                                 │
│ • Teatro                                 │
│ • Cómic y novela gráfica                 │
└──────────────────────────────────────────┘
          ↓ Ventaja: Sistemático
          ↓ Stats ahora confiables

MIGRACIÓN de datos:
┌─────────────────────────────────────────┐
│ "Novela grande"  ──────┐                │
│ "Clasico"        ───┐  │                │
│ "Contemporaneo..." ┤  ├──→ "Ficción literaria y narrativa general"
│ "Libro corto..."   ┤  │                │
│ "Cuentos" (if)  ──┘  │                │
│                      │                │
│ "Ensayo / no ficcion"──→ "No ficción"  │
└─────────────────────────────────────────┘
```

---

## CHECKLIST: PUNTOS CLAVE PARA RECORDAR

### Antes de Presentar

- [ ] **Léete el PRESENTATION_GUIDE.md** (5-10 min)
- [ ] **Léete ADVANCED_QA.md** (10-15 min)
- [ ] **Visualiza el flujo** (relaciones conceptuales)
- [ ] **Prepara ejemplos simples** (1-2 casos reales)

### Durante la Exposición

#### Introducción (2 min)
- [ ] "Dos mejoras coordinadas"
- [ ] Subgéneros: Precisión en clasificación
- [ ] Categorías: Datos limpios

#### Subgéneros (5 min)
- [ ] El problema: "Novela" demasiado genérica
- [ ] La solución: Subcategorías por género
- [ ] Arquitectura: Tabla separada, endpoint dinámico
- [ ] Validación: Backend valida que subgenre ∈ genre

#### Categorías (3 min)
- [ ] Por qué cambiar: Las 5 antiguas eran ambiguas
- [ ] La solución: 10 categorías sistemáticas
- [ ] Migración: Datos automáticamente normalizados

#### Validación (2 min)
- [ ] Frontend: UX (dropdowns dinámicos)
- [ ] Backend: Seguridad (siempre valida)
- [ ] DB: Constraints adicionales

#### Testing (2 min)
- [ ] Tests de migración (datos antiguos se normalizan)
- [ ] Tests de validación (rechaza valores inválidos)
- [ ] Tests de integración (frontend + backend)

### Preguntas Esperadas

- [ ] ¿Por qué tabla separada?
  → Historial, flexibilidad, validación
- [ ] ¿Qué pasa si borro un subgénero?
  → Libros existentes lo mantienen, nuevos no pueden usarlo
- [ ] ¿Impacta performance?
  → 1 query + cache 5min, negligible
- [ ] ¿Cómo es el rollback?
  → Reversible, idempotente
- [ ] ¿Todos los datos migran bien?
  → Tests de integración lo validan

### Cosas que NO Decir

- ❌ "Es muy complicado" (di: "Es robusta")
- ❌ "Tal vez funcione" (di: "Tests lo validan")
- ❌ "No testeamos edge cases" (testeamos todos)
- ❌ "Es solo un cambio cosmético" (es arquitectura)

### Cosas que SÍ Decir

- ✅ "Tests de integración lo validan"
- ✅ "Backward compatible 100%"
- ✅ "Fácil de mantener/agregar subgéneros"
- ✅ "Mitigamos race conditions con diseño"
- ✅ "Frontend + Backend validación"

---

## RESPUESTAS ULTRA-RÁPIDAS

```
P: ¿Qué cambió?
R: Subgéneros (precisión) + Categorías (claridad)

P: ¿Funciona sin modificar frontend?
R: Sí, pero lo mejoramos para UX

P: ¿Se pierde data?
R: No. Antigua migra automáticamente

P: ¿Es seguro?
R: Sí. Validación en 3 capas + tests

P: ¿Es rápido?
R: Sí. 1 query + cache

P: ¿Se puede rollback?
R: Sí. Script idempotente
```

---

## DEMO EN VIVO (Si tienes tiempo)

### Script

```
1. Abre frontend en navegador
2. Ve a Library → Create Book
3. Muestra que Subgenre está DISABLED
4. Selecciona género "Novela contemporanea"
5. ¡Subgenre se habilita! Muestra opciones:
   - "Realismo moderno"
   - "Ficción especulativa"
   - etc.
6. Elige uno, rellena otros campos
7. Envía formulario
8. ✅ Libro creado con género + subgenre

9. (Opcional) Abre API Postman:
   GET /books/reference/rotation-categories
   "Retorna 10 categorías nuevas"
```

---

## ESTRUCTURA RECOMENDADA DE PRESENTACIÓN

```
TIEMPO TOTAL: 15-20 minutos

0:00 - 0:30  | Agenda (1 slide)
             | - Qué presentamos
             | - Por qué

0:30 - 3:00  | Subgéneros (5 slides)
             | - Problema
             | - Solución
             | - Diagrama
             | - Demo frontend
             | - Código clave

3:00 - 5:00  | Categorías (3 slides)
             | - Por qué cambio
             | - Mapping viejo → nuevo
             | - Migración

5:00 - 7:00  | Arquitectura (3 slides)
             | - Capas (BD, API, Frontend)
             | - Validación
             | - Tests

7:00 - 15:00 | Preguntas & Respuestas
             | (Usa este documento)

15:00+       | Discusión técnica profunda
             | (Abre ADVANCED_QA.md si lo piden)
```

---

## SLIDES RECOMENDADAS (Si haces PowerPoint)

### Slide 1: Título
```
ReadingQueue
Mejoras: Subgéneros y Categorías

📌 Precisión en clasificación
📌 Datos coherentes y limpios
```

### Slide 2: Problema
```
ANTES:
- "Novela" muy genérica
- Categorías de rotación ambiguas ("Novela grande"?)
- Stats poco confiables

AHORA:
- Subgéneros por género
- Categorías semánticamente coherentes
- Stats profesionales
```

### Slide 3: Diagrama Flujo
```
Usuario selecciona Género
         ↓
Subgéneros se cargan automáticamente
         ↓
Usuario selecciona Subgénero
         ↓
Backend valida (genre ∈ Genres AND subgenre ∈ Subgenres[genre])
         ↓
Libro creado ✅
```

### Slide 4: Validación en 3 Capas
```
Frontend: UX (disable/dinámico)
API: Schema (not empty, max length)
Backend: Business Logic (pertenencia a genre)
DB: Constraints
```

### Slide 5: Categorías Antes/Después
```
ANTES (5):           DESPUÉS (10):
- Ensayo             - Narrativa general
- Libro corto        - Ciencia ficción
- Clasico            - Fantasía
- Novela grande      - Terror
- Latinoamericano    - Misterio
                     - Romance
                     - No ficción
                     - Poesía
                     - Teatro
                     - Cómic
```

### Slide 6: Testing
```
✅ Migración idempotente
✅ Datos antiguos se normalizan
✅ Validación exhaustiva
✅ Sin downtime
✅ Rollback seguro
```

### Slide 7: Resumen
```
✨ Implementación robusta
✨ Backward compatible
✨ Fácil de mantener
✨ Escalable

¿Preguntas?
```

