using System.Data;
using FluentAssertions;
using Moq;
using ReadingQueue.Application.Services;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class GenerateQueueTests
{
    private readonly Mock<IBookRepository>      _books   = new();
    private readonly Mock<IQueueRepository>     _queue   = new();
    private readonly Mock<IDbConnectionFactory> _factory = new();
    private readonly Mock<IDbConnection>        _conn    = new();
    private readonly Mock<IDbTransaction>       _tx      = new();
    private readonly QueueScoringService        _scoring = new();
    private readonly GenerateQueue              _sut;

    public GenerateQueueTests()
    {
        _conn.Setup(c => c.BeginTransaction()).Returns(_tx.Object);
        _conn.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_tx.Object);
        _tx.Setup(t => t.Connection).Returns(_conn.Object);
        _factory.Setup(f => f.Create()).Returns(_conn.Object);

        _sut = new GenerateQueue(_books.Object, _queue.Object, _scoring, _factory.Object);
    }

    private static Book MakeBook(int id, int priority = 3, string cat = "Novela grande")
        => new(id, 1, $"Libro {id}", "Autor", "Clasico", null, "Colombia", null,
               priority, "Baja - cualquier momento", "Solemne / quiero leer algo grande",
               cat, false, null, null, DateTime.UtcNow, DateTime.UtcNow);

    private static QueueItem MakeQueueItem(int bookId, int position)
        => new(bookId, 1, bookId, position, DateTime.UtcNow, "Filter", MakeBook(bookId));

    [Fact]
    public async Task ExecuteAsync_NoUnreadBooks_CallsReplaceWithEmptyAndReturnsEmpty()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync([]);
        _queue.Setup(r => r.ReplaceQueueAsync(1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
              .Returns(Task.CompletedTask);
        _queue.Setup(r => r.GetByUserAsync(1, default)).ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(new GenerateQueue.Command(1));

        result.Should().BeEmpty();
        _queue.Verify(r => r.ReplaceQueueAsync(
            1,
            It.Is<IEnumerable<(int, int, string)>>(items => !items.Any()),
            _tx.Object, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithBooks_CallsGetUnreadWithCorrectUserId()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(42, default)).ReturnsAsync([MakeBook(1)]);
        _queue.Setup(r => r.ReplaceQueueAsync(42, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
              .Returns(Task.CompletedTask);
        _queue.Setup(r => r.GetByUserAsync(42, default)).ReturnsAsync([MakeQueueItem(1, 1)]);

        await _sut.ExecuteAsync(new GenerateQueue.Command(42));

        _books.Verify(r => r.GetUnreadByUserAsync(42, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_WithBooks_CallsReplaceWithActiveTransaction()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync([MakeBook(1)]);
        _queue.Setup(r => r.ReplaceQueueAsync(1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
              .Returns(Task.CompletedTask);
        _queue.Setup(r => r.GetByUserAsync(1, default)).ReturnsAsync([MakeQueueItem(1, 1)]);

        await _sut.ExecuteAsync(new GenerateQueue.Command(1));

        _queue.Verify(r => r.ReplaceQueueAsync(
            1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_With30Books_PersistsMaximum20Items()
    {
        var books = Enumerable.Range(1, 30).Select(i => MakeBook(i)).ToList();
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync(books);

        IEnumerable<(int, int, string)>? capturedItems = null;
        _queue.Setup(r => r.ReplaceQueueAsync(1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
              .Callback<int, IEnumerable<(int, int, string)>, IDbTransaction, CancellationToken>(
                  (_, items, _, _) => capturedItems = items.ToList())
              .Returns(Task.CompletedTask);
        _queue.Setup(r => r.GetByUserAsync(1, default)).ReturnsAsync([]);

        await _sut.ExecuteAsync(new GenerateQueue.Command(1));

        capturedItems.Should().HaveCount(20);
    }

    [Fact]
    public async Task ExecuteAsync_ReplaceThrows_RollsBackTransaction()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync([MakeBook(1)]);
        _queue.Setup(r => r.ReplaceQueueAsync(1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
              .ThrowsAsync(new Exception("DB error"));

        await _sut.Invoking(s => s.ExecuteAsync(new GenerateQueue.Command(1)))
                  .Should().ThrowAsync<Exception>();

        _tx.Verify(t => t.Rollback(), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_GeneratedItems_HaveSourceFilter()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync([MakeBook(1), MakeBook(2)]);

        IEnumerable<(int bookId, int position, string source)>? capturedItems = null;
        _queue.Setup(r => r.ReplaceQueueAsync(1, It.IsAny<IEnumerable<(int, int, string)>>(), _tx.Object, default))
              .Callback<int, IEnumerable<(int, int, string)>, IDbTransaction, CancellationToken>(
                  (_, items, _, _) => capturedItems = items.ToList())
              .Returns(Task.CompletedTask);
        _queue.Setup(r => r.GetByUserAsync(1, default)).ReturnsAsync([]);

        await _sut.ExecuteAsync(new GenerateQueue.Command(1));

        capturedItems.Should().AllSatisfy(item => item.source.Should().Be("Filter"));
    }
}
