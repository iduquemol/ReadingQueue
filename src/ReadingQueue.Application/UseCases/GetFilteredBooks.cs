using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GetFilteredBooks
{
    private readonly IBookRepository _books;

    public GetFilteredBooks(IBookRepository books) => _books = books;

    public record Query(int UserId, BookFilter Filter);

    public async Task<IEnumerable<Book>> ExecuteAsync(Query q, CancellationToken ct = default)
        => await _books.GetByUserAsync(q.UserId, q.Filter, ct);
}
