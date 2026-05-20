using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.ValueObjects;

public sealed record SpecialLists(
    IReadOnlyList<Book> Next5,
    IReadOnlyList<Book> WhenTired,
    IReadOnlyList<Book> HistoricalDebt
);
