using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlStatsRepository : IStatsRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlStatsRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<DashboardStats> GetDashboardAsync(
        int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();

        var counts = await conn.QuerySingleAsync<dynamic>(
            StatsQueries.GetCounts, new { UserId = userId });

        int totalBooks  = (int)counts.TotalBooks;
        int readBooks   = (int)counts.ReadBooks;
        int unreadBooks = (int)counts.UnreadBooks;

        var readPercentage = totalBooks == 0
            ? 0.0
            : Math.Round((double)readBooks / totalBooks * 100, 1);

        var byGenre = (await conn.QueryAsync<GenreStat>(
            StatsQueries.GetByGenre, new { UserId = userId })).ToList();

        var byRotation = (await conn.QueryAsync<RotationStat>(
            StatsQueries.GetByRotationCategory, new { UserId = userId })).ToList();

        var byMentalEnergy = (await conn.QueryAsync<MentalEnergyStat>(
            StatsQueries.GetByMentalEnergy, new { UserId = userId })).ToList();

        var byCountry = (await conn.QueryAsync<CountryStat>(
            StatsQueries.GetByCountryTop10, new { UserId = userId })).ToList();

        var topUnread = (await conn.QueryAsync<Book>(
            StatsQueries.GetTopUnreadPriority, new { UserId = userId })).ToList();

        var recentlyRead = (await conn.QueryAsync<Book>(
            StatsQueries.GetRecentlyRead, new { UserId = userId })).ToList();

        return new DashboardStats(
            totalBooks, readBooks, unreadBooks, readPercentage,
            byGenre, byRotation, byMentalEnergy, byCountry,
            topUnread, recentlyRead);
    }
}
