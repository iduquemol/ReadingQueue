using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;

namespace ReadingQueue.Api.Tests.Endpoints;

public class BookEndpointsTests : IClassFixture<BookEndpointsFixture>
{
    private readonly BookEndpointsFixture _fixture;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BookEndpointsTests(BookEndpointsFixture fixture) => _fixture = fixture;

    // ── GET /api/books ──────────────────────────────────────────────────────────

    [Fact]
    public async Task GetBooks_NoFilters_Returns200WithAllUserBooks()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client);
        await _fixture.CreateBookAsync(client);

        var resp = await client.GetAsync("/api/books");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await resp.Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        books.Should().HaveCountGreaterThanOrEqualTo(2);
    }

    // ── GET /api/books con filtros ──────────────────────────────────────────────

    [Fact]
    public async Task GetBooks_WithGenreFilter_ReturnsOnlyMatchingGenre()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook("Clasico"));
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook("Novela contemporanea"));

        var resp = await client.GetAsync("/api/books?genre=Clasico");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await resp.Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        books.Should().OnlyContain(b => b.Genre == "Clasico");
    }

    [Fact]
    public async Task GetBooks_WithIsReadFalse_ReturnsOnlyUnreadBooks()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var unreadBook = await _fixture.CreateBookAsync(client);
        var readBook   = await _fixture.CreateBookAsync(client);
        await client.PostAsJsonAsync($"/api/books/{readBook.Id}/read", new { });

        var resp = await client.GetAsync("/api/books?isRead=false");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await resp.Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        books.Should().NotBeEmpty();
        books.Should().OnlyContain(b => !b.IsRead);
        _ = unreadBook;
    }

    [Fact]
    public async Task GetBooks_WithSearchQuery_FindsBookByTitleSubstring()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var req = BookEndpointsFixture.DefaultBook() with
        {
            Title = $"El amor en tiempos del marquez {Guid.NewGuid():N}"
        };
        await _fixture.CreateBookAsync(client, req);

        var resp = await client.GetAsync("/api/books?q=marquez");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await resp.Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        books.Should().NotBeEmpty();
        books.Should().OnlyContain(b =>
            b.Title.Contains("marquez", StringComparison.OrdinalIgnoreCase) ||
            b.Author.Contains("marquez", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task GetBooks_WithMinPriority_ReturnsOnlyHighPriorityBooks()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook(priority: 5));
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook(priority: 4));
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook(priority: 2));

        var resp = await client.GetAsync("/api/books?minPriority=4");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await resp.Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        books.Should().OnlyContain(b => b.Priority >= 4);
    }

    [Fact]
    public async Task GetBooks_WithGenreAndIsReadFilters_AppliesBothFilters()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook("Clasico"));
        var readClasico = await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook("Clasico"));
        await _fixture.CreateBookAsync(client, BookEndpointsFixture.DefaultBook("Novela contemporanea"));
        await client.PostAsJsonAsync($"/api/books/{readClasico.Id}/read", new { });

        var resp = await client.GetAsync("/api/books?genre=Clasico&isRead=false");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var books = await resp.Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        books.Should().OnlyContain(b => b.Genre == "Clasico" && !b.IsRead);
    }

    // ── POST /api/books ─────────────────────────────────────────────────────────

    [Fact]
    public async Task CreateBook_ValidRequest_Returns201WithBookData()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var req = BookEndpointsFixture.DefaultBook();

        var resp = await client.PostAsJsonAsync("/api/books", req);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var book = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        book.Should().NotBeNull();
        book!.Id.Should().BeGreaterThan(0);
        book.Title.Should().Be(req.Title);
        book.Author.Should().Be(req.Author);
        book.Genre.Should().Be(req.Genre);
        book.IsRead.Should().BeFalse();
    }

    [Fact]
    public async Task CreateBook_InvalidGenre_Returns422WithGenreError()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var req = BookEndpointsFixture.DefaultBook("GeneroInvalido");

        var resp = await client.PostAsJsonAsync("/api/books", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        var json = await resp.Content.ReadFromJsonAsync<JsonElement>(JsonOpts);
        json.GetProperty("errors").TryGetProperty("genre", out _).Should().BeTrue();
    }

    [Fact]
    public async Task CreateBook_UserIdComesFromJwtNotBody_Returns201WithCorrectUserId()
    {
        var (client, auth) = await _fixture.RegisterAndLoginAsync();

        var resp = await client.PostAsJsonAsync("/api/books", BookEndpointsFixture.DefaultBook());

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var book = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        book!.UserId.Should().Be(auth.UserId);
    }

    [Fact]
    public async Task CreateBook_BodyWithIsReadTrue_BookCreatedWithIsReadFalse()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var body = new
        {
            title            = $"Libro {Guid.NewGuid():N}",
            author           = "Autor Test",
            genre            = "Clasico",
            country          = "Colombia",
            priority         = 3,
            mentalEnergy     = "Baja - cualquier momento",
            recommendedMood  = "Analitico / quiero aprender algo",
            rotationCategory = "Clasico",
            isRead           = true
        };

        var resp = await client.PostAsJsonAsync("/api/books", body);

        resp.StatusCode.Should().Be(HttpStatusCode.Created);
        var book = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        book!.IsRead.Should().BeFalse();
    }

    // ── PUT /api/books/{id} ─────────────────────────────────────────────────────

    [Fact]
    public async Task UpdateBook_ValidRequest_Returns200WithUpdatedBookAndNewerUpdatedAt()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);

        var updateReq = new UpdateBookRequest(
            Title:            "Titulo Actualizado",
            Author:           book.Author,
            Genre:            book.Genre,
            Country:          book.Country,
            WhyRead:          null,
            Priority:         5,
            MentalEnergy:     book.MentalEnergy,
            RecommendedMood:  book.RecommendedMood,
            RotationCategory: book.RotationCategory,
            Notes:            null);

        var resp = await client.PutAsJsonAsync($"/api/books/{book.Id}", updateReq);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        updated!.Title.Should().Be("Titulo Actualizado");
        updated.Priority.Should().Be(5);
        updated.UpdatedAt.Should().BeOnOrAfter(book.CreatedAt);
    }

    // ── DELETE /api/books/{id} ──────────────────────────────────────────────────

    [Fact]
    public async Task DeleteBook_OwnBook_Returns204()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);

        var resp = await client.DeleteAsync($"/api/books/{book.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task DeleteBook_OwnBook_RemovesFromReadingQueueInDb()
    {
        var (client, auth) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await _fixture.ExecuteSqlAsync(
            "INSERT INTO ReadingQueue (UserId, BookId, Position) VALUES (@UserId, @BookId, 1)",
            new { UserId = auth.UserId, BookId = book.Id });

        await client.DeleteAsync($"/api/books/{book.Id}");

        var count = await _fixture.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM ReadingQueue WHERE BookId = @BookId",
            new { BookId = book.Id });
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteBook_OwnBook_RemovesFromAISuggestionsInDb()
    {
        var (client, auth) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await _fixture.ExecuteSqlAsync(
            "INSERT INTO AISuggestions (UserId, BookId, Reasoning, Score) VALUES (@UserId, @BookId, 'test', 0)",
            new { UserId = auth.UserId, BookId = book.Id });

        await client.DeleteAsync($"/api/books/{book.Id}");

        var count = await _fixture.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM AISuggestions WHERE BookId = @BookId",
            new { BookId = book.Id });
        count.Should().Be(0);
    }

    // ── POST /api/books/{id}/read ───────────────────────────────────────────────

    [Fact]
    public async Task MarkAsRead_WithReadAtAndNotes_Returns200AndConfirmsInDb()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book   = await _fixture.CreateBookAsync(client);
        var readAt = DateTime.UtcNow.AddDays(-1);

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/read",
            new { readAt, notes = "Muy bueno" });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        result!.IsRead.Should().BeTrue();
        result.Notes.Should().Be("Muy bueno");

        var dbIsRead = await _fixture.QueryScalarAsync<bool>(
            "SELECT IsRead FROM Books WHERE Id = @Id", new { Id = book.Id });
        dbIsRead.Should().BeTrue();
    }

    [Fact]
    public async Task MarkAsRead_NoReadAt_Returns200WithIsReadTrueAndReadAtApproxNow()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book   = await _fixture.CreateBookAsync(client);
        var before = DateTime.UtcNow;

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/read", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        result!.IsRead.Should().BeTrue();
        result.ReadAt.Should().NotBeNull();
        result.ReadAt!.Value.Should().BeOnOrAfter(before.AddSeconds(-5));
        result.ReadAt.Value.Should().BeOnOrBefore(DateTime.UtcNow.AddSeconds(5));
    }

    [Fact]
    public async Task MarkAsRead_RemovesFromReadingQueueInDb()
    {
        var (client, auth) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await _fixture.ExecuteSqlAsync(
            "INSERT INTO ReadingQueue (UserId, BookId, Position) VALUES (@UserId, @BookId, 1)",
            new { UserId = auth.UserId, BookId = book.Id });

        await client.PostAsJsonAsync($"/api/books/{book.Id}/read", new { });

        var count = await _fixture.QueryScalarAsync<int>(
            "SELECT COUNT(*) FROM ReadingQueue WHERE BookId = @BookId",
            new { BookId = book.Id });
        count.Should().Be(0);
    }

    [Fact]
    public async Task MarkAsRead_AlreadyRead_Returns200()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await client.PostAsJsonAsync($"/api/books/{book.Id}/read", new { });

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/read", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    // ── POST /api/books/{id}/unread ─────────────────────────────────────────────

    [Fact]
    public async Task MarkAsUnread_ReadBook_Returns200WithIsReadFalseAndReadAtNull()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await client.PostAsJsonAsync($"/api/books/{book.Id}/read",
            new { readAt = DateTime.UtcNow });

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/unread", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        result!.IsRead.Should().BeFalse();
        result.ReadAt.Should().BeNull();

        var dbReadAt = await _fixture.QueryScalarAsync<DateTime?>(
            "SELECT ReadAt FROM Books WHERE Id = @Id", new { Id = book.Id });
        dbReadAt.Should().BeNull();
    }

    [Fact]
    public async Task MarkAsUnread_NotesArePreserved()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await client.PostAsJsonAsync($"/api/books/{book.Id}/read",
            new { notes = "Notas importantes" });

        var resp = await client.PostAsJsonAsync($"/api/books/{book.Id}/unread", new { });

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<BookResponse>(JsonOpts);
        result!.Notes.Should().Be("Notas importantes");
    }

    // ── GET /api/books/reference ────────────────────────────────────────────────

    [Fact]
    public async Task GetReferenceGenres_Returns200WithExactly7Genres()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();

        var resp = await client.GetAsync("/api/books/reference/genres");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var genres = await resp.Content.ReadFromJsonAsync<string[]>(JsonOpts);
        genres.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetReferenceMentalEnergy_Returns200With5LevelsOrderedBySortOrder()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();

        var resp = await client.GetAsync("/api/books/reference/mental-energy");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var levels = await resp.Content.ReadFromJsonAsync<string[]>(JsonOpts);
        levels.Should().HaveCount(5);
        levels![0].Should().Be("Baja - cualquier momento");
    }

    [Fact]
    public async Task GetReferenceGenres_SecondCall_Returns200SameResponse()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();

        var resp1 = await client.GetAsync("/api/books/reference/genres");
        var resp2 = await client.GetAsync("/api/books/reference/genres");

        resp1.StatusCode.Should().Be(HttpStatusCode.OK);
        resp2.StatusCode.Should().Be(HttpStatusCode.OK);
        var genres1 = await resp1.Content.ReadFromJsonAsync<string[]>(JsonOpts);
        var genres2 = await resp2.Content.ReadFromJsonAsync<string[]>(JsonOpts);
        genres2.Should().BeEquivalentTo(genres1!);
    }
}
