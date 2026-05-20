using Dapper;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlBookRepositoryTests : IClassFixture<BookRepositoryFixture>
{
    private readonly SqlBookRepository _sut;
    private readonly BookRepositoryFixture _fixture;

    public SqlBookRepositoryTests(BookRepositoryFixture fixture)
    {
        _fixture = fixture;
        _sut     = new SqlBookRepository(fixture.Factory);
    }

    private CreateBookData MakeData(string genre = "Clasico", int priority = 3,
        bool? overrideIsRead = null)
        => new(
            Title:            $"Titulo {Guid.NewGuid():N}",
            Author:           "Autor Test",
            Genre:            genre,
            Country:          "Colombia",
            WhyRead:          null,
            Priority:         priority,
            MentalEnergy:     "Baja - cualquier momento",
            RecommendedMood:  "Analitico / quiero aprender algo",
            RotationCategory: "Clasico",
            Notes:            null
        );

    private async Task<int> CreateBookAsync(CreateBookData? data = null)
        => await _sut.CreateAsync(_fixture.UserId, data ?? MakeData());

    // ── GetByUserAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_EmptyLibrary_ReturnsEmptyList()
    {
        using var conn  = new SqlConnection(_fixture.ConnectionString);
        var tempUserId  = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Users (Email, PasswordHash, DisplayName)
            OUTPUT INSERTED.Id
            VALUES (@Email, 'h', 'Empty User');
            """, new { Email = $"empty_{Guid.NewGuid():N}@test.com" });

        var result = await _sut.GetByUserAsync(tempUserId, new BookFilter());

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_InsertsBook_ReturnsPositiveId()
    {
        var id = await CreateBookAsync();

        id.Should().BeGreaterThan(0);
    }

    // ── GetByIdAsync ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByIdAsync_CorrectUserAndId_ReturnsBook()
    {
        var id = await CreateBookAsync();

        var book = await _sut.GetByIdAsync(id, _fixture.UserId);

        book.Should().NotBeNull();
        book!.Id.Should().Be(id);
        book.UserId.Should().Be(_fixture.UserId);
    }

    [Fact]
    public async Task GetByIdAsync_WrongUserId_ReturnsNull()
    {
        var id = await CreateBookAsync();

        var result = await _sut.GetByIdAsync(id, userId: 999_999);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_NonExistentId_ReturnsNull()
    {
        var result = await _sut.GetByIdAsync(bookId: 999_999, _fixture.UserId);

        result.Should().BeNull();
    }

    // ── Filtros ───────────────────────────────────────────────────────────────

    [Fact]
    public async Task GetByUserAsync_FilterByGenre_ReturnsOnlyMatchingBooks()
    {
        await CreateBookAsync(MakeData(genre: "Clasico"));
        await CreateBookAsync(MakeData(genre: "Cuentos"));

        var result = await _sut.GetByUserAsync(_fixture.UserId, new BookFilter(Genre: "Clasico"));

        result.Should().OnlyContain(b => b.Genre == "Clasico");
    }

    [Fact]
    public async Task GetByUserAsync_FilterIsReadFalse_ReturnsOnlyUnread()
    {
        var bookId = await CreateBookAsync();
        using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Books SET IsRead=1, ReadAt=GETUTCDATE() WHERE Id=@Id",
            new { Id = bookId });

        var result = await _sut.GetByUserAsync(_fixture.UserId, new BookFilter(IsRead: false));

        result.Should().OnlyContain(b => !b.IsRead);
    }

    [Fact]
    public async Task GetByUserAsync_FilterMinPriority_ReturnsOnlyHighPriorityBooks()
    {
        await CreateBookAsync(MakeData(priority: 2));
        await CreateBookAsync(MakeData(priority: 4));
        await CreateBookAsync(MakeData(priority: 5));

        var result = await _sut.GetByUserAsync(_fixture.UserId, new BookFilter(MinPriority: 4));

        result.Should().OnlyContain(b => b.Priority >= 4);
    }

    [Fact]
    public async Task GetByUserAsync_SearchQueryMatchesTitle_ReturnsBook()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        await _sut.CreateAsync(_fixture.UserId, MakeData() with { Title = $"Unico{unique}Titulo" });

        var result = await _sut.GetByUserAsync(_fixture.UserId,
            new BookFilter(SearchQuery: unique.ToUpper()));

        result.Should().Contain(b => b.Title.Contains(unique));
    }

    [Fact]
    public async Task GetByUserAsync_SearchQueryMatchesAuthor_ReturnsBook()
    {
        var unique = Guid.NewGuid().ToString("N")[..8];
        await _sut.CreateAsync(_fixture.UserId, MakeData() with { Author = $"Autor{unique}Test" });

        var result = await _sut.GetByUserAsync(_fixture.UserId,
            new BookFilter(SearchQuery: unique.ToLower()));

        result.Should().Contain(b => b.Author.Contains(unique));
    }

    [Fact]
    public async Task GetByUserAsync_NoFilter_OrderedByPriorityDescThenCreatedAtAsc()
    {
        var idLow  = await CreateBookAsync(MakeData(priority: 1));
        var idHigh = await CreateBookAsync(MakeData(priority: 5));
        var idMid  = await CreateBookAsync(MakeData(priority: 3));

        var result = (await _sut.GetByUserAsync(_fixture.UserId, new BookFilter())).ToList();

        result.First(b => b.Priority == 5).Should().NotBeNull();
        var priorities = result.Select(b => b.Priority).ToList();
        priorities.Should().BeInDescendingOrder();
    }

    // ── UpdateAsync ───────────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateAsync_ChangesFields_AndUpdatedAt()
    {
        var id   = await CreateBookAsync();
        var orig = await _sut.GetByIdAsync(id, _fixture.UserId);

        await Task.Delay(100);
        var updated = new UpdateBookData("Nuevo Titulo", "Nuevo Autor", "Cuentos",
            "Mexico", "Porque quiero", 4, "Alta - concentracion",
            "Curioso / quiero algo fresco", "Libro corto o cuentos", "Notas nuevas");
        await _sut.UpdateAsync(id, _fixture.UserId, updated);

        var book = await _sut.GetByIdAsync(id, _fixture.UserId);
        book!.Title.Should().Be("Nuevo Titulo");
        book.Genre.Should().Be("Cuentos");
        book.UpdatedAt.Should().BeAfter(orig!.CreatedAt);
    }

    // ── MarkAsUnreadAsync ─────────────────────────────────────────────────────

    [Fact]
    public async Task MarkAsUnreadAsync_SetsIsReadFalse_AndClearsReadAt()
    {
        var id = await CreateBookAsync();
        using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Books SET IsRead=1, ReadAt=GETUTCDATE() WHERE Id=@Id",
            new { Id = id });

        await _sut.MarkAsUnreadAsync(id, _fixture.UserId);

        var book = await _sut.GetByIdAsync(id, _fixture.UserId);
        book!.IsRead.Should().BeFalse();
        book.ReadAt.Should().BeNull();
    }

    // ── GetUnreadByUserAsync / GetReadByUserAsync ─────────────────────────────

    [Fact]
    public async Task GetUnreadByUserAsync_ReturnsOnlyUnreadBooks()
    {
        var id = await CreateBookAsync();
        using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Books SET IsRead=1, ReadAt=GETUTCDATE() WHERE Id=@Id",
            new { Id = id });

        var result = await _sut.GetUnreadByUserAsync(_fixture.UserId);

        result.Should().OnlyContain(b => !b.IsRead);
    }

    [Fact]
    public async Task GetReadByUserAsync_ReturnsOnlyReadBooks()
    {
        var id = await CreateBookAsync();
        using var conn = new SqlConnection(_fixture.ConnectionString);
        await conn.ExecuteAsync(
            "UPDATE Books SET IsRead=1, ReadAt=GETUTCDATE() WHERE Id=@Id",
            new { Id = id });

        var result = await _sut.GetReadByUserAsync(_fixture.UserId);

        result.Should().OnlyContain(b => b.IsRead);
    }
}
