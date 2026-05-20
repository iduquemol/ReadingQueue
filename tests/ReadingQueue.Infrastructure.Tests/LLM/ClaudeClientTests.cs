using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Polly;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Infrastructure.LLM;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace ReadingQueue.Infrastructure.Tests.LLM;

public class ClaudeClientTests : IDisposable
{
    private readonly WireMockServer _wireMock;
    private readonly IOptions<ClaudeOptions> _opts;
    private readonly ResiliencePipeline _passThrough = new ResiliencePipelineBuilder().Build();

    public ClaudeClientTests()
    {
        _wireMock = WireMockServer.Start();
        _opts = Options.Create(new ClaudeOptions
        {
            ApiKey         = "test-key",
            Model          = "claude-sonnet-4-5",
            MaxTokens      = 100,
            TimeoutSeconds = 5,
            BaseUrl        = _wireMock.Url!
        });
    }

    public void Dispose() => _wireMock.Stop();

    // ── helpers ───────────────────────────────────────────────────────────────

    private static string AnthropicResponse(string text) => $$"""
        {
          "id": "msg_test",
          "type": "message",
          "role": "assistant",
          "content": [{"type": "text", "text": {{System.Text.Json.JsonSerializer.Serialize(text)}}}],
          "model": "claude-sonnet-4-5",
          "stop_reason": "end_turn",
          "usage": {"input_tokens": 10, "output_tokens": 20}
        }
        """;

    private static string ValidSuggestionsJson() =>
        """{"suggestions":[{"bookId":7,"score":9.2,"reasoning":"Excelente complemento."}]}""";

    private ClaudeClient CreateClient(HttpClient? http = null)
        => new(_opts, NullLogger<ClaudeClient>.Instance, _passThrough, http);

    private static List<Book> EmptyBooks() => [];

    // ── CA-01: respuesta JSON válida → lista de BookSuggestion ───────────────

    [Fact]
    public async Task ValidResponse_ReturnsSuggestions()
    {
        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AnthropicResponse(ValidSuggestionsJson())));

        var client = CreateClient();
        var result = await client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks());

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        var s = result!.First();
        s.BookId.Should().Be(7);
        s.Score.Should().Be(9.2);
        s.Reasoning.Should().Be("Excelente complemento.");
    }

    // ── CA-03: respuesta sin campo 'suggestions' → null ──────────────────────

    [Fact]
    public async Task ResponseWithoutSuggestionsKey_ReturnsNull()
    {
        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AnthropicResponse("""{"other":"data"}""")));

        var client = CreateClient();
        var result = await client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks());

        result.Should().BeNull();
    }

    // ── CA-03: texto plano (JSON inválido en la respuesta de API) → null ─────

    [Fact]
    public async Task InvalidJsonInText_ReturnsNull()
    {
        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AnthropicResponse("esto no es JSON")));

        var client = CreateClient();
        var result = await client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks());

        result.Should().BeNull();
    }

    // ── CA-04: HTTP 503 → retorna null (fallback) ─────────────────────────────

    [Fact]
    public async Task Http503_ReturnsNull()
    {
        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create().WithStatusCode(503));

        // pipeline sin retry para que el test sea rápido
        var client = CreateClient();
        var result = await client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks());

        result.Should().BeNull();
    }

    // ── CA-02: timeout → null ────────────────────────────────────────────────

    [Fact]
    public async Task Timeout_ReturnsNull()
    {
        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithDelay(10_000) // 10 segundos — excede el timeout de 5s en las opciones
                .WithStatusCode(200)
                .WithBody(AnthropicResponse(ValidSuggestionsJson())));

        // pipeline con timeout de 1 segundo para que el test no tarde
        var quickPipeline = new ResiliencePipelineBuilder()
            .AddTimeout(TimeSpan.FromSeconds(1))
            .Build();
        var opts = Options.Create(new ClaudeOptions
        {
            ApiKey  = "test-key",
            BaseUrl = _wireMock.Url!
        });
        var client = new ClaudeClient(opts, NullLogger<ClaudeClient>.Instance, quickPipeline);

        var result = await client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks());

        result.Should().BeNull();
    }

    // ── CA-11: CancellationToken cancelado → OperationCanceledException ──────

    [Fact]
    public async Task CancelledToken_ThrowsOperationCanceledException()
    {
        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithDelay(10_000)
                .WithStatusCode(200)
                .WithBody(AnthropicResponse(ValidSuggestionsJson())));

        var cts = new CancellationTokenSource();
        var client = CreateClient();

        // Cancelar antes de que arranque la llamada
        await cts.CancelAsync();

        Func<Task> act = () => client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks(), cts.Token);
        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ── CA-19: ApiKey vacío → constructor no lanza ────────────────────────────

    [Fact]
    public void EmptyApiKey_ConstructorDoesNotThrow()
    {
        var opts = Options.Create(new ClaudeOptions { ApiKey = "" });

        var act = () => new ClaudeClient(opts, NullLogger<ClaudeClient>.Instance, _passThrough);

        act.Should().NotThrow();
    }

    // ── CA-17: source 'AI' reflejado → score afecta el orden ─────────────────

    [Fact]
    public async Task MultipleSuggestions_ReturnedInCorrectOrder()
    {
        var suggestionsJson = """
            {"suggestions":[
              {"bookId":1,"score":3.0,"reasoning":"Bajo."},
              {"bookId":2,"score":9.5,"reasoning":"Alto."}
            ]}
            """;

        _wireMock
            .Given(Request.Create().WithPath("/v1/messages").UsingPost())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody(AnthropicResponse(suggestionsJson)));

        var client = CreateClient();
        var result = (await client.GenerateSuggestionsAsync(EmptyBooks(), EmptyBooks()))!.ToList();

        result.Should().HaveCount(2);
        result[0].BookId.Should().Be(1);
        result[1].BookId.Should().Be(2);
    }
}
