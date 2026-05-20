using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Testcontainers.MsSql;

namespace ReadingQueue.Api.Tests.Endpoints;

public class AuthEndpointsFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public string ConnectionString { get; private set; } = null!;
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public const string JwtSecret   = "test-secret-key-for-auth-integration-tests-32c";
    public const string JwtIssuer   = "readingqueue-api";
    public const string JwtAudience = "readingqueue-client";

    public AuthEndpointsFixture()
    {
        TestcontainersSettings.ResourceReaperEnabled = false;

#pragma warning disable CS0618
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
#pragma warning restore CS0618
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b => b.ConfigureAppConfiguration((_, cfg) =>
                cfg.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:DefaultConnection"] = ConnectionString,
                    ["Jwt:SecretKey"]           = JwtSecret,
                    ["Jwt:Issuer"]              = JwtIssuer,
                    ["Jwt:Audience"]            = JwtAudience,
                    ["Jwt:AccessTokenMinutes"]  = "15",
                    ["Jwt:RefreshTokenDays"]    = "7",
                })));
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    public string GenerateExpiredToken(int userId, string email, string displayName)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub,   userId.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, email),
            new Claim(JwtRegisteredClaimNames.Name,  displayName),
            new Claim(JwtRegisteredClaimNames.Jti,   Guid.NewGuid().ToString()),
        };

        var key   = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(JwtSecret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer:             JwtIssuer,
            audience:           JwtAudience,
            claims:             claims,
            notBefore:          DateTime.UtcNow.AddHours(-2),
            expires:            DateTime.UtcNow.AddSeconds(-1),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
