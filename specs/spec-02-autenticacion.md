# spec-02-autenticacion.md
# Feature: Autenticación de Usuarios (JWT + Refresh Token)

## 1. Resumen

Implementar el sistema de autenticación completo que permite a usuarios
registrarse, iniciar sesión y mantener sesiones seguras mediante JWT Bearer.
Incluye access tokens de corta duración (15 minutos), refresh tokens
persistidos en base de datos (7 días), hashing de passwords con BCrypt, y
el helper `HttpContext.GetUserId()` que todos los specs posteriores usarán
para aislar datos por usuario. Al terminar este spec, los endpoints de
autenticación están operativos y el middleware de autorización está
configurado globalmente para proteger todos los endpoints futuros.

---

## 2. Motivación

Los Specs 3, 4 y 5 construyen funcionalidad sobre la identidad del usuario:
cada libro, cada cola y cada sugerencia pertenece a un `UserId` específico.
Sin este spec no existe `UserId` confiable en el contexto de ninguna
petición. El aislamiento de datos por usuario es un principio no negociable
de la constitution — y ese aislamiento solo es posible si primero existe
un mecanismo de autenticación robusto que entregue el `UserId` desde el
JWT, nunca desde el body del request.

---

## 3. Usuarios y Casos de Uso

| Actor | Caso de uso |
|---|---|
| Usuario anónimo | Registrarse con email, password y nombre para mostrar |
| Usuario anónimo | Iniciar sesión con email y password |
| Usuario autenticado | Renovar su access token con un refresh token válido |
| Usuario autenticado | Cerrar sesión (revocar refresh token activo) |
| Sistema (todos los specs) | Extraer el `UserId` del JWT en cualquier endpoint protegido |
| Sistema | Rechazar peticiones sin token o con token expirado con `401` |

---

## 4. Requisitos Funcionales

### RF-01 — Registro de usuario
- El sistema acepta `email`, `password` y `displayName`.
- `email` debe ser único en la tabla `Users` — si ya existe retorna `409`.
- `password` debe tener mínimo 8 caracteres, al menos una mayúscula y al
  menos un número. Si no cumple retorna `422` con los errores detallados.
- El password nunca se almacena en texto plano — siempre se hashea con
  BCrypt (work factor 12) antes de persistir.
- Al registrarse correctamente, el sistema crea el usuario y retorna
  directamente un access token + refresh token (el usuario queda logueado).
- El usuario recién creado queda con `IsActive = true`.

### RF-02 — Login
- El sistema acepta `email` y `password`.
- Si el email no existe o el password no coincide, retorna `401` con un
  mensaje genérico — nunca revelar cuál de los dos falló.
- Si el usuario tiene `IsActive = false`, retorna `401` con mensaje
  "Cuenta desactivada".
- Al autenticarse correctamente, retorna un access token JWT y un refresh
  token nuevo persistido en `RefreshTokens`.
- Cada login genera un refresh token nuevo. Los anteriores no se revocan
  automáticamente (un usuario puede tener sesiones en múltiples
  dispositivos).

### RF-03 — Refresh token
- El sistema acepta un `refreshToken` (string).
- Verifica que el token exista en `RefreshTokens`, no esté revocado
  (`IsRevoked = false`) y no haya expirado (`ExpiresAt > UTCNOW`).
- Si cualquiera de esas condiciones falla, retorna `401`.
- Si es válido, genera un nuevo access token JWT y un nuevo refresh token,
  revoca el refresh token usado (`IsRevoked = true`) y persiste el nuevo.
  Esto implementa rotación de refresh tokens.
- El nuevo refresh token hereda el `UserId` del token que se está
  rotando.

### RF-04 — Logout
- El sistema acepta un `refreshToken` (string).
- Busca el token en `RefreshTokens` y lo marca como revocado
  (`IsRevoked = true`).
- Si el token no existe, retorna `200` igualmente — el logout es
  idempotente.
- No invalida otros refresh tokens del mismo usuario (logout de
  dispositivo único, no global).

### RF-05 — Helper de extracción de UserId
- Existe un método de extensión estático
  `HttpContext.GetUserId(): int` en `ReadingQueue.Api`.
- Lee el claim `sub` del JWT y lo retorna como `int`.
- Si el claim no existe o no es parseable como `int`, lanza
  `UnauthorizedException` — nunca retorna 0 ni null.
- Este método es el ÚNICO mecanismo permitido para obtener el `UserId`
  en un endpoint. Queda prohibido leer `UserId` del body o de query
  params en cualquier endpoint protegido.

### RF-06 — Middleware de autorización global
- Todos los endpoints del sistema requieren autenticación por defecto,
  aplicado con `.RequireAuthorization()` en el grupo raíz de `Program.cs`.
- Los únicos endpoints exentos son `POST /api/auth/register`,
  `POST /api/auth/login` y `GET /health`.
- Una petición sin token o con token inválido o expirado recibe `401`
  — nunca `403` ni redireccionamiento.

### RF-07 — Perfil del usuario autenticado
- `GET /api/auth/me` retorna el `id`, `email` y `displayName` del usuario
  autenticado extraídos de la BD (no del JWT).
- Requiere token válido. Si el `UserId` del JWT no existe en BD retorna
  `404`.

---

## 5. Requisitos No Funcionales

- **Seguridad de passwords**: BCrypt con work factor 12. Nunca MD5, SHA1
  ni SHA256 para passwords.
- **Tokens cortos**: El access token expira en 15 minutos para minimizar
  el impacto de una filtración.
- **Rotación de refresh tokens**: Cada uso del refresh token genera uno
  nuevo y revoca el anterior — si un token robado se usa después de que
  el usuario legítimo lo rotó, el intento falla con `401`.
- **Mensajes genéricos en auth**: Login fallido nunca revela si el email
  existe o no. Siempre el mismo mensaje: "Credenciales inválidas".
- **SecretKey fuera del código**: La clave de firma del JWT nunca aparece
  en código fuente ni en `appsettings.json` commiteado. Solo en variable
  de entorno `Jwt__SecretKey`.
- **Claims mínimos en JWT**: El token solo incluye `sub` (UserId),
  `email`, `name` y las fechas estándar `iat`/`exp`/`nbf`. No incluye
  roles ni permisos — este sistema es single-role.

---

## 6. Modelo de Dominio

```csharp
// src/ReadingQueue.Domain/Entities/User.cs
public sealed class User
{
    public int Id { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public string DisplayName { get; }
    public DateTime CreatedAt { get; }
    public bool IsActive { get; }

    public User(int id, string email, string passwordHash,
                string displayName, DateTime createdAt, bool isActive)
    {
        Id           = id;
        Email        = email;
        PasswordHash = passwordHash;
        DisplayName  = displayName;
        CreatedAt    = createdAt;
        IsActive     = isActive;
    }
}

// src/ReadingQueue.Domain/Entities/RefreshToken.cs
public sealed class RefreshToken
{
    public int Id { get; }
    public int UserId { get; }
    public string Token { get; }
    public DateTime ExpiresAt { get; }
    public DateTime CreatedAt { get; }
    public bool IsRevoked { get; }

    public bool IsValid(DateTime utcNow)
        => !IsRevoked && ExpiresAt > utcNow;
}

// src/ReadingQueue.Domain/Interfaces/IUserRepository.cs
public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<int> CreateAsync(string email, string passwordHash,
                          string displayName, CancellationToken ct = default);
}

// src/ReadingQueue.Domain/Interfaces/IRefreshTokenRepository.cs
public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task CreateAsync(int userId, string token,
                     DateTime expiresAt, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}

// src/ReadingQueue.Domain/Interfaces/IAuthService.cs
public interface IAuthService
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hash);
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}

// src/ReadingQueue.Domain/Exceptions/UnauthorizedException.cs
public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}

// src/ReadingQueue.Domain/Exceptions/ConflictException.cs
public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
```

---

## 7. Modelo de Application (Use Cases)

```csharp
// src/ReadingQueue.Application/UseCases/RegisterUser.cs
public sealed class RegisterUser
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public record Command(string Email, string Password, string DisplayName);
    public record Result(string AccessToken, string RefreshToken,
                         int UserId, string DisplayName);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct)
    {
        // 1. Verificar email único
        // 2. Hashear password
        // 3. Crear usuario
        // 4. Generar access token + refresh token
        // 5. Persistir refresh token
        // 6. Retornar tokens
    }
}

// src/ReadingQueue.Application/UseCases/LoginUser.cs
public sealed class LoginUser
{
    public record Command(string Email, string Password);
    public record Result(string AccessToken, string RefreshToken,
                         int UserId, string DisplayName);
    // Lanza UnauthorizedException si credenciales inválidas
}

// src/ReadingQueue.Application/UseCases/RefreshAccessToken.cs
public sealed class RefreshAccessToken
{
    public record Command(string RefreshToken);
    public record Result(string AccessToken, string NewRefreshToken);
    // Lanza UnauthorizedException si token inválido/expirado/revocado
}

// src/ReadingQueue.Application/UseCases/LogoutUser.cs
public sealed class LogoutUser
{
    public record Command(string RefreshToken);
    // Revoca el refresh token. Idempotente — no lanza si no existe.
}

// src/ReadingQueue.Application/UseCases/GetCurrentUser.cs
public sealed class GetCurrentUser
{
    public record Query(int UserId);
    public record Result(int Id, string Email, string DisplayName);
    // Lanza UnauthorizedException si UserId no existe en BD
}
```

---

## 8. Contrato de API

### POST `/api/auth/register`
Registra un nuevo usuario y retorna tokens de sesión.

**Request:**
```json
{
  "email": "usuario@ejemplo.com",
  "password": "MiPassword123",
  "displayName": "Juan García"
}
```

**Response `201 Created`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "d4f8a2b1c3e5f7a9...",
  "userId": 42,
  "displayName": "Juan García"
}
```

**Responses de error:**
- `409 Conflict` — el email ya está registrado
  ```json
  { "error": "El email ya está registrado." }
  ```
- `422 Unprocessable Entity` — validación fallida
  ```json
  {
    "errors": {
      "password": ["Mínimo 8 caracteres, una mayúscula y un número."],
      "email": ["Formato de email inválido."]
    }
  }
  ```

---

### POST `/api/auth/login`
Autentica un usuario existente.

**Request:**
```json
{
  "email": "usuario@ejemplo.com",
  "password": "MiPassword123"
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "d4f8a2b1c3e5f7a9...",
  "userId": 42,
  "displayName": "Juan García"
}
```

**Responses de error:**
- `401 Unauthorized` — credenciales inválidas o cuenta desactivada
  ```json
  { "error": "Credenciales inválidas." }
  ```

---

### POST `/api/auth/refresh`
Rota el refresh token y emite un nuevo access token.

**Request:**
```json
{
  "refreshToken": "d4f8a2b1c3e5f7a9..."
}
```

**Response `200 OK`:**
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "refreshToken": "f1e2d3c4b5a6..."
}
```

**Responses de error:**
- `401 Unauthorized` — token inválido, expirado o ya revocado
  ```json
  { "error": "Token de renovación inválido." }
  ```

---

### POST `/api/auth/logout`
Revoca el refresh token del dispositivo actual.

**Request:**
```json
{
  "refreshToken": "d4f8a2b1c3e5f7a9..."
}
```

**Response `200 OK`:**
```json
{ "message": "Sesión cerrada correctamente." }
```

Requiere JWT válido en el header `Authorization: Bearer <token>`.
Es idempotente: si el token ya fue revocado o no existe, igual retorna `200`.

---

### GET `/api/auth/me`
Retorna el perfil del usuario autenticado.

Requiere JWT válido en el header `Authorization: Bearer <token>`.

**Response `200 OK`:**
```json
{
  "id": 42,
  "email": "usuario@ejemplo.com",
  "displayName": "Juan García"
}
```

**Responses de error:**
- `401 Unauthorized` — sin token o token inválido
- `404 Not Found` — UserId del JWT no existe en BD (caso raro: usuario eliminado)

---

## 9. Implementación de Infraestructura

### JwtService

```csharp
// src/ReadingQueue.Infrastructure/Auth/JwtService.cs
public sealed class JwtService : IAuthService
{
    private readonly JwtOptions _options;

    public string HashPassword(string plain)
        => BCrypt.Net.BCrypt.HashPassword(plain, workFactor: 12);

    public bool VerifyPassword(string plain, string hash)
        => BCrypt.Net.BCrypt.Verify(plain, hash);

    public string GenerateAccessToken(User user)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(JwtRegisteredClaimNames.Name,  user.DisplayName),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var key   = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             _options.Issuer,
            audience:           _options.Audience,
            claims:             claims,
            notBefore:          DateTime.UtcNow,
            expires:            DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
}
```

### HttpContext Extension

```csharp
// src/ReadingQueue.Api/Extensions/HttpContextExtensions.cs
public static class HttpContextExtensions
{
    public static int GetUserId(this HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(sub, out var userId))
            return userId;

        throw new UnauthorizedException("UserId no encontrado en el token.");
    }
}
```

### Repositorios SQL

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlUserRepository.cs
public sealed class SqlUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            UserQueries.GetByEmail,
            new { Email = email }
        );
    }

    public async Task<int> CreateAsync(
        string email, string passwordHash, string displayName, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            UserQueries.Insert,
            new { Email = email, PasswordHash = passwordHash, DisplayName = displayName }
        );
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            UserQueries.GetById,
            new { UserId = userId }
        );
    }
}
```

### Queries SQL

```csharp
// src/ReadingQueue.Infrastructure/Sql/UserQueries.cs
internal static class UserQueries
{
    internal const string GetByEmail = """
        SELECT Id, Email, PasswordHash, DisplayName, CreatedAt, IsActive
        FROM Users
        WHERE Email = @Email;
        """;

    internal const string GetById = """
        SELECT Id, Email, PasswordHash, DisplayName, CreatedAt, IsActive
        FROM Users
        WHERE Id = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO Users (Email, PasswordHash, DisplayName)
        OUTPUT INSERTED.Id
        VALUES (@Email, @PasswordHash, @DisplayName);
        """;
}

// src/ReadingQueue.Infrastructure/Sql/RefreshTokenQueries.cs
internal static class RefreshTokenQueries
{
    internal const string GetByToken = """
        SELECT Id, UserId, Token, ExpiresAt, CreatedAt, IsRevoked
        FROM RefreshTokens
        WHERE Token = @Token;
        """;

    internal const string Insert = """
        INSERT INTO RefreshTokens (UserId, Token, ExpiresAt)
        VALUES (@UserId, @Token, @ExpiresAt);
        """;

    internal const string Revoke = """
        UPDATE RefreshTokens
        SET IsRevoked = 1
        WHERE Token = @Token;
        """;
}
```

### Registro en Program.cs

```csharp
// Fragmento relevante de src/ReadingQueue.Api/Program.cs

// ── Opciones JWT desde configuración ──────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// ── Autenticación JWT Bearer ───────────────────────────────────────────────
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        var jwtSection = builder.Configuration.GetSection("Jwt");
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
            ClockSkew                = TimeSpan.Zero  // sin margen extra
        };
    });

builder.Services.AddAuthorization();

// ── Servicios de dominio ───────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService,           JwtService>();
builder.Services.AddScoped<IUserRepository,        SqlUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, SqlRefreshTokenRepository>();

// ── Use cases ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<RegisterUser>();
builder.Services.AddScoped<LoginUser>();
builder.Services.AddScoped<RefreshAccessToken>();
builder.Services.AddScoped<LogoutUser>();
builder.Services.AddScoped<GetCurrentUser>();

// ── Grupos de endpoints ────────────────────────────────────────────────────
var auth = app.MapGroup("/api/auth");   // sin RequireAuthorization()
auth.MapPost("/register", AuthEndpoints.Register);
auth.MapPost("/login",    AuthEndpoints.Login);
auth.MapPost("/refresh",  AuthEndpoints.Refresh);
auth.MapPost("/logout",   AuthEndpoints.Logout)   .RequireAuthorization();
auth.MapGet ("/me",       AuthEndpoints.Me)        .RequireAuthorization();

// Los grupos de specs futuros van con RequireAuthorization() global:
// var books = app.MapGroup("/api/books").RequireAuthorization();
// var queue = app.MapGroup("/api/queue").RequireAuthorization();
```

### Manejo global de excepciones de auth

```csharp
// El middleware de excepciones (ya registrado en Spec 1 o aquí)
// debe mapear las excepciones de dominio a respuestas HTTP:

// UnauthorizedException  → 401
// ConflictException      → 409
// ValidationException    → 422

app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, message) = ex switch
    {
        UnauthorizedException e => (401, e.Message),
        ConflictException     e => (409, e.Message),
        _                       => (500, "Error interno del servidor.")
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { error = message });
}));
```

---

## 10. Configuración de appsettings

```json
// src/ReadingQueue.Api/appsettings.json
{
  "Jwt": {
    "SecretKey": "",
    "Issuer": "readingqueue-api",
    "Audience": "readingqueue-client",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

```json
// src/ReadingQueue.Api/appsettings.Development.json
{
  "Jwt": {
    "SecretKey": "development-only-secret-min-32-chars-not-for-prod"
  }
}
```

`SecretKey` en producción siempre viene de la variable de entorno
`Jwt__SecretKey`. El valor de `appsettings.Development.json` es solo
para correr tests locales sin Docker — nunca va a producción.

---

## 11. Validaciones con FluentValidation

```csharp
// src/ReadingQueue.Api/Validators/RegisterRequestValidator.cs
public sealed class RegisterRequestValidator
    : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(256);

        RuleFor(x => x.Password)
            .NotEmpty()
            .MinimumLength(8)
            .Matches("[A-Z]").WithMessage("Debe contener al menos una mayúscula.")
            .Matches("[0-9]").WithMessage("Debe contener al menos un número.");

        RuleFor(x => x.DisplayName)
            .NotEmpty()
            .MaximumLength(200);
    }
}

// src/ReadingQueue.Api/Validators/LoginRequestValidator.cs
public sealed class LoginRequestValidator
    : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty()
            .EmailAddress();

        RuleFor(x => x.Password)
            .NotEmpty();
    }
}
```

---

## 12. Criterios de Aceptación

| ID | Criterio | Verificación |
|---|---|---|
| CA-01 | Registro con email nuevo retorna `201` con access token y refresh token | Test integration |
| CA-02 | Registro con email duplicado retorna `409` | Test integration |
| CA-03 | Registro con password sin mayúscula retorna `422` con mensaje descriptivo | Test unitario validador |
| CA-04 | Registro con password sin número retorna `422` con mensaje descriptivo | Test unitario validador |
| CA-05 | El password nunca se almacena en texto plano — siempre hash BCrypt | Query SQL directa en test integration |
| CA-06 | Login con credenciales correctas retorna `200` con tokens | Test integration |
| CA-07 | Login con email inexistente retorna `401` con mensaje genérico | Test integration |
| CA-08 | Login con password incorrecto retorna `401` con el mismo mensaje genérico que CA-07 | Test integration |
| CA-09 | El mensaje de error de login no revela si el email existe | Verificación de igualdad de mensajes CA-07 y CA-08 |
| CA-10 | Refresh con token válido retorna nuevo access token y nuevo refresh token | Test integration |
| CA-11 | El refresh token usado queda revocado en BD tras la rotación | Query SQL directa en test integration |
| CA-12 | Refresh con token ya revocado retorna `401` | Test integration |
| CA-13 | Refresh con token expirado retorna `401` | Test integration con token de expiración manipulada |
| CA-14 | Logout revoca el refresh token en BD | Query SQL directa en test integration |
| CA-15 | Logout con token ya revocado retorna `200` (idempotente) | Test integration |
| CA-16 | `GET /api/auth/me` retorna perfil correcto con token válido | Test integration |
| CA-17 | `GET /api/books` sin token retorna `401` | Test integration |
| CA-18 | `GET /api/books` con token expirado retorna `401` | Test integration con token de vida manipulada |
| CA-19 | `HttpContext.GetUserId()` extrae el `UserId` correcto del claim `sub` | Test unitario |
| CA-20 | `HttpContext.GetUserId()` lanza `UnauthorizedException` si no hay claim `sub` | Test unitario |
| CA-21 | El JWT generado contiene exactamente los claims `sub`, `email`, `name`, `jti`, `iat`, `exp`, `nbf` | Test unitario JwtService |
| CA-22 | `ClockSkew` es `TimeSpan.Zero` — un token expirado hace 1 segundo ya no es válido | Test unitario con reloj manipulado |

---

## 13. Archivos que este spec genera

```
src/
  ReadingQueue.Domain/
    Entities/
      User.cs
      RefreshToken.cs
    Interfaces/
      IUserRepository.cs
      IRefreshTokenRepository.cs
      IAuthService.cs
    Exceptions/
      UnauthorizedException.cs
      ConflictException.cs

  ReadingQueue.Application/
    UseCases/
      RegisterUser.cs
      LoginUser.cs
      RefreshAccessToken.cs
      LogoutUser.cs
      GetCurrentUser.cs

  ReadingQueue.Infrastructure/
    Auth/
      JwtService.cs
      JwtOptions.cs
    Data/
      SqlUserRepository.cs
      SqlRefreshTokenRepository.cs
    Sql/
      UserQueries.cs
      RefreshTokenQueries.cs

  ReadingQueue.Api/
    Endpoints/
      AuthEndpoints.cs
    Extensions/
      HttpContextExtensions.cs
    Validators/
      RegisterRequestValidator.cs
      LoginRequestValidator.cs
    Requests/
      RegisterRequest.cs
      LoginRequest.cs
      RefreshRequest.cs
      LogoutRequest.cs
    Responses/
      AuthResponse.cs
      TokenResponse.cs
      UserProfileResponse.cs

tests/
  ReadingQueue.Domain.Tests/
    UserEntityTests.cs
    RefreshTokenEntityTests.cs

  ReadingQueue.Application.Tests/
    RegisterUserTests.cs
    LoginUserTests.cs
    RefreshAccessTokenTests.cs
    LogoutUserTests.cs

  ReadingQueue.Infrastructure.Tests/
    Auth/
      JwtServiceTests.cs
    Data/
      SqlUserRepositoryTests.cs        ← Testcontainers
      SqlRefreshTokenRepositoryTests.cs ← Testcontainers

  ReadingQueue.Api.Tests/
    AuthEndpointsTests.cs              ← TestServer + cliente HTTP real
    HttpContextExtensionsTests.cs
```
