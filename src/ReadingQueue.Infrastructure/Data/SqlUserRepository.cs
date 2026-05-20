using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlUserRepository : IUserRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlUserRepository(IDbConnectionFactory factory)
        => _factory = factory;

    public async Task<User?> GetByEmailAsync(string email, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            UserQueries.GetByEmail, new { Email = email });
    }

    public async Task<User?> GetByIdAsync(int userId, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<User>(
            UserQueries.GetById, new { UserId = userId });
    }

    public async Task<int> CreateAsync(
        string email, string passwordHash, string displayName, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.ExecuteScalarAsync<int>(
            UserQueries.Insert,
            new { Email = email, PasswordHash = passwordHash, DisplayName = displayName });
    }
}
