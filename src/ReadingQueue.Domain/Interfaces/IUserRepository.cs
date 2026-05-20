using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email, CancellationToken ct = default);
    Task<User?> GetByIdAsync(int userId, CancellationToken ct = default);
    Task<int> CreateAsync(string email, string passwordHash,
                          string displayName, CancellationToken ct = default);
}
