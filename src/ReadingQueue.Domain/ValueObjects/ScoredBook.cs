using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.ValueObjects;

public sealed record ScoredBook(
    Book   Book,
    double NormalizedPriority,
    double VarietyBonus,
    double AiScore,
    double NormalizedAge,
    double CompositeScore
);
