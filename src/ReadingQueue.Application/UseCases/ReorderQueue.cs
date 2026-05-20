using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class ReorderQueue
{
    private readonly IQueueRepository     _queue;
    private readonly IDbConnectionFactory _factory;

    public ReorderQueue(IQueueRepository queue, IDbConnectionFactory factory)
    {
        _queue   = queue;
        _factory = factory;
    }

    public record Command(int UserId, IReadOnlyList<QueueItemPosition> Positions);
    public record QueueItemPosition(int BookId, int Position);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Command cmd, CancellationToken ct = default)
    {
        var positions = cmd.Positions.ToList();

        if (positions.Select(p => p.Position).Distinct().Count() != positions.Count)
            throw new ValidationException("positions", "Hay posiciones duplicadas.");

        var currentQueue   = (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
        var currentBookIds = currentQueue.Select(q => q.BookId).ToHashSet();
        var invalidIds     = positions
            .Select(p => p.BookId)
            .Where(id => !currentBookIds.Contains(id))
            .ToList();

        if (invalidIds.Count > 0)
            throw new ValidationException("bookIds",
                $"Los libros {string.Join(", ", invalidIds)} no están en la cola.");

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            await _queue.UpdatePositionsAsync(
                cmd.UserId,
                positions.Select(p => (p.BookId, p.Position)),
                tx, ct);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        return (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
    }
}
