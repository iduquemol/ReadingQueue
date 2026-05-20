using System.Data;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IQueueRepository
{
    Task<IEnumerable<QueueItem>> GetByUserAsync(
        int userId, CancellationToken ct = default);

    Task ReplaceQueueAsync(
        int userId,
        IEnumerable<(int bookId, int position, string source)> items,
        IDbTransaction tx,
        CancellationToken ct = default);

    Task UpdatePositionsAsync(
        int userId,
        IEnumerable<(int bookId, int position)> positions,
        IDbTransaction tx,
        CancellationToken ct = default);

    Task RemoveItemAsync(
        int userId, int bookId, CancellationToken ct = default);

    Task<bool> ContainsBookAsync(
        int userId, int bookId, CancellationToken ct = default);
}
