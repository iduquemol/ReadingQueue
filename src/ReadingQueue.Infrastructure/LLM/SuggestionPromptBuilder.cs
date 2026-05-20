using System.Text.Json;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Infrastructure.LLM;

internal static class SuggestionPromptBuilder
{
    private const string SystemPrompt = """
        Eres un asistente experto en recomendacion de libros.
        Tu unica funcion es analizar el historial de lectura de un usuario
        y sugerir libros de una lista de pendientes, priorizando variedad
        de generos y afinidad con los libros leidos recientemente.
        Responde UNICAMENTE con JSON valido, sin texto adicional,
        sin bloques de codigo markdown, sin explicaciones fuera del JSON.
        El JSON debe tener exactamente esta estructura:
        {"suggestions":[{"bookId":1,"score":8.5,"reasoning":"Razon breve."}]}
        """;

    internal static string GetSystemPrompt() => SystemPrompt;

    internal static string BuildUserMessage(
        IEnumerable<Book> readBooks,
        IEnumerable<Book> unreadBooks)
    {
        var readList = readBooks
            .OrderByDescending(b => b.ReadAt)
            .Take(30)
            .Select(b => new
            {
                id     = b.Id,
                title  = b.Title,
                author = b.Author,
                genre  = b.Genre
            });

        var unreadList = unreadBooks
            .OrderByDescending(b => b.Priority)
            .Take(50)
            .Select(b => new
            {
                id       = b.Id,
                title    = b.Title,
                author   = b.Author,
                genre    = b.Genre,
                priority = b.Priority
            });

        return JsonSerializer.Serialize(new
        {
            readBooks   = readList,
            unreadBooks = unreadList
        });
    }
}
