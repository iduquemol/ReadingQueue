using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlAISuggestionRepositoryTests : IClassFixture<BookRepositoryFixture>
{
    private readonly BookRepositoryFixture      _fixture;
    private readonly SqlAISuggestionRepository  _sut;
    private readonly SqlBookRepository          _books;

    public SqlAISuggestionRepositoryTests(BookRepositoryFixture fixture)
    {
        _fixture = fixture;
        _sut     = new SqlAISuggestionRepository(fixture.Factory);
        _books   = new SqlBookRepository(fixture.Factory);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private async Task<int> CreateUserAsync()
    {
        using var conn = new SqlConnection(_fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Users (Email, PasswordHash, DisplayName)
            OUTPUT INSERTED.Id
            VALUES (@Email, 'hash', 'AI Test User');
            """, new { Email = $"ai_{Guid.NewGuid():N}@test.com" });
    }

    private async Task<int> CreateBookAsync(int userId, string title = "Libro Test")
    {
        using var conn = new SqlConnection(_fixture.ConnectionString);
        return await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Books
                (UserId, Title, Author, Genre, Country, Priority,
                 MentalEnergy, RecommendedMood, RotationCategory)
            OUTPUT INSERTED.Id
            VALUES (@UserId, @Title, 'Autor', 'Clasico', 'Colombia', 3,
                    'Baja - cualquier momento',
                    'Solemne / quiero leer algo grande',
                    'Clasico');
            """, new { UserId = userId, Title = title });
    }

    private static IEnumerable<BookSuggestion> MakeSuggestions(params (int bookId, double score)[] items)
        => items.Select(x => new BookSuggestion(x.bookId, x.score, $"Razon del libro {x.bookId}."));

    // ── GetLatestByUserAsync: vacío ───────────────────────────────────────────

    [Fact]
    public async Task GetLatestByUserAsync_EmptyTable_ReturnsEmptyList()
    {
        var userId = await CreateUserAsync();

        var result = await _sut.GetLatestByUserAsync(userId);

        result.Should().BeEmpty();
    }

    // ── SaveSuggestionsAsync: persistencia básica ─────────────────────────────

    [Fact]
    public async Task SaveSuggestionsAsync_PersistsCorrectFields()
    {
        var userId = await CreateUserAsync();
        var bookId = await CreateBookAsync(userId);
        var suggestions = MakeSuggestions((bookId, 7.5));

        await _sut.SaveSuggestionsAsync(userId, suggestions, [bookId]);

        var results = (await _sut.GetLatestByUserAsync(userId)).ToList();
        results.Should().HaveCount(1);
        var s = results.First();
        s.UserId.Should().Be(userId);
        s.BookId.Should().Be(bookId);
        s.Score.Should().Be(7.50m);
        s.Reasoning.Should().Be($"Razon del libro {bookId}.");
    }

    // ── SaveSuggestionsAsync: WasAccepted ────────────────────────────────────

    [Fact]
    public async Task SaveSuggestionsAsync_SetsWasAcceptedCorrectly()
    {
        var userId  = await CreateUserAsync();
        var book1   = await CreateBookAsync(userId, "Libro Aceptado");
        var book2   = await CreateBookAsync(userId, "Libro Rechazado");
        var suggestions = MakeSuggestions((book1, 9.0), (book2, 6.0));

        // solo book1 fue aceptado
        await _sut.SaveSuggestionsAsync(userId, suggestions, [book1]);

        var results = (await _sut.GetLatestByUserAsync(userId))
            .ToDictionary(s => s.BookId);
        results[book1].WasAccepted.Should().BeTrue();
        results[book2].WasAccepted.Should().BeFalse();
    }

    // ── SaveSuggestionsAsync: historial acumulativo ───────────────────────────

    [Fact]
    public async Task SaveSuggestionsAsync_CalledTwice_AccumulatesRows()
    {
        var userId  = await CreateUserAsync();
        var book1   = await CreateBookAsync(userId);
        var book2   = await CreateBookAsync(userId);

        await _sut.SaveSuggestionsAsync(userId, MakeSuggestions((book1, 8.0)), []);
        await _sut.SaveSuggestionsAsync(userId, MakeSuggestions((book2, 7.0)), []);

        var results = (await _sut.GetLatestByUserAsync(userId, take: 50)).ToList();
        results.Should().HaveCount(2);
    }

    // ── GetLatestByUserAsync: límite de take ──────────────────────────────────

    [Fact]
    public async Task GetLatestByUserAsync_RespectsMaxTake()
    {
        var userId = await CreateUserAsync();
        var books  = await Task.WhenAll(
            Enumerable.Range(0, 5).Select(_ => CreateBookAsync(userId)));
        var suggestions = books.Select(b => new BookSuggestion(b, 5.0, "Razon."));

        await _sut.SaveSuggestionsAsync(userId, suggestions, []);

        var results = (await _sut.GetLatestByUserAsync(userId, take: 3)).ToList();
        results.Should().HaveCount(3);
    }

    // ── GetLatestByUserAsync: orden DESC ──────────────────────────────────────

    [Fact]
    public async Task GetLatestByUserAsync_OrderedByGeneratedAtDesc()
    {
        var userId = await CreateUserAsync();
        var book1  = await CreateBookAsync(userId, "Primero");
        var book2  = await CreateBookAsync(userId, "Segundo");

        await _sut.SaveSuggestionsAsync(userId, MakeSuggestions((book1, 8.0)), []);
        await Task.Delay(20); // asegurar que GeneratedAt difiera
        await _sut.SaveSuggestionsAsync(userId, MakeSuggestions((book2, 9.0)), []);

        var results = (await _sut.GetLatestByUserAsync(userId, take: 50)).ToList();
        results.First().BookId.Should().Be(book2);
    }

    // ── GetLatestByUserAsync: aislamiento por usuario ─────────────────────────

    [Fact]
    public async Task GetLatestByUserAsync_DoesNotReturnOtherUsersSuggestions()
    {
        var userId1 = await CreateUserAsync();
        var userId2 = await CreateUserAsync();
        var book1   = await CreateBookAsync(userId1);
        var book2   = await CreateBookAsync(userId2);

        await _sut.SaveSuggestionsAsync(userId1, MakeSuggestions((book1, 8.0)), []);
        await _sut.SaveSuggestionsAsync(userId2, MakeSuggestions((book2, 8.0)), []);

        var results = (await _sut.GetLatestByUserAsync(userId1)).ToList();
        results.Should().HaveCount(1);
        results.First().UserId.Should().Be(userId1);
    }

    // ── GetLatestByUserAsync: JOIN con Books ──────────────────────────────────

    [Fact]
    public async Task GetLatestByUserAsync_BookTitleMatchesBooks()
    {
        var userId = await CreateUserAsync();
        var bookId = await CreateBookAsync(userId, "Don Quijote");

        await _sut.SaveSuggestionsAsync(userId, MakeSuggestions((bookId, 9.0)), [bookId]);

        var results = (await _sut.GetLatestByUserAsync(userId)).ToList();
        results.First().BookTitle.Should().Be("Don Quijote");
    }
}
