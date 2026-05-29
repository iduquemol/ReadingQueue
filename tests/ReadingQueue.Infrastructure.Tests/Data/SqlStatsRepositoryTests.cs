using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlStatsRepositoryTests : IClassFixture<BookRepositoryFixture>
{
    private readonly BookRepositoryFixture _fixture;
    private readonly SqlStatsRepository   _sut;
    private readonly SqlBookRepository    _books;

    public SqlStatsRepositoryTests(BookRepositoryFixture fixture)
    {
        _fixture = fixture;
        _sut     = new SqlStatsRepository(fixture.Factory);
        _books   = new SqlBookRepository(fixture.Factory);
    }

    private async Task<int> CreateUserAsync()
    {
        using var conn = new SqlConnection(_fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Users (Email, PasswordHash, DisplayName)
            OUTPUT INSERTED.Id
            VALUES (@Email, 'hash', 'Stats Test User');
            """, new { Email = $"stats_{Guid.NewGuid():N}@test.com" });
    }

    private async Task<int> CreateBookAsync(int userId,
        string genre = "Clasico",
        string mentalEnergy = "Baja - cualquier momento",
        string country = "Colombia",
        int priority = 3,
        bool isRead = false)
    {
        var id = await _books.CreateAsync(userId, new CreateBookData(
            Title:            $"Libro {Guid.NewGuid():N}",
            Author:           "Autor Test",
            Genre:            genre,
            Subgenre:         "",
            Country:          country,
            WhyRead:          null,
            Priority:         priority,
            MentalEnergy:     mentalEnergy,
            RecommendedMood:  "Solemne / quiero leer algo grande",
            RotationCategory: "Clasico",
            Notes:            null));

        if (isRead)
        {
            using var conn = new SqlConnection(_fixture.ConnectionString);
            await conn.ExecuteAsync(
                "UPDATE Books SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @Id",
                new { Id = id });
        }
        return id;
    }

    // ── GetDashboardAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task GetDashboardAsync_EmptyLibrary_ReturnZerosAndEmptyLists()
    {
        var userId = await CreateUserAsync();

        var result = await _sut.GetDashboardAsync(userId);

        result.TotalBooks.Should().Be(0);
        result.ReadBooks.Should().Be(0);
        result.UnreadBooks.Should().Be(0);
        result.ByGenre.Should().BeEmpty();
        result.ByRotationCategory.Should().BeEmpty();
        result.ByMentalEnergy.Should().BeEmpty();
        result.ByCountry.Should().BeEmpty();
        result.TopUnreadPriority.Should().BeEmpty();
        result.RecentlyRead.Should().BeEmpty();
    }

    [Fact]
    public async Task GetDashboardAsync_CountsAreCorrectAfterInsertingBooks()
    {
        var userId = await CreateUserAsync();
        await CreateBookAsync(userId, isRead: false);
        await CreateBookAsync(userId, isRead: false);
        await CreateBookAsync(userId, isRead: true);

        var result = await _sut.GetDashboardAsync(userId);

        result.TotalBooks.Should().Be(3);
        result.ReadBooks.Should().Be(1);
        result.UnreadBooks.Should().Be(2);
    }

    [Fact]
    public async Task GetDashboardAsync_ReadPercentage_IsCorrect()
    {
        var userId = await CreateUserAsync();
        await CreateBookAsync(userId, isRead: true);
        await CreateBookAsync(userId, isRead: false);
        await CreateBookAsync(userId, isRead: false);
        await CreateBookAsync(userId, isRead: false);

        var result = await _sut.GetDashboardAsync(userId);

        result.ReadPercentage.Should().Be(25.0);
    }

    [Fact]
    public async Task GetDashboardAsync_ByMentalEnergy_IsOrderedBySortOrderAsc()
    {
        var userId = await CreateUserAsync();
        await CreateBookAsync(userId, mentalEnergy: "Alta - concentracion");
        await CreateBookAsync(userId, mentalEnergy: "Baja - cualquier momento");
        await CreateBookAsync(userId, mentalEnergy: "Media - tarde tranquila");

        var result = await _sut.GetDashboardAsync(userId);

        var levels = result.ByMentalEnergy.Select(m => m.Level).ToList();
        levels.Should().ContainInOrder(
            "Baja - cualquier momento",
            "Media - tarde tranquila",
            "Alta - concentracion");
    }

    [Fact]
    public async Task GetDashboardAsync_ByCountry_ReturnsMaximum10()
    {
        var userId = await CreateUserAsync();
        var countries = new[]
        {
            "Colombia", "Argentina", "Mexico", "España", "Peru",
            "Chile", "Venezuela", "Bolivia", "Ecuador", "Uruguay", "Paraguay", "Cuba"
        };
        foreach (var country in countries)
            await CreateBookAsync(userId, country: country);

        var result = await _sut.GetDashboardAsync(userId);

        result.ByCountry.Should().HaveCount(10);
    }

    [Fact]
    public async Task GetDashboardAsync_TopUnreadPriority_ReturnsMax3Books()
    {
        var userId = await CreateUserAsync();
        await CreateBookAsync(userId, priority: 5, isRead: false);
        await CreateBookAsync(userId, priority: 4, isRead: false);
        await CreateBookAsync(userId, priority: 3, isRead: false);
        await CreateBookAsync(userId, priority: 2, isRead: false);
        await CreateBookAsync(userId, priority: 1, isRead: false);

        var result = await _sut.GetDashboardAsync(userId);

        result.TopUnreadPriority.Should().HaveCount(3);
        result.TopUnreadPriority[0].Priority.Should().Be(5);
        result.TopUnreadPriority[1].Priority.Should().Be(4);
        result.TopUnreadPriority[2].Priority.Should().Be(3);
    }

    [Fact]
    public async Task GetDashboardAsync_RecentlyRead_ReturnsMax5OrderedByReadAtDesc()
    {
        var userId = await CreateUserAsync();
        using var conn = new SqlConnection(_fixture.ConnectionString);

        for (var i = 1; i <= 7; i++)
        {
            var id = await CreateBookAsync(userId, isRead: false);
            await conn.ExecuteAsync(
                "UPDATE Books SET IsRead = 1, ReadAt = @ReadAt WHERE Id = @Id",
                new { Id = id, ReadAt = DateTime.UtcNow.AddDays(-i) });
        }

        var result = await _sut.GetDashboardAsync(userId);

        result.RecentlyRead.Should().HaveCount(5);
        for (var i = 1; i < result.RecentlyRead.Count; i++)
            result.RecentlyRead[i].ReadAt.Should().BeBefore(result.RecentlyRead[i - 1].ReadAt!.Value);
    }

    [Fact]
    public async Task GetDashboardAsync_ByGenre_GroupsCorrectly()
    {
        var userId = await CreateUserAsync();
        await CreateBookAsync(userId, genre: "Clasico");
        await CreateBookAsync(userId, genre: "Clasico");
        await CreateBookAsync(userId, genre: "Poesia");

        var result = await _sut.GetDashboardAsync(userId);

        var clasicoStat = result.ByGenre.FirstOrDefault(g => g.Genre == "Clasico");
        clasicoStat.Should().NotBeNull();
        clasicoStat!.Total.Should().Be(2);

        var poesiaStat = result.ByGenre.FirstOrDefault(g => g.Genre == "Poesia");
        poesiaStat.Should().NotBeNull();
        poesiaStat!.Total.Should().Be(1);
    }
}
