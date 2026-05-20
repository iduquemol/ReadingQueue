using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class MarkBookAsRead
{
    private readonly IBookRepository _books;
    private readonly IDbConnectionFactory _factory;

    public MarkBookAsRead(IBookRepository books, IDbConnectionFactory factory)
    {
        _books   = books;
        _factory = factory;
    }

    public record Command(int BookId, int UserId, DateTime? ReadAt, string? Notes);

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        _ = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        var readAt = cmd.ReadAt ?? DateTime.UtcNow;

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await _books.MarkAsReadAsync(cmd.BookId, cmd.UserId, readAt, cmd.Notes, tx, ct);
            await _books.RemoveFromQueueIfPresentAsync(cmd.BookId, cmd.UserId, tx, ct);
            tx.Commit();
        }
        catch
        {
            tx.Rollback();
            throw;
        }

        return (await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct))!;
    }
}
