using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlRefreshTokenRepositoryTests : IClassFixture<AuthContainerFixture>
{
    private readonly SqlRefreshTokenRepository _sut;
    private readonly SqlUserRepository _users;

    public SqlRefreshTokenRepositoryTests(AuthContainerFixture fixture)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = fixture.ConnectionString
            })
            .Build();

        var factory = new SqlConnectionFactory(config);
        _sut   = new SqlRefreshTokenRepository(factory);
        _users = new SqlUserRepository(factory);
    }

    private async Task<int> CreateUserAsync()
        => await _users.CreateAsync(
            $"rt_{Guid.NewGuid():N}@example.com", "hash", "RT User");

    [Fact]
    public async Task GetByTokenAsync_TokenNotFound_ReturnsNull()
    {
        var result = await _sut.GetByTokenAsync("nonexistent-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_InsertToken_DoesNotThrow()
    {
        var userId = await CreateUserAsync();

        var act = async () => await _sut.CreateAsync(
            userId, Guid.NewGuid().ToString(), DateTime.UtcNow.AddDays(7));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task GetByTokenAsync_AfterCreate_ReturnsTokenWithCorrectFields()
    {
        var userId    = await CreateUserAsync();
        var tokenVal  = Guid.NewGuid().ToString();
        var expiresAt = DateTime.UtcNow.AddDays(7);

        await _sut.CreateAsync(userId, tokenVal, expiresAt);
        var result = await _sut.GetByTokenAsync(tokenVal);

        result.Should().NotBeNull();
        result!.UserId.Should().Be(userId);
        result.IsRevoked.Should().BeFalse();
    }

    [Fact]
    public async Task RevokeAsync_SetsIsRevokedTrue()
    {
        var userId   = await CreateUserAsync();
        var tokenVal = Guid.NewGuid().ToString();
        await _sut.CreateAsync(userId, tokenVal, DateTime.UtcNow.AddDays(7));

        await _sut.RevokeAsync(tokenVal);

        var result = await _sut.GetByTokenAsync(tokenVal);
        result!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task GetByTokenAsync_AfterRevoke_ReturnsIsRevokedTrue()
    {
        var userId   = await CreateUserAsync();
        var tokenVal = Guid.NewGuid().ToString();
        await _sut.CreateAsync(userId, tokenVal, DateTime.UtcNow.AddDays(7));
        await _sut.RevokeAsync(tokenVal);

        var result = await _sut.GetByTokenAsync(tokenVal);

        result!.IsRevoked.Should().BeTrue();
    }

    [Fact]
    public async Task RevokeAsync_TokenNotFound_DoesNotThrow()
    {
        var act = async () => await _sut.RevokeAsync("token-that-does-not-exist");

        await act.Should().NotThrowAsync();
    }
}
