namespace ReadingQueue.Domain.ValueObjects;

public sealed record CreateBookData(
    string  Title,
    string  Author,
    string  Genre,
    string  Subgenre,
    string  Country,
    string? WhyRead,
    int     Priority,
    string  MentalEnergy,
    string  RecommendedMood,
    string  RotationCategory,
    string? Notes
);
