using System.Text.Json;
using FluentAssertions;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Infrastructure.LLM;

namespace ReadingQueue.Infrastructure.Tests.LLM;

public class SuggestionPromptBuilderTests
{
    private static Book MakeBook(int id = 1, int priority = 3,
        DateTime? readAt = null, bool isRead = false)
        => new(id, 1, $"Libro {id}", $"Autor {id}",
               "Novela latinoamericana", null, "Colombia", null,
               priority, "Baja - cualquier momento",
               "Solemne / quiero leer algo grande", "Novela grande",
               isRead, readAt,
               null,
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    // ── SystemPrompt ──────────────────────────────────────────────────────────

    [Fact]
    public void GetSystemPrompt_ReturnsNonEmptyString()
    {
        var prompt = SuggestionPromptBuilder.GetSystemPrompt();

        prompt.Should().NotBeNullOrWhiteSpace();
    }

    // ── UserMessage: campo readBooks ──────────────────────────────────────────

    [Fact]
    public void BuildUserMessage_WithReadBooks_IncludesIdTitleAuthorGenre()
    {
        var readBook = MakeBook(id: 7, isRead: true, readAt: DateTime.UtcNow);
        var msg = SuggestionPromptBuilder.BuildUserMessage([readBook], []);

        var doc = JsonDocument.Parse(msg);
        var first = doc.RootElement.GetProperty("readBooks").EnumerateArray().First();

        first.TryGetProperty("id",     out _).Should().BeTrue();
        first.TryGetProperty("title",  out _).Should().BeTrue();
        first.TryGetProperty("author", out _).Should().BeTrue();
        first.TryGetProperty("genre",  out _).Should().BeTrue();
    }

    [Fact]
    public void BuildUserMessage_WithReadBooks_DoesNotIncludePriority()
    {
        var readBook = MakeBook(id: 7, isRead: true, readAt: DateTime.UtcNow);
        var msg = SuggestionPromptBuilder.BuildUserMessage([readBook], []);

        var doc = JsonDocument.Parse(msg);
        var first = doc.RootElement.GetProperty("readBooks").EnumerateArray().First();

        first.TryGetProperty("priority", out _).Should().BeFalse();
    }

    [Fact]
    public void BuildUserMessage_WithoutReadBooks_ReturnsEmptyReadBooksArray()
    {
        var msg = SuggestionPromptBuilder.BuildUserMessage([], [MakeBook()]);

        var doc = JsonDocument.Parse(msg);
        doc.RootElement.GetProperty("readBooks").GetArrayLength().Should().Be(0);
    }

    // ── UserMessage: campo unreadBooks ────────────────────────────────────────

    [Fact]
    public void BuildUserMessage_UnreadBooks_IncludesPriorityField()
    {
        var unread = MakeBook(id: 3, priority: 5);
        var msg = SuggestionPromptBuilder.BuildUserMessage([], [unread]);

        var doc = JsonDocument.Parse(msg);
        var first = doc.RootElement.GetProperty("unreadBooks").EnumerateArray().First();

        first.TryGetProperty("priority", out _).Should().BeTrue();
    }

    [Fact]
    public void BuildUserMessage_With60UnreadBooks_SendsOnly50HighestPriority()
    {
        var books = Enumerable.Range(1, 60)
            .Select(i => MakeBook(id: i, priority: i <= 50 ? 5 : 1))
            .ToList();

        var msg = SuggestionPromptBuilder.BuildUserMessage([], books);

        var doc = JsonDocument.Parse(msg);
        doc.RootElement.GetProperty("unreadBooks").GetArrayLength().Should().Be(50);

        var ids = doc.RootElement.GetProperty("unreadBooks")
            .EnumerateArray()
            .Select(el => el.GetProperty("id").GetInt32())
            .ToList();

        ids.Should().NotContain(i => i > 50);
    }

    [Fact]
    public void BuildUserMessage_With35ReadBooks_SendsOnly30MostRecent()
    {
        var now = DateTime.UtcNow;
        var readBooks = Enumerable.Range(1, 35)
            .Select(i => new Book(i, 1, $"Libro {i}", "Autor", "Genero", null, "Pais",
                null, 3, "Baja - cualquier momento", "Mood", "Clasico",
                true, now.AddDays(-i), null,
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc)))
            .ToList();

        var msg = SuggestionPromptBuilder.BuildUserMessage(readBooks, []);

        var doc = JsonDocument.Parse(msg);
        doc.RootElement.GetProperty("readBooks").GetArrayLength().Should().Be(30);
    }

    // ── Salida JSON ───────────────────────────────────────────────────────────

    [Fact]
    public void BuildUserMessage_OutputIsValidJson()
    {
        var msg = SuggestionPromptBuilder.BuildUserMessage([MakeBook()], [MakeBook(id: 2)]);

        var act = () => JsonDocument.Parse(msg);

        act.Should().NotThrow();
    }
}
