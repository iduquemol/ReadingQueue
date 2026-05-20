using Dapper;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Sql;

namespace ReadingQueue.Infrastructure.Data;

public sealed class SqlRefreshTokenRepository : IRefreshTokenRepository
{
    private readonly IDbConnectionFactory _factory;

    public SqlRefreshTokenRepository(IDbConnectionFactory factory)
        => _factory = factory;

    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        return await conn.QuerySingleOrDefaultAsync<RefreshToken>(
            RefreshTokenQueries.GetByToken, new { Token = token });
    }

    public async Task CreateAsync(
        int userId, string token, DateTime expiresAt, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(RefreshTokenQueries.Insert,
            new { UserId = userId, Token = token, ExpiresAt = expiresAt });
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        using var conn = _factory.Create();
        await conn.ExecuteAsync(RefreshTokenQueries.Revoke, new { Token = token });
    }
}
