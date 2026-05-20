using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetBookByIdTests
{
    private readonly Mock<IBookRepository> _books = new();
    private readonly GetBookById _sut;

    public GetBookByIdTests() => _sut = new GetBookById(_books.Object);

    private static Book MakeBook()
        => new(7, 42, "Titulo", "Autor", "Clasico", "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", false, null, null,
               DateTime.UtcNow, DateTime.UtcNow);

    [Fact]
    public async Task ExecuteAsync_BookFound_ReturnsBook()
    {
        var book = MakeBook();
        _books.Setup(r => r.GetByIdAsync(7, 42, default)).ReturnsAsync(book);

        var result = await _sut.ExecuteAsync(new GetBookById.Query(7, 42));

        result.Should().Be(book);
    }

    [Fact]
    public async Task ExecuteAsync_BookNotFound_ThrowsBookNotFoundException()
    {
        _books.Setup(r => r.GetByIdAsync(99, 42, default)).ReturnsAsync((Book?)null);

        await _sut.Invoking(s => s.ExecuteAsync(new GetBookById.Query(99, 42)))
                  .Should().ThrowAsync<BookNotFoundException>()
                  .WithMessage("*99*");
    }
}
