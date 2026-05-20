namespace ReadingQueue.Api.Responses;

public sealed record AISuggestionResponse(
    int       BookId,
    string    BookTitle,
    decimal   Score,
    string    Reasoning,
    DateTime  GeneratedAt,
    bool?     WasAccepted
);
