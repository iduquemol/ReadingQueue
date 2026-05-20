using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetBookById
{
    private readonly IBookRepository _books;

    public GetBookById(IBookRepository books) => _books = books;

    public record Query(int BookId, int UserId);

    public async Task<Book> ExecuteAsync(Query q, CancellationToken ct = default)
    {
        var book = await _books.GetByIdAsync(q.BookId, q.UserId, ct);
        return book ?? throw new BookNotFoundException(q.BookId);
    }
}
