using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class RemoveFromQueue
{
    private readonly IQueueRepository _queue;

    public RemoveFromQueue(IQueueRepository queue) => _queue = queue;

    public record Command(int UserId, int BookId);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var exists = await _queue.ContainsBookAsync(cmd.UserId, cmd.BookId, ct);
        if (!exists) throw new BookNotFoundException(cmd.BookId);

        await _queue.RemoveItemAsync(cmd.UserId, cmd.BookId, ct);
    }
}
