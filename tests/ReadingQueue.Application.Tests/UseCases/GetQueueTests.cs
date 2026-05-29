using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetQueueTests
{
    private readonly Mock<IQueueRepository> _queue = new();
    private readonly GetQueue _sut;

    public GetQueueTests() => _sut = new GetQueue(_queue.Object);

    private static QueueItem MakeItem(int id)
        => new(id, 1, id, id,
               DateTime.UtcNow, "Filter",
               new Book(id, 1, $"Libro {id}", "Autor", "Clasico", null, "Colombia", null,
                        3, "Baja - cualquier momento", "Solemne / quiero leer algo grande",
                        "Clasico", false, null, null, DateTime.UtcNow, DateTime.UtcNow));

    [Fact]
    public async Task ExecuteAsync_DelegatesToRepositoryWithCorrectUserId()
    {
        _queue.Setup(r => r.GetByUserAsync(42, default)).ReturnsAsync([]);

        await _sut.ExecuteAsync(new GetQueue.Query(42));

        _queue.Verify(r => r.GetByUserAsync(42, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExactlyWhatRepositoryReturns()
    {
        var expected = new[] { MakeItem(1), MakeItem(2) };
        _queue.Setup(r => r.GetByUserAsync(1, default)).ReturnsAsync(expected);

        var result = await _sut.ExecuteAsync(new GetQueue.Query(1));

        result.Should().BeEquivalentTo(expected);
    }
}
