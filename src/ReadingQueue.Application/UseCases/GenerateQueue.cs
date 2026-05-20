using ReadingQueue.Application.Services;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GenerateQueue
{
    private readonly IBookRepository     _books;
    private readonly IQueueRepository    _queue;
    private readonly QueueScoringService _scoring;
    private readonly IDbConnectionFactory _factory;

    public GenerateQueue(IBookRepository books, IQueueRepository queue,
        QueueScoringService scoring, IDbConnectionFactory factory)
    {
        _books   = books;
        _queue   = queue;
        _scoring = scoring;
        _factory = factory;
    }

    public record Command(int UserId);

    public async Task<IReadOnlyList<QueueItem>> ExecuteAsync(
        Command cmd, CancellationToken ct = default)
    {
        var unread = (await _books.GetUnreadByUserAsync(cmd.UserId, ct)).ToList();
        var scored = _scoring.Score(unread, aiScores: null);

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var items = scored.Select((sb, idx) => (sb.Book.Id, idx + 1, "Filter"));
            await _queue.ReplaceQueueAsync(cmd.UserId, items, tx, ct);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        return (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
    }
}
