using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class GetDashboardStats
{
    private readonly IStatsRepository _stats;

    public GetDashboardStats(IStatsRepository stats) => _stats = stats;

    public record Query(int UserId);

    public async Task<DashboardStats> ExecuteAsync(Query q, CancellationToken ct = default)
        => await _stats.GetDashboardAsync(q.UserId, ct);
}
