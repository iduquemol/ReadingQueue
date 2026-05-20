namespace ReadingQueue.Domain.ValueObjects;

public sealed record BookFilter(
    string?  Genre        = null,
    string?  Country      = null,
    string?  MentalEnergy = null,
    string?  Mood         = null,
    string?  Rotation     = null,
    int?     MinPriority  = null,
    bool?    IsRead       = null,
    string?  SearchQuery  = null
);
