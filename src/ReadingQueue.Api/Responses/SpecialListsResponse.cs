namespace ReadingQueue.Api.Responses;

public sealed record SpecialListsResponse(
    IReadOnlyList<BookResponse> Next5,
    IReadOnlyList<BookResponse> WhenTired,
    IReadOnlyList<BookResponse> HistoricalDebt
);
