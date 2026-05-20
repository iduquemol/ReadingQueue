namespace ReadingQueue.Domain.Entities;

public sealed class User
{
    public int Id { get; }
    public string Email { get; }
    public string PasswordHash { get; }
    public string DisplayName { get; }
    public DateTime CreatedAt { get; }
    public bool IsActive { get; }

    public User(int id, string email, string passwordHash,
                string displayName, DateTime createdAt, bool isActive)
    {
        Id           = id;
        Email        = email;
        PasswordHash = passwordHash;
        DisplayName  = displayName;
        CreatedAt    = createdAt;
        IsActive     = isActive;
    }
}
