namespace ReadingQueue.Api.Responses;

public sealed record QueueItemResponse(
    int          Position,
    DateTime     AddedAt,
    string       Source,
    BookResponse Book
);
