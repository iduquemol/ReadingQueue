using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using DotNet.Testcontainers.Configurations;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;
using Testcontainers.MsSql;

namespace ReadingQueue.Api.Tests.Endpoints;

public class QueueEndpointsFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public string                         ConnectionString { get; private set; } = null!;
    public WebApplicationFactory<Program> Factory          { get; private set; } = null!;

    private const string JwtSecret   = "test-secret-key-for-queue-integration-tests-32c";
    private const string JwtIssuer   = "readingqueue-api";
    private const string JwtAudience = "readingqueue-client";

    public static readonly JsonSerializerOptions JsonOpts =
        new() { PropertyNameCaseInsensitive = true };

    public QueueEndpointsFixture()
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
                    ["Jwt:SecretKey"]          = JwtSecret,
                    ["Jwt:Issuer"]             = JwtIssuer,
                    ["Jwt:Audience"]           = JwtAudience,
                    ["Jwt:AccessTokenMinutes"] = "15",
                    ["Jwt:RefreshTokenDays"]   = "7",
                    ["Claude:ApiKey"]          = "test-key",
                    ["Claude:BaseUrl"]         = "http://127.0.0.1:65535",
                    ["Claude:MaxRetries"]      = "0",
                    ["Claude:TimeoutSeconds"]  = "3",
                })));
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await _container.DisposeAsync();
    }

    public HttpClient CreateClient() => Factory.CreateClient();

    public HttpClient AuthClient(string token)
    {
        var c = CreateClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    public async Task<(HttpClient Client, AuthResponse Auth)> RegisterAndLoginAsync(
        string? email = null, string password = "Password1", string displayName = "Test User")
    {
        email ??= $"{Guid.NewGuid():N}@test.com";
        var resp = await CreateClient().PostAsJsonAsync("/api/auth/register",
            new { email, password, displayName });
        resp.EnsureSuccessStatusCode();
        var auth = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts)
            ?? throw new InvalidOperationException("Register returned null body");
        return (AuthClient(auth.AccessToken), auth);
    }

    public async Task<BookResponse> CreateBookAsync(HttpClient client,
        CreateBookRequest? req = null)
    {
        req ??= DefaultBook();
        var resp = await client.PostAsJsonAsync("/api/books", req);
        resp.EnsureSuccessStatusCode();
        return await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts)
               ?? throw new InvalidOperationException("CreateBook returned null");
    }

    public async Task CreateManyBooksAsync(HttpClient client, int count,
        string mentalEnergy = "Baja - cualquier momento",
        string genre = "Clasico",
        string rotationCategory = "Clasico")
    {
        for (var i = 0; i < count; i++)
            await CreateBookAsync(client, new CreateBookRequest(
                Title:            $"Libro {Guid.NewGuid():N}",
                Author:           "Autor Test",
                Genre:            genre,
                Country:          "Colombia",
                WhyRead:          null,
                Priority:         3,
                MentalEnergy:     mentalEnergy,
                RecommendedMood:  "Solemne / quiero leer algo grande",
                RotationCategory: rotationCategory,
                Notes:            null));
    }

    public static CreateBookRequest DefaultBook(
        string genre = "Clasico",
        string mentalEnergy = "Baja - cualquier momento",
        string rotationCategory = "Clasico",
        int priority = 3) => new(
        Title:            $"Libro {Guid.NewGuid():N}",
        Author:           "Autor Test",
        Genre:            genre,
        Country:          "Colombia",
        WhyRead:          null,
        Priority:         priority,
        MentalEnergy:     mentalEnergy,
        RecommendedMood:  "Solemne / quiero leer algo grande",
        RotationCategory: rotationCategory,
        Notes:            null);

    public async Task<T?> QueryScalarAsync<T>(string sql, object? param = null)
    {
        using var conn = new SqlConnection(ConnectionString);
        return await conn.ExecuteScalarAsync<T>(sql, param);
    }

    public async Task ExecuteSqlAsync(string sql, object? param = null)
    {
        using var conn = new SqlConnection(ConnectionString);
        await conn.ExecuteAsync(sql, param);
    }
}
