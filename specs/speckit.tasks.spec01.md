# SPEC-01 · Tasks — Infraestructura Base y Esquema de Datos
> Proyecto: `ReadingQueue` (solución .NET 9 + React/Vite)
> Regla: **Tests primero, implementación después** (constitution §13)
> Cobertura mínima: **80%** en Domain y Application · Integración con Testcontainers en Infrastructure

---

## Bloque A — Estructura de solución .NET (RF-01)

### TASK-01-A1 · Crear la solución y los 4 proyectos

- **Acción:** Scaffolding manual (no hay tests previos para estructura de archivos).
- **Comandos:**

```powershell
dotnet new sln -n ReadingQueue

# Proyectos en src/
dotnet new classlib -n ReadingQueue.Domain         -o src/ReadingQueue.Domain         -f net9.0
dotnet new classlib -n ReadingQueue.Application    -o src/ReadingQueue.Application    -f net9.0
dotnet new classlib -n ReadingQueue.Infrastructure -o src/ReadingQueue.Infrastructure -f net9.0
dotnet new web      -n ReadingQueue.Api            -o src/ReadingQueue.Api            -f net9.0

# Proyectos en tests/
dotnet new xunit -n ReadingQueue.Domain.Tests         -o tests/ReadingQueue.Domain.Tests
dotnet new xunit -n ReadingQueue.Application.Tests    -o tests/ReadingQueue.Application.Tests
dotnet new xunit -n ReadingQueue.Infrastructure.Tests -o tests/ReadingQueue.Infrastructure.Tests
dotnet new xunit -n ReadingQueue.Api.Tests            -o tests/ReadingQueue.Api.Tests

# Agregar a la solución
dotnet sln add src/ReadingQueue.Domain/ReadingQueue.Domain.csproj
dotnet sln add src/ReadingQueue.Application/ReadingQueue.Application.csproj
dotnet sln add src/ReadingQueue.Infrastructure/ReadingQueue.Infrastructure.csproj
dotnet sln add src/ReadingQueue.Api/ReadingQueue.Api.csproj
dotnet sln add tests/ReadingQueue.Domain.Tests/ReadingQueue.Domain.Tests.csproj
dotnet sln add tests/ReadingQueue.Application.Tests/ReadingQueue.Application.Tests.csproj
dotnet sln add tests/ReadingQueue.Infrastructure.Tests/ReadingQueue.Infrastructure.Tests.csproj
dotnet sln add tests/ReadingQueue.Api.Tests/ReadingQueue.Api.Tests.csproj
```

- **Referencias entre proyectos** (dirección constitution §4.1):

```powershell
# Application → Domain
dotnet add src/ReadingQueue.Application reference src/ReadingQueue.Domain

# Infrastructure → Domain
dotnet add src/ReadingQueue.Infrastructure reference src/ReadingQueue.Domain

# Api → Application + Infrastructure
dotnet add src/ReadingQueue.Api reference src/ReadingQueue.Application
dotnet add src/ReadingQueue.Api reference src/ReadingQueue.Infrastructure

# Tests referencian solo su proyecto objetivo
dotnet add tests/ReadingQueue.Infrastructure.Tests reference src/ReadingQueue.Infrastructure
dotnet add tests/ReadingQueue.Api.Tests reference src/ReadingQueue.Api
```

- **Paquetes NuGet obligatorios:**

```powershell
# Infrastructure
dotnet add src/ReadingQueue.Infrastructure package Dapper
dotnet add src/ReadingQueue.Infrastructure package Microsoft.Data.SqlClient
dotnet add src/ReadingQueue.Infrastructure package dbup-sqlserver
dotnet add src/ReadingQueue.Infrastructure package BCrypt.Net-Next

# Api
dotnet add src/ReadingQueue.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/ReadingQueue.Api package Microsoft.AspNetCore.OpenApi

# Tests de Infrastructure
dotnet add tests/ReadingQueue.Infrastructure.Tests package Testcontainers.MsSql
dotnet add tests/ReadingQueue.Infrastructure.Tests package FluentAssertions
dotnet add tests/ReadingQueue.Infrastructure.Tests package Moq

# Tests de Api
dotnet add tests/ReadingQueue.Api.Tests package FluentAssertions
dotnet add tests/ReadingQueue.Api.Tests package Microsoft.AspNetCore.Mvc.Testing
```

- **Completado cuando:** `dotnet build` retorna `0 Error(s)  0 Warning(s)` y los 4 proyectos en `src/` existen con las referencias correctas.

---

## Bloque B — IDbConnectionFactory (RF-06)

> Este bloque va antes de las migraciones porque MigrationRunner la usa.

### TASK-01-B1 · Test: `SqlConnectionFactory` se construye y crea conexiones

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlConnectionFactoryTests.cs`
- **Casos:**
  - [ ] Constructor con `IConfiguration` que tiene `ConnectionStrings:DefaultConnection` → no lanza excepción.
  - [ ] Constructor con `IConfiguration` sin `ConnectionStrings:DefaultConnection` → lanza `InvalidOperationException`.
  - [ ] `Create()` retorna una instancia de `IDbConnection` no nula.
  - [ ] `Create()` retorna una **nueva instancia** en cada llamada (no singleton de conexión).
- **Nota:** Usar Moq para `IConfiguration`. No requiere SQL Server real.
- **Completado cuando:** test compila y falla (rojo).

### TASK-01-B2 · Implementar `IDbConnectionFactory` y `SqlConnectionFactory`

```csharp
// src/ReadingQueue.Infrastructure/Data/IDbConnectionFactory.cs
using System.Data;

namespace ReadingQueue.Infrastructure.Data;

public interface IDbConnectionFactory
{
    IDbConnection Create();
}
```

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlConnectionFactory.cs
using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration config)
    {
        _connectionString = config.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:DefaultConnection is not configured.");
    }

    public IDbConnection Create() => new SqlConnection(_connectionString);
}
```

- **Completado cuando:** tests de TASK-01-B1 pasan (verde).

---

## Bloque C — Migraciones DbUp (RF-03, RF-04, RF-05)

### TASK-01-C1 · Test de integración: migraciones crean el esquema completo

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Migrations/MigrationRunnerTests.cs`
- **Tecnología:** Testcontainers para SQL Server real (no mocks de BD).
- **Casos:**
  - [ ] Ejecutar `MigrationRunner.Run(connectionString)` contra BD vacía → no lanza excepción.
  - [ ] Tras migrar, `INFORMATION_SCHEMA.TABLES` contiene: `Users`, `Books`, `ReadingQueue`, `AISuggestions`, `RefreshTokens`, `Genres`, `MentalEnergyLevels`, `Moods`, `RotationCategories`.
  - [ ] `SELECT COUNT(*) FROM Genres` = 7.
  - [ ] `SELECT COUNT(*) FROM MentalEnergyLevels` = 5.
  - [ ] `SELECT COUNT(*) FROM Moods` = 7.
  - [ ] `SELECT COUNT(*) FROM RotationCategories` = 5.
  - [ ] Ejecutar `MigrationRunner.Run` dos veces seguidas → no lanza excepción (idempotente).
  - [ ] Log de DbUp en segunda ejecución indica que no hay scripts nuevos.
- **Completado cuando:** test compila y falla (rojo).

### TASK-01-C2 · Implementar `MigrationRunner` y scripts SQL

**Archivo:** `src/ReadingQueue.Infrastructure/Migrations/MigrationRunner.cs`

```csharp
using System.Reflection;
using DbUp;

namespace ReadingQueue.Infrastructure.Migrations;

public static class MigrationRunner
{
    public static void Run(string connectionString)
    {
        EnsureDatabase.For.SqlDatabase(connectionString);

        var upgrader = DeployChanges.To
            .SqlDatabase(connectionString)
            .WithScriptsEmbeddedInAssembly(
                Assembly.GetExecutingAssembly(),
                s => s.Contains("ReadingQueue.Infrastructure.Migrations.Scripts"))
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

**Archivo:** `src/ReadingQueue.Infrastructure/Migrations/Scripts/001_initial_schema.sql`
(marcar como `EmbeddedResource` en el `.csproj`)

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

CREATE INDEX IX_Books_UserId         ON Books(UserId);
CREATE INDEX IX_Books_UserId_Genre   ON Books(UserId, Genre);
CREATE INDEX IX_Books_UserId_IsRead  ON Books(UserId, IsRead);

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

    CONSTRAINT UQ_Queue_UserBook UNIQUE (UserId, BookId)
);

CREATE INDEX IX_Queue_UserId ON ReadingQueue(UserId, Position);

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
-- Tablas de referencia (enumeraciones — solo lectura desde API)
-- ============================================================
CREATE TABLE Genres (
    Name NVARCHAR(100) NOT NULL PRIMARY KEY
);

CREATE TABLE MentalEnergyLevels (
    Name      NVARCHAR(100) NOT NULL PRIMARY KEY,
    SortOrder TINYINT       NOT NULL
);

CREATE TABLE Moods (
    Name NVARCHAR(200) NOT NULL PRIMARY KEY
);

CREATE TABLE RotationCategories (
    Name NVARCHAR(100) NOT NULL PRIMARY KEY
);
```

**Archivo:** `src/ReadingQueue.Infrastructure/Migrations/Scripts/002_seed_reference_data.sql`
(marcar como `EmbeddedResource` en el `.csproj`)

```sql
-- Géneros (7 valores)
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

-- Niveles de energía mental (5 valores)
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

-- Ánimos recomendados (7 valores)
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
IF NOT EXISTS (SELECT 1 FROM Moods WHERE Name = 'Sensible / quiero pocas palabras')
    INSERT INTO Moods (Name) VALUES ('Sensible / quiero pocas palabras');

-- Categorías de rotación (5 valores)
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

**Importante:** Agregar al `.csproj` de Infrastructure para que los SQL queden embebidos:

```xml
<ItemGroup>
  <EmbeddedResource Include="Migrations\Scripts\*.sql" />
</ItemGroup>
```

- **Completado cuando:** tests de TASK-01-C1 pasan (verde).

---

## Bloque D — Health Check endpoint (RF-07)

### TASK-01-D1 · Test: `GET /health` responde correctamente

- **Archivo test:** `tests/ReadingQueue.Api.Tests/Endpoints/HealthEndpointsTests.cs`
- **Tecnología:** `WebApplicationFactory<Program>` + cliente HTTP real.
- **Casos:**
  - [ ] `GET /health` con BD accesible → `200 OK` con body `{ "status": "ok", "database": "reachable" }`.
  - [ ] El campo `timestamp` está presente y parseable como ISO 8601 UTC.
  - [ ] `GET /health` **no** requiere token JWT (sin `Authorization` header → igual `200 OK`).
  - [ ] `GET /health` con BD inaccesible → `503 Service Unavailable` con `{ "status": "degraded", "database": "unreachable" }`.
- **Nota:** Simular BD inaccesible inyectando `IDbConnectionFactory` mockeada que lanza `SqlException`.
- **Completado cuando:** test compila y falla (rojo).

### TASK-01-D2 · Implementar `HealthEndpoints` y registrar en `Program.cs`

```csharp
// src/ReadingQueue.Api/Endpoints/HealthEndpoints.cs
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Api.Endpoints;

public static class HealthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", async (IDbConnectionFactory factory) =>
        {
            try
            {
                using var conn = factory.Create();
                await conn.OpenAsync();

                return Results.Ok(new
                {
                    status = "ok",
                    database = "reachable",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch
            {
                return Results.Json(
                    new { status = "degraded", database = "unreachable", timestamp = DateTimeOffset.UtcNow },
                    statusCode: 503);
            }
        })
        .WithName("GetHealth")
        .WithSummary("Verifica estado del servidor y la base de datos")
        .WithTags("Health")
        .AllowAnonymous();
    }
}
```

```csharp
// src/ReadingQueue.Api/Program.cs — estructura mínima
var builder = WebApplication.CreateBuilder(args);

// DI
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Migraciones — fail fast si hay error
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")!;
MigrationRunner.Run(connectionString);

// Endpoints
HealthEndpoints.Map(app);

app.Run();
```

- **Completado cuando:** tests de TASK-01-D1 pasan (verde).

---

## Bloque E — Docker Compose y Dockerfiles (RF-02)

> Este bloque no tiene tests unitarios — la verificación es operacional (CA-01, CA-02, CA-07).

### TASK-01-E1 · Crear Dockerfile del backend (multi-stage)

**Archivo:** `src/ReadingQueue.Api/Dockerfile`

```dockerfile
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

COPY ["ReadingQueue.Api/ReadingQueue.Api.csproj",                         "ReadingQueue.Api/"]
COPY ["ReadingQueue.Application/ReadingQueue.Application.csproj",         "ReadingQueue.Application/"]
COPY ["ReadingQueue.Domain/ReadingQueue.Domain.csproj",                   "ReadingQueue.Domain/"]
COPY ["ReadingQueue.Infrastructure/ReadingQueue.Infrastructure.csproj",   "ReadingQueue.Infrastructure/"]

RUN dotnet restore "ReadingQueue.Api/ReadingQueue.Api.csproj"

COPY . .
RUN dotnet publish "ReadingQueue.Api/ReadingQueue.Api.csproj" \
    -c Release -o /app/publish --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0 AS runtime
WORKDIR /app
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "ReadingQueue.Api.dll"]
```

- **Criterio:** La imagen final no contiene el SDK de .NET (CA-12).

### TASK-01-E2 · Crear Dockerfile del frontend (multi-stage)

**Archivo:** `frontend/Dockerfile`

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

**Archivo:** `frontend/nginx.conf`

```nginx
server {
    listen 80;

    location / {
        root   /usr/share/nginx/html;
        index  index.html;
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass         http://api:8080/api/;
        proxy_http_version 1.1;
        proxy_set_header   Host $host;
        proxy_set_header   X-Real-IP $remote_addr;
    }
}
```

### TASK-01-E3 · Crear `docker-compose.yml` y `.env.example`

**Archivo:** `docker-compose.yml` (raíz del repo)

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

**Archivo:** `.env.example` (raíz del repo)

```
# Base de datos SQL Server
SA_PASSWORD=YourStrong@Password123

# JWT — mínimo 32 caracteres, generar con: openssl rand -base64 32
JWT_SECRET_KEY=replace-with-a-long-random-secret-key-min-32-chars

# Claude (Anthropic) — obtener en console.anthropic.com
CLAUDE_API_KEY=sk-ant-api03-...

# Solo para el frontend en desarrollo local (no aplica en Docker)
VITE_API_BASE_URL=http://localhost:5000
```

- **Completado cuando:** `docker-compose up` levanta los 3 servicios sin errores (CA-01).

---

## Bloque F — Scaffolding del Frontend React (RF-08)

> Verificación visual y de compilación — no hay tests unitarios en este bloque.

### TASK-01-F1 · Inicializar proyecto Vite + React + TypeScript

```powershell
# Desde la raíz del repositorio
npm create vite@latest frontend -- --template react-ts
cd frontend
npm install
```

### TASK-01-F2 · Instalar y configurar Tailwind CSS

```powershell
npm install -D tailwindcss postcss autoprefixer tailwindcss-animate
npx tailwindcss init -p
```

Reemplazar `frontend/tailwind.config.ts` con la configuración completa de la spec
(incluye colores CSS variables de shadcn/ui — ver §10 de spec-01).

**Archivo:** `frontend/src/index.css` (cabecera obligatoria):

```css
@tailwind base;
@tailwind components;
@tailwind utilities;

/* Variables CSS de shadcn/ui */
@layer base {
  :root {
    --background: 0 0% 100%;
    --foreground: 222.2 84% 4.9%;
    --border: 214.3 31.8% 91.4%;
    --input: 214.3 31.8% 91.4%;
    --ring: 222.2 84% 4.9%;
    --primary: 222.2 47.4% 11.2%;
    --primary-foreground: 210 40% 98%;
    --secondary: 210 40% 96.1%;
    --secondary-foreground: 222.2 47.4% 11.2%;
    --muted: 210 40% 96.1%;
    --muted-foreground: 215.4 16.3% 46.9%;
    --accent: 210 40% 96.1%;
    --accent-foreground: 222.2 47.4% 11.2%;
    --destructive: 0 84.2% 60.2%;
    --destructive-foreground: 210 40% 98%;
    --radius: 0.5rem;
  }
}
```

### TASK-01-F3 · Inicializar shadcn/ui y agregar componente Button

```powershell
npx shadcn@latest init
npx shadcn@latest add button
```

Verificar que `frontend/src/components/ui/button.tsx` existe y **no editarlo** directamente.

### TASK-01-F4 · Crear la estructura de carpetas definitiva

```powershell
# Desde frontend/src/
New-Item -ItemType File -Path api/index.ts, hooks/index.ts, lib/utils.ts, pages/index.ts, stores/index.ts, types/index.ts -Force
```

Contenido mínimo de `frontend/src/lib/utils.ts` (requerido por shadcn/ui):

```typescript
import { type ClassValue, clsx } from 'clsx'
import { twMerge } from 'tailwind-merge'

export function cn(...inputs: ClassValue[]) {
  return twMerge(clsx(inputs))
}
```

```powershell
npm install clsx tailwind-merge
```

### TASK-01-F5 · Configurar `vite.config.ts` con proxy y alias `@/`

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

### TASK-01-F6 · Página de prueba en `App.tsx`

```tsx
// frontend/src/App.tsx
import { Button } from '@/components/ui/button'

export default function App() {
  return (
    <main className="flex min-h-screen items-center justify-center bg-background">
      <div className="text-center space-y-4">
        <h1 className="text-2xl font-bold text-foreground">
          Cola Inteligente de Lectura
        </h1>
        <Button>Comenzar</Button>
      </div>
    </main>
  )
}
```

- **Completado cuando:** `npm run dev` arranca sin errores y el `Button` de shadcn/ui se ve en `http://localhost:5173` (CA-07, CA-08, CA-09, CA-10).

---

## Bloque G — Variables de entorno y .gitignore (RF-09)

> Sin tests — verificación con `git status`.

### TASK-01-G1 · Crear `.gitignore`

**Archivo:** `.gitignore` (raíz del repo)

```gitignore
# .NET
bin/
obj/
*.user
*.suo
.vs/
*.csproj.user

# Node / frontend
node_modules/
frontend/dist/
frontend/.vite/

# Secretos — NUNCA commitear
.env
.env.local
.env.*.local
*.env

# Docker local overrides
docker-compose.override.yml

# Herramientas del sistema
.DS_Store
Thumbs.db
```

- **Criterio:** `git status` no muestra `.env` aunque el archivo exista localmente (CA-11).

---

## Bloque H — Verificación Final

### TASK-01-H1 · Build .NET sin errores ni warnings

```powershell
dotnet build ReadingQueue.sln
```

- **Criterio:** `0 Error(s)  0 Warning(s)` (CA-13).

### TASK-01-H2 · Todos los tests de infrastructure pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Infrastructure.Tests/ReadingQueue.Infrastructure.Tests.csproj --logger "console;verbosity=normal"
```

- **Criterio:** Tests de `SqlConnectionFactoryTests` y `MigrationRunnerTests` pasan.

### TASK-01-H3 · Tests de API pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Api.Tests/ReadingQueue.Api.Tests.csproj --logger "console;verbosity=normal"
```

- **Criterio:** Tests de `HealthEndpointsTests` pasan.

### TASK-01-H4 · Entorno Docker completo levanta en un solo comando

```powershell
# Desde la raíz — copiar .env.example a .env y rellenar SA_PASSWORD mínimo
Copy-Item .env.example .env
docker-compose up --build
```

- **Verificaciones manuales:**

| CA | Check | Comando de verificación |
|---|---|---|
| CA-01 | 3 servicios `running` | `docker-compose ps` |
| CA-02 | Health devuelve 200 | `curl http://localhost:5000/health` |
| CA-03 | 9 tablas en BD | `sqlcmd -S localhost -U sa -P ... -Q "SELECT TABLE_NAME FROM ReadingQueueDb.INFORMATION_SCHEMA.TABLES"` |
| CA-04 | Counts correctos | `SELECT COUNT(*) FROM Genres` → 7, etc. |
| CA-05 | Sin re-ejecución de migraciones | Reiniciar contenedor api y revisar logs de DbUp |
| CA-07 | Frontend carga | Abrir `http://localhost:3000` en navegador |
| CA-08 | Button shadcn visible | Verificación visual |
| CA-12 | Imagen sin SDK | `docker image inspect readingqueue-api \| grep dotnet/sdk` → vacío |

---

## Resumen de archivos a crear en SPEC-01

| # | Archivo | Bloque |
|---|---|---|
| 1 | `ReadingQueue.sln` | A |
| 2 | `src/ReadingQueue.Domain/ReadingQueue.Domain.csproj` | A |
| 3 | `src/ReadingQueue.Application/ReadingQueue.Application.csproj` | A |
| 4 | `src/ReadingQueue.Infrastructure/ReadingQueue.Infrastructure.csproj` | A |
| 5 | `src/ReadingQueue.Api/ReadingQueue.Api.csproj` | A |
| 6 | `src/ReadingQueue.Infrastructure/Data/IDbConnectionFactory.cs` | B |
| 7 | `src/ReadingQueue.Infrastructure/Data/SqlConnectionFactory.cs` | B |
| 8 | `src/ReadingQueue.Infrastructure/Migrations/MigrationRunner.cs` | C |
| 9 | `src/ReadingQueue.Infrastructure/Migrations/Scripts/001_initial_schema.sql` | C |
| 10 | `src/ReadingQueue.Infrastructure/Migrations/Scripts/002_seed_reference_data.sql` | C |
| 11 | `src/ReadingQueue.Api/Endpoints/HealthEndpoints.cs` | D |
| 12 | `src/ReadingQueue.Api/Program.cs` | D |
| 13 | `src/ReadingQueue.Api/appsettings.json` | D |
| 14 | `src/ReadingQueue.Api/Dockerfile` | E |
| 15 | `frontend/Dockerfile` | E |
| 16 | `frontend/nginx.conf` | E |
| 17 | `docker-compose.yml` | E |
| 18 | `.env.example` | E |
| 19 | `frontend/vite.config.ts` | F |
| 20 | `frontend/tailwind.config.ts` | F |
| 21 | `frontend/tsconfig.json` | F |
| 22 | `frontend/src/index.css` | F |
| 23 | `frontend/src/App.tsx` | F |
| 24 | `frontend/src/lib/utils.ts` | F |
| 25 | `frontend/src/components/ui/button.tsx` | F (shadcn CLI) |
| 26 | `.gitignore` | G |
| 27 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlConnectionFactoryTests.cs` | B |
| 28 | `tests/ReadingQueue.Infrastructure.Tests/Migrations/MigrationRunnerTests.cs` | C |
| 29 | `tests/ReadingQueue.Api.Tests/Endpoints/HealthEndpointsTests.cs` | D |

---

## Checklist SPEC-01

### Bloque A — Estructura de solución
- [x] TASK-01-A1 · Solución .NET con 4 proyectos + 4 proyectos de test creados y referenciados

### Bloque B — IDbConnectionFactory
- [x] TASK-01-B1 · Test `SqlConnectionFactory` (rojo)
- [x] TASK-01-B2 · Impl `IDbConnectionFactory` + `SqlConnectionFactory` (verde)

### Bloque C — Migraciones DbUp
- [x] TASK-01-C1 · Test de integración de migraciones con Testcontainers (rojo)
- [x] TASK-01-C2 · Impl `MigrationRunner` + scripts `001` y `002` (verde)

### Bloque D — Health Check
- [x] TASK-01-D1 · Test `GET /health` con WebApplicationFactory (rojo)
- [x] TASK-01-D2 · Impl `HealthEndpoints` + `Program.cs` mínimo (verde)

### Bloque E — Docker Compose
- [x] TASK-01-E1 · Dockerfile del backend (multi-stage)
- [x] TASK-01-E2 · Dockerfile del frontend (multi-stage) + nginx.conf
- [x] TASK-01-E3 · `docker-compose.yml` + `.env.example`

### Bloque F — Frontend React
- [x] TASK-01-F1 · Vite + React + TypeScript inicializado
- [x] TASK-01-F2 · Tailwind CSS configurado
- [x] TASK-01-F3 · shadcn/ui inicializado + componente Button
- [x] TASK-01-F4 · Estructura de carpetas definitiva creada
- [x] TASK-01-F5 · `vite.config.ts` con proxy y alias `@/`
- [x] TASK-01-F6 · `App.tsx` de prueba con Button visible

### Bloque G — Secretos y .gitignore
- [x] TASK-01-G1 · `.gitignore` que excluye `.env` y secrets

### Bloque H — Verificación Final
- [x] TASK-01-H1 · `dotnet build` → 0 errores, 0 warnings
- [x] TASK-01-H2 · Tests de Infrastructure pasan (verde)
- [x] TASK-01-H3 · Tests de Api pasan (verde)
- [x] TASK-01-H4 · `docker-compose up --build` → 3 servicios healthy + verificaciones CA-01..CA-13
