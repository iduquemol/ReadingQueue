using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IRefreshTokenRepository
{
    Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default);
    Task CreateAsync(int userId, string token,
                     DateTime expiresAt, CancellationToken ct = default);
    Task RevokeAsync(string token, CancellationToken ct = default);
}
