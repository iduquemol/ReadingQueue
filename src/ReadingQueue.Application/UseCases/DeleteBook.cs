using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class DeleteBook
{
    private readonly IBookRepository _books;
    private readonly IDbConnectionFactory _factory;

    public DeleteBook(IBookRepository books, IDbConnectionFactory factory)
    {
        _books   = books;
        _factory = factory;
    }

    public record Command(int BookId, int UserId);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        _ = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await _books.DeleteFromQueueAsync(cmd.BookId, cmd.UserId, tx, ct);
            await _books.DeleteFromSuggestionsAsync(cmd.BookId, cmd.UserId, tx, ct);
            await _books.DeleteAsync(cmd.BookId, cmd.UserId, tx, ct);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }
    }
}
