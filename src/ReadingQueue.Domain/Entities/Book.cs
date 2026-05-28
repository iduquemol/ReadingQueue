namespace ReadingQueue.Domain.Entities;

public sealed class Book
{
    public int       Id               { get; init; }
    public int       UserId           { get; init; }
    public string    Title            { get; init; } = null!;
    public string    Author           { get; init; } = null!;
    public string    Genre            { get; init; } = null!;
    public string?   Subgenre         { get; init; }
    public string    Country          { get; init; } = null!;
    public string?   WhyRead          { get; init; }
    public int       Priority         { get; init; }
    public string    MentalEnergy     { get; init; } = null!;
    public string    RecommendedMood  { get; init; } = null!;
    public string    RotationCategory { get; init; } = null!;
    public bool      IsRead           { get; init; }
    public DateTime? ReadAt           { get; init; }
    public string?   Notes            { get; init; }
    public DateTime  CreatedAt        { get; init; }
    public DateTime  UpdatedAt        { get; init; }

    public Book() { }

    public Book(int id, int userId, string title, string author,
                string genre, string? subgenre, string country, string? whyRead,
                int priority, string mentalEnergy, string recommendedMood,
                string rotationCategory, bool isRead, DateTime? readAt,
                string? notes, DateTime createdAt, DateTime updatedAt)
    {
        Id               = id;
        UserId           = userId;
        Title            = title;
        Author           = author;
        Genre            = genre;
        Subgenre         = subgenre;
        Country          = country;
        WhyRead          = whyRead;
        Priority         = priority;
        MentalEnergy     = mentalEnergy;
        RecommendedMood  = recommendedMood;
        RotationCategory = rotationCategory;
        IsRead           = isRead;
        ReadAt           = readAt;
        Notes            = notes;
        CreatedAt        = createdAt;
        UpdatedAt        = updatedAt;
    }
}
