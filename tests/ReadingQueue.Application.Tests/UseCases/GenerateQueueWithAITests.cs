using System.Data;
using FluentAssertions;
using Moq;
using ReadingQueue.Application.Services;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Tests.UseCases;

public class GenerateQueueWithAITests
{
    private readonly Mock<IBookRepository>         _books    = new();
    private readonly Mock<IQueueRepository>        _queue    = new();
    private readonly Mock<IAISuggestionRepository> _suggRepo = new();
    private readonly Mock<ILLMClient>              _llm      = new();
    private readonly Mock<IDbConnectionFactory>    _factory  = new();
    private readonly Mock<IDbConnection>           _conn     = new();
    private readonly Mock<IDbTransaction>          _tx       = new();
    private readonly QueueScoringService           _scoring  = new();
    private readonly GenerateQueueWithAI           _sut;

    public GenerateQueueWithAITests()
    {
        _conn.Setup(c => c.BeginTransaction()).Returns(_tx.Object);
        _conn.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_tx.Object);
        _tx.Setup(t => t.Connection).Returns(_conn.Object);
        _factory.Setup(f => f.Create()).Returns(_conn.Object);

        _sut = new GenerateQueueWithAI(
            _books.Object, _queue.Object, _suggRepo.Object,
            _llm.Object, _scoring, _factory.Object);
    }

    // ── helpers ───────────────────────────────────────────────────────────────

    private static Book MakeBook(int id, int priority = 3, string cat = "Novela grande")
        => new(id, 1, $"Libro {id}", "Autor", "Clasico", null, "Colombia", null,
               priority, "Baja - cualquier momento", "Solemne / quiero leer algo grande",
               cat, false, null, null, DateTime.UtcNow, DateTime.UtcNow);

    private static QueueItem MakeQueueItem(int bookId, int position, string source = "AI")
        => new(bookId, 1, bookId, position, DateTime.UtcNow, source, MakeBook(bookId));

    private void SetupFullFlow(
        int userId,
        IEnumerable<Book> unread,
        IEnumerable<Book>? read = null,
        IEnumerable<BookSuggestion>? suggestions = null,
        IEnumerable<QueueItem>? queueResult = null)
    {
        _books.Setup(r => r.GetUnreadByUserAsync(userId, default)).ReturnsAsync(unread.ToList());
        _books.Setup(r => r.GetReadByUserAsync(userId, default)).ReturnsAsync((read ?? []).ToList());
        _llm.Setup(l => l.GenerateSuggestionsAsync(
                It.IsAny<IEnumerable<Book>>(), It.IsAny<IEnumerable<Book>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(suggestions);
        _queue.Setup(r => r.ReplaceQueueAsync(
                userId, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
            .Returns(Task.CompletedTask);
        _suggRepo.Setup(r => r.SaveSuggestionsAsync(
                It.IsAny<int>(), It.IsAny<IEnumerable<BookSuggestion>>(),
                It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _queue.Setup(r => r.GetByUserAsync(userId, default))
            .ReturnsAsync(queueResult?.ToList() ?? []);
    }

    // ── D1: sin libros no leídos → vacío, sin llamar al LLM ─────────────────

    [Fact]
    public async Task ExecuteAsync_NoUnreadBooks_ReturnsEmptyFalseWithoutCallingLlm()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(1));

        result.Queue.Should().BeEmpty();
        result.AiContributed.Should().BeFalse();
        _llm.Verify(l => l.GenerateSuggestionsAsync(
            It.IsAny<IEnumerable<Book>>(), It.IsAny<IEnumerable<Book>>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── D2: LLM retorna sugerencias → AiContributed = true, source = "AI" ───

    [Fact]
    public async Task ExecuteAsync_LlmReturnsSuggestions_SetsAiContributedAndSourceAI()
    {
        var book = MakeBook(1);
        var suggestion = new BookSuggestion(1, 8.0, "Razon.");

        IEnumerable<(int bookId, int position, string source)>? captured = null;
        SetupFullFlow(1, [book], suggestions: [suggestion]);
        _queue.Setup(r => r.ReplaceQueueAsync(
                1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
            .Callback<int, IEnumerable<(int, int, string)>, IDbTransaction, CancellationToken>(
                (_, items, _, _) => captured = items.ToList())
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(1));

        result.AiContributed.Should().BeTrue();
        captured.Should().NotBeNull();
        captured!.Should().AllSatisfy(item => item.source.Should().Be("AI"));
    }

    // ── D3: LLM retorna null → AiContributed = false, source = "Filter" ─────

    [Fact]
    public async Task ExecuteAsync_LlmReturnsNull_SetsFallbackAndSourceFilter()
    {
        var book = MakeBook(1);

        IEnumerable<(int bookId, int position, string source)>? captured = null;
        SetupFullFlow(1, [book], suggestions: null);
        _queue.Setup(r => r.ReplaceQueueAsync(
                1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
            .Callback<int, IEnumerable<(int, int, string)>, IDbTransaction, CancellationToken>(
                (_, items, _, _) => captured = items.ToList())
            .Returns(Task.CompletedTask);

        var result = await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(1));

        result.AiContributed.Should().BeFalse();
        captured.Should().NotBeNull();
        captured!.Should().AllSatisfy(item => item.source.Should().Be("Filter"));
    }

    // ── D4: LLM retorna sugerencias → SaveSuggestionsAsync con userId correcto

    [Fact]
    public async Task ExecuteAsync_LlmReturnsSuggestions_CallsSaveWithCorrectUserId()
    {
        var book = MakeBook(1);
        var suggestion = new BookSuggestion(1, 8.0, "Razon.");
        SetupFullFlow(42, [book], suggestions: [suggestion]);

        await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(42));

        _suggRepo.Verify(r => r.SaveSuggestionsAsync(
            42,
            It.IsAny<IEnumerable<BookSuggestion>>(),
            It.IsAny<IEnumerable<int>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    // ── D5: LLM retorna null → SaveSuggestionsAsync NO se llama ─────────────

    [Fact]
    public async Task ExecuteAsync_LlmReturnsNull_DoesNotCallSaveSuggestions()
    {
        var book = MakeBook(1);
        SetupFullFlow(1, [book], suggestions: null);

        await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(1));

        _suggRepo.Verify(r => r.SaveSuggestionsAsync(
            It.IsAny<int>(), It.IsAny<IEnumerable<BookSuggestion>>(),
            It.IsAny<IEnumerable<int>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // ── D6: ReplaceQueueAsync lanza → rollback ───────────────────────────────

    [Fact]
    public async Task ExecuteAsync_ReplaceThrows_RollsBackTransaction()
    {
        var book = MakeBook(1);
        SetupFullFlow(1, [book], suggestions: null);
        _queue.Setup(r => r.ReplaceQueueAsync(
                1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
            .ThrowsAsync(new Exception("DB error"));

        await _sut.Invoking(s => s.ExecuteAsync(new GenerateQueueWithAI.Command(1)))
                  .Should().ThrowAsync<Exception>();

        _tx.Verify(t => t.Rollback(), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Never);
    }

    // ── D7: con libros leídos → GetReadByUserAsync con userId correcto ────────

    [Fact]
    public async Task ExecuteAsync_CallsGetReadByUserWithCorrectUserId()
    {
        var book = MakeBook(1);
        SetupFullFlow(42, [book], suggestions: null);

        await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(42));

        _books.Verify(r => r.GetReadByUserAsync(42, default), Times.Once);
    }

    // ── D8: libro con aiScore=10 aparece antes que libro con aiScore=0 ────────

    [Fact]
    public async Task ExecuteAsync_BookWithHighAiScore_AppearsFirstInQueue()
    {
        var book1 = MakeBook(1, priority: 3, cat: "Clasico");
        var book2 = MakeBook(2, priority: 3, cat: "Clasico");
        var suggestion = new BookSuggestion(1, 10.0, "Razon.");  // solo book1 tiene score alto

        IEnumerable<(int bookId, int position, string source)>? captured = null;
        SetupFullFlow(1, [book1, book2], suggestions: [suggestion]);
        _queue.Setup(r => r.ReplaceQueueAsync(
                1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
            .Callback<int, IEnumerable<(int, int, string)>, IDbTransaction, CancellationToken>(
                (_, items, _, _) => captured = items.ToList())
            .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(new GenerateQueueWithAI.Command(1));

        captured.Should().NotBeNull();
        captured!.First().bookId.Should().Be(1);
    }
}
