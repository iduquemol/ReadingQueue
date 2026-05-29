using System.Data;
using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class MarkBookAsReadTests
{
    private readonly Mock<IBookRepository>      _books   = new();
    private readonly Mock<IDbConnectionFactory> _factory = new();
    private readonly Mock<IDbConnection>        _conn    = new();
    private readonly Mock<IDbTransaction>       _tx      = new();
    private readonly MarkBookAsRead _sut;

    public MarkBookAsReadTests()
    {
        _sut = new MarkBookAsRead(_books.Object, _factory.Object);

        _conn.Setup(c => c.BeginTransaction()).Returns(_tx.Object);
        _conn.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_tx.Object);
        _tx.Setup(t => t.Connection).Returns(_conn.Object);
        _factory.Setup(f => f.Create()).Returns(_conn.Object);
    }

    private Book MakeBook(bool isRead = false)
        => new(7, 42, "Titulo", "Autor", "Clasico", null, "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", isRead, isRead ? DateTime.UtcNow : null, null,
               DateTime.UtcNow, DateTime.UtcNow);

    private void SetupHappyPath(Book? getByIdResult = null)
    {
        var book = getByIdResult ?? MakeBook();
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(book);
        _books.Setup(r => r.MarkAsReadAsync(7, 42, It.IsAny<DateTime>(),
                          It.IsAny<string?>(), _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.RemoveFromQueueIfPresentAsync(7, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
    }

    [Fact]
    public async Task ExecuteAsync_BookNotFound_ThrowsBookNotFoundException()
    {
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync((Book?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new MarkBookAsRead.Command(7, 42, null, null)))
                  .Should().ThrowAsync<BookNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_NullReadAt_UsesApproximateUtcNow()
    {
        SetupHappyPath();
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(MakeBook(isRead: true));

        var before = DateTime.UtcNow;
        await _sut.ExecuteAsync(new MarkBookAsRead.Command(7, 42, null, null));
        var after  = DateTime.UtcNow;

        _books.Verify(r => r.MarkAsReadAsync(7, 42,
            It.Is<DateTime>(d => d >= before && d <= after),
            null, _tx.Object, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ExplicitReadAt_PassesThatDateToRepository()
    {
        SetupHappyPath();
        var explicitDate = new DateTime(2026, 3, 15, 0, 0, 0, DateTimeKind.Utc);
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(MakeBook(isRead: true));

        await _sut.ExecuteAsync(new MarkBookAsRead.Command(7, 42, explicitDate, null));

        _books.Verify(r => r.MarkAsReadAsync(7, 42, explicitDate,
            null, _tx.Object, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_CallsRemoveFromQueueInSameTransaction()
    {
        SetupHappyPath();
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(MakeBook(isRead: true));

        await _sut.ExecuteAsync(new MarkBookAsRead.Command(7, 42, null, null));

        _books.Verify(r => r.RemoveFromQueueIfPresentAsync(7, 42, _tx.Object, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_AlreadyRead_IsIdempotent()
    {
        SetupHappyPath(MakeBook(isRead: true));
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(MakeBook(isRead: true));

        var act = () => _sut.ExecuteAsync(new MarkBookAsRead.Command(7, 42, null, null));

        await act.Should().NotThrowAsync();
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsUpdatedBook()
    {
        var updatedBook = MakeBook(isRead: true);
        SetupHappyPath();
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(updatedBook);

        var result = await _sut.ExecuteAsync(new MarkBookAsRead.Command(7, 42, null, null));

        result.IsRead.Should().BeTrue();
    }
}
