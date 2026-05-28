namespace ReadingQueue.Api.Responses;

using System.Text.Json.Serialization;

public sealed record QueueItemResponse(
    int          Position,
    DateTime     AddedAt,
    string       Source,
    [property: JsonPropertyName("aiReasoning")]
    string?      AiReasoning,
    BookResponse Book
);
