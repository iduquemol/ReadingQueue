using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetQueue
{
    private readonly IQueueRepository _queue;

    public GetQueue(IQueueRepository queue) => _queue = queue;

    public record Query(int UserId);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Query q, CancellationToken ct = default)
        => (await _queue.GetByUserAsync(q.UserId, ct)).ToList();
}
