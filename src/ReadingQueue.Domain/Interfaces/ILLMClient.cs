using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface ILLMClient
{
    /// <summary>
    /// Retorna null si Claude no está disponible o la respuesta no es parseable.
    /// El llamador activa el fallback cuando recibe null.
    /// </summary>
    Task<IEnumerable<BookSuggestion>?> GenerateSuggestionsAsync(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks,
        CancellationToken ct = default);
}
