using FluentAssertions;
using Moq;
using ReadingQueue.Application.Services;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetSpecialListsTests
{
    private readonly Mock<IBookRepository> _books   = new();
    private readonly QueueScoringService   _scoring = new();
    private readonly GetSpecialLists       _sut;

    public GetSpecialListsTests() => _sut = new GetSpecialLists(_books.Object, _scoring);

    private static Book MakeBook(
        int id,
        int priority = 3,
        string genre = "Clasico",
        string mentalEnergy = "Media - momento especifico",
        int daysOld = 0)
    {
        var createdAt = DateTime.UtcNow.AddDays(-daysOld);
        return new Book(id, 1, $"Libro {id}", "Autor", genre, "Colombia", null,
                        priority, mentalEnergy, "Solemne / quiero leer algo grande",
                        "Clasico", false, null, null, createdAt, createdAt);
    }

    [Fact]
    public async Task ExecuteAsync_Next5_ContainsMaximum5Books()
    {
        var books = Enumerable.Range(1, 10).Select(i => MakeBook(i)).ToList();
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync(books);

        var result = await _sut.ExecuteAsync(new GetSpecialLists.Query(1));

        result.Next5.Count.Should().BeInRange(0, 5);
    }

    [Fact]
    public async Task ExecuteAsync_WhenTired_OnlyIncludesBajaEnergyBooks()
    {
        var books = new[]
        {
            MakeBook(1, mentalEnergy: "Baja - cualquier momento"),
            MakeBook(2, mentalEnergy: "Media - momento especifico"),
            MakeBook(3, mentalEnergy: "Alta - concentracion"),
            MakeBook(4, mentalEnergy: "Baja - cualquier momento"),
        };
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync(books);

        var result = await _sut.ExecuteAsync(new GetSpecialLists.Query(1));

        result.WhenTired.Should().AllSatisfy(
            b => b.MentalEnergy.Should().Be("Baja - cualquier momento"));
        result.WhenTired.Should().HaveCount(2);
    }

    [Fact]
    public async Task ExecuteAsync_HistoricalDebt_OnlyIncludesClasicOrNovelaClasica()
    {
        var books = new[]
        {
            MakeBook(1, genre: "Clasico"),
            MakeBook(2, genre: "Novela clasica"),
            MakeBook(3, genre: "Novela latinoamericana"),
            MakeBook(4, genre: "Clasico"),
        };
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync(books);

        var result = await _sut.ExecuteAsync(new GetSpecialLists.Query(1));

        result.HistoricalDebt.Should().HaveCount(3);
        result.HistoricalDebt.Should().AllSatisfy(
            b => b.Genre.Should().BeOneOf("Clasico", "Novela clasica"));
    }

    [Fact]
    public async Task ExecuteAsync_NoUnreadBooks_AllListsAreEmpty()
    {
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync([]);

        var result = await _sut.ExecuteAsync(new GetSpecialLists.Query(1));

        result.Next5.Should().BeEmpty();
        result.WhenTired.Should().BeEmpty();
        result.HistoricalDebt.Should().BeEmpty();
    }

    [Fact]
    public async Task ExecuteAsync_WhenTired_IsOrderedByPriorityDescThenCreatedAtAsc()
    {
        var books = new[]
        {
            MakeBook(1, priority: 3, mentalEnergy: "Baja - cualquier momento", daysOld: 1),
            MakeBook(2, priority: 5, mentalEnergy: "Baja - cualquier momento", daysOld: 5),
            MakeBook(3, priority: 5, mentalEnergy: "Baja - cualquier momento", daysOld: 2),
            MakeBook(4, priority: 1, mentalEnergy: "Baja - cualquier momento", daysOld: 0),
        };
        _books.Setup(r => r.GetUnreadByUserAsync(1, default)).ReturnsAsync(books);

        var result = await _sut.ExecuteAsync(new GetSpecialLists.Query(1));

        // Priority 5 first; among priority 5, oldest createdAt (daysOld=5) first
        result.WhenTired[0].Id.Should().Be(2);  // priority 5, oldest
        result.WhenTired[1].Id.Should().Be(3);  // priority 5, newer
        result.WhenTired[2].Id.Should().Be(1);  // priority 3
        result.WhenTired[3].Id.Should().Be(4);  // priority 1
    }
}
