using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetDashboardStatsTests
{
    private readonly Mock<IStatsRepository> _stats = new();
    private readonly GetDashboardStats      _sut;

    public GetDashboardStatsTests() => _sut = new GetDashboardStats(_stats.Object);

    private static DashboardStats MakeStats(int total = 4, int read = 1)
        => new(total, read, total - read, total == 0 ? 0.0 : Math.Round((double)read / total * 100, 1),
               [], [], [], [], [], []);

    [Fact]
    public async Task ExecuteAsync_DelegatesToRepositoryWithCorrectUserId()
    {
        _stats.Setup(r => r.GetDashboardAsync(42, default)).ReturnsAsync(MakeStats());

        await _sut.ExecuteAsync(new GetDashboardStats.Query(42));

        _stats.Verify(r => r.GetDashboardAsync(42, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExactlyWhatRepositoryReturns()
    {
        var expected = MakeStats(10, 3);
        _stats.Setup(r => r.GetDashboardAsync(1, default)).ReturnsAsync(expected);

        var result = await _sut.ExecuteAsync(new GetDashboardStats.Query(1));

        result.Should().BeSameAs(expected);
    }
}
