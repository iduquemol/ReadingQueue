using System.Data;
using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class DeleteBookTests
{
    private readonly Mock<IBookRepository>      _books   = new();
    private readonly Mock<IDbConnectionFactory> _factory = new();
    private readonly Mock<IDbConnection>        _conn    = new();
    private readonly Mock<IDbTransaction>       _tx      = new();
    private readonly DeleteBook _sut;

    public DeleteBookTests()
    {
        _sut = new DeleteBook(_books.Object, _factory.Object);

        _conn.Setup(c => c.BeginTransaction()).Returns(_tx.Object);
        _conn.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_tx.Object);
        _tx.Setup(t => t.Connection).Returns(_conn.Object);
        _factory.Setup(f => f.Create()).Returns(_conn.Object);
    }

    private static Book MakeBook()
        => new(5, 42, "Titulo", "Autor", "Clasico", "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", false, null, null, DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_BookNotFound_ThrowsBookNotFoundException()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync((Book?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new DeleteBook.Command(5, 42)))
                  .Should().ThrowAsync<BookNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_BookExists_CallsDeleteFromQueueBeforeDelete()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(MakeBook());
        var sequence = new MockSequence();
        _books.InSequence(sequence)
              .Setup(r => r.DeleteFromQueueAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.InSequence(sequence)
              .Setup(r => r.DeleteFromSuggestionsAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.InSequence(sequence)
              .Setup(r => r.DeleteAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(new DeleteBook.Command(5, 42));

        _books.Verify(r => r.DeleteFromQueueAsync(5, 42, _tx.Object, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_BookExists_CallsDeleteFromSuggestionsBeforeDelete()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(MakeBook());
        _books.Setup(r => r.DeleteFromQueueAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.DeleteFromSuggestionsAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.DeleteAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(new DeleteBook.Command(5, 42));

        _books.Verify(r => r.DeleteFromSuggestionsAsync(5, 42, _tx.Object, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_BookExists_CallsDeleteAsyncWithTransaction()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(MakeBook());
        _books.Setup(r => r.DeleteFromQueueAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.DeleteFromSuggestionsAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.DeleteAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(new DeleteBook.Command(5, 42));

        _books.Verify(r => r.DeleteAsync(5, 42, _tx.Object, default), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_DeleteThrows_RollsBackTransaction()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(MakeBook());
        _books.Setup(r => r.DeleteFromQueueAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.DeleteFromSuggestionsAsync(5, 42, _tx.Object, default))
              .Returns(Task.CompletedTask);
        _books.Setup(r => r.DeleteAsync(5, 42, _tx.Object, default))
              .ThrowsAsync(new Exception("DB error"));

        await _sut.Invoking(s => s.ExecuteAsync(new DeleteBook.Command(5, 42)))
                  .Should().ThrowAsync<Exception>();

        _tx.Verify(t => t.Rollback(), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Never);
    }
}
