using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlQueueRepositoryTests : IClassFixture<BookRepositoryFixture>
{
    private readonly BookRepositoryFixture  _fixture;
    private readonly SqlQueueRepository     _sut;
    private readonly SqlBookRepository      _books;

    public SqlQueueRepositoryTests(BookRepositoryFixture fixture)
    {
        _fixture = fixture;
        _sut     = new SqlQueueRepository(fixture.Factory);
        _books   = new SqlBookRepository(fixture.Factory);
    }

    private async Task<int> CreateUserAsync()
    {
        using var conn = new SqlConnection(_fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Users (Email, PasswordHash, DisplayName)
            OUTPUT INSERTED.Id
            VALUES (@Email, 'hash', 'Queue Test User');
            """, new { Email = $"queue_{Guid.NewGuid():N}@test.com" });
    }

    private async Task<int> CreateBookAsync(int userId,
        string genre = "Clasico",
        string rotationCategory = "Clasico",
        string mentalEnergy = "Baja - cualquier momento",
        int priority = 3)
        => await _books.CreateAsync(userId, new CreateBookData(
            Title:            $"Libro {Guid.NewGuid():N}",
            Author:           "Autor Test",
            Genre:            genre,
            Country:          "Colombia",
            WhyRead:          null,
            Priority:         priority,
            MentalEnergy:     mentalEnergy,
            RecommendedMood:  "Solemne / quiero leer algo grande",
            RotationCategory: rotationCategory,
            Notes:            null));

    private async Task InsertQueueItemAsync(int userId, int bookId, int position,
        string source = "Filter")
    {
        using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync("""
            INSERT INTO ReadingQueue (UserId, BookId, Position, Source)
            VALUES (@UserId, @BookId, @Position, @Source);
            """, new { UserId = userId, BookId = bookId, Position = position, Source = source });
    }

    // ── GetByUserAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_EmptyQueue_ReturnsEmptyList()
    {
        var userId = await CreateUserAsync();

        var result = await _sut.GetByUserAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserAsync_ReturnsItemsOrderedByPositionAsc()
    {
        var userId = await CreateUserAsync();
        var book1  = await CreateBookAsync(userId);
        var book2  = await CreateBookAsync(userId);
        var book3  = await CreateBookAsync(userId);
        await InsertQueueItemAsync(userId, book3, 1);
        await InsertQueueItemAsync(userId, book1, 2);
        await InsertQueueItemAsync(userId, book2, 3);

        var result = (await _sut.GetByUserAsync(userId)).ToList();

        result.Should().HaveCount(3);
        result[0].BookId.Should().Be(book3);
        result[1].BookId.Should().Be(book1);
        result[2].BookId.Should().Be(book2);
    }

    [Fact]
    public async Task GetByUserAsync_ExcludesReadBooks()
    {
        var userId = await CreateUserAsync();
        var bookId = await CreateBookAsync(userId);
        await InsertQueueItemAsync(userId, bookId, 1);

        using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Books SET IsRead = 1, ReadAt = GETUTCDATE() WHERE Id = @Id",
            new { Id = bookId });

        var result = await _sut.GetByUserAsync(userId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByUserAsync_DoesNotReturnOtherUsersItems()
    {
        var userId1 = await CreateUserAsync();
        var userId2 = await CreateUserAsync();
        var book1   = await CreateBookAsync(userId1);
        var book2   = await CreateBookAsync(userId2);
        await InsertQueueItemAsync(userId1, book1, 1);
        await InsertQueueItemAsync(userId2, book2, 1);

        var result = (await _sut.GetByUserAsync(userId1)).ToList();

        result.Should().HaveCount(1);
        result[0].UserId.Should().Be(userId1);
    }

    // ── ReplaceQueueAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ReplaceQueueAsync_InsertsItemsAndReturnedByGetByUser()
    {
        var userId = await CreateUserAsync();
        var book1  = await CreateBookAsync(userId);
        var book2  = await CreateBookAsync(userId);

        using var conn = new SqlConnection(_fixture.ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        await _sut.ReplaceQueueAsync(userId,
            [(book1, 1, "Filter"), (book2, 2, "Filter")], tx);
        tx.Commit();

        var result = (await _sut.GetByUserAsync(userId)).ToList();
        result.Should().HaveCount(2);
        result[0].BookId.Should().Be(book1);
        result[1].BookId.Should().Be(book2);
    }

    [Fact]
    public async Task ReplaceQueueAsync_DeletesPreviousQueueBeforeInserting()
    {
        var userId = await CreateUserAsync();
        var book1  = await CreateBookAsync(userId);
        var book2  = await CreateBookAsync(userId);
        await InsertQueueItemAsync(userId, book1, 1);

        using var conn = new SqlConnection(_fixture.ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        await _sut.ReplaceQueueAsync(userId, [(book2, 1, "Filter")], tx);
        tx.Commit();

        var result = (await _sut.GetByUserAsync(userId)).ToList();
        result.Should().HaveCount(1);
        result[0].BookId.Should().Be(book2);
    }

    // ── UpdatePositionsAsync ──────────────────────────────────────────────────

    [Fact]
    public async Task UpdatePositionsAsync_ModifiesPositionsCorrectly()
    {
        var userId = await CreateUserAsync();
        var book1  = await CreateBookAsync(userId);
        var book2  = await CreateBookAsync(userId);
        await InsertQueueItemAsync(userId, book1, 1);
        await InsertQueueItemAsync(userId, book2, 2);

        using var conn = new SqlConnection(_fixture.ConnectionString);
        conn.Open();
        using var tx = conn.BeginTransaction();
        await _sut.UpdatePositionsAsync(userId, [(book1, 2), (book2, 1)], tx);
        tx.Commit();

        var result = (await _sut.GetByUserAsync(userId)).ToList();
        result[0].BookId.Should().Be(book2);
        result[1].BookId.Should().Be(book1);
    }

    // ── ContainsBookAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task ContainsBookAsync_BookInQueue_ReturnsTrue()
    {
        var userId = await CreateUserAsync();
        var bookId = await CreateBookAsync(userId);
        await InsertQueueItemAsync(userId, bookId, 1);

        var result = await _sut.ContainsBookAsync(userId, bookId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task ContainsBookAsync_BookNotInQueue_ReturnsFalse()
    {
        var userId = await CreateUserAsync();
        var bookId = await CreateBookAsync(userId);

        var result = await _sut.ContainsBookAsync(userId, bookId);

        result.Should().BeFalse();
    }

    // ── RemoveItemAsync ───────────────────────────────────────────────────────

    [Fact]
    public async Task RemoveItemAsync_RemovesSpecificItemWithoutAffectingOthers()
    {
        var userId = await CreateUserAsync();
        var book1  = await CreateBookAsync(userId);
        var book2  = await CreateBookAsync(userId);
        var book3  = await CreateBookAsync(userId);
        await InsertQueueItemAsync(userId, book1, 1);
        await InsertQueueItemAsync(userId, book2, 2);
        await InsertQueueItemAsync(userId, book3, 3);

        await _sut.RemoveItemAsync(userId, book2);

        var result = (await _sut.GetByUserAsync(userId)).ToList();
        result.Should().HaveCount(2);
        result.Should().NotContain(q => q.BookId == book2);
        result.Should().Contain(q => q.BookId == book1);
        result.Should().Contain(q => q.BookId == book3);
    }
}
