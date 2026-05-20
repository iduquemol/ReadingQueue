using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetCurrentUserTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly GetCurrentUser _sut;

    public GetCurrentUserTests()
    {
        _sut = new GetCurrentUser(_users.Object);
    }

    [Fact]
    public async Task ExecuteAsync_UserNotFound_ThrowsUnauthorizedException()
    {
        _users.Setup(r => r.GetByIdAsync(99, default)).ReturnsAsync((User?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new(99)))
                  .Should().ThrowAsync<UnauthorizedException>();
    }

    [Fact]
    public async Task ExecuteAsync_UserFound_ReturnsCorrectId()
    {
        var user = new User(42, "u@example.com", "hash", "My Name", DateTime.UtcNow, true);
        _users.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(user);

        var result = await _sut.ExecuteAsync(new(42));

        result.Id.Should().Be(42);
    }

    [Fact]
    public async Task ExecuteAsync_UserFound_ReturnsCorrectEmailAndDisplayName()
    {
        var user = new User(42, "u@example.com", "hash", "My Name", DateTime.UtcNow, true);
        _users.Setup(r => r.GetByIdAsync(42, default)).ReturnsAsync(user);

        var result = await _sut.ExecuteAsync(new(42));

        result.Email.Should().Be("u@example.com");
        result.DisplayName.Should().Be("My Name");
    }
}
