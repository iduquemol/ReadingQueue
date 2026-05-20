using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class LogoutUserTests
{
    private readonly Mock<IRefreshTokenRepository> _tokens = new();
    private readonly LogoutUser _sut;

    public LogoutUserTests()
    {
        _sut = new LogoutUser(_tokens.Object);
    }

    [Fact]
    public async Task ExecuteAsync_ValidToken_CallsRevokeOnce()
    {
        await _sut.ExecuteAsync(new("some-token"));

        _tokens.Verify(t => t.RevokeAsync("some-token", default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_NonExistentToken_DoesNotThrow()
    {
        // RevokeAsync en BD no lanza si el token no existe (UPDATE sin filas = ok)
        _tokens.Setup(t => t.RevokeAsync(It.IsAny<string>(), default))
               .Returns(Task.CompletedTask);

        await _sut.Invoking(s => s.ExecuteAsync(new("ghost-token")))
                  .Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsCompletedTask()
    {
        var task = _sut.ExecuteAsync(new("token"));

        await task;
        task.IsCompletedSuccessfully.Should().BeTrue();
    }
}
