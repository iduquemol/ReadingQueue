namespace ReadingQueue.Domain.ValueObjects;

public sealed record UpdateBookData(
    string  Title,
    string  Author,
    string  Genre,
    string  Country,
    string? WhyRead,
    int     Priority,
    string  MentalEnergy,
    string  RecommendedMood,
    string  RotationCategory,
    string? Notes
);
