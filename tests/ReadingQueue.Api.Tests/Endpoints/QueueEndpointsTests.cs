using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;

namespace ReadingQueue.Api.Tests.Endpoints;

public class QueueEndpointsTests : IClassFixture<QueueEndpointsFixture>
{
    private readonly QueueEndpointsFixture _fixture;

    public QueueEndpointsTests(QueueEndpointsFixture fixture) => _fixture = fixture;

    // ── GET /api/queue ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetQueue_NoItemsInQueue_Returns200WithEmptyArray()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();

        var resp = await client.GetAsync("/api/queue");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await resp.Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);
        items.Should().BeEmpty();
    }

    [Fact]
    public async Task GetQueue_ReturnsItemsOrderedByPositionAsc()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 3);
        await client.PostAsync("/api/queue/generate", null);

        var resp = await client.GetAsync("/api/queue");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var items = await resp.Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);
        items.Should().NotBeEmpty();
        items!.Select(i => i.Position).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task GetQueue_DoesNotReturnReadBooksEvenIfInTable()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await client.PostAsync("/api/queue/generate", null);
        await client.PostAsJsonAsync($"/api/books/{book.Id}/read", new { });

        var resp = await client.GetAsync("/api/queue");

        var items = await resp.Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);
        items.Should().NotContain(i => i.Book.Id == book.Id);
    }

    [Fact]
    public async Task GetQueue_DoesNotReturnOtherUsersItems()
    {
        var (client1, _) = await _fixture.RegisterAndLoginAsync();
        var (client2, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client1, 2);
        await _fixture.CreateManyBooksAsync(client2, 2);
        await client1.PostAsync("/api/queue/generate", null);
        await client2.PostAsync("/api/queue/generate", null);

        var items1 = await (await client1.GetAsync("/api/queue"))
            .Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);
        var items2 = await (await client2.GetAsync("/api/queue"))
            .Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);

        var ids1 = items1!.Select(i => i.Book.Id).ToHashSet();
        var ids2 = items2!.Select(i => i.Book.Id).ToHashSet();
        ids1.Should().NotIntersectWith(ids2);
    }

    // ── POST /api/queue/generate ──────────────────────────────────────────────

    [Fact]
    public async Task GenerateQueue_NoUnreadBooks_Returns200WithEmptyArray()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(QueueEndpointsFixture.JsonOpts);
        result!.Queue.Should().BeEmpty();
    }

    [Fact]
    public async Task GenerateQueue_WithBooks_ReturnsQueueAndPersistsInDb()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 3);

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(QueueEndpointsFixture.JsonOpts);
        result!.Queue.Should().HaveCount(3);

        var getResp = await client.GetAsync("/api/queue");
        var persisted = await getResp.Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);
        persisted.Should().HaveCount(3);
    }

    [Fact]
    public async Task GenerateQueue_With30Books_PersistsMaximum20()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 30);

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(QueueEndpointsFixture.JsonOpts);
        result!.Queue.Should().HaveCount(20);
    }

    // ── PUT /api/queue/reorder ────────────────────────────────────────────────

    [Fact]
    public async Task ReorderQueue_ValidOrder_Returns200AndDbConfirmsPositions()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 3);
        await client.PostAsync("/api/queue/generate", null);

        var queue = await (await client.GetAsync("/api/queue"))
            .Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);

        var reversed = queue!
            .Select((item, idx) => new QueuePositionItem(item.Book.Id, queue.Length - idx))
            .ToList();
        var req = new ReorderQueueRequest(reversed);

        var resp = await client.PutAsJsonAsync("/api/queue/reorder", req);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var reordered = await resp.Content.ReadFromJsonAsync<QueueItemResponse[]>(QueueEndpointsFixture.JsonOpts);
        reordered!.Select(i => i.Position).Should().BeInAscendingOrder();
        reordered.First().Book.Id.Should().Be(reversed.OrderBy(p => p.Position).First().BookId);
    }

    [Fact]
    public async Task ReorderQueue_BookIdNotInQueue_Returns422()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 2);
        await client.PostAsync("/api/queue/generate", null);
        var req = new ReorderQueueRequest([new QueuePositionItem(999999, 1)]);

        var resp = await client.PutAsJsonAsync("/api/queue/reorder", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task ReorderQueue_DuplicatePositions_Returns422()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var b1 = await _fixture.CreateBookAsync(client);
        var b2 = await _fixture.CreateBookAsync(client);
        await client.PostAsync("/api/queue/generate", null);
        var req = new ReorderQueueRequest([
            new QueuePositionItem(b1.Id, 1),
            new QueuePositionItem(b2.Id, 1)
        ]);

        var resp = await client.PutAsJsonAsync("/api/queue/reorder", req);

        resp.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    // ── DELETE /api/queue/{bookId} ────────────────────────────────────────────

    [Fact]
    public async Task DeleteFromQueue_BookInQueue_Returns204AndDbConfirmsDeletion()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await client.PostAsync("/api/queue/generate", null);

        var resp = await client.DeleteAsync($"/api/queue/{book.Id}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent);
        var count = await _fixture.QueryScalarAsync<int>(
            "SELECT COUNT(1) FROM ReadingQueue WHERE BookId = @BookId", new { BookId = book.Id });
        count.Should().Be(0);
    }

    [Fact]
    public async Task DeleteFromQueue_BookNotInQueue_Returns404()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();

        var resp = await client.DeleteAsync("/api/queue/999999");

        resp.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    // ── Auth required ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("GET",    "/api/queue")]
    [InlineData("POST",   "/api/queue/generate")]
    [InlineData("PUT",    "/api/queue/reorder")]
    [InlineData("DELETE", "/api/queue/1")]
    public async Task AllEndpoints_WithoutToken_Return401(string method, string path)
    {
        var client = _fixture.CreateClient();
        using var req = new HttpRequestMessage(new HttpMethod(method), path);

        var resp = await client.SendAsync(req);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
