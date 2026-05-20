using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Services;

public sealed class QueueScoringService
{
    private static readonly string[] RotationOrder =
    [
        "Ensayo / no ficcion",
        "Libro corto o cuentos",
        "Clasico",
        "Novela grande",
        "Contemporaneo latinoamericano o raro"
    ];

    public IReadOnlyList<ScoredBook> Score(
        IEnumerable<Book> unreadBooks,
        IReadOnlyDictionary<int, double>? aiScores = null)
    {
        var books = unreadBooks.ToList();
        if (books.Count == 0) return [];

        aiScores ??= new Dictionary<int, double>();

        // Capture now once and use full-day precision to avoid floating-point drift
        var now          = DateTime.UtcNow;
        var booksWithAge = books
            .Select(b => (Book: b, AgeDays: Math.Floor((now - b.CreatedAt).TotalDays)))
            .ToList();
        var maxDays = booksWithAge.Max(x => x.AgeDays);

        var preliminary = booksWithAge.Select(x =>
        {
            var normalizedPriority = (x.Book.Priority - 1) / 4.0;
            // Older books get higher NormalizedAge; same-day books all get 1.0
            var normalizedAge      = maxDays == 0 ? 1.0 : x.AgeDays / maxDays;
            var aiScore            = aiScores.TryGetValue(x.Book.Id, out var s) ? s / 10.0 : 0.0;

            return new ScoredBook(x.Book, normalizedPriority, 0.0, aiScore, normalizedAge, 0.0);
        })
        .OrderByDescending(sb =>
              sb.NormalizedPriority * 0.40
            + sb.AiScore            * 0.20
            + sb.NormalizedAge      * 0.10)
        .ToList();

        var result    = new List<ScoredBook>(Math.Min(books.Count, 20));
        var remaining = preliminary.ToList();
        string? lastCat = null;

        while (result.Count < 20 && remaining.Count > 0)
        {
            var candidate = remaining.FirstOrDefault(sb => sb.Book.RotationCategory != lastCat)
                         ?? remaining.First();

            var hasBonus  = candidate.Book.RotationCategory != lastCat;
            var withBonus = candidate with
            {
                VarietyBonus   = hasBonus ? 1.0 : 0.0,
                CompositeScore = candidate.NormalizedPriority * 0.40
                               + (hasBonus ? 1.0 : 0.0)      * 0.30
                               + candidate.AiScore            * 0.20
                               + candidate.NormalizedAge      * 0.10
            };

            result.Add(withBonus);
            lastCat = candidate.Book.RotationCategory;
            remaining.Remove(candidate);
        }

        return result;
    }

    public IReadOnlyList<Book> GetNext5(IEnumerable<Book> unreadBooks)
        => Score(unreadBooks).Take(5).Select(sb => sb.Book).ToList();
}
