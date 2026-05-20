namespace ReadingQueue.Domain.Entities;

public sealed class RefreshToken
{
    public int Id { get; }
    public int UserId { get; }
    public string Token { get; }
    public DateTime ExpiresAt { get; }
    public DateTime CreatedAt { get; }
    public bool IsRevoked { get; }

    public RefreshToken(int id, int userId, string token,
                        DateTime expiresAt, DateTime createdAt, bool isRevoked)
    {
        Id        = id;
        UserId    = userId;
        Token     = token;
        ExpiresAt = expiresAt;
        CreatedAt = createdAt;
        IsRevoked = isRevoked;
    }

    public bool IsValid(DateTime utcNow) => !IsRevoked && ExpiresAt > utcNow;
}
