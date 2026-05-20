using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadingQueue.Api.Responses;

namespace ReadingQueue.Api.Tests.Endpoints;

public class AuthEndpointsTests : IClassFixture<AuthEndpointsFixture>
{
    private readonly AuthEndpointsFixture _fixture;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public AuthEndpointsTests(AuthEndpointsFixture fixture) => _fixture = fixture;

    // ── Helpers ─────────────────────────────────────────────────────────��─────

    private HttpClient NewClient() => _fixture.CreateClient();

    private HttpClient AuthClient(string token)
    {
        var c = NewClient();
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return c;
    }

    private static string Email() => $"{Guid.NewGuid():N}@test.com";

    private async Task<(HttpResponseMessage response, AuthResponse? body)> RegisterAsync(
        string email, string password = "Password1", string displayName = "Test User")
    {
        var resp = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { email, password, displayName });
        AuthResponse? body = null;
        if (resp.IsSuccessStatusCode)
            body = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return (resp, body);
    }

    private async Task<(HttpResponseMessage response, AuthResponse? body)> LoginAsync(
        string email, string password = "Password1")
    {
        var resp = await NewClient().PostAsJsonAsync("/api/auth/login",
            new { email, password });
        AuthResponse? body = null;
        if (resp.IsSuccessStatusCode)
            body = await resp.Content.ReadFromJsonAsync<AuthResponse>(JsonOpts);
        return (resp, body);
    }

    private async Task<string?> QueryScalarAsync(string sql, object? param = null)
    {
        using var conn = new SqlConnection(_fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<string>(sql, param);
    }

    // ── Register ─────────���────────────────────────────────────────────────────

    [Fact]
    public async Task Register_ValidRequest_Returns201WithTokens()
    {
        var (resp, body) = await RegisterAsync(Email());

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Register_DuplicateEmail_Returns409()
    {
        var email = Email();
        await RegisterAsync(email);

        var (resp, _) = await RegisterAsync(email);

        resp.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_PasswordNoUppercase_Returns422WithPasswordErrors()
    {
        var resp = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { email = Email(), password = "password1", displayName = "User" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        json.GetProperty("errors").GetProperty("password").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Register_PasswordNoNumber_Returns422WithPasswordErrors()
    {
        var resp = await NewClient().PostAsJsonAsync("/api/auth/register",
            new { email = Email(), password = "PasswordOnly", displayName = "User" });

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        json.GetProperty("errors").GetProperty("password").GetArrayLength().Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Register_PasswordNotStoredAsPlainText()
    {
        var email    = Email();
        var password = "Password1";
        await RegisterAsync(email, password);

        var hash = await QueryScalarAsync(
            "SELECT PasswordHash FROM Users WHERE Email = @Email", new { Email = email });

        hash.Should().NotBeNull();
        hash.Should().NotBe(password);
        BCrypt.Net.BCrypt.Verify(password, hash).Should().BeTrue();
    }

    // ── Login ────────────────────────────��─────────────────────────────────��──

    [Fact]
    public async Task Login_ValidCredentials_Returns200WithTokens()
    {
        var email = Email();
        await RegisterAsync(email);

        var (resp, body) = await LoginAsync(email);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Login_UnknownEmail_Returns401WithGenericMessage()
    {
        var resp = await NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = "noexist@test.com", password = "Password1" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        json.GetProperty("error").GetString().Should().Be("Credenciales inválidas.");
    }

    [Fact]
    public async Task Login_WrongPassword_Returns401WithSameGenericMessage()
    {
        var email = Email();
        await RegisterAsync(email);

        var resp = await NewClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPassword1" });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        json.GetProperty("error").GetString().Should().Be("Credenciales inválidas.");
    }

    [Fact]
    public async Task Login_UnknownEmailAndWrongPassword_SameErrorMessage()
    {
        var email = Email();
        await RegisterAsync(email);

        var resp1 = await NewClient().PostAsJsonAsync("/api/auth/login",
            new { email = "ghost@test.com", password = "Password1" });
        var resp2 = await NewClient().PostAsJsonAsync("/api/auth/login",
            new { email, password = "WrongPassword1" });

        var msg1 = (await resp1.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
                   .GetProperty("error").GetString();
        var msg2 = (await resp2.Content.ReadFromJsonAsync<JsonElement>(JsonOpts))
                   .GetProperty("error").GetString();

        msg1.Should().Be(msg2);
    }

    // ── Refresh ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task Refresh_ValidToken_Returns200WithNewTokens()
    {
        var email   = Email();
        var (_, reg) = await RegisterAsync(email);

        var resp = await NewClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = reg!.RefreshToken });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<TokenResponse>(JsonOpts);
        body!.AccessToken.Should().NotBeNullOrEmpty();
        body.RefreshToken.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task Refresh_OldTokenIsRevokedInDb()
    {
        var (_, reg) = await RegisterAsync(Email());
        var oldToken = reg!.RefreshToken;

        await NewClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = oldToken });

        var isRevoked = await QueryScalarAsync(
            "SELECT CAST(IsRevoked AS NVARCHAR) FROM RefreshTokens WHERE Token = @Token",
            new { Token = oldToken });

        isRevoked.Should().Be("1");
    }

    [Fact]
    public async Task Refresh_AlreadyRevokedToken_Returns401()
    {
        var (_, reg) = await RegisterAsync(Email());
        await NewClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = reg!.RefreshToken });

        var resp = await NewClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = reg.RefreshToken });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_ExpiredToken_Returns401()
    {
        var (_, reg) = await RegisterAsync(Email());

        // Revocamos el de BD e insertamos uno expirado directamente
        using var conn = new SqlConnection(_fixture.ConnectionString);
        var userId = await conn.ExecuteScalarAsync<int>(
            "SELECT UserId FROM RefreshTokens WHERE Token = @Token",
            new { Token = reg!.RefreshToken });
        var expiredToken = Guid.NewGuid().ToString();
        await conn.ExecuteAsync(
            "INSERT INTO RefreshTokens (UserId, Token, ExpiresAt) VALUES (@UserId, @Token, @ExpiresAt)",
            new { UserId = userId, Token = expiredToken, ExpiresAt = DateTime.UtcNow.AddSeconds(-1) });

        var resp = await NewClient().PostAsJsonAsync("/api/auth/refresh",
            new { refreshToken = expiredToken });

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Logout ────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Logout_ValidToken_Returns200AndRevokesInDb()
    {
        var email      = Email();
        var (_, reg)   = await RegisterAsync(email);
        var (_, login) = await LoginAsync(email);
        var token      = login!.RefreshToken;

        var resp = await AuthClient(login.AccessToken)
            .PostAsJsonAsync("/api/auth/logout", new { refreshToken = token });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);

        var isRevoked = await QueryScalarAsync(
            "SELECT CAST(IsRevoked AS NVARCHAR) FROM RefreshTokens WHERE Token = @Token",
            new { Token = token });
        isRevoked.Should().Be("1");
    }

    [Fact]
    public async Task Logout_AlreadyRevokedToken_Returns200()
    {
        var (_, reg) = await RegisterAsync(Email());
        var client   = AuthClient(reg!.AccessToken);

        await client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = reg.RefreshToken });
        var resp = await client.PostAsJsonAsync("/api/auth/logout",
            new { refreshToken = reg.RefreshToken });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── Me ────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Me_ValidToken_Returns200WithProfile()
    {
        var email      = Email();
        var (_, reg)   = await RegisterAsync(email, displayName: "My Name");

        var resp = await AuthClient(reg!.AccessToken).GetAsync("/api/auth/me");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await resp.Content.ReadFromJsonAsync<UserProfileResponse>(JsonOpts);
        body!.Email.Should().Be(email);
        body.DisplayName.Should().Be("My Name");
        body.Id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task Me_NoToken_Returns401()
    {
        var resp = await NewClient().GetAsync("/api/auth/me");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ── Global auth protection ──────────────────────────────���─────────────────

    [Fact]
    public async Task ProtectedEndpoint_ExpiredToken_Returns401()
    {
        var (_, reg) = await RegisterAsync(Email());
        var expired  = _fixture.GenerateExpiredToken(reg!.UserId, Email(), "User");

        var resp = await AuthClient(expired).GetAsync("/api/auth/me");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
