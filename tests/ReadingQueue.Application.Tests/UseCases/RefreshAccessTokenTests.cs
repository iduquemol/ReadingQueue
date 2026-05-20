using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class RefreshAccessTokenTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly RefreshAccessToken _sut;

    public RefreshAccessTokenTests()
    {
        _sut = new RefreshAccessToken(_users.Object, _tokens.Object, _auth.Object);
    }

    private static User MakeUser()
        => new(10, "u@example.com", "hash", "User", DateTime.UtcNow, true);

    private static RefreshToken ValidToken()
        => new(1, 10, "valid-token", DateTime.UtcNow.AddDays(7), DateTime.UtcNow, false);

    private static RefreshToken RevokedToken()
        => new(1, 10, "revoked-token", DateTime.UtcNow.AddDays(7), DateTime.UtcNow, true);

    private static RefreshToken ExpiredToken()
        => new(1, 10, "expired-token", DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow, false);

    [Fact]
    public async Task ExecuteAsync_TokenNotFound_ThrowsUnauthorizedException()
    {
        _tokens.Setup(t => t.GetByTokenAsync("unknown", default))
               .ReturnsAsync((RefreshToken?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new("unknown")))
                  .Should().ThrowAsync<UnauthorizedException>()
                  .WithMessage("Token de renovación inválido.");
    }

    [Fact]
    public async Task ExecuteAsync_RevokedToken_ThrowsUnauthorizedException()
    {
        _tokens.Setup(t => t.GetByTokenAsync("revoked-token", default))
               .ReturnsAsync(RevokedToken());

        await _sut.Invoking(s => s.ExecuteAsync(new("revoked-token")))
                  .Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ExecuteAsync_ExpiredToken_ThrowsUnauthorizedException()
    {
        _tokens.Setup(t => t.GetByTokenAsync("expired-token", default))
               .ReturnsAsync(ExpiredToken());

        await _sut.Invoking(s => s.ExecuteAsync(new("expired-token")))
                  .Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ExecuteAsync_ValidToken_RevokesUsedToken()
    {
        _tokens.Setup(t => t.GetByTokenAsync("valid-token", default))
               .ReturnsAsync(ValidToken());
        _users.Setup(r => r.GetByIdAsync(10, default)).ReturnsAsync(MakeUser());
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("new-access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("new-refresh");

        await _sut.ExecuteAsync(new("valid-token"));

        _tokens.Verify(t => t.RevokeAsync("valid-token", default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidToken_PersistsNewRefreshToken()
    {
        _tokens.Setup(t => t.GetByTokenAsync("valid-token", default))
               .ReturnsAsync(ValidToken());
        _users.Setup(r => r.GetByIdAsync(10, default)).ReturnsAsync(MakeUser());
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("new-access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("new-refresh");

        await _sut.ExecuteAsync(new("valid-token"));

        _tokens.Verify(t => t.CreateAsync(10, "new-refresh", It.IsAny<DateTime>(), default),
                       Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidToken_ReturnsNewTokens()
    {
        _tokens.Setup(t => t.GetByTokenAsync("valid-token", default))
               .ReturnsAsync(ValidToken());
        _users.Setup(r => r.GetByIdAsync(10, default)).ReturnsAsync(MakeUser());
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("new-access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("new-refresh");

        var result = await _sut.ExecuteAsync(new("valid-token"));

        result.AccessToken.Should().Be("new-access");
        result.NewRefreshToken.Should().Be("new-refresh");
    }
}
