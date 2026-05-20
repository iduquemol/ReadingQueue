using System.Data;
using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class ReorderQueueTests
{
    private readonly Mock<IQueueRepository>     _queue   = new();
    private readonly Mock<IDbConnectionFactory> _factory = new();
    private readonly Mock<IDbConnection>        _conn    = new();
    private readonly Mock<IDbTransaction>       _tx      = new();
    private readonly ReorderQueue _sut;

    public ReorderQueueTests()
    {
        _conn.Setup(c => c.BeginTransaction()).Returns(_tx.Object);
        _conn.Setup(c => c.BeginTransaction(It.IsAny<IsolationLevel>())).Returns(_tx.Object);
        _tx.Setup(t => t.Connection).Returns(_conn.Object);
        _factory.Setup(f => f.Create()).Returns(_conn.Object);

        _sut = new ReorderQueue(_queue.Object, _factory.Object);
    }

    private static QueueItem MakeItem(int bookId, int position)
        => new(bookId, 1, bookId, position, DateTime.UtcNow, "Filter",
               new Book(bookId, 1, $"Libro {bookId}", "Autor", "Clasico", "Colombia", null,
                        3, "Baja - cualquier momento", "Solemne / quiero leer algo grande",
                        "Clasico", false, null, null, DateTime.UtcNow, DateTime.UtcNow));

    [Fact]
    public async Task ExecuteAsync_DuplicatePositions_ThrowsValidationException()
    {
        var positions = new[]
        {
            new ReorderQueue.QueueItemPosition(1, 1),
            new ReorderQueue.QueueItemPosition(2, 1)
        };

        var ex = await _sut.Invoking(s => s.ExecuteAsync(new ReorderQueue.Command(1, positions)))
                           .Should().ThrowAsync<ValidationException>();

        ex.Which.Errors.Should().ContainKey("positions");
    }

    [Fact]
    public async Task ExecuteAsync_BookIdNotInQueue_ThrowsValidationException()
    {
        _queue.Setup(r => r.GetByUserAsync(1, default))
              .ReturnsAsync([MakeItem(1, 1), MakeItem(2, 2)]);

        var positions = new[]
        {
            new ReorderQueue.QueueItemPosition(1, 2),
            new ReorderQueue.QueueItemPosition(99, 1)  // 99 not in queue
        };

        var ex = await _sut.Invoking(s => s.ExecuteAsync(new ReorderQueue.Command(1, positions)))
                           .Should().ThrowAsync<ValidationException>();

        ex.Which.Errors.Should().ContainKey("bookIds");
    }

    [Fact]
    public async Task ExecuteAsync_ValidPositions_CallsUpdatePositionsWithTransaction()
    {
        _queue.Setup(r => r.GetByUserAsync(1, default))
              .ReturnsAsync([MakeItem(1, 1), MakeItem(2, 2)]);
        _queue.Setup(r => r.UpdatePositionsAsync(1, It.IsAny<IEnumerable<(int, int)>>(), _tx.Object, default))
              .Returns(Task.CompletedTask);

        var positions = new[]
        {
            new ReorderQueue.QueueItemPosition(2, 1),
            new ReorderQueue.QueueItemPosition(1, 2)
        };

        await _sut.ExecuteAsync(new ReorderQueue.Command(1, positions));

        _queue.Verify(r => r.UpdatePositionsAsync(
            1, It.IsAny<IEnumerable<(int, int)>>(), _tx.Object, default), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_UpdatePositionsThrows_RollsBackTransaction()
    {
        _queue.Setup(r => r.GetByUserAsync(1, default))
              .ReturnsAsync([MakeItem(1, 1), MakeItem(2, 2)]);
        _queue.Setup(r => r.UpdatePositionsAsync(1, It.IsAny<IEnumerable<(int, int)>>(), _tx.Object, default))
              .ThrowsAsync(new Exception("DB error"));

        var positions = new[]
        {
            new ReorderQueue.QueueItemPosition(1, 2),
            new ReorderQueue.QueueItemPosition(2, 1)
        };

        await _sut.Invoking(s => s.ExecuteAsync(new ReorderQueue.Command(1, positions)))
                  .Should().ThrowAsync<Exception>();

        _tx.Verify(t => t.Rollback(), Times.Once);
        _tx.Verify(t => t.Commit(), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ValidPositions_ReturnsUpdatedQueue()
    {
        var updatedQueue = new[] { MakeItem(2, 1), MakeItem(1, 2) };
        _queue.SetupSequence(r => r.GetByUserAsync(1, default))
              .ReturnsAsync([MakeItem(1, 1), MakeItem(2, 2)])
              .ReturnsAsync(updatedQueue);
        _queue.Setup(r => r.UpdatePositionsAsync(1, It.IsAny<IEnumerable<(int, int)>>(), _tx.Object, default))
              .Returns(Task.CompletedTask);

        var positions = new[]
        {
            new ReorderQueue.QueueItemPosition(2, 1),
            new ReorderQueue.QueueItemPosition(1, 2)
        };

        var result = await _sut.ExecuteAsync(new ReorderQueue.Command(1, positions));

        result.Should().BeEquivalentTo(updatedQueue);
    }
}
