using System.IdentityModel.Tokens.Jwt;
using FluentAssertions;
using Microsoft.Extensions.Options;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Infrastructure.Auth;

namespace ReadingQueue.Infrastructure.Tests.Auth;

public class JwtServiceTests
{
    private readonly JwtService _sut;
    private readonly JwtOptions _options;

    public JwtServiceTests()
    {
        _options = new JwtOptions
        {
            SecretKey           = "test-secret-key-that-is-long-enough-32chars",
            Issuer              = "test-issuer",
            Audience            = "test-audience",
            AccessTokenMinutes  = 15,
            RefreshTokenDays    = 7
        };
        _sut = new JwtService(Options.Create(_options));
    }

    // ── HashPassword ──────────────────────────────────────────────────────────

    [Fact]
    public void HashPassword_ReturnsNonNullString()
    {
        var hash = _sut.HashPassword("MyPassword1");

        hash.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void HashPassword_DoesNotReturnPlainText()
    {
        var hash = _sut.HashPassword("MyPassword1");

        hash.Should().NotBe("MyPassword1");
    }

    [Fact]
    public void HashPassword_TwoCalls_ProduceDifferentHashes()
    {
        var hash1 = _sut.HashPassword("MyPassword1");
        var hash2 = _sut.HashPassword("MyPassword1");

        hash1.Should().NotBe(hash2);
    }

    // ── VerifyPassword ────────────────────────────────────────────────────────

    [Fact]
    public void VerifyPassword_CorrectPassword_ReturnsTrue()
    {
        var hash = _sut.HashPassword("MyPassword1");

        _sut.VerifyPassword("MyPassword1", hash).Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WrongPassword_ReturnsFalse()
    {
        var hash = _sut.HashPassword("MyPassword1");

        _sut.VerifyPassword("WrongPassword", hash).Should().BeFalse();
    }

    // ── GenerateAccessToken — claims (CA-21) ──────────────────────────────────

    private static User MakeUser()
        => new(42, "user@example.com", "hash", "Juan García", DateTime.UtcNow, true);

    private JwtSecurityToken DecodeToken(string raw)
        => new JwtSecurityTokenHandler().ReadJwtToken(raw);

    [Fact]
    public void GenerateAccessToken_ContainsSub_EqualToUserId()
    {
        var user  = MakeUser();
        var token = DecodeToken(_sut.GenerateAccessToken(user));

        token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub)
             .Value.Should().Be(user.Id.ToString());
    }

    [Fact]
    public void GenerateAccessToken_ContainsEmail()
    {
        var user  = MakeUser();
        var token = DecodeToken(_sut.GenerateAccessToken(user));

        token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Email)
             .Value.Should().Be(user.Email);
    }

    [Fact]
    public void GenerateAccessToken_ContainsName()
    {
        var user  = MakeUser();
        var token = DecodeToken(_sut.GenerateAccessToken(user));

        token.Claims.First(c => c.Type == JwtRegisteredClaimNames.Name)
             .Value.Should().Be(user.DisplayName);
    }

    [Fact]
    public void GenerateAccessToken_ContainsJti()
    {
        var user  = MakeUser();
        var token = DecodeToken(_sut.GenerateAccessToken(user));

        var jti = token.Claims.FirstOrDefault(c => c.Type == JwtRegisteredClaimNames.Jti);
        jti.Should().NotBeNull();
        jti!.Value.Should().NotBeNullOrEmpty();
        Guid.TryParse(jti.Value, out _).Should().BeTrue();
    }

    [Fact]
    public void GenerateAccessToken_DoesNotContainExtraClaims()
    {
        var user  = MakeUser();
        var token = DecodeToken(_sut.GenerateAccessToken(user));

        var allowed = new HashSet<string>
        {
            JwtRegisteredClaimNames.Sub,
            JwtRegisteredClaimNames.Email,
            JwtRegisteredClaimNames.Name,
            JwtRegisteredClaimNames.Jti,
            JwtRegisteredClaimNames.Iat,
            JwtRegisteredClaimNames.Exp,
            JwtRegisteredClaimNames.Nbf,
            JwtRegisteredClaimNames.Iss,
            JwtRegisteredClaimNames.Aud,
        };

        var extras = token.Claims.Select(c => c.Type).Except(allowed).ToList();
        extras.Should().BeEmpty(because: "el JWT solo debe tener los claims definidos en spec-02 §5");
    }

    // ── GenerateAccessToken — expiración (CA-22) ──────────────────────────────

    [Fact]
    public void GenerateAccessToken_ExpiresInConfiguredMinutes()
    {
        // JWT trunca exp a segundos — comparar sin sub-segundos
        var before = DateTime.UtcNow.TruncateToSeconds();
        var user   = MakeUser();
        var token  = DecodeToken(_sut.GenerateAccessToken(user));
        var after  = DateTime.UtcNow.TruncateToSeconds();

        var expectedMin = before.AddMinutes(_options.AccessTokenMinutes);
        var expectedMax = after.AddMinutes(_options.AccessTokenMinutes);

        token.ValidTo.Should().BeOnOrAfter(expectedMin)
             .And.BeOnOrBefore(expectedMax);
    }

    // ── GenerateRefreshToken ──────────────────────────────────────────────────

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        _sut.GenerateRefreshToken().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public void GenerateRefreshToken_TwoCalls_ProduceDifferentValues()
    {
        var t1 = _sut.GenerateRefreshToken();
        var t2 = _sut.GenerateRefreshToken();

        t1.Should().NotBe(t2);
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsValidBase64()
    {
        var token = _sut.GenerateRefreshToken();

        var act = () => Convert.FromBase64String(token);
        act.Should().NotThrow();
    }
}

file static class DateTimeExtensions
{
    public static DateTime TruncateToSeconds(this DateTime dt)
        => new(dt.Year, dt.Month, dt.Day, dt.Hour, dt.Minute, dt.Second, dt.Kind);
}
