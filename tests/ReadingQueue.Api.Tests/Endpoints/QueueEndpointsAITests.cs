using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using ReadingQueue.Api.Responses;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;

namespace ReadingQueue.Api.Tests.Endpoints;

public class QueueEndpointsAITests : IClassFixture<QueueEndpointsAIFixture>
{
    private readonly QueueEndpointsAIFixture _fixture;

    public QueueEndpointsAITests(QueueEndpointsAIFixture fixture) => _fixture = fixture;

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string AnthropicResponse(string text) => $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "content": [{"type": "text", "text": {{JsonSerializer.Serialize(text)}}}],
          "model": "claude-sonnet-4-6",
          "stop_reason": "end_turn",
          "usage": {"input_tokens": 10, "output_tokens": 20}
        }
        """;

    private static string SuggestionsJson(int bookId, double score = 9.0) =>
        $$"""{"suggestions":[{"bookId":{{bookId}},"score":{{score}},"reasoning":"Excelente complemento del libro."}]}""";

    private void SetupWireMockSuccess(int bookId) =>
        _fixture.WireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AnthropicResponse(SuggestionsJson(bookId))));

    // ── EA-01: respuesta válida → aiContributed=true, source="AI" ────────────

    [Fact]
    public async Task GenerateQueue_ValidClaudeResponse_ReturnsAiContributedTrue()
    {
        _fixture.WireMock.ResetMappings();
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        SetupWireMockSuccess(book.Id);

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(
            QueueEndpointsAIFixture.JsonOpts);
        result!.AiContributed.Should().BeTrue();
        result.Queue.Should().HaveCount(1);
        result.Queue.First().Source.Should().Be("AI");
        result.Queue.First().AiReasoning.Should().NotBeNullOrEmpty();
    }

    // ── EA-02: timeout → aiContributed=false, source="Filter" ────────────────

    [Fact]
    public async Task GenerateQueue_ClaudeTimeout_ReturnsFallback()
    {
        _fixture.WireMock.ResetMappings();
        _fixture.WireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithDelay(10_000)
                .WithStatusCode(200)
                .WithBody(AnthropicResponse(SuggestionsJson(1))));

        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client);

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(
            QueueEndpointsAIFixture.JsonOpts);
        result!.AiContributed.Should().BeFalse();
        result.Queue.First().Source.Should().Be("Filter");
        result.Queue.First().AiReasoning.Should().BeNull();
    }

    // ── EA-03: JSON inválido en respuesta Claude → aiContributed=false ────────

    [Fact]
    public async Task GenerateQueue_InvalidClaudeJson_ReturnsFallback()
    {
        _fixture.WireMock.ResetMappings();
        _fixture.WireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AnthropicResponse("esto no es JSON")));

        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client);

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(
            QueueEndpointsAIFixture.JsonOpts);
        result!.AiContributed.Should().BeFalse();
    }

    // ── EA-04: HTTP 503 → nunca retorna 500, fallback determinístico ──────────

    [Fact]
    public async Task GenerateQueue_Claude503_Returns200WithFallback()
    {
        _fixture.WireMock.ResetMappings();
        _fixture.WireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client);

        var resp = await client.PostAsync("/api/queue/generate", null);

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await resp.Content.ReadFromJsonAsync<GenerateQueueResponse>(
            QueueEndpointsAIFixture.JsonOpts);
        result!.AiContributed.Should().BeFalse();
    }

    // ── EA-05: sugerencias persisten en AISuggestions ────────────────────────

    [Fact]
    public async Task GenerateQueue_ValidClaudeResponse_PersistsSuggestionsInDb()
    {
        _fixture.WireMock.ResetMappings();
        var (client, auth) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        SetupWireMockSuccess(book.Id);

        await client.PostAsync("/api/queue/generate", null);

        var count = await _fixture.QueryScalarAsync<int>(
            "SELECT COUNT(1) FROM AISuggestions WHERE UserId = @UserId",
            new { UserId = auth.UserId });
        count.Should().Be(1);
    }

    // ── EA-06: WasAccepted=true para libros que entraron en cola ─────────────

    [Fact]
    public async Task GenerateQueue_BookInQueue_SetsWasAcceptedTrue()
    {
        _fixture.WireMock.ResetMappings();
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        SetupWireMockSuccess(book.Id);

        await client.PostAsync("/api/queue/generate", null);

        var wasAccepted = await _fixture.QueryScalarAsync<bool?>(
            "SELECT WasAccepted FROM AISuggestions WHERE BookId = @BookId",
            new { BookId = book.Id });
        wasAccepted.Should().BeTrue();
    }

    // ── EA-07: llamar generate dos veces → filas se acumulan ─────────────────

    [Fact]
    public async Task GenerateQueue_CalledTwice_AccumulatesSuggestionsRows()
    {
        _fixture.WireMock.ResetMappings();
        var (client, auth) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        SetupWireMockSuccess(book.Id);

        await client.PostAsync("/api/queue/generate", null);
        await client.PostAsync("/api/queue/generate", null);

        var count = await _fixture.QueryScalarAsync<int>(
            "SELECT COUNT(1) FROM AISuggestions WHERE UserId = @UserId",
            new { UserId = auth.UserId });
        count.Should().Be(2);
    }

    // ── EA-08: GET /suggestions → 200, máx 20, orden DESC ────────────────────

    [Fact]
    public async Task GetSuggestions_AfterGenerate_ReturnsSuggestionsOrderedDesc()
    {
        _fixture.WireMock.ResetMappings();
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        SetupWireMockSuccess(book.Id);
        await client.PostAsync("/api/queue/generate", null);

        var resp = await client.GetAsync("/api/queue/suggestions");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var suggestions = await resp.Content.ReadFromJsonAsync<AISuggestionResponse[]>(
            QueueEndpointsAIFixture.JsonOpts);
        suggestions.Should().NotBeEmpty();
        suggestions!.First().BookId.Should().Be(book.Id);
        suggestions.First().BookTitle.Should().NotBeNullOrEmpty();
        suggestions.First().Reasoning.Should().Be("Excelente complemento del libro.");
    }

    // ── EA-09: GET /suggestions sin token → 401 ──────────────────────────────

    [Fact]
    public async Task GetSuggestions_WithoutToken_Returns401()
    {
        var resp = await _fixture.CreateClient().GetAsync("/api/queue/suggestions");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
