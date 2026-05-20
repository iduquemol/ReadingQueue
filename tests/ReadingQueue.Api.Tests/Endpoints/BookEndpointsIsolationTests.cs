using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;

namespace ReadingQueue.Api.Tests.Endpoints;

public class BookEndpointsIsolationTests : IClassFixture<BookEndpointsFixture>
{
    private readonly BookEndpointsFixture _fixture;
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public BookEndpointsIsolationTests(BookEndpointsFixture fixture) => _fixture = fixture;

    [Fact]
    public async Task GetBooks_TwoUsers_EachOnlySeesOwnBooks()
    {
        var (client1, _) = await _fixture.RegisterAndLoginAsync();
        var (client2, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client1);
        await _fixture.CreateBookAsync(client2);

        var books1 = await (await client1.GetAsync("/api/books"))
            .Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);
        var books2 = await (await client2.GetAsync("/api/books"))
            .Content.ReadFromJsonAsync<BookResponse[]>(JsonOpts);

        books1!.Select(b => b.Id).Should()
            .NotIntersectWith(books2!.Select(b => b.Id));
    }

    [Fact]
    public async Task GetBookById_OtherUsersBook_Returns404()
    {
        var (client1, _) = await _fixture.RegisterAndLoginAsync();
        var (client2, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client1);

        var resp = await client2.GetAsync($"/api/books/{book.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateBook_OtherUsersBook_Returns404()
    {
        var (client1, _) = await _fixture.RegisterAndLoginAsync();
        var (client2, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client1);

        var updateReq = new UpdateBookRequest(
            Title:            "Hack",
            Author:           "Hacker",
            Genre:            "Clasico",
            Country:          "Colombia",
            WhyRead:          null,
            Priority:         3,
            MentalEnergy:     "Baja - cualquier momento",
            RecommendedMood:  "Analitico / quiero aprender algo",
            RotationCategory: "Clasico",
            Notes:            null);

        var resp = await client2.PutAsJsonAsync($"/api/books/{book.Id}", updateReq);

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task DeleteBook_OtherUsersBook_Returns404()
    {
        var (client1, _) = await _fixture.RegisterAndLoginAsync();
        var (client2, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client1);

        var resp = await client2.DeleteAsync($"/api/books/{book.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetBooks_NoToken_Returns401()
    {
        var resp = await _fixture.CreateClient().GetAsync("/api/books");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task CreateBook_NoToken_Returns401()
    {
        var resp = await _fixture.CreateClient()
            .PostAsJsonAsync("/api/books", BookEndpointsFixture.DefaultBook());

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
