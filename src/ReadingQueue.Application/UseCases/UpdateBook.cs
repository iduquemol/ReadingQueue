using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Application.UseCases;

public sealed class UpdateBook
{
    private readonly IBookRepository _books;
    private readonly IReferenceDataRepository _refs;

    public UpdateBook(IBookRepository books, IReferenceDataRepository refs)
    {
        _books = books;
        _refs  = refs;
    }

    public record Command(
        int     BookId,
        int     UserId,
        string  Title,
        string  Author,
        string  Genre,
        string  Country,
        string? WhyRead,
        int     Priority,
        string  MentalEnergy,
        string  RecommendedMood,
        string  RotationCategory,
        string? Notes
    );

    public async Task<Book> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        _ = await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct)
            ?? throw new BookNotFoundException(cmd.BookId);

        await ValidateReferenceValuesAsync(cmd, ct);

        var data = new UpdateBookData(cmd.Title, cmd.Author, cmd.Genre, cmd.Country,
                       cmd.WhyRead, cmd.Priority, cmd.MentalEnergy,
                       cmd.RecommendedMood, cmd.RotationCategory, cmd.Notes);
        await _books.UpdateAsync(cmd.BookId, cmd.UserId, data, ct);
        return (await _books.GetByIdAsync(cmd.BookId, cmd.UserId, ct))!;
    }

    private async Task ValidateReferenceValuesAsync(Command cmd, CancellationToken ct)
    {
        var genres    = (await _refs.GetGenresAsync(ct)).ToHashSet();
        var energies  = (await _refs.GetMentalEnergyLevelsAsync(ct)).ToHashSet();
        var moods     = (await _refs.GetMoodsAsync(ct)).ToHashSet();
        var rotations = (await _refs.GetRotationCategoriesAsync(ct)).ToHashSet();

        if (!genres.Contains(cmd.Genre))
            throw new ValidationException("genre",
                $"'{cmd.Genre}' no es un género válido.");
        if (!energies.Contains(cmd.MentalEnergy))
            throw new ValidationException("mentalEnergy",
                $"'{cmd.MentalEnergy}' no es un nivel de energía válido.");
        if (!moods.Contains(cmd.RecommendedMood))
            throw new ValidationException("recommendedMood",
                $"'{cmd.RecommendedMood}' no es un ánimo válido.");
        if (!rotations.Contains(cmd.RotationCategory))
            throw new ValidationException("rotationCategory",
                $"'{cmd.RotationCategory}' no es una categoría de rotación válida.");
    }
}
