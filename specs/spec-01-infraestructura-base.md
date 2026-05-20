# spec-01-infraestructura-base.md
# Feature: Infraestructura Base y Esquema de Datos

## 1. Resumen

Establecer toda la infraestructura sobre la que se construirán los specs
posteriores: estructura de solución .NET con los 4 proyectos, contenedores
Docker con SQL Server, sistema de migraciones DbUp con el esquema completo
de todas las tablas, seed de los valores canónicos de enumeraciones, y el
scaffolding del frontend React + TypeScript + Vite + Tailwind + shadcn/ui
configurado y arrancando. Al terminar este spec el entorno completo debe
poder levantarse con un solo `docker-compose up` y mostrar la app en el
navegador con la estructura de carpetas definitiva lista para recibir código
de negocio.

---

## 2. Motivación

Ningún spec posterior puede implementarse sin este cimiento. El Spec 2
necesita la tabla `Users` y la factory de conexión. El Spec 3 necesita la
tabla `Books` y los índices. El Spec 4 necesita `ReadingQueue`. El Spec 5
necesita `AISuggestions`. El frontend de los Specs 6 en adelante necesita
Vite configurado con las rutas base de shadcn. Todo esto se define una
sola vez aquí y nunca se toca de nuevo — solo se agregan migraciones nuevas.

---

## 3. Usuarios y Casos de Uso

| Actor | Caso de uso |
|---|---|
| Desarrollador | Levantar el entorno completo con `docker-compose up` |
| Desarrollador | Ver la app frontend en `http://localhost:3000` |
| Desarrollador | Ver la API respondiendo en `http://localhost:5000/health` |
| Desarrollador | Confirmar que las migraciones corrieron automáticamente al arrancar |
| Desarrollador | Confirmar que los valores de referencia (géneros, ánimos, etc.) existen en BD |
| Desarrollador | Agregar una migración nueva sin tocar las existentes |
| CI/CD | Construir las imágenes Docker del backend y frontend sin errores |

---

## 4. Requisitos Funcionales

### RF-01 — Estructura de solución .NET
- La solución debe contener exactamente 4 proyectos en `src/`:
  `ReadingQueue.Domain`, `ReadingQueue.Application`,
  `ReadingQueue.Infrastructure`, `ReadingQueue.Api`.
- Las referencias entre proyectos siguen la dirección:
  `Api → Application → Domain` e `Infrastructure → Domain`.
  `Application` nunca referencia `Infrastructure`.
- Los proyectos de test en `tests/` referencian solo el proyecto que prueban.

### RF-02 — Docker Compose funcional
- `docker-compose up` debe levantar 3 servicios: `api`, `frontend`,
  `sqlserver`.
- El servicio `api` no arranca hasta que `sqlserver` esté saludable
  (healthcheck con `sqlcmd`).
- El servicio `frontend` sirve la app en el puerto 3000.
- El servicio `api` expone el puerto 5000.
- Los datos de SQL Server persisten en un volumen Docker nombrado
  `sqldata` para no perderse entre reinicios.

### RF-03 — Migraciones automáticas con DbUp
- Al arrancar, el backend ejecuta DbUp antes de levantar el servidor HTTP.
- DbUp lee los scripts `.sql` de
  `src/ReadingQueue.Infrastructure/Migrations/` en orden numérico.
- Si las migraciones ya corrieron, DbUp las omite silenciosamente.
- Si una migración falla, el proceso lanza excepción y la app no arranca.
- Nunca se modifica un script de migración ya ejecutado — siempre se crea
  uno nuevo.

### RF-04 — Esquema completo en migración 001
- El script `001_initial_schema.sql` crea todas las tablas del sistema:
  `Users`, `Books`, `ReadingQueue`, `AISuggestions`, `RefreshTokens`.
- Incluye todos los índices definidos en la constitution.
- Incluye todos los constraints (`CHECK`, `UNIQUE`) definidos en la
  constitution.

### RF-05 — Seed de datos de referencia en migración 002
- El script `002_seed_reference_data.sql` inserta los valores canónicos
  de las 4 enumeraciones en tablas de referencia:
  `Genres`, `MentalEnergyLevels`, `Moods`, `RotationCategories`.
- El seed es idempotente: usa `IF NOT EXISTS` antes de cada insert.
- Los 7 géneros, 5 niveles de energía, 7 ánimos y 5 categorías de
  rotación definidos en la constitution deben existir tras el seed.

### RF-06 — Factory de conexión inyectable
- Existe una interfaz `IDbConnectionFactory` con un método `Create()`
  que retorna `IDbConnection`.
- La implementación `SqlConnectionFactory` lee la cadena de conexión de
  `IConfiguration` bajo la clave `ConnectionStrings:DefaultConnection`.
- Nunca se inyecta `IDbConnection` directamente en repositorios.

### RF-07 — Health check del backend
- El endpoint `GET /health` retorna `200 OK` con body `{ "status": "ok" }`
  cuando el servidor está levantado y la BD es alcanzable.
- Si la BD no responde, retorna `503 Service Unavailable`.

### RF-08 — Scaffolding del frontend
- El proyecto frontend arranca con `npm run dev` sin errores.
- Vite está configurado con proxy hacia `http://api:5000` para las rutas
  `/api/*` (evita CORS en desarrollo).
- Tailwind CSS está configurado y funcional: una clase utilitaria aplicada
  a un elemento de prueba debe verse reflejada.
- shadcn/ui está inicializado: el componente `Button` de shadcn debe poder
  importarse y renderizarse sin errores.
- La estructura de carpetas `src/api`, `src/components/ui`, `src/pages`,
  `src/stores`, `src/hooks`, `src/types`, `src/lib` debe existir con un
  archivo `index.ts` o `.tsx` vacío en cada una.

### RF-09 — Variables de entorno y secretos
- Existe un archivo `.env.example` en la raíz con todas las variables
  documentadas y sin valores reales.
- El `.gitignore` excluye `.env`, `.env.local`, y cualquier archivo con
  secretos reales.
- La API lee `ConnectionStrings__DefaultConnection`, `Jwt__SecretKey` y
  `Claude__ApiKey` desde variables de entorno, nunca desde código.

---

## 5. Requisitos No Funcionales

- **Reproducibilidad**: cualquier desarrollador que clone el repo y ejecute
  `docker-compose up` debe tener el entorno corriendo en menos de 5 minutos
  sin configuración manual adicional.
- **Inmutabilidad de migraciones**: los scripts `.sql` numerados nunca se
  modifican después de ejecutarse en cualquier entorno. Solo se agregan
  scripts nuevos con el siguiente número secuencial.
- **Sin secretos en repo**: el repositorio nunca debe contener API keys,
  passwords ni connection strings reales. Todo va en variables de entorno.
- **Multi-stage Docker**: las imágenes de producción son multi-stage.
  La imagen final del backend no contiene el SDK de .NET, solo el runtime.
  La imagen final del frontend no contiene node_modules, solo los archivos
  estáticos servidos por nginx.

---

## 6. Modelo de Dominio

Este spec no define entidades de negocio — eso corresponde a los Specs 2-5.
Lo que sí define es la infraestructura de soporte que todos los demás usan:

```csharp
// src/ReadingQueue.Infrastructure/Data/IDbConnectionFactory.cs
public interface IDbConnectionFactory
{
    IDbConnection Create();
}

// src/ReadingQueue.Infrastructure/Data/SqlConnectionFactory.cs
public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured.");
    }

    public IDbConnection Create()
        => new SqlConnection(_connectionString);
}
```

```csharp
// src/ReadingQueue.Infrastructure/Migrations/MigrationRunner.cs
public static class MigrationRunner
{
    public static void Run(string connectionString)
    {
        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                s => s.StartsWith("ReadingQueue.Infrastructure.Migrations"))
            .WithTransactionPerScript()
            .LogToConsole()
            .Build();

        var result = upgrader.PerformUpgrade();

        if (!result.Successful)
            throw new Exception(
                $"Migración fallida: {result.Error.Message}", result.Error);
    }
}
```

```csharp
// src/ReadingQueue.Api/Program.cs — llamada a migraciones antes del app.Run()
var connectionString = builder.Configuration
    .GetConnectionString("DefaultConnection")!;

MigrationRunner.Run(connectionString);  // Falla rápido si hay error

app.Run();
```

---

## 7. Contrato de API

Este spec expone un único endpoint operacional:

### GET `/health`
Verifica que el servidor y la base de datos están operativos.

**Response `200 OK`:**
```json
{
  "status": "ok",
  "database": "reachable",
  "timestamp": "2026-05-04T10:00:00Z"
}
```

**Response `503 Service Unavailable`:**
```json
{
  "status": "degraded",
  "database": "unreachable",
  "timestamp": "2026-05-04T10:00:00Z"
}
```

No requiere autenticación. Es el endpoint que usa el healthcheck de Docker
Compose para esperar a que la API esté lista antes de declarar el servicio
como saludable.

---

## 8. Esquema de Base de Datos

### 001_initial_schema.sql

```sql
-- ============================================================
-- Usuarios del sistema
-- ============================================================
CREATE TABLE Users (
    Id           INT IDENTITY(1,1) PRIMARY KEY,
    Email        NVARCHAR(256)  NOT NULL,
    PasswordHash NVARCHAR(512)  NOT NULL,
    DisplayName  NVARCHAR(200)  NOT NULL,
    CreatedAt    DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    IsActive     BIT            NOT NULL DEFAULT 1,

    CONSTRAINT UQ_Users_Email UNIQUE (Email)
);

-- ============================================================
-- Biblioteca de libros por usuario
-- ============================================================
CREATE TABLE Books (
    Id               INT IDENTITY(1,1) PRIMARY KEY,
    UserId           INT             NOT NULL REFERENCES Users(Id),
    Title            NVARCHAR(500)   NOT NULL,
    Author           NVARCHAR(300)   NOT NULL,
    Genre            NVARCHAR(100)   NOT NULL,
    Country          NVARCHAR(100)   NOT NULL,
    WhyRead          NVARCHAR(1000)  NULL,
    Priority         TINYINT         NOT NULL DEFAULT 3,
    MentalEnergy     NVARCHAR(100)   NOT NULL,
    RecommendedMood  NVARCHAR(200)   NOT NULL,
    RotationCategory NVARCHAR(100)   NOT NULL,
    IsRead           BIT             NOT NULL DEFAULT 0,
    ReadAt           DATETIME2       NULL,
    Notes            NVARCHAR(2000)  NULL,
    CreatedAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    UpdatedAt        DATETIME2       NOT NULL DEFAULT GETUTCDATE(),

    CONSTRAINT CK_Books_Priority CHECK (Priority BETWEEN 1 AND 5)
);

CREATE INDEX IX_Books_UserId
    ON Books(UserId);

CREATE INDEX IX_Books_UserId_Genre
    ON Books(UserId, Genre);

CREATE INDEX IX_Books_UserId_IsRead
    ON Books(UserId, IsRead);

-- ============================================================
-- Cola de lectura activa por usuario
-- ============================================================
CREATE TABLE ReadingQueue (
    Id       INT IDENTITY(1,1) PRIMARY KEY,
    UserId   INT           NOT NULL REFERENCES Users(Id),
    BookId   INT           NOT NULL REFERENCES Books(Id),
    Position INT           NOT NULL,
    AddedAt  DATETIME2     NOT NULL DEFAULT GETUTCDATE(),
    Source   NVARCHAR(50)  NOT NULL DEFAULT 'Manual',
    -- Valores válidos de Source: 'Manual', 'AI', 'Filter'

    CONSTRAINT UQ_Queue_UserBook UNIQUE (UserId, BookId)
);

CREATE INDEX IX_Queue_UserId
    ON ReadingQueue(UserId, Position);

-- ============================================================
-- Sugerencias generadas por Claude
-- ============================================================
CREATE TABLE AISuggestions (
    Id          INT IDENTITY(1,1) PRIMARY KEY,
    UserId      INT             NOT NULL REFERENCES Users(Id),
    BookId      INT             NOT NULL REFERENCES Books(Id),
    Reasoning   NVARCHAR(2000)  NOT NULL,
    Score       DECIMAL(5,2)    NOT NULL,
    GeneratedAt DATETIME2       NOT NULL DEFAULT GETUTCDATE(),
    WasAccepted BIT             NULL
);

-- ============================================================
-- Refresh tokens de sesión
-- ============================================================
CREATE TABLE RefreshTokens (
    Id        INT IDENTITY(1,1) PRIMARY KEY,
    UserId    INT            NOT NULL REFERENCES Users(Id),
    Token     NVARCHAR(512)  NOT NULL,
    ExpiresAt DATETIME2      NOT NULL,
    CreatedAt DATETIME2      NOT NULL DEFAULT GETUTCDATE(),
    IsRevoked BIT            NOT NULL DEFAULT 0,

    CONSTRAINT UQ_RefreshTokens_Token UNIQUE (Token)
);

-- ============================================================
-- Tablas de referencia (enumeraciones)
-- ============================================================
CREATE TABLE Genres (
    Name NVARCHAR(100) NOT NULL PRIMARY KEY
);

CREATE TABLE MentalEnergyLevels (
    Name  NVARCHAR(100) NOT NULL PRIMARY KEY,
    SortOrder TINYINT   NOT NULL
);

CREATE TABLE Moods (
    Name NVARCHAR(200) NOT NULL PRIMARY KEY
);

CREATE TABLE RotationCategories (
    Name NVARCHAR(100) NOT NULL PRIMARY KEY
);
```

### 002_seed_reference_data.sql

```sql
-- ============================================================
-- Géneros (7 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'No ficción / ensayo')
    INSERT INTO Genres (Name) VALUES ('No ficción / ensayo');

IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Clásico')
    INSERT INTO Genres (Name) VALUES ('Clásico');

IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Novela contemporánea')
    INSERT INTO Genres (Name) VALUES ('Novela contemporánea');

IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Novela latinoamericana')
    INSERT INTO Genres (Name) VALUES ('Novela latinoamericana');

IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Cuentos')
    INSERT INTO Genres (Name) VALUES ('Cuentos');

IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Novela clásica')
    INSERT INTO Genres (Name) VALUES ('Novela clásica');

IF NOT EXISTS (SELECT 1 FROM Genres WHERE Name = 'Poesía')
    INSERT INTO Genres (Name) VALUES ('Poesía');

-- ============================================================
-- Niveles de energía mental (5 valores, con orden de menor a mayor)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = '🟢 Baja – cualquier momento')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('🟢 Baja – cualquier momento', 1);

IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = '🔵 Media – tarde tranquila')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('🔵 Media – tarde tranquila', 2);

IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = '🟡 Media-alta – fin de semana')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('🟡 Media-alta – fin de semana', 3);

IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = '🟠 Alta – concentración')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('🟠 Alta – concentración', 4);

IF NOT EXISTS (SELECT 1 FROM MentalEnergyLevels WHERE Name = '🔴 Máxima – modo lector')
    INSERT INTO MentalEnergyLevels (Name, SortOrder) VALUES ('🔴 Máxima – modo lector', 5);

-- ============================================================
-- Ánimos recomendados (7 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Analítico / quiero aprender algo')
    INSERT INTO Moods (Name) VALUES ('Analítico / quiero aprender algo');

IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Solemne / quiero leer algo grande')
    INSERT INTO Moods (Name) VALUES ('Solemne / quiero leer algo grande');

IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Curioso / quiero algo fresco')
    INSERT INTO Moods (Name) VALUES ('Curioso / quiero algo fresco');

IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Identidad / quiero leer en español')
    INSERT INTO Moods (Name) VALUES ('Identidad / quiero leer en español');

IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Cansado / quiero entrar y salir')
    INSERT INTO Moods (Name) VALUES ('Cansado / quiero entrar y salir');

IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Contemplativo / quiero algo que dure')
    INSERT INTO Moods (Name) VALUES ('Contemplativo / quiero algo que dure');

IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Sensible / quiero pocos palabras')
    INSERT INTO Moods (Name) VALUES ('Sensible / quiero pocos palabras');

-- ============================================================
-- Categorías de rotación (5 valores)
-- ============================================================
IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Ensayo / no ficción')
    INSERT INTO RotationCategories (Name) VALUES ('Ensayo / no ficción');

IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Libro corto o cuentos')
    INSERT INTO RotationCategories (Name) VALUES ('Libro corto o cuentos');

IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Clásico')
    INSERT INTO RotationCategories (Name) VALUES ('Clásico');

IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Novela grande')
    INSERT INTO RotationCategories (Name) VALUES ('Novela grande');

IF NOT EXISTS (SELECT 1 FROM RotationCategories WHERE Name = 'Contemporáneo latinoamericano o raro')
    INSERT INTO RotationCategories (Name) VALUES ('Contemporáneo latinoamericano o raro');
```

---

## 9. Configuración de Contenedores

### docker-compose.yml

```yaml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2022-latest
    environment:
      ACCEPT_EULA: "Y"
      SA_PASSWORD: "${SA_PASSWORD}"
      MSSQL_PID: "Express"
    ports:
      - "1433:1433"
    volumes:
      - sqldata:/var/opt/mssql
    healthcheck:
      test: >
        /opt/mssql-tools18/bin/sqlcmd
        -S localhost -U sa -P "${SA_PASSWORD}"
        -No -Q "SELECT 1"
      interval: 10s
      timeout: 5s
      retries: 10
      start_period: 30s

  api:
    build:
      context: ./src
      dockerfile: ReadingQueue.Api/Dockerfile
    ports:
      - "5000:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Development
      - ConnectionStrings__DefaultConnection=Server=sqlserver,1433;Database=ReadingQueueDb;User Id=sa;Password=${SA_PASSWORD};TrustServerCertificate=True;
      - Jwt__SecretKey=${JWT_SECRET_KEY}
      - Claude__ApiKey=${CLAUDE_API_KEY}
    depends_on:
      sqlserver:
        condition: service_healthy

  frontend:
    build:
      context: ./frontend
      dockerfile: Dockerfile
    ports:
      - "3000:80"
    depends_on:
      - api

volumes:
  sqldata:
```

### .env.example

```
# Base de datos SQL Server
SA_PASSWORD=YourStrong@Password123

# JWT — mínimo 32 caracteres, generado con: openssl rand -base64 32
JWT_SECRET_KEY=replace-with-a-long-random-secret-key-min-32-chars

# Claude (Anthropic) — obtener en console.anthropic.com
CLAUDE_API_KEY=sk-ant-api03-...

# Solo para el frontend en desarrollo local (no aplica en Docker)
VITE_API_BASE_URL=http://localhost:5000
```

### src/ReadingQueue.Api/Dockerfile

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ReadingQueue.Api/ReadingQueue.Api.csproj",           "ReadingQueue.Api/"]
COPY ["ReadingQueue.Application/ReadingQueue.Application.csproj", "ReadingQueue.Application/"]
COPY ["ReadingQueue.Domain/ReadingQueue.Domain.csproj",     "ReadingQueue.Domain/"]
COPY ["ReadingQueue.Infrastructure/ReadingQueue.Infrastructure.csproj", "ReadingQueue.Infrastructure/"]

RUN dotnet restore "ReadingQueue.Api/ReadingQueue.Api.csproj"

COPY . .
RUN dotnet publish "ReadingQueue.Api/ReadingQueue.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReadingQueue.Api.dll"]
```

### frontend/Dockerfile

```dockerfile
FROM node:20-alpine AS build
WORKDIR /app
COPY package*.json ./
RUN npm ci
COPY . .
RUN npm run build

FROM nginx:alpine AS runtime
COPY --from=build /app/dist /usr/share/nginx/html
COPY nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
```

### frontend/nginx.conf

```nginx
server {
    listen 80;

    location / {
        root   /usr/share/nginx/html;
        index  index.html;
        try_files $uri $uri/ /index.html;  # SPA fallback
    }

    location /api/ {
        proxy_pass         http://api:8080/api/;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
    }
}
```

---

## 10. Configuración del Frontend

### vite.config.ts

```typescript
import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import path from 'path'

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
```

### tailwind.config.ts

```typescript
import type { Config } from 'tailwindcss'

const config: Config = {
  darkMode: ['class'],
  content: [
    './index.html',
    './src/**/*.{ts,tsx}',
  ],
  theme: {
    extend: {
      // shadcn/ui requiere estas extensiones de color
      colors: {
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: {
          DEFAULT: 'hsl(var(--primary))',
          foreground: 'hsl(var(--primary-foreground))',
        },
        secondary: {
          DEFAULT: 'hsl(var(--secondary))',
          foreground: 'hsl(var(--secondary-foreground))',
        },
        muted: {
          DEFAULT: 'hsl(var(--muted))',
          foreground: 'hsl(var(--muted-foreground))',
        },
        accent: {
          DEFAULT: 'hsl(var(--accent))',
          foreground: 'hsl(var(--accent-foreground))',
        },
        destructive: {
          DEFAULT: 'hsl(var(--destructive))',
          foreground: 'hsl(var(--destructive-foreground))',
        },
      },
      borderRadius: {
        lg: 'var(--radius)',
        md: 'calc(var(--radius) - 2px)',
        sm: 'calc(var(--radius) - 4px)',
      },
    },
  },
  plugins: [require('tailwindcss-animate')],
}

export default config
```

### tsconfig.json (paths relevantes)

```json
{
  "compilerOptions": {
    "target": "ES2020",
    "lib": ["ES2020", "DOM", "DOM.Iterable"],
    "module": "ESNext",
    "moduleResolution": "bundler",
    "strict": true,
    "noUnusedLocals": true,
    "noUnusedParameters": true,
    "noImplicitAny": true,
    "baseUrl": ".",
    "paths": {
      "@/*": ["./src/*"]
    },
    "jsx": "react-jsx"
  },
  "include": ["src"]
}
```

---

## 11. Criterios de Aceptación

| ID | Criterio | Verificación |
|---|---|---|
| CA-01 | `docker-compose up` levanta los 3 servicios sin errores | Ejecución manual + `docker-compose ps` |
| CA-02 | `GET /health` retorna `200 OK` con `"status": "ok"` | curl / Postman |
| CA-03 | Las 5 tablas de negocio existen en la BD tras el primer arranque | Query SQL: `SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES` |
| CA-04 | Las 4 tablas de referencia tienen los valores canónicos completos | Query SQL: `SELECT COUNT(*) FROM Genres` → 7, etc. |
| CA-05 | Reiniciar el contenedor no re-ejecuta las migraciones ya aplicadas | Log de DbUp muestra "No new scripts to execute" |
| CA-06 | Un script de migración con error detiene el arranque de la API | Test manual: insertar SQL inválido en un script nuevo |
| CA-07 | El frontend carga en `http://localhost:3000` sin errores de consola | Verificación en navegador |
| CA-08 | El componente `Button` de shadcn/ui se renderiza en la página de prueba | Verificación visual |
| CA-09 | Una clase de Tailwind aplicada a un elemento produce el estilo esperado | Verificación visual |
| CA-10 | El alias `@/` resuelve correctamente un import en el frontend | `import { something } from '@/lib/utils'` sin error en Vite |
| CA-11 | El archivo `.env` no existe ni puede comitearse (está en `.gitignore`) | `git status` no muestra `.env` |
| CA-12 | La imagen Docker del backend no contiene el SDK de .NET | `docker image inspect` muestra solo el runtime |
| CA-13 | La solución .NET compila sin warnings con `dotnet build` | Ejecución en CI |
| CA-14 | `IDbConnectionFactory` puede inyectarse en un test unitario con Moq | Test unitario en `Infrastructure.Tests` |

---

## 12. Archivos que este spec genera

```
/                                         ← raíz del repositorio
├── .env.example
├── .gitignore
├── docker-compose.yml
├── ReadingQueue.sln
│
├── src/
│   ├── ReadingQueue.Domain/
│   │   └── ReadingQueue.Domain.csproj   ← sin dependencias externas
│   │
│   ├── ReadingQueue.Application/
│   │   └── ReadingQueue.Application.csproj   ← ref: Domain
│   │
│   ├── ReadingQueue.Infrastructure/
│   │   ├── ReadingQueue.Infrastructure.csproj  ← ref: Domain, Dapper, DbUp
│   │   ├── Data/
│   │   │   ├── IDbConnectionFactory.cs
│   │   │   └── SqlConnectionFactory.cs
│   │   └── Migrations/
│   │       ├── MigrationRunner.cs
│   │       ├── 001_initial_schema.sql
│   │       └── 002_seed_reference_data.sql
│   │
│   └── ReadingQueue.Api/
│       ├── ReadingQueue.Api.csproj       ← ref: Application, Infrastructure
│       ├── Dockerfile
│       ├── Program.cs
│       ├── appsettings.json
│       ├── appsettings.Development.json
│       └── Endpoints/
│           └── HealthEndpoints.cs
│
├── frontend/
│   ├── Dockerfile
│   ├── nginx.conf
│   ├── index.html
│   ├── package.json
│   ├── vite.config.ts
│   ├── tailwind.config.ts
│   ├── tsconfig.json
│   └── src/
│       ├── main.tsx
│       ├── App.tsx                       ← página de prueba con Button shadcn
│       ├── index.css                     ← variables CSS de shadcn + @tailwind
│       ├── api/
│       │   └── index.ts
│       ├── components/
│       │   └── ui/                       ← generado por shadcn CLI
│       │       └── button.tsx
│       ├── hooks/
│       │   └── index.ts
│       ├── lib/
│       │   └── utils.ts                  ← cn() helper de shadcn
│       ├── pages/
│       │   └── index.ts
│       ├── stores/
│       │   └── index.ts
│       └── types/
│           └── index.ts
│
└── tests/
    ├── ReadingQueue.Domain.Tests/
    │   └── ReadingQueue.Domain.Tests.csproj
    ├── ReadingQueue.Application.Tests/
    │   └── ReadingQueue.Application.Tests.csproj
    ├── ReadingQueue.Infrastructure.Tests/
    │   ├── ReadingQueue.Infrastructure.Tests.csproj  ← ref: Testcontainers
    │   └── Data/
    │       └── SqlConnectionFactoryTests.cs
    └── ReadingQueue.Api.Tests/
        └── ReadingQueue.Api.Tests.csproj
```
