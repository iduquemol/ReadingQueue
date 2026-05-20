using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlAISuggestionRepository : IAISuggestionRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlAISuggestionRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task SaveSuggestionsAsync(
        int userId,
        IEnumerable<BookSuggestion> suggestions,
        IEnumerable<int> acceptedBookIds,
        CancellationToken ct = default)
    {
        var accepted = acceptedBookIds.ToHashSet();
        using var conn = _factory.Create();

        foreach (var s in suggestions)
        {
            await conn.ExecuteAsync(AISuggestionQueries.Insert, new
            {
                UserId      = userId,
                BookId      = s.BookId,
                Reasoning   = s.Reasoning,
                Score       = (decimal)s.Score,
                WasAccepted = accepted.Contains(s.BookId)
            });
        }
    }

    public async Task<IEnumerable<AISuggestion>> GetLatestByUserAsync(
        int userId, int take = 20, CancellationToken ct = default)
    {
        using var conn = _factory.Create();

        var rows = await conn.QueryAsync(
            AISuggestionQueries.GetLatestByUser,
            new { UserId = userId, Take = take });

        return rows.Select(r => new AISuggestion(
            id:          (int)r.Id,
            userId:      (int)r.UserId,
            bookId:      (int)r.BookId,
            reasoning:   (string)r.Reasoning,
            score:       (decimal)r.Score,
            generatedAt: (DateTime)r.GeneratedAt,
            wasAccepted: (bool?)r.WasAccepted)
        {
            BookTitle = (string)r.BookTitle
        }).ToList();
    }

    public async Task<bool> HasGeneratedTodayAsync(int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        var result = await conn.ExecuteScalarAsync<int>(
            AISuggestionQueries.HasGeneratedToday,
            new { UserId = userId });
        return result == 1;
    }
}
