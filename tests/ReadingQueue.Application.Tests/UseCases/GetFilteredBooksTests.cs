using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetFilteredBooksTests
{
    private readonly Mock<IBookRepository> _books = new();
    private readonly GetFilteredBooks _sut;

    public GetFilteredBooksTests() => _sut = new GetFilteredBooks(_books.Object);

    private static Book MakeBook(int id = 1)
        => new(id, 42, "Titulo", "Autor", "Clasico", "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", false, null, null,
               DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_DelegatesToRepositoryWithCorrectParams()
    {
        var filter = new BookFilter(Genre: "Clasico", IsRead: false);
        _books.Setup(r => r.GetByUserAsync(42, filter, default))
              .ReturnsAsync([MakeBook()]);

        await _sut.ExecuteAsync(new GetFilteredBooks.Query(42, filter));

        _books.Verify(r => r.GetByUserAsync(42, filter, default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ReturnsExactlyWhatRepositoryReturns()
    {
        var expected = new[] { MakeBook(1), MakeBook(2) };
        var filter   = new BookFilter();
        _books.Setup(r => r.GetByUserAsync(42, filter, default))
              .ReturnsAsync(expected);

        var result = await _sut.ExecuteAsync(new GetFilteredBooks.Query(42, filter));

        result.Should().BeEquivalentTo(expected);
    }
}
