using FluentAssertions;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Domain.Tests;

public class ScoredBookTests
{
    private static Book MakeBook()
        => new(1, 42, "Título", "Autor", "Clasico", "España", null,
               3, "Alta - concentracion", "Analitico / quiero aprender algo",
               "Clasico", false, null, null,
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
               new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

    [Fact]
    public void AiScore_Zero_ConstructsWithoutException()
    {
        var act = () => new ScoredBook(MakeBook(), 0.5, 0.3, 0.0, 0.7, 0.42);

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(0.0, 0.0, 0.0, 0.0)]
    [InlineData(1.0, 1.0, 1.0, 1.0)]
    [InlineData(0.5, 0.5, 0.5, 0.5)]
    [InlineData(0.4, 0.3, 0.2, 0.1)]
    public void CompositeScore_IsWithinRange(
        double normalizedPriority, double varietyBonus, double aiScore, double normalizedAge)
    {
        var composite = normalizedPriority * 0.40
                      + varietyBonus       * 0.30
                      + aiScore            * 0.20
                      + normalizedAge      * 0.10;

        var sb = new ScoredBook(MakeBook(), normalizedPriority, varietyBonus, aiScore, normalizedAge, composite);

        sb.CompositeScore.Should().BeInRange(0.0, 1.0);
    }

    [Fact]
    public void WithExpression_CreatesCopyWithoutAffectingOriginal()
    {
        var original = new ScoredBook(MakeBook(), 0.5, 0.0, 0.0, 0.8, 0.28);

        var modified = original with { VarietyBonus = 1.0, CompositeScore = 0.58 };

        original.VarietyBonus.Should().Be(0.0);
        original.CompositeScore.Should().Be(0.28);
        modified.VarietyBonus.Should().Be(1.0);
        modified.CompositeScore.Should().Be(0.58);
        modified.Book.Should().BeSameAs(original.Book);
    }
}
