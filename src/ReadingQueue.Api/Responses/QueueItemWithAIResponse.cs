namespace ReadingQueue.Api.Responses;

public sealed record QueueItemWithAIResponse(
    int          Position,
    DateTime     AddedAt,
    string       Source,
    string?      AiReasoning,
    BookResponse Book
);
