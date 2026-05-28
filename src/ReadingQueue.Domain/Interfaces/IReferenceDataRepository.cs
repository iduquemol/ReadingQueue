namespace ReadingQueue.Domain.Interfaces;

public interface IReferenceDataRepository
{
    Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct = default);
    Task<IEnumerable<string>> GetSubgenresByGenreAsync(string genre, CancellationToken ct = default);
}
