# SPEC-02 · Tasks — Autenticación de Usuarios (JWT + Refresh Token)
> Proyecto: `ReadingQueue` (solución .NET 9 + React/Vite)
> Regla: **Tests primero, implementación después** (constitution §13)
> Cobertura mínima: **80%** en Domain y Application · Integración con Testcontainers en Infrastructure

---

## Bloque A — Paquetes NuGet nuevos

> Sin tests — preparación del entorno de compilación.

### TASK-02-A1 · Agregar paquetes NuGet requeridos por Spec-02

- **Acción:** Agregar las dependencias nuevas que Spec-02 introduce. Los paquetes ya instalados en Spec-01 no se repiten.

```powershell
# Infrastructure — manejo de JWT (JwtSecurityToken, JwtSecurityTokenHandler, etc.)
dotnet add src/ReadingQueue.Infrastructure package System.IdentityModel.Tokens.Jwt

# Api — validadores de requests
dotnet add src/ReadingQueue.Api package FluentValidation

# Tests de Domain — primer uso real de este proyecto de tests
dotnet add tests/ReadingQueue.Domain.Tests package FluentAssertions
dotnet add tests/ReadingQueue.Domain.Tests package Moq
dotnet add tests/ReadingQueue.Domain.Tests reference src/ReadingQueue.Domain

# Tests de Application — primer uso real de este proyecto de tests
dotnet add tests/ReadingQueue.Application.Tests package FluentAssertions
dotnet add tests/ReadingQueue.Application.Tests package Moq
dotnet add tests/ReadingQueue.Application.Tests reference src/ReadingQueue.Application
```

- **Nota sobre Infrastructure.Tests y Api.Tests:** ya tienen FluentAssertions, Moq y sus referencias de proyecto desde Spec-01. Solo agregar lo que falte si el build lo indica.
- **Completado cuando:** `dotnet build ReadingQueue.sln` → `0 Error(s)  0 Warning(s)`.

---

## Bloque B — Domain: Entidades, Excepciones e Interfaces

### TASK-02-B1 · Test: entidades de dominio y lógica de negocio

- **Archivo test:** `tests/ReadingQueue.Domain.Tests/UserEntityTests.cs`
- **Casos:**
  - [ ] Constructor de `User` asigna correctamente todas las propiedades (`Id`, `Email`, `PasswordHash`, `DisplayName`, `CreatedAt`, `IsActive`).
  - [ ] `User` con `IsActive = false` puede construirse sin excepción.

- **Archivo test:** `tests/ReadingQueue.Domain.Tests/RefreshTokenEntityTests.cs`
- **Casos:**
  - [ ] `IsValid(utcNow)` retorna `true` cuando `IsRevoked = false` y `ExpiresAt > utcNow`.
  - [ ] `IsValid(utcNow)` retorna `false` cuando `IsRevoked = true`, aunque no haya expirado.
  - [ ] `IsValid(utcNow)` retorna `false` cuando `ExpiresAt <= utcNow`, aunque no esté revocado.
  - [ ] `IsValid(utcNow)` retorna `false` cuando ambas condiciones fallan.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-B2 · Implementar entidades y excepciones de dominio

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Entities/
    User.cs
    RefreshToken.cs
  Exceptions/
    UnauthorizedException.cs
    ConflictException.cs
```

```csharp
// src/ReadingQueue.Domain/Entities/User.cs
namespace ReadingQueue.Domain.Entities;

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
```

```csharp
// src/ReadingQueue.Domain/Entities/RefreshToken.cs
namespace ReadingQueue.Domain.Entities;

public sealed class RefreshToken
{
    public int Id { get; }
    public int UserId { get; }
    public string Token { get; }
    public DateTime ExpiresAt { get; }
    public DateTime CreatedAt { get; }
    public bool IsRevoked { get; }

    public RefreshToken(int id, int userId, string token,
                        DateTime expiresAt, DateTime createdAt, bool isRevoked)
    {
        Id        = id;
        UserId    = userId;
        Token     = token;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        IsRevoked = isRevoked;
    }

    public bool IsValid(DateTime utcNow) => !IsRevoked && ExpiresAt > utcNow;
}
```

```csharp
// src/ReadingQueue.Domain/Exceptions/UnauthorizedException.cs
namespace ReadingQueue.Domain.Exceptions;

public sealed class UnauthorizedException : Exception
{
    public UnauthorizedException(string message) : base(message) { }
}
```

```csharp
// src/ReadingQueue.Domain/Exceptions/ConflictException.cs
namespace ReadingQueue.Domain.Exceptions;

public sealed class ConflictException : Exception
{
    public ConflictException(string message) : base(message) { }
}
```

- **Completado cuando:** tests de TASK-02-B1 pasan (verde).

### TASK-02-B3 · Crear interfaces de dominio

> Sin tests previos — las interfaces son contratos puros, se verifican implícitamente en los tests de use cases (Bloque E).

**Archivos a crear:**

```
src/ReadingQueue.Domain/
  Interfaces/
    IUserRepository.cs
    IRefreshTokenRepository.cs
    IAuthService.cs
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IUserRepository.cs
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<int> CreateAsync(string email, string passwordHash,
                          string displayName, CancellationToken ct = default);
}
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IRefreshTokenRepository.cs
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task CreateAsync(int userId, string token,
                     DateTime expiresAt, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}
```

```csharp
// src/ReadingQueue.Domain/Interfaces/IAuthService.cs
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IAuthService
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hash);
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
```

- **Completado cuando:** `dotnet build ReadingQueue.sln` → `0 Error(s)`.

---

## Bloque C — Infrastructure: JwtService

### TASK-02-C1 · Test: `JwtService` — hashing y generación de tokens

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Auth/JwtServiceTests.cs`
- **Setup:** Crear `JwtOptions` con valores de prueba: `SecretKey = "test-secret-key-that-is-long-enough-32"`, `Issuer = "test-issuer"`, `Audience = "test-audience"`, `AccessTokenMinutes = 15`, `RefreshTokenDays = 7`.

- **Casos — HashPassword / VerifyPassword:**
  - [ ] `HashPassword` retorna un string no nulo y diferente del texto plano.
  - [ ] Dos llamadas a `HashPassword` con la misma contraseña producen hashes distintos (BCrypt usa salt aleatorio).
  - [ ] `VerifyPassword` retorna `true` para la contraseña correcta contra su hash.
  - [ ] `VerifyPassword` retorna `false` para una contraseña incorrecta.

- **Casos — GenerateAccessToken (CA-21):**
  - [ ] El token decodificado contiene el claim `sub` igual a `user.Id.ToString()`.
  - [ ] El token decodificado contiene el claim `email` igual a `user.Email`.
  - [ ] El token decodificado contiene el claim `name` igual a `user.DisplayName`.
  - [ ] El token decodificado contiene el claim `jti` (no vacío, es un GUID).
  - [ ] El token **no** contiene claims adicionales más allá de `sub`, `email`, `name`, `jti`, `iat`, `exp`, `nbf`.

- **Casos — GenerateAccessToken expiración (CA-22):**
  - [ ] El campo `exp` del token corresponde a `UtcNow + 15 minutos` (±5 segundos de tolerancia).

- **Casos — GenerateRefreshToken:**
  - [ ] Retorna un string no nulo y no vacío.
  - [ ] Dos llamadas consecutivas producen valores distintos.
  - [ ] El string resultante es base64 válido.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-C2 · Implementar `JwtService` y `JwtOptions`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Auth/
    JwtOptions.cs
    JwtService.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Auth/JwtOptions.cs
namespace ReadingQueue.Infrastructure.Auth;

public sealed class JwtOptions
{
    public string SecretKey { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 15;
    public int RefreshTokenDays { get; set; } = 7;
}
```

```csharp
// src/ReadingQueue.Infrastructure/Auth/JwtService.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Infrastructure.Auth;

public sealed class JwtService : IAuthService
{
    private readonly JwtOptions _options;

    public JwtService(IOptions<JwtOptions> options)
        => _options = options.Value;

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

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
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

- **Completado cuando:** tests de TASK-02-C1 pasan (verde).

---

## Bloque D — Infrastructure: Repositorios SQL

> Usa Testcontainers con SQL Server real. Se reutiliza la fixture compartida del Spec-01 o se crea una nueva `AuthRepositoryFixture`.

### TASK-02-D1 · Test: `SqlUserRepository` contra SQL Server real

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlUserRepositoryTests.cs`
- **Fixture:** `IClassFixture<AuthContainerFixture>` — inicia contenedor, ejecuta migraciones, expone `ConnectionString`.
- **Casos:**
  - [ ] `GetByEmailAsync` con email inexistente → retorna `null`.
  - [ ] `CreateAsync` inserta un usuario y retorna un `Id > 0`.
  - [ ] `GetByEmailAsync` tras `CreateAsync` → retorna el usuario con el email correcto.
  - [ ] `GetByIdAsync` con el Id creado → retorna el usuario con `IsActive = true`.
  - [ ] `GetByIdAsync` con Id inexistente → retorna `null`.
  - [ ] El campo `PasswordHash` almacenado nunca es igual al password en texto plano (CA-05).

- **Nota:** La fixture `AuthContainerFixture` debe llamar `TestcontainersSettings.ResourceReaperEnabled = false` en su constructor, igual que `MigrationRunnerFixture` en Spec-01.
- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-D2 · Implementar `SqlUserRepository` y `UserQueries`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Data/
    SqlUserRepository.cs
  Sql/
    UserQueries.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/UserQueries.cs
namespace ReadingQueue.Infrastructure.Sql;

internal static class UserQueries
{
    internal const string GetByEmail = """
        SELECT Id, Email, PasswordHash, DisplayName, CreatedAt, IsActive
        FROM Users WHERE Email = @Email;
        """;

    internal const string GetById = """
        SELECT Id, Email, PasswordHash, DisplayName, CreatedAt, IsActive
        FROM Users WHERE Id = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO Users (Email, PasswordHash, DisplayName)
        OUTPUT INSERTED.Id
        VALUES (@Email, @PasswordHash, @DisplayName);
        """;
}
```

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlUserRepository.cs
using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlUserRepository(IDbConnectionFactory factory)
        => _factory = factory;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            UserQueries.GetByEmail, new { Email = email });
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            UserQueries.GetById, new { UserId = userId });
    }

    public async Task<int> CreateAsync(
        string email, string passwordHash, string displayName, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            UserQueries.Insert,
            new { Email = email, PasswordHash = passwordHash, DisplayName = displayName });
    }
}
```

- **Completado cuando:** tests de TASK-02-D1 pasan (verde).

### TASK-02-D3 · Test: `SqlRefreshTokenRepository` contra SQL Server real

- **Archivo test:** `tests/ReadingQueue.Infrastructure.Tests/Data/SqlRefreshTokenRepositoryTests.cs`
- **Fixture:** misma `AuthContainerFixture` que D1 (compartida vía `IClassFixture`).
- **Casos:**
  - [ ] `GetByTokenAsync` con token inexistente → retorna `null`.
  - [ ] `CreateAsync` inserta un refresh token sin lanzar excepción.
  - [ ] `GetByTokenAsync` tras `CreateAsync` → retorna el token con `IsRevoked = false` y `UserId` correcto.
  - [ ] `RevokeAsync` cambia `IsRevoked` a `true` en BD.
  - [ ] `GetByTokenAsync` tras `RevokeAsync` → `IsRevoked = true`.
  - [ ] `RevokeAsync` con token inexistente → no lanza excepción (idempotente).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-D4 · Implementar `SqlRefreshTokenRepository` y `RefreshTokenQueries`

**Archivos a crear:**

```
src/ReadingQueue.Infrastructure/
  Data/
    SqlRefreshTokenRepository.cs
  Sql/
    RefreshTokenQueries.cs
```

```csharp
// src/ReadingQueue.Infrastructure/Sql/RefreshTokenQueries.cs
namespace ReadingQueue.Infrastructure.Sql;

internal static class RefreshTokenQueries
{
    internal const string GetByToken = """
        SELECT Id, UserId, Token, ExpiresAt, CreatedAt, IsRevoked
        FROM RefreshTokens WHERE Token = @Token;
        """;

    internal const string Insert = """
        INSERT INTO RefreshTokens (UserId, Token, ExpiresAt)
        VALUES (@UserId, @Token, @ExpiresAt);
        """;

    internal const string Revoke = """
        UPDATE RefreshTokens SET IsRevoked = 1 WHERE Token = @Token;
        """;
}
```

```csharp
// src/ReadingQueue.Infrastructure/Data/SqlRefreshTokenRepository.cs
using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlRefreshTokenRepository(IDbConnectionFactory factory)
        => _factory = factory;

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            RefreshTokenQueries.GetByToken, new { Token = token });
    }

    public async Task CreateAsync(
        int userId, string token, DateTime expiresAt, CancellationToken ct)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(RefreshTokenQueries.Insert,
            new { UserId = userId, Token = token, ExpiresAt = expiresAt });
    }

    public async Task RevokeAsync(string token, CancellationToken ct)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(RefreshTokenQueries.Revoke, new { Token = token });
    }
}
```

- **Completado cuando:** tests de TASK-02-D3 pasan (verde).

---

## Bloque E — Application: Use Cases

> Todos los tests usan Moq para `IUserRepository`, `IRefreshTokenRepository` e `IAuthService`. Sin Testcontainers en este bloque.

### TASK-02-E1 · Test: `RegisterUser`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/RegisterUserTests.cs`
- **Casos:**
  - [ ] Email ya registrado (`GetByEmailAsync` retorna un `User` existente) → lanza `ConflictException` con mensaje que mencione el email.
  - [ ] Email disponible → llama a `HashPassword` una vez con el password recibido.
  - [ ] Email disponible → llama a `CreateAsync` con el hash (no el texto plano).
  - [ ] Email disponible → retorna `Result` con `AccessToken` y `RefreshToken` no vacíos.
  - [ ] Email disponible → retorna `Result` con `UserId` igual al Id creado y `DisplayName` correcto.
  - [ ] Email disponible → llama a `IRefreshTokenRepository.CreateAsync` para persistir el refresh token.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-E2 · Implementar `RegisterUser`

- **Archivo:** `src/ReadingQueue.Application/UseCases/RegisterUser.cs`

```csharp
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class RegisterUser
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public RegisterUser(IUserRepository users,
                        IRefreshTokenRepository tokens,
                        IAuthService auth)
    {
        _users  = users;
        _tokens = tokens;
        _auth   = auth;
    }

    public record Command(string Email, string Password, string DisplayName);
    public record Result(string AccessToken, string RefreshToken,
                         int UserId, string DisplayName);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(cmd.Email, ct);
        if (existing is not null)
            throw new ConflictException("El email ya está registrado.");

        var hash   = _auth.HashPassword(cmd.Password);
        var userId = await _users.CreateAsync(cmd.Email, hash, cmd.DisplayName, ct);

        var user         = new Domain.Entities.User(userId, cmd.Email, hash,
                               cmd.DisplayName, DateTime.UtcNow, true);
        var accessToken  = _auth.GenerateAccessToken(user);
        var refreshToken = _auth.GenerateRefreshToken();

        await _tokens.CreateAsync(userId, refreshToken,
            DateTime.UtcNow.AddDays(7), ct);

        return new Result(accessToken, refreshToken, userId, cmd.DisplayName);
    }
}
```

- **Completado cuando:** tests de TASK-02-E1 pasan (verde).

### TASK-02-E3 · Test: `LoginUser`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/LoginUserTests.cs`
- **Casos:**
  - [ ] Email no existe (`GetByEmailAsync` retorna `null`) → lanza `UnauthorizedException` con mensaje `"Credenciales inválidas."`.
  - [ ] Password incorrecto (`VerifyPassword` retorna `false`) → lanza `UnauthorizedException` con el **mismo** mensaje que el caso anterior (CA-09).
  - [ ] Usuario con `IsActive = false` → lanza `UnauthorizedException` con mensaje `"Cuenta desactivada."`.
  - [ ] Credenciales correctas → retorna `Result` con `AccessToken` y `RefreshToken` no vacíos.
  - [ ] Credenciales correctas → retorna `Result` con `UserId` y `DisplayName` del usuario encontrado.
  - [ ] Credenciales correctas → llama a `IRefreshTokenRepository.CreateAsync` para persistir el nuevo refresh token.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-E4 · Implementar `LoginUser`

- **Archivo:** `src/ReadingQueue.Application/UseCases/LoginUser.cs`

```csharp
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class LoginUser
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public LoginUser(IUserRepository users,
                     IRefreshTokenRepository tokens,
                     IAuthService auth)
    {
        _users  = users;
        _tokens = tokens;
        _auth   = auth;
    }

    public record Command(string Email, string Password);
    public record Result(string AccessToken, string RefreshToken,
                         int UserId, string DisplayName);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(cmd.Email, ct);

        if (user is null || !_auth.VerifyPassword(cmd.Password, user.PasswordHash))
            throw new UnauthorizedException("Credenciales inválidas.");

        if (!user.IsActive)
            throw new UnauthorizedException("Cuenta desactivada.");

        var accessToken  = _auth.GenerateAccessToken(user);
        var refreshToken = _auth.GenerateRefreshToken();

        await _tokens.CreateAsync(user.Id, refreshToken,
            DateTime.UtcNow.AddDays(7), ct);

        return new Result(accessToken, refreshToken, user.Id, user.DisplayName);
    }
}
```

- **Completado cuando:** tests de TASK-02-E3 pasan (verde).

### TASK-02-E5 · Test: `RefreshAccessToken`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/RefreshAccessTokenTests.cs`
- **Casos:**
  - [ ] `GetByTokenAsync` retorna `null` → lanza `UnauthorizedException` con mensaje `"Token de renovación inválido."`.
  - [ ] Token con `IsRevoked = true` → lanza `UnauthorizedException` (CA-12).
  - [ ] Token con `ExpiresAt` pasado → lanza `UnauthorizedException` (CA-13).
  - [ ] Token válido → llama a `RevokeAsync` para revocar el token usado (CA-11).
  - [ ] Token válido → llama a `CreateAsync` para persistir el nuevo refresh token.
  - [ ] Token válido → retorna `Result` con nuevo `AccessToken` y nuevo `RefreshToken` (ambos distintos a los originales).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-E6 · Implementar `RefreshAccessToken`

- **Archivo:** `src/ReadingQueue.Application/UseCases/RefreshAccessToken.cs`

```csharp
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class RefreshAccessToken
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public RefreshAccessToken(IUserRepository users,
                               IRefreshTokenRepository tokens,
                               IAuthService auth)
    {
        _users  = users;
        _tokens = tokens;
        _auth   = auth;
    }

    public record Command(string RefreshToken);
    public record Result(string AccessToken, string NewRefreshToken);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var stored = await _tokens.GetByTokenAsync(cmd.RefreshToken, ct);

        if (stored is null || !stored.IsValid(DateTime.UtcNow))
            throw new UnauthorizedException("Token de renovación inválido.");

        var user = await _users.GetByIdAsync(stored.UserId, ct)
            ?? throw new UnauthorizedException("Token de renovación inválido.");

        await _tokens.RevokeAsync(cmd.RefreshToken, ct);

        var newAccessToken  = _auth.GenerateAccessToken(user);
        var newRefreshToken = _auth.GenerateRefreshToken();

        await _tokens.CreateAsync(user.Id, newRefreshToken,
            DateTime.UtcNow.AddDays(7), ct);

        return new Result(newAccessToken, newRefreshToken);
    }
}
```

- **Completado cuando:** tests de TASK-02-E5 pasan (verde).

### TASK-02-E7 · Test: `LogoutUser`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/LogoutUserTests.cs`
- **Casos:**
  - [ ] Llamar con un token válido → llama a `RevokeAsync` exactamente una vez.
  - [ ] Llamar con un token que no existe en BD (`GetByTokenAsync` retorna `null`) → **no** lanza excepción (idempotente, CA-15).
  - [ ] La operación es `Task` — no retorna datos.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-E8 · Implementar `LogoutUser`

- **Archivo:** `src/ReadingQueue.Application/UseCases/LogoutUser.cs`

```csharp
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class LogoutUser
{
    private readonly IRefreshTokenRepository _tokens;

    public LogoutUser(IRefreshTokenRepository tokens)
        => _tokens = tokens;

    public record Command(string RefreshToken);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct = default)
        => await _tokens.RevokeAsync(cmd.RefreshToken, ct);
}
```

- **Completado cuando:** tests de TASK-02-E7 pasan (verde).

### TASK-02-E9 · Test: `GetCurrentUser`

- **Archivo test:** `tests/ReadingQueue.Application.Tests/UseCases/GetCurrentUserTests.cs`
- **Casos:**
  - [ ] `GetByIdAsync` retorna `null` → lanza `UnauthorizedException`.
  - [ ] `GetByIdAsync` retorna un usuario → retorna `Result` con `Id`, `Email` y `DisplayName` correctos.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-E10 · Implementar `GetCurrentUser`

- **Archivo:** `src/ReadingQueue.Application/UseCases/GetCurrentUser.cs`

```csharp
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetCurrentUser
{
    private readonly IUserRepository _users;

    public GetCurrentUser(IUserRepository users) => _users = users;

    public record Query(int UserId);
    public record Result(int Id, string Email, string DisplayName);

    public async Task<Result> ExecuteAsync(Query query, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(query.UserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.");

        return new Result(user.Id, user.Email, user.DisplayName);
    }
}
```

- **Completado cuando:** tests de TASK-02-E9 pasan (verde).

---

## Bloque F — API: Validadores, Extensions y Modelos

### TASK-02-F1 · Test: validadores de request

- **Archivo test:** `tests/ReadingQueue.Api.Tests/Validators/RegisterRequestValidatorTests.cs`
- **Casos:**
  - [ ] Request con todos los campos válidos → `IsValid` retorna `true`.
  - [ ] `password` de 7 caracteres → `IsValid` retorna `false` (CA-03 parcial).
  - [ ] `password` sin mayúscula → `IsValid` retorna `false` con mensaje que indica el requisito (CA-03).
  - [ ] `password` sin número → `IsValid` retorna `false` con mensaje que indica el requisito (CA-04).
  - [ ] `email` con formato inválido → `IsValid` retorna `false`.
  - [ ] `displayName` vacío → `IsValid` retorna `false`.

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-F2 · Implementar validadores, requests, responses y extensión de `HttpContext`

**Archivos a crear:**

```
src/ReadingQueue.Api/
  Requests/
    RegisterRequest.cs
    LoginRequest.cs
    RefreshRequest.cs
    LogoutRequest.cs
  Responses/
    AuthResponse.cs
    TokenResponse.cs
    UserProfileResponse.cs
  Validators/
    RegisterRequestValidator.cs
    LoginRequestValidator.cs
  Extensions/
    HttpContextExtensions.cs
```

```csharp
// src/ReadingQueue.Api/Requests/RegisterRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record RegisterRequest(string Email, string Password, string DisplayName);

// src/ReadingQueue.Api/Requests/LoginRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record LoginRequest(string Email, string Password);

// src/ReadingQueue.Api/Requests/RefreshRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record RefreshRequest(string RefreshToken);

// src/ReadingQueue.Api/Requests/LogoutRequest.cs
namespace ReadingQueue.Api.Requests;
public sealed record LogoutRequest(string RefreshToken);
```

```csharp
// src/ReadingQueue.Api/Responses/AuthResponse.cs
namespace ReadingQueue.Api.Responses;
public sealed record AuthResponse(
    string AccessToken, string RefreshToken, int UserId, string DisplayName);

// src/ReadingQueue.Api/Responses/TokenResponse.cs
namespace ReadingQueue.Api.Responses;
public sealed record TokenResponse(string AccessToken, string RefreshToken);

// src/ReadingQueue.Api/Responses/UserProfileResponse.cs
namespace ReadingQueue.Api.Responses;
public sealed record UserProfileResponse(int Id, string Email, string DisplayName);
```

```csharp
// src/ReadingQueue.Api/Validators/RegisterRequestValidator.cs
using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class RegisterRequestValidator : AbstractValidator<RegisterRequest>
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
```

```csharp
// src/ReadingQueue.Api/Validators/LoginRequestValidator.cs
using FluentValidation;
using ReadingQueue.Api.Requests;

namespace ReadingQueue.Api.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress();
        RuleFor(x => x.Password).NotEmpty();
    }
}
```

```csharp
// src/ReadingQueue.Api/Extensions/HttpContextExtensions.cs
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ReadingQueue.Domain.Exceptions;

namespace ReadingQueue.Api.Extensions;

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

- **Completado cuando:** tests de TASK-02-F1 pasan (verde).

### TASK-02-F3 · Test: `HttpContextExtensions.GetUserId()`

- **Archivo test:** `tests/ReadingQueue.Api.Tests/Extensions/HttpContextExtensionsTests.cs`
- **Casos:**
  - [ ] `HttpContext` con claim `sub = "42"` → `GetUserId()` retorna `42` (CA-19).
  - [ ] `HttpContext` con claim `NameIdentifier = "99"` (fallback) → `GetUserId()` retorna `99`.
  - [ ] `HttpContext` sin ningún claim de identidad → `GetUserId()` lanza `UnauthorizedException` (CA-20).
  - [ ] `HttpContext` con claim `sub = "no-es-int"` → `GetUserId()` lanza `UnauthorizedException`.

- **Nota:** Construir un `DefaultHttpContext` con `ClaimsPrincipal` configurado manualmente, sin necesidad de `WebApplicationFactory`.
- **Completado cuando:** tests compilan y pasan (verde) — la implementación ya existe de F2.

---

## Bloque G — API: AuthEndpoints y Program.cs

### TASK-02-G1 · Test de integración: `AuthEndpoints`

- **Archivo test:** `tests/ReadingQueue.Api.Tests/Endpoints/AuthEndpointsTests.cs`
- **Tecnología:** `WebApplicationFactory<Program>` con base de datos SQL Server real (Testcontainers). A diferencia del HealthEndpoints test, aquí se usa un contenedor real porque los endpoints persisten datos.

- **Fixture:** `IClassFixture<AuthEndpointsFixture>` que:
  - Inicia un contenedor Testcontainers SQL Server.
  - Construye la `WebApplicationFactory` sobreescribiendo `ConnectionStrings:DefaultConnection`.
  - Configura `Jwt:SecretKey` con un valor de test fijo.
  - Expone un `HttpClient` pre-configurado.

- **Helpers de fixture:**
  - `RegisterAndLoginAsync(email, password, displayName)` → retorna `AuthResponse` (reutilizable entre tests).
  - `GenerateExpiredToken(userId, email, displayName)` → genera un JWT con `exp` en el pasado usando el mismo secret key de test.

- **Casos — Register (CA-01, CA-02, CA-03, CA-04, CA-05):**
  - [ ] `POST /api/auth/register` con datos válidos → `201 Created` con `accessToken` y `refreshToken` no vacíos (CA-01).
  - [ ] `POST /api/auth/register` con mismo email dos veces → `409 Conflict` (CA-02).
  - [ ] `POST /api/auth/register` con password sin mayúscula → `422 Unprocessable Entity` con campo `errors.password` (CA-03).
  - [ ] `POST /api/auth/register` con password sin número → `422` con mensaje descriptivo (CA-04).
  - [ ] Tras registro, query directa a BD verifica que `PasswordHash` no es igual al password original (CA-05).

- **Casos — Login (CA-06, CA-07, CA-08, CA-09):**
  - [ ] `POST /api/auth/login` con credenciales correctas → `200 OK` con tokens (CA-06).
  - [ ] `POST /api/auth/login` con email inexistente → `401` con `{ "error": "Credenciales inválidas." }` (CA-07).
  - [ ] `POST /api/auth/login` con password incorrecto → `401` con el **mismo** mensaje que CA-07 (CA-08, CA-09).

- **Casos — Refresh (CA-10, CA-11, CA-12, CA-13):**
  - [ ] `POST /api/auth/refresh` con token válido → `200 OK` con nuevos `accessToken` y `refreshToken` (CA-10).
  - [ ] Tras `POST /api/auth/refresh`, el token original está revocado en BD (CA-11).
  - [ ] `POST /api/auth/refresh` con el token ya revocado → `401` (CA-12).
  - [ ] `POST /api/auth/refresh` con token expirado (generado con `exp` pasado) → `401` (CA-13).

- **Casos — Logout (CA-14, CA-15):**
  - [ ] `POST /api/auth/logout` con token válido → `200 OK` y token revocado en BD (CA-14).
  - [ ] `POST /api/auth/logout` con token ya revocado → `200 OK` igualmente (CA-15).

- **Casos — Me (CA-16):**
  - [ ] `GET /api/auth/me` con JWT válido → `200 OK` con `id`, `email` y `displayName` correctos (CA-16).
  - [ ] `GET /api/auth/me` sin token → `401` (CA-17 variante).

- **Casos — Protección global (CA-17, CA-18):**
  - [ ] `GET /api/books` sin token → `401` (CA-17). *(El endpoint no existe aún; basta con que el middleware rechace la petición — no debe ser `404`). Nota: ajustar si el middleware retorna 404 antes que 401 cuando la ruta no existe; en ese caso usar un endpoint protegido que sí exista, como `GET /api/auth/me`.*
  - [ ] `GET /api/auth/me` con JWT expirado → `401` (CA-18).

- **Completado cuando:** tests compilan y fallan (rojo).

### TASK-02-G2 · Implementar `AuthEndpoints` y actualizar `Program.cs`

**Archivos a crear / actualizar:**

```
src/ReadingQueue.Api/
  Endpoints/
    AuthEndpoints.cs       ← nuevo
  Program.cs               ← actualizar
  appsettings.json         ← actualizar (agregar sección Jwt)
  appsettings.Development.json ← nuevo
```

```csharp
// src/ReadingQueue.Api/Endpoints/AuthEndpoints.cs
using FluentValidation;
using ReadingQueue.Api.Extensions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;
using ReadingQueue.Api.Validators;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Exceptions;

namespace ReadingQueue.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register).AllowAnonymous();
        group.MapPost("/login",    Login).AllowAnonymous();
        group.MapPost("/refresh",  Refresh).AllowAnonymous();
        group.MapPost("/logout",   Logout).RequireAuthorization();
        group.MapGet ("/me",       Me).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest req,
        RegisterUser useCase,
        RegisterRequestValidator validator)
    {
        var validation = await validator.ValidateAsync(req);
        if (!validation.IsValid)
            return Results.UnprocessableEntity(new
            {
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName.ToLower())
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });

        var result = await useCase.ExecuteAsync(
            new RegisterUser.Command(req.Email, req.Password, req.DisplayName));

        return Results.Created("/api/auth/me",
            new AuthResponse(result.AccessToken, result.RefreshToken,
                             result.UserId, result.DisplayName));
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        LoginUser useCase)
    {
        var result = await useCase.ExecuteAsync(
            new LoginUser.Command(req.Email, req.Password));

        return Results.Ok(new AuthResponse(result.AccessToken, result.RefreshToken,
                                           result.UserId, result.DisplayName));
    }

    private static async Task<IResult> Refresh(
        RefreshRequest req,
        RefreshAccessToken useCase)
    {
        var result = await useCase.ExecuteAsync(
            new RefreshAccessToken.Command(req.RefreshToken));

        return Results.Ok(new TokenResponse(result.AccessToken, result.NewRefreshToken));
    }

    private static async Task<IResult> Logout(
        LogoutRequest req,
        LogoutUser useCase)
    {
        await useCase.ExecuteAsync(new LogoutUser.Command(req.RefreshToken));
        return Results.Ok(new { message = "Sesión cerrada correctamente." });
    }

    private static async Task<IResult> Me(
        HttpContext ctx,
        GetCurrentUser useCase)
    {
        var userId = ctx.GetUserId();
        var result = await useCase.ExecuteAsync(new GetCurrentUser.Query(userId));
        return Results.Ok(new UserProfileResponse(result.Id, result.Email, result.DisplayName));
    }
}
```

**Actualizar `Program.cs`** — agregar JWT auth, autorización global, DI de use cases y manejo de excepciones:

```csharp
// src/ReadingQueue.Api/Program.cs (completo)
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using ReadingQueue.Api.Endpoints;
using ReadingQueue.Api.Validators;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Infrastructure.Auth;
using ReadingQueue.Infrastructure.Data;
using ReadingQueue.Infrastructure.Migrations;
using ReadingQueue.Domain.Interfaces;

var builder = WebApplication.CreateBuilder(args);

// ── JWT options ──────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// ── Authentication JWT Bearer ────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
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
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── Infraestructura ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAuthService,            JwtService>();
builder.Services.AddScoped<IUserRepository,         SqlUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, SqlRefreshTokenRepository>();

// ── Use cases ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<RegisterUser>();
builder.Services.AddScoped<LoginUser>();
builder.Services.AddScoped<RefreshAccessToken>();
builder.Services.AddScoped<LogoutUser>();
builder.Services.AddScoped<GetCurrentUser>();

// ── Validadores ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<RegisterRequestValidator>();
builder.Services.AddScoped<LoginRequestValidator>();

builder.Services.AddOpenApi();

var app = builder.Build();

// ── Migraciones ──────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
    MigrationRunner.Run(connectionString);

// ── Middleware de excepciones ─────────────────────────────────────────────────
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, body) = ex switch
    {
        UnauthorizedException e => (401, (object)new { error = e.Message }),
        ConflictException     e => (409, (object)new { error = e.Message }),
        _                       => (500, (object)new { error = "Error interno del servidor." })
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(body);
}));

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
HealthEndpoints.Map(app);
AuthEndpoints.Map(app);

app.Run();

public partial class Program { }
```

**Actualizar `appsettings.json`** — agregar sección Jwt con valores por defecto:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  "Jwt": {
    "SecretKey": "",
    "Issuer": "readingqueue-api",
    "Audience": "readingqueue-client",
    "AccessTokenMinutes": 15,
    "RefreshTokenDays": 7
  }
}
```

**Crear `appsettings.Development.json`** — secret key solo para entorno de desarrollo local:

```json
{
  "Jwt": {
    "SecretKey": "development-only-secret-min-32-chars-not-for-prod"
  }
}
```

- **Completado cuando:** tests de TASK-02-G1 pasan (verde).

---

## Bloque H — Verificación Final

### TASK-02-H1 · Build .NET sin errores ni warnings

```powershell
dotnet build ReadingQueue.sln
```

- **Criterio:** `0 Error(s)  0 Warning(s)`.

### TASK-02-H2 · Tests de Domain y Application pasan (verde)

```powershell
dotnet test tests/ReadingQueue.Domain.Tests/ReadingQueue.Domain.Tests.csproj --no-build -v normal
dotnet test tests/ReadingQueue.Application.Tests/ReadingQueue.Application.Tests.csproj --no-build -v normal
```

- **Criterio:** todos los tests unitarios pasan. Cobertura ≥ 80% en Domain y Application.

### TASK-02-H3 · Tests de Infrastructure pasan (verde, incluyendo nuevos)

```powershell
dotnet test tests/ReadingQueue.Infrastructure.Tests/ReadingQueue.Infrastructure.Tests.csproj --no-build -v normal
```

- **Criterio:** tests de `JwtServiceTests`, `SqlUserRepositoryTests` y `SqlRefreshTokenRepositoryTests` pasan junto con los tests de Spec-01.

### TASK-02-H4 · Tests de API pasan (verde, incluyendo nuevos)

```powershell
dotnet test tests/ReadingQueue.Api.Tests/ReadingQueue.Api.Tests.csproj --no-build -v normal
```

- **Criterio:** tests de `AuthEndpointsTests`, `RegisterRequestValidatorTests` y `HttpContextExtensionsTests` pasan junto con `HealthEndpointsTests` de Spec-01.

---

## Resumen de archivos que genera SPEC-02

| # | Archivo | Bloque |
|---|---|---|
| 1 | `src/ReadingQueue.Domain/Entities/User.cs` | B |
| 2 | `src/ReadingQueue.Domain/Entities/RefreshToken.cs` | B |
| 3 | `src/ReadingQueue.Domain/Exceptions/UnauthorizedException.cs` | B |
| 4 | `src/ReadingQueue.Domain/Exceptions/ConflictException.cs` | B |
| 5 | `src/ReadingQueue.Domain/Interfaces/IUserRepository.cs` | B |
| 6 | `src/ReadingQueue.Domain/Interfaces/IRefreshTokenRepository.cs` | B |
| 7 | `src/ReadingQueue.Domain/Interfaces/IAuthService.cs` | B |
| 8 | `src/ReadingQueue.Infrastructure/Auth/JwtOptions.cs` | C |
| 9 | `src/ReadingQueue.Infrastructure/Auth/JwtService.cs` | C |
| 10 | `src/ReadingQueue.Infrastructure/Data/SqlUserRepository.cs` | D |
| 11 | `src/ReadingQueue.Infrastructure/Sql/UserQueries.cs` | D |
| 12 | `src/ReadingQueue.Infrastructure/Data/SqlRefreshTokenRepository.cs` | D |
| 13 | `src/ReadingQueue.Infrastructure/Sql/RefreshTokenQueries.cs` | D |
| 14 | `src/ReadingQueue.Application/UseCases/RegisterUser.cs` | E |
| 15 | `src/ReadingQueue.Application/UseCases/LoginUser.cs` | E |
| 16 | `src/ReadingQueue.Application/UseCases/RefreshAccessToken.cs` | E |
| 17 | `src/ReadingQueue.Application/UseCases/LogoutUser.cs` | E |
| 18 | `src/ReadingQueue.Application/UseCases/GetCurrentUser.cs` | E |
| 19 | `src/ReadingQueue.Api/Requests/RegisterRequest.cs` | F |
| 20 | `src/ReadingQueue.Api/Requests/LoginRequest.cs` | F |
| 21 | `src/ReadingQueue.Api/Requests/RefreshRequest.cs` | F |
| 22 | `src/ReadingQueue.Api/Requests/LogoutRequest.cs` | F |
| 23 | `src/ReadingQueue.Api/Responses/AuthResponse.cs` | F |
| 24 | `src/ReadingQueue.Api/Responses/TokenResponse.cs` | F |
| 25 | `src/ReadingQueue.Api/Responses/UserProfileResponse.cs` | F |
| 26 | `src/ReadingQueue.Api/Validators/RegisterRequestValidator.cs` | F |
| 27 | `src/ReadingQueue.Api/Validators/LoginRequestValidator.cs` | F |
| 28 | `src/ReadingQueue.Api/Extensions/HttpContextExtensions.cs` | F |
| 29 | `src/ReadingQueue.Api/Endpoints/AuthEndpoints.cs` | G |
| 30 | `src/ReadingQueue.Api/Program.cs` (actualizado) | G |
| 31 | `src/ReadingQueue.Api/appsettings.json` (actualizado) | G |
| 32 | `src/ReadingQueue.Api/appsettings.Development.json` | G |
| 33 | `tests/ReadingQueue.Domain.Tests/UserEntityTests.cs` | B |
| 34 | `tests/ReadingQueue.Domain.Tests/RefreshTokenEntityTests.cs` | B |
| 35 | `tests/ReadingQueue.Infrastructure.Tests/Auth/JwtServiceTests.cs` | C |
| 36 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlUserRepositoryTests.cs` | D |
| 37 | `tests/ReadingQueue.Infrastructure.Tests/Data/SqlRefreshTokenRepositoryTests.cs` | D |
| 38 | `tests/ReadingQueue.Application.Tests/UseCases/RegisterUserTests.cs` | E |
| 39 | `tests/ReadingQueue.Application.Tests/UseCases/LoginUserTests.cs` | E |
| 40 | `tests/ReadingQueue.Application.Tests/UseCases/RefreshAccessTokenTests.cs` | E |
| 41 | `tests/ReadingQueue.Application.Tests/UseCases/LogoutUserTests.cs` | E |
| 42 | `tests/ReadingQueue.Application.Tests/UseCases/GetCurrentUserTests.cs` | E |
| 43 | `tests/ReadingQueue.Api.Tests/Validators/RegisterRequestValidatorTests.cs` | F |
| 44 | `tests/ReadingQueue.Api.Tests/Extensions/HttpContextExtensionsTests.cs` | F |
| 45 | `tests/ReadingQueue.Api.Tests/Endpoints/AuthEndpointsTests.cs` | G |

---

## Checklist SPEC-02

### Bloque A — Paquetes NuGet
- [x] TASK-02-A1 · Paquetes y referencias de proyectos de test agregados

### Bloque B — Domain
- [x] TASK-02-B1 · Tests de entidades `User` y `RefreshToken` (rojo)
- [x] TASK-02-B2 · Impl entidades + excepciones (verde)
- [x] TASK-02-B3 · Interfaces de dominio creadas

### Bloque C — JwtService
- [x] TASK-02-C1 · Tests de `JwtService` — hash, tokens, claims (rojo)
- [x] TASK-02-C2 · Impl `JwtService` + `JwtOptions` (verde)

### Bloque D — Repositorios SQL
- [x] TASK-02-D1 · Tests de `SqlUserRepository` con Testcontainers (rojo)
- [x] TASK-02-D2 · Impl `SqlUserRepository` + `UserQueries` (verde)
- [x] TASK-02-D3 · Tests de `SqlRefreshTokenRepository` con Testcontainers (rojo)
- [x] TASK-02-D4 · Impl `SqlRefreshTokenRepository` + `RefreshTokenQueries` (verde)

### Bloque E — Use Cases
- [x] TASK-02-E1 · Tests de `RegisterUser` (rojo)
- [x] TASK-02-E2 · Impl `RegisterUser` (verde)
- [x] TASK-02-E3 · Tests de `LoginUser` (rojo)
- [x] TASK-02-E4 · Impl `LoginUser` (verde)
- [x] TASK-02-E5 · Tests de `RefreshAccessToken` (rojo)
- [x] TASK-02-E6 · Impl `RefreshAccessToken` (verde)
- [x] TASK-02-E7 · Tests de `LogoutUser` (rojo)
- [x] TASK-02-E8 · Impl `LogoutUser` (verde)
- [x] TASK-02-E9 · Tests de `GetCurrentUser` (rojo)
- [x] TASK-02-E10 · Impl `GetCurrentUser` (verde)

### Bloque F — API: Validators, Extensions, Modelos
- [x] TASK-02-F1 · Tests de `RegisterRequestValidator` (rojo)
- [x] TASK-02-F2 · Impl validators + requests + responses + `HttpContextExtensions` (verde)
- [x] TASK-02-F3 · Tests de `HttpContextExtensions.GetUserId()` (verde — impl ya existe)

### Bloque G — AuthEndpoints + Program.cs
- [x] TASK-02-G1 · Tests de integración `AuthEndpoints` con Testcontainers (rojo)
- [x] TASK-02-G2 · Impl `AuthEndpoints` + `Program.cs` actualizado (verde)

### Bloque H — Verificación Final
- [x] TASK-02-H1 · `dotnet build` → 0 errores, 0 warnings
- [x] TASK-02-H2 · Tests de Domain y Application pasan (verde)
- [x] TASK-02-H3 · Tests de Infrastructure pasan (verde, incluye nuevos)
- [x] TASK-02-H4 · Tests de Api pasan (verde, incluye nuevos)
