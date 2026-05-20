using System.Diagnostics;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Polly;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Infrastructure.LLM;

public sealed class ClaudeClient : ILLMClient
{
    private readonly ClaudeOptions        _options;
    private readonly ILogger<ClaudeClient> _logger;
    private readonly ResiliencePipeline    _pipeline;
    private readonly HttpClient            _httpClient;

    // Constructor principal — usado por el DI container
    public ClaudeClient(
        IOptions<ClaudeOptions>  options,
        ILogger<ClaudeClient>    logger,
        [FromKeyedServices("claude-pipeline")] ResiliencePipeline pipeline)
        : this(options, logger, pipeline, new HttpClient()) { }

    // Constructor interno — usado en tests para inyectar HttpClient (WireMock o mock)
    internal ClaudeClient(
        IOptions<ClaudeOptions>  options,
        ILogger<ClaudeClient>    logger,
        ResiliencePipeline       pipeline,
        HttpClient?              httpClient)
    {
        _options    = options.Value;
        _logger     = logger;
        _pipeline   = pipeline;
        _httpClient = httpClient ?? new HttpClient();
    }

    private string ApiUrl
    {
        get
        {
            var baseUrl = string.IsNullOrWhiteSpace(_options.BaseUrl)
                ? "https://api.anthropic.com"
                : _options.BaseUrl.TrimEnd('/');
            return $"{baseUrl}/v1/messages";
        }
    }

    public async Task<IEnumerable<BookSuggestion>?> GenerateSuggestionsAsync(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks,
        CancellationToken ct = default)
    {
        var readList   = readBooks.ToList();
        var unreadList = unreadBooks.ToList();

        _logger.LogInformation(
            "Llamando a Claude para sugerencias. Libros leidos: {Read}, no leidos: {Unread}.",
            readList.Count, unreadList.Count);

        var sw = Stopwatch.StartNew();

        try
        {
            IEnumerable<BookSuggestion>? result = null;

            await _pipeline.ExecuteAsync(async token =>
            {
                var requestBody = new
                {
                    model      = _options.Model,
                    max_tokens = _options.MaxTokens,
                    system     = SuggestionPromptBuilder.GetSystemPrompt(),
                    messages   = new[]
                    {
                        new
                        {
                            role    = "user",
                            content = SuggestionPromptBuilder.BuildUserMessage(readList, unreadList)
                        }
                    }
                };

                using var request = new HttpRequestMessage(HttpMethod.Post, ApiUrl);
                request.Headers.Add("x-api-key", _options.ApiKey);
                request.Headers.Add("anthropic-version", "2023-06-01");
                request.Content = new StringContent(
                    JsonSerializer.Serialize(requestBody),
                    Encoding.UTF8,
                    "application/json");

                var response = await _httpClient.SendAsync(request, token);
                response.EnsureSuccessStatusCode();

                var body = await response.Content.ReadAsStringAsync(token);
                result = ParseApiResponse(body);
            }, ct);

            sw.Stop();
            _logger.LogInformation(
                "Claude respondio en {Ms}ms. Sugerencias recibidas: {Count}.",
                sw.ElapsedMilliseconds, result?.Count() ?? 0);

            return result;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            sw.Stop();
            _logger.LogError(ex,
                "Error inesperado llamando a Claude tras {Ms}ms. Activando fallback determinístico.",
                sw.ElapsedMilliseconds);
            return null;
        }
    }

    private IEnumerable<BookSuggestion>? ParseApiResponse(string body)
    {
        try
        {
            var doc = JsonDocument.Parse(body);

            if (!doc.RootElement.TryGetProperty("content", out var contentArr))
            {
                _logger.LogWarning(
                    "Respuesta de Claude sin campo 'content'. Fragmento: {Frag}",
                    body[..Math.Min(200, body.Length)]);
                return null;
            }

            var text = contentArr
                .EnumerateArray()
                .Where(el => el.TryGetProperty("type", out var t) && t.GetString() == "text")
                .Select(el => el.TryGetProperty("text", out var t) ? t.GetString() : null)
                .FirstOrDefault();

            if (string.IsNullOrEmpty(text))
            {
                _logger.LogWarning("Claude no retorno contenido de texto en la respuesta.");
                return null;
            }

            return ParseSuggestions(text);
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "JSON invalido en la respuesta de la API. Fragmento: {Frag}",
                body[..Math.Min(200, body.Length)]);
            return null;
        }
    }

    private IEnumerable<BookSuggestion>? ParseSuggestions(string raw)
    {
        try
        {
            var doc = JsonDocument.Parse(raw);

            if (!doc.RootElement.TryGetProperty("suggestions", out var arr))
            {
                _logger.LogWarning(
                    "Claude no retorno 'suggestions'. Fragmento: {Frag}",
                    raw[..Math.Min(200, raw.Length)]);
                return null;
            }

            return arr.EnumerateArray()
                .Select(el => new BookSuggestion(
                    BookId:    el.GetProperty("bookId").GetInt32(),
                    Score:     el.GetProperty("score").GetDouble(),
                    Reasoning: el.GetProperty("reasoning").GetString() ?? string.Empty))
                .ToList();
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex,
                "JSON invalido en sugerencias de Claude. Fragmento: {Frag}",
                raw[..Math.Min(200, raw.Length)]);
            return null;
        }
    }
}
