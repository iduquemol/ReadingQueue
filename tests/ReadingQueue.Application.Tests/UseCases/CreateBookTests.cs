using FluentAssertions;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.Tests.UseCases;

public class CreateBookTests
{
    private readonly Mock<IBookRepository>         _books = new();
    private readonly Mock<IReferenceDataRepository> _refs  = new();
    private readonly CreateBook _sut;

    public CreateBookTests()
    {
        _sut = new CreateBook(_books.Object, _refs.Object);

        _refs.Setup(r => r.GetGenresAsync(default))
             .ReturnsAsync(["Clasico", "Cuentos", "Novela contemporanea"]);
        _refs.Setup(r => r.GetMentalEnergyLevelsAsync(default))
             .ReturnsAsync(["Baja - cualquier momento", "Alta - concentracion"]);
        _refs.Setup(r => r.GetMoodsAsync(default))
             .ReturnsAsync(["Analitico / quiero aprender algo", "Curioso / quiero algo fresco"]);
        _refs.Setup(r => r.GetRotationCategoriesAsync(default))
             .ReturnsAsync(["Clasico", "Novela grande"]);
    }

    private static Book MadeBook(int id = 1)
        => new(id, 42, "Titulo", "Autor", "Clasico", "Colombia", null,
               3, "Baja - cualquier momento", "Analitico / quiero aprender algo",
               "Clasico", false, null, null, DateTime.UtcNow, DateTime.UtcNow);

    private CreateBook.Command ValidCommand() => new(
        UserId: 42, Title: "Titulo", Author: "Autor",
        Genre: "Clasico", Country: "Colombia", WhyRead: null,
        Priority: 3, MentalEnergy: "Baja - cualquier momento",
        RecommendedMood: "Analitico / quiero aprender algo",
        RotationCategory: "Clasico", Notes: null);

    [Fact]
    public async Task ExecuteAsync_InvalidGenre_ThrowsValidationExceptionWithFieldGenre()
    {
        var cmd = ValidCommand() with { Genre = "Genero Inventado" };

        await _sut.Invoking(s => s.ExecuteAsync(cmd))
                  .Should().ThrowAsync<ValidationException>()
                  .Where(e => e.Errors.ContainsKey("genre"));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMentalEnergy_ThrowsValidationException()
    {
        var cmd = ValidCommand() with { MentalEnergy = "Energia Invalida" };

        await _sut.Invoking(s => s.ExecuteAsync(cmd))
                  .Should().ThrowAsync<ValidationException>()
                  .Where(e => e.Errors.ContainsKey("mentalEnergy"));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidMood_ThrowsValidationException()
    {
        var cmd = ValidCommand() with { RecommendedMood = "Animo Invalido" };

        await _sut.Invoking(s => s.ExecuteAsync(cmd))
                  .Should().ThrowAsync<ValidationException>()
                  .Where(e => e.Errors.ContainsKey("recommendedMood"));
    }

    [Fact]
    public async Task ExecuteAsync_InvalidRotationCategory_ThrowsValidationException()
    {
        var cmd = ValidCommand() with { RotationCategory = "Rotacion Invalida" };

        await _sut.Invoking(s => s.ExecuteAsync(cmd))
                  .Should().ThrowAsync<ValidationException>()
                  .Where(e => e.Errors.ContainsKey("rotationCategory"));
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_CallsCreateAsyncWithCorrectData()
    {
        _books.Setup(r => r.CreateAsync(42, It.IsAny<CreateBookData>(), default))
              .ReturnsAsync(1);
        _books.Setup(r => r.GetByIdAsync(1, 42, default)).ReturnsAsync(MadeBook());

        await _sut.ExecuteAsync(ValidCommand());

        _books.Verify(r => r.CreateAsync(42, It.Is<CreateBookData>(d =>
            d.Title == "Titulo" && d.Genre == "Clasico"), default), Times.Once);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_UserIdComesFromCommand()
    {
        _books.Setup(r => r.CreateAsync(42, It.IsAny<CreateBookData>(), default))
              .ReturnsAsync(1);
        _books.Setup(r => r.GetByIdAsync(1, 42, default)).ReturnsAsync(MadeBook());

        await _sut.ExecuteAsync(ValidCommand());

        _books.Verify(r => r.CreateAsync(42, It.IsAny<CreateBookData>(), default), Times.Once);
        _books.Verify(r => r.CreateAsync(It.Is<int>(id => id != 42),
            It.IsAny<CreateBookData>(), default), Times.Never);
    }

    [Fact]
    public async Task ExecuteAsync_ValidCommand_ReturnsCreatedBook()
    {
        var book = MadeBook(99);
        _books.Setup(r => r.CreateAsync(42, It.IsAny<CreateBookData>(), default))
              .ReturnsAsync(99);
        _books.Setup(r => r.GetByIdAsync(99, 42, default)).ReturnsAsync(book);

        var result = await _sut.ExecuteAsync(ValidCommand());

        result.Should().Be(book);
    }
}
