using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class LoginUserTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly LoginUser _sut;

    public LoginUserTests()
    {
        _sut = new LoginUser(_users.Object, _tokens.Object, _auth.Object);
    }

    private static User ActiveUser()
        => new(1, "user@example.com", "hash", "Test User", DateTime.UtcNow, true);

    private static User InactiveUser()
        => new(1, "user@example.com", "hash", "Test User", DateTime.UtcNow, false);

    [Fact]
    public async Task ExecuteAsync_EmailNotFound_ThrowsUnauthorizedException()
    {
        _users.Setup(r => r.GetByEmailAsync("user@example.com", default))
              .ReturnsAsync((User?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new("user@example.com", "pass")))
                  .Should().ThrowAsync<UnauthorizedException>()
                  .WithMessage("Credenciales inválidas.");
    }

    [Fact]
    public async Task ExecuteAsync_WrongPassword_ThrowsUnauthorizedException()
    {
        _users.Setup(r => r.GetByEmailAsync("user@example.com", default))
              .ReturnsAsync(ActiveUser());
        _auth.Setup(a => a.VerifyPassword("wrong", "hash")).Returns(false);

        await _sut.Invoking(s => s.ExecuteAsync(new("user@example.com", "wrong")))
                  .Should().ThrowAsync<UnauthorizedException>()
                  .WithMessage("Credenciales inválidas.");
    }

    [Fact]
    public async Task ExecuteAsync_WrongPassword_SameMessageAsEmailNotFound()
    {
        // CA-09: el mensaje nunca revela cuál falló
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        var exEmailNotFound = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.ExecuteAsync(new("x@x.com", "pass")));

        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync(ActiveUser());
        _auth.Setup(a => a.VerifyPassword(It.IsAny<string>(), It.IsAny<string>()))
             .Returns(false);
        var exWrongPass = await Assert.ThrowsAsync<UnauthorizedException>(
            () => _sut.ExecuteAsync(new("user@example.com", "wrong")));

        exEmailNotFound.Message.Should().Be(exWrongPass.Message);
    }

    [Fact]
    public async Task ExecuteAsync_InactiveUser_ThrowsUnauthorizedException()
    {
        _users.Setup(r => r.GetByEmailAsync("user@example.com", default))
              .ReturnsAsync(InactiveUser());
        _auth.Setup(a => a.VerifyPassword("pass", "hash")).Returns(true);

        await _sut.Invoking(s => s.ExecuteAsync(new("user@example.com", "pass")))
                  .Should().ThrowAsync<UnauthorizedException>()
                  .WithMessage("Cuenta desactivada.");
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_ReturnsTokens()
    {
        _users.Setup(r => r.GetByEmailAsync("user@example.com", default))
              .ReturnsAsync(ActiveUser());
        _auth.Setup(a => a.VerifyPassword("pass", "hash")).Returns(true);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        var result = await _sut.ExecuteAsync(new("user@example.com", "pass"));

        result.AccessToken.Should().Be("access");
        result.RefreshToken.Should().Be("refresh");
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_ReturnsCorrectUserInfo()
    {
        _users.Setup(r => r.GetByEmailAsync("user@example.com", default))
              .ReturnsAsync(ActiveUser());
        _auth.Setup(a => a.VerifyPassword("pass", "hash")).Returns(true);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        var result = await _sut.ExecuteAsync(new("user@example.com", "pass"));

        result.UserId.Should().Be(1);
        result.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task ExecuteAsync_ValidCredentials_PersistsRefreshToken()
    {
        _users.Setup(r => r.GetByEmailAsync("user@example.com", default))
              .ReturnsAsync(ActiveUser());
        _auth.Setup(a => a.VerifyPassword("pass", "hash")).Returns(true);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        await _sut.ExecuteAsync(new("user@example.com", "pass"));

        _tokens.Verify(t => t.CreateAsync(1, "refresh", It.IsAny<DateTime>(), default),
                       Times.Once);
    }
}
