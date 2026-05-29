using ReadingQueue.Application.Services;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GenerateQueueWithAI
{
    private readonly IBookRepository          _books;
    private readonly IQueueRepository         _queue;
    private readonly IAISuggestionRepository  _suggRepo;
    private readonly ILLMClient               _llm;
    private readonly QueueScoringService      _scoring;
    private readonly IDbConnectionFactory     _factory;

    public GenerateQueueWithAI(
        IBookRepository books, IQueueRepository queue,
        IAISuggestionRepository suggRepo, ILLMClient llm,
        QueueScoringService scoring, IDbConnectionFactory factory)
    {
        _books    = books;
        _queue    = queue;
        _suggRepo = suggRepo;
        _llm      = llm;
        _scoring  = scoring;
        _factory  = factory;
    }

    public record Command(int UserId);
    public record Result(
        IReadOnlyList<QueueItem>          Queue,
        bool                              AiContributed,
        IReadOnlyDictionary<int, string>  AiReasoningByBookId);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var unread = (await _books.GetUnreadByUserAsync(cmd.UserId, ct)).ToList();
        if (unread.Count == 0) return new Result([], false, new Dictionary<int, string>());

        var limitReached = await _suggRepo.HasGeneratedTodayAsync(cmd.UserId, ct);

        var read        = limitReached ? [] : (await _books.GetReadByUserAsync(cmd.UserId, ct)).ToList();
        var suggestions = limitReached ? null : await _llm.GenerateSuggestionsAsync(read, unread, ct);
        var aiContributed = suggestions is not null;
        var suggList      = suggestions?.ToList();

        IReadOnlyDictionary<int, double> aiScores = aiContributed
            ? suggList!.ToDictionary(s => s.BookId, s => s.Score)
            : new Dictionary<int, double>();

        IReadOnlyDictionary<int, string> reasoningMap = aiContributed
            ? suggList!.ToDictionary(s => s.BookId, s => s.Reasoning)
            : new Dictionary<int, string>();

        var scored   = _scoring.Score(unread, aiScores);
        var accepted = scored.Select(sb => sb.Book.Id).ToList();

        using var conn = _factory.Create();
        conn.Open();
        using var tx = conn.BeginTransaction();
        try
        {
            var items = scored.Select((sb, idx) =>
            {
                var source = aiContributed && reasoningMap.ContainsKey(sb.Book.Id) ? "AI" : "Filter";
                return (sb.Book.Id, idx + 1, source);
            });
            await _queue.ReplaceQueueAsync(cmd.UserId, items, tx, ct);
            if (aiContributed)
                await _suggRepo.SaveSuggestionsAsync(cmd.UserId, suggestions!, accepted, ct);
            tx.Commit();
        }
        catch { tx.Rollback(); throw; }

        var queue = (await _queue.GetByUserAsync(cmd.UserId, ct)).ToList();
        return new Result(queue, aiContributed, reasoningMap);
    }
}
