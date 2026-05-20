using FluentAssertions;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Tests;

public class RefreshTokenEntityTests
{
    private static RefreshToken Make(bool isRevoked, DateTime expiresAt)
        => new(1, 10, "token-value", expiresAt, DateTime.UtcNow, isRevoked);

    [Fact]
    public void IsValid_NotRevokedAndNotExpired_ReturnsTrue()
    {
        var token = Make(isRevoked: false, expiresAt: DateTime.UtcNow.AddDays(1));

        token.IsValid(DateTime.UtcNow).Should().BeTrue();
    }

    [Fact]
    public void IsValid_Revoked_ReturnsFalse()
    {
        var token = Make(isRevoked: true, expiresAt: DateTime.UtcNow.AddDays(1));

        token.IsValid(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsValid_Expired_ReturnsFalse()
    {
        var token = Make(isRevoked: false, expiresAt: DateTime.UtcNow.AddSeconds(-1));

        token.IsValid(DateTime.UtcNow).Should().BeFalse();
    }

    [Fact]
    public void IsValid_RevokedAndExpired_ReturnsFalse()
    {
        var token = Make(isRevoked: true, expiresAt: DateTime.UtcNow.AddSeconds(-1));

        token.IsValid(DateTime.UtcNow).Should().BeFalse();
    }
}
