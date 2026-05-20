using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class RegisterUserTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly Mock<IAuthService> _auth = new();
    private readonly RegisterUser _sut;

    public RegisterUserTests()
    {
        _sut = new RegisterUser(_users.Object, _tokens.Object, _auth.Object);
    }

    private static RegisterUser.Command ValidCommand(string email = "new@example.com")
        => new(email, "Password1", "Test User");

    [Fact]
    public async Task ExecuteAsync_EmailAlreadyRegistered_ThrowsConflictException()
    {
        var existing = new User(1, "new@example.com", "hash", "Existing", DateTime.UtcNow, true);
        _users.Setup(r => r.GetByEmailAsync("new@example.com", default))
              .ReturnsAsync(existing);

        await _sut.Invoking(s => s.ExecuteAsync(ValidCommand()))
                  .Should().ThrowAsync<ConflictException>();
    }

    [Fact]
    public async Task ExecuteAsync_NewEmail_CallsHashPasswordWithPlainPassword()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        _users.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                                         It.IsAny<string>(), default))
              .ReturnsAsync(1);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        await _sut.ExecuteAsync(ValidCommand());

        _auth.Verify(a => a.HashPassword("Password1"), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NewEmail_CallsCreateAsyncWithHash_NotPlainText()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        _auth.Setup(a => a.HashPassword("Password1")).Returns("bcrypt-hash");
        _users.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                                         It.IsAny<string>(), default))
              .ReturnsAsync(1);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        await _sut.ExecuteAsync(ValidCommand());

        _users.Verify(r => r.CreateAsync(
            It.IsAny<string>(), "bcrypt-hash", It.IsAny<string>(), default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NewEmail_ReturnsNonEmptyTokens()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        _users.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                                         It.IsAny<string>(), default))
              .ReturnsAsync(42);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access-token");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh-token");

        var result = await _sut.ExecuteAsync(ValidCommand());

        result.AccessToken.Should().Be("access-token");
        result.RefreshToken.Should().Be("refresh-token");
    }

    [Fact]
    public async Task ExecuteAsync_NewEmail_ReturnsCorrectUserIdAndDisplayName()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        _users.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                                         It.IsAny<string>(), default))
              .ReturnsAsync(42);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        var result = await _sut.ExecuteAsync(ValidCommand());

        result.UserId.Should().Be(42);
        result.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task ExecuteAsync_NewEmail_PersistsRefreshToken()
    {
        _users.Setup(r => r.GetByEmailAsync(It.IsAny<string>(), default))
              .ReturnsAsync((User?)null);
        _users.Setup(r => r.CreateAsync(It.IsAny<string>(), It.IsAny<string>(),
                                         It.IsAny<string>(), default))
              .ReturnsAsync(1);
        _auth.Setup(a => a.GenerateAccessToken(It.IsAny<User>())).Returns("access");
        _auth.Setup(a => a.GenerateRefreshToken()).Returns("refresh");

        await _sut.ExecuteAsync(ValidCommand());

        _tokens.Verify(t => t.CreateAsync(1, "refresh", It.IsAny<DateTime>(), default),
                       Times.Once);
    }
}
