using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface IAISuggestionRepository
{
    Task SaveSuggestionsAsync(
        int userId,
        IEnumerable<BookSuggestion> suggestions,
        IEnumerable<int> acceptedBookIds,
        CancellationToken ct = default);

    Task<IEnumerable<AISuggestion>> GetLatestByUserAsync(
        int userId,
        int take = 20,
        CancellationToken ct = default);

    Task<bool> HasGeneratedTodayAsync(int userId, CancellationToken ct = default);
}
