using FluentAssertions;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Domain.Tests;

public class AISuggestionTests
{
    private static readonly DateTime FixedDate =
        new(2026, 5, 10, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Constructor_AssignsAllProperties()
    {
        var suggestion = new AISuggestion(
            id:          1,
            userId:      42,
            bookId:      7,
            reasoning:   "Complementa tus lecturas recientes.",
            score:       8.5m,
            generatedAt: FixedDate,
            wasAccepted: true);

        suggestion.Id.Should().Be(1);
        suggestion.UserId.Should().Be(42);
        suggestion.BookId.Should().Be(7);
        suggestion.Reasoning.Should().Be("Complementa tus lecturas recientes.");
        suggestion.Score.Should().Be(8.5m);
        suggestion.GeneratedAt.Should().Be(FixedDate);
        suggestion.WasAccepted.Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData(true)]
    [InlineData(false)]
    public void WasAccepted_AcceptsNullTrueAndFalse(bool? wasAccepted)
    {
        var act = () => new AISuggestion(1, 1, 1, "Razon.", 5.0m, FixedDate, wasAccepted);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0.00)]
    [InlineData(5.50)]
    [InlineData(10.00)]
    public void Score_AcceptsRangeZeroToTen(double score)
    {
        var act = () => new AISuggestion(1, 1, 1, "Razon.", (decimal)score, FixedDate, null);

        act.Should().NotThrow();
    }
}
