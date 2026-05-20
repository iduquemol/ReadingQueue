using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Tests.UseCases;

public class UpdateBookTests
{
    private readonly Mock<IBookRepository>         _books = new();
    private readonly Mock<IReferenceDataRepository> _refs  = new();
    private readonly UpdateBook _sut;

    public UpdateBookTests()
    {
        _sut = new UpdateBook(_books.Object, _refs.Object);

        _refs.Setup(r => r.GetGenresAsync(default))
             .ReturnsAsync(["Clasico", "Cuentos"]);
        _refs.Setup(r => r.GetMentalEnergyLevelsAsync(default))
             .ReturnsAsync(["Baja - cualquier momento", "Alta - concentracion"]);
        _refs.Setup(r => r.GetMoodsAsync(default))
             .ReturnsAsync(["Analitico / quiero aprender algo"]);
        _refs.Setup(r => r.GetRotationCategoriesAsync(default))
             .ReturnsAsync(["Clasico", "Novela grande"]);
    }

    private static Book MakeBook(int id = 5)
        => new(id, 42, "Titulo", "Autor", "Clasico", "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", false, null, null, DateTime.UtcNow, DateTime.UtcNow);

    private UpdateBook.Command ValidCommand() => new(
        BookId: 5, UserId: 42, Title: "Nuevo Titulo", Author: "Autor",
        Genre: "Clasico", Country: "Colombia", WhyRead: null,
        Priority: 4, MentalEnergy: "Baja - cualquier momento",
        RecommendedMood: "Analitico / quiero aprender algo",
        RotationCategory: "Clasico", Notes: null);

    [Fact]
    public async Task ExecuteAsync_BookNotFound_ThrowsBookNotFoundException()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync((Book?)null);

        await _sut.Invoking(s => s.ExecuteAsync(ValidCommand()))
                  .Should().ThrowAsync<BookNotFoundException>();
    }

    [Fact]
    public async Task ExecuteAsync_InvalidGenre_ThrowsValidationException()
    {
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(MakeBook());
        var cmd = ValidCommand() with { Genre = "Genero Invalido" };

        await _sut.Invoking(s => s.ExecuteAsync(cmd))
                  .Should().ThrowAsync<ValidationException>()
                  .Where(e => e.Errors.ContainsKey("genre"));
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CallsUpdateAsync()
    {
        var updated = MakeBook();
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(MakeBook());
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(updated);

        await _sut.ExecuteAsync(ValidCommand());

        _books.Verify(r => r.UpdateAsync(5, 42, It.IsAny<UpdateBookData>(), default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsUpdatedBook()
    {
        var updated = MakeBook(5);
        _books.Setup(r => r.GetByIdAsync(5, 42, default)).ReturnsAsync(updated);

        var result = await _sut.ExecuteAsync(ValidCommand());

        result.Should().Be(updated);
    }
}
