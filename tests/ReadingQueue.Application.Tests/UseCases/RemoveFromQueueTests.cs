using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class RemoveFromQueueTests
{
    private readonly Mock<IQueueRepository> _queue = new();
    private readonly RemoveFromQueue _sut;

    public RemoveFromQueueTests() => _sut = new RemoveFromQueue(_queue.Object);

    [Fact]
    public async Task ExecuteAsync_BookNotInQueue_ThrowsBookNotFoundException()
    {
        _queue.Setup(r => r.ContainsBookAsync(1, 99, default)).ReturnsAsync(false);

        await _sut.Invoking(s => s.ExecuteAsync(new RemoveFromQueue.Command(1, 99)))
                  .Should().ThrowAsync<BookNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_BookInQueue_CallsRemoveItemAsync()
    {
        _queue.Setup(r => r.ContainsBookAsync(1, 5, default)).ReturnsAsync(true);
        _queue.Setup(r => r.RemoveItemAsync(1, 5, default)).Returns(Task.CompletedTask);

        await _sut.ExecuteAsync(new RemoveFromQueue.Command(1, 5));

        _queue.Verify(r => r.RemoveItemAsync(1, 5, default), Times.Once);
    }
}
