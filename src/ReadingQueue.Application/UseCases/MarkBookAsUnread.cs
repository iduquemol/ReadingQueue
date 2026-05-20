using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class MarkBookAsUnread
{
    private readonly IBookRepository _books;

    public MarkBookAsUnread(IBookRepository books) => _books = books;

    public record Command(int BookId, int UserId);

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        _ = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        await _books.MarkAsUnreadAsync(cmd.BookId, cmd.UserId, ct);
        return (await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct))!;
    }
}
