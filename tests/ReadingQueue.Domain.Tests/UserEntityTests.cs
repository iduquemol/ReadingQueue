using FluentAssertions;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Tests;

public class UserEntityTests
{
    [Fact]
    public void Constructor_AssignsAllProperties()
    {
        var createdAt = new DateTime(2024, 1, 15, 10, 0, 0, DateTimeKind.Utc);

        var user = new User(42, "user@example.com", "hash123",
                            "Juan García", createdAt, true);

        user.Id.Should().Be(42);
        user.Email.Should().Be("user@example.com");
        user.PasswordHash.Should().Be("hash123");
        user.DisplayName.Should().Be("Juan García");
        user.CreatedAt.Should().Be(createdAt);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Constructor_WithIsActiveFalse_CanBeConstructedWithoutException()
    {
        var act = () => new User(1, "inactive@example.com", "hash",
                                 "Inactive User", DateTime.UtcNow, false);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_WithIsActiveFalse_StoresValueCorrectly()
    {
        var user = new User(1, "inactive@example.com", "hash",
                            "Inactive User", DateTime.UtcNow, false);

        user.IsActive.Should().BeFalse();
    }
}
