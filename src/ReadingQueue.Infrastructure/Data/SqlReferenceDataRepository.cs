using Dapper;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlReferenceDataRepository : IReferenceDataRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlReferenceDataRepository(IDbConnectionFactory factory) => _factory = factory;

    public async Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetGenres);
    }

    public async Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetMentalEnergyLevels);
    }

    public async Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetMoods);
    }

    public async Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetRotationCategories);
    }

    public async Task<IEnumerable<string>> GetSubgenresByGenreAsync(string genre, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QueryAsync<string>(ReferenceQueries.GetSubgenresByGenre, new { Genre = genre });
    }
}
