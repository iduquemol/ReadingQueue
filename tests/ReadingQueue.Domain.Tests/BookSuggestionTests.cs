using FluentAssertions;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Tests;

public class BookSuggestionTests
{
    [Fact]
    public void Constructor_AssignsAllProperties()
    {
        var suggestion = new BookSuggestion(
            BookId:    7,
            Score:     9.2,
            Reasoning: "Excelente complemento para tus lecturas recientes.");

        suggestion.BookId.Should().Be(7);
        suggestion.Score.Should().Be(9.2);
        suggestion.Reasoning.Should().Be("Excelente complemento para tus lecturas recientes.");
    }

    [Fact]
    public void WithExpression_CreatesIndependentCopy()
    {
        var original = new BookSuggestion(1, 8.0, "Razon original.");

        var copy = original with { Score = 5.0 };

        original.Score.Should().Be(8.0);
        copy.Score.Should().Be(5.0);
        copy.BookId.Should().Be(original.BookId);
        copy.Reasoning.Should().Be(original.Reasoning);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(5.0)]
    [InlineData(10.0)]
    public void Score_InValidRange_DoesNotThrow(double score)
    {
        var act = () => new BookSuggestion(1, score, "Razon.");

        act.Should().NotThrow();
    }
}
