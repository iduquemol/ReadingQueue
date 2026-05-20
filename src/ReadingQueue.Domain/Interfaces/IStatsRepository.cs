using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Interfaces;

public interface IStatsRepository
{
    Task<DashboardStats> GetDashboardAsync(
        int userId, CancellationToken ct = default);
}
