namespace ReadingQueue.Api.Responses;

public sealed record BookResponse(
    int       Id,
    int       UserId,
    string    Title,
    string    Author,
    string    Genre,
    string    Country,
    string?   WhyRead,
    int       Priority,
    string    MentalEnergy,
    string    RecommendedMood,
    string    RotationCategory,
    bool      IsRead,
    DateTime? ReadAt,
    string?   Notes,
    DateTime  CreatedAt,
    DateTime  UpdatedAt
);
