namespace ReadingQueue.Api.Requests;

public sealed record UpdateBookRequest(
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
