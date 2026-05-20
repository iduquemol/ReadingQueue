using FluentAssertions;
using ReadingQueue.Application.Services;
using ReadingQueue.Domain.Entities;

namespace ReadingQueue.Application.Tests.Services;

public class QueueScoringServiceTests
{
    private readonly QueueScoringService _sut = new();

    private static Book MakeBook(
        int id = 1,
        int priority = 3,
        string rotationCategory = "Novela grande",
        int daysOld = 0,
        string mentalEnergy = "Media - momento especifico")
    {
        var createdAt = DateTime.UtcNow.AddDays(-daysOld);
        return new Book(id, 1, $"Libro {id}", "Autor", "Clasico", "Colombia", null,
                        priority, mentalEnergy, "Solemne / quiero leer algo grande",
                        rotationCategory, false, null, null,
                        createdAt, createdAt);
    }

    [Fact]
    public void Score_EmptyList_ReturnsEmpty()
    {
        var result = _sut.Score([]);

        result.Should().BeEmpty();
    }

    [Fact]
    public void Score_HighPriorityAppearsBeforeLowPriority()
    {
        var low  = MakeBook(id: 1, priority: 3, rotationCategory: "Novela grande");
        var high = MakeBook(id: 2, priority: 5, rotationCategory: "Novela grande");

        var result = _sut.Score([low, high]);

        result[0].Book.Id.Should().Be(high.Id);
    }

    [Fact]
    public void Score_OlderBookAppearsBeforeNewerBook_WhenEqualPriorityAndCategory()
    {
        var older = MakeBook(id: 1, priority: 3, rotationCategory: "Novela grande", daysOld: 10);
        var newer = MakeBook(id: 2, priority: 3, rotationCategory: "Novela grande", daysOld: 0);

        var result = _sut.Score([older, newer]);

        result[0].Book.Id.Should().Be(older.Id);
    }

    [Fact]
    public void Score_DoesNotRepeatSameRotationCategory_InConsecutivePositions()
    {
        var books = Enumerable.Range(1, 10)
            .Select(i => MakeBook(i, priority: 3, rotationCategory: i % 2 == 0 ? "Novela grande" : "Clasico"))
            .ToList();

        var result = _sut.Score(books);

        for (var i = 1; i < result.Count; i++)
        {
            result[i].Book.RotationCategory.Should()
                .NotBe(result[i - 1].Book.RotationCategory,
                       $"positions {i - 1} and {i} should have different rotation categories");
        }
    }

    [Fact]
    public void Score_With30UnreadBooks_ReturnsExactly20()
    {
        var books = Enumerable.Range(1, 30)
            .Select(i => MakeBook(i, priority: 3))
            .ToList();

        var result = _sut.Score(books);

        result.Should().HaveCount(20);
    }

    [Fact]
    public void Score_AiScoreIsZero_WhenAiScoresIsNull()
    {
        var books = new[] { MakeBook(1), MakeBook(2) };

        var result = _sut.Score(books, aiScores: null);

        result.Should().AllSatisfy(sb => sb.AiScore.Should().Be(0.0));
    }

    [Fact]
    public void Score_NullAiScores_ProducesSameResultAsEmptyDictionary()
    {
        var books = Enumerable.Range(1, 5).Select(i => MakeBook(i)).ToList();

        var withNull  = _sut.Score(books, aiScores: null);
        var withEmpty = _sut.Score(books, aiScores: new Dictionary<int, double>());

        withNull.Select(sb => sb.Book.Id)
                .Should().Equal(withEmpty.Select(sb => sb.Book.Id));
    }

    [Fact]
    public void GetNext5_ReturnsExactly5_WhenAtLeast5Available()
    {
        var books = Enumerable.Range(1, 10).Select(i => MakeBook(i)).ToList();

        var result = _sut.GetNext5(books);

        result.Should().HaveCount(5);
    }

    [Fact]
    public void GetNext5_ReturnsLessThan5_WhenFewerAvailable()
    {
        var books = new[] { MakeBook(1), MakeBook(2) };

        var result = _sut.GetNext5(books);

        result.Should().HaveCount(2);
    }

    [Fact]
    public void Score_SingleBook_NormalizedAgeIsOne()
    {
        var book = MakeBook(1, daysOld: 5);

        var result = _sut.Score([book]);

        result[0].NormalizedAge.Should().Be(1.0);
    }

    [Fact]
    public void Score_AllBooksCreatedSameDay_NormalizedAgeIsOneForAll()
    {
        var books = Enumerable.Range(1, 5)
            .Select(i => MakeBook(i, daysOld: 0))
            .ToList();

        var result = _sut.Score(books);

        result.Should().AllSatisfy(sb => sb.NormalizedAge.Should().Be(1.0));
    }
}
