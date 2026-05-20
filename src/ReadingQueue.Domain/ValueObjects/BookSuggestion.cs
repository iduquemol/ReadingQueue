namespace ReadingQueue.Domain.ValueObjects;

public sealed record BookSuggestion(
    int    BookId,
    double Score,
    string Reasoning
);
