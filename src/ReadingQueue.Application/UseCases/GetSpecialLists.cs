using ReadingQueue.Application.Services;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GetSpecialLists
{
    private readonly IBookRepository     _books;
    private readonly QueueScoringService _scoring;

    public GetSpecialLists(IBookRepository books, QueueScoringService scoring)
    {
        _books   = books;
        _scoring = scoring;
    }

    public record Query(int UserId);

    public async Task<SpecialLists> ExecuteAsync(Query q, CancellationToken ct = default)
    {
        var allUnread = (await _books.GetUnreadByUserAsync(q.UserId, ct)).ToList();

        var next5 = _scoring.GetNext5(allUnread);

        var whenTired = allUnread
            .Where(b => b.MentalEnergy == "Baja - cualquier momento")
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        var historicalDebt = allUnread
            .Where(b => b.Genre is "Clasico" or "Novela clasica")
            .OrderByDescending(b => b.Priority)
            .ThenBy(b => b.CreatedAt)
            .ToList();

        return new SpecialLists(next5, whenTired, historicalDebt);
    }
}
