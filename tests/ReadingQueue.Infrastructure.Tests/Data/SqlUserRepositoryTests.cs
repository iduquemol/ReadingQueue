using FluentAssertions;
using Microsoft.Extensions.Configuration;
using ReadingQueue.Infrastructure.Data;
using ReadingQueue.Infrastructure.Migrations;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlUserRepositoryTests : IClassFixture<AuthContainerFixture>
{
    private readonly SqlUserRepository _sut;
    private readonly string _connectionString;

    public SqlUserRepositoryTests(AuthContainerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString
            })
            .Build();
        _sut = new SqlUserRepository(new SqlConnectionFactory(config));
    }

    [Fact]
    public async Task GetByEmailAsync_EmailNotFound_ReturnsNull()
    {
        var result = await _sut.GetByEmailAsync("noexiste@example.com");

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_InsertsUser_ReturnsPositiveId()
    {
        var id = await _sut.CreateAsync(
            $"new_{Guid.NewGuid():N}@example.com", "hash", "Test User");

        id.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task GetByEmailAsync_AfterCreate_ReturnsUserWithCorrectEmail()
    {
        var email = $"find_{Guid.NewGuid():N}@example.com";
        await _sut.CreateAsync(email, "hashvalue", "Find User");

        var user = await _sut.GetByEmailAsync(email);

        user.Should().NotBeNull();
        user!.Email.Should().Be(email);
    }

    [Fact]
    public async Task GetByIdAsync_AfterCreate_ReturnsActiveUser()
    {
        var email = $"byid_{Guid.NewGuid():N}@example.com";
        var id    = await _sut.CreateAsync(email, "hashvalue", "ById User");

        var user = await _sut.GetByIdAsync(id);

        user.Should().NotBeNull();
        user!.Id.Should().Be(id);
        user.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_IdNotFound_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(999_999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_PasswordHashIsNotStoredAsPlainText()
    {
        var plainPassword = "MyPlainPassword1";
        var hash          = BCrypt.Net.BCrypt.HashPassword(plainPassword, workFactor: 12);
        var email         = $"hash_{Guid.NewGuid():N}@example.com";

        var id   = await _sut.CreateAsync(email, hash, "Hash User");
        var user = await _sut.GetByIdAsync(id);

        user!.PasswordHash.Should().NotBe(plainPassword);
        user.PasswordHash.Should().Be(hash);
    }
}
