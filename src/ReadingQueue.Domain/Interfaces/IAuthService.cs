using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Interfaces;

public interface IAuthService
{
    string HashPassword(string plainPassword);
    bool VerifyPassword(string plainPassword, string hash);
    string GenerateAccessToken(User user);
    string GenerateRefreshToken();
}
