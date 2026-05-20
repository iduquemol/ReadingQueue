using Microsoft.Extensions.Caching.Memory;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetReferenceData
{
    private readonly IReferenceDataRepository _refs;
    private readonly IMemoryCache _cache;

    public GetReferenceData(IReferenceDataRepository refs, IMemoryCache cache)
    {
        _refs  = refs;
        _cache = cache;
    }

    public async Task<IEnumerable<string>> GetGenresAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:genres", () => _refs.GetGenresAsync(ct));

    public async Task<IEnumerable<string>> GetMentalEnergyLevelsAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:energy", () => _refs.GetMentalEnergyLevelsAsync(ct));

    public async Task<IEnumerable<string>> GetMoodsAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:moods", () => _refs.GetMoodsAsync(ct));

    public async Task<IEnumerable<string>> GetRotationCategoriesAsync(CancellationToken ct = default)
        => await GetOrCreateAsync("ref:rotations", () => _refs.GetRotationCategoriesAsync(ct));

    private async Task<IEnumerable<string>> GetOrCreateAsync(
        string key, Func<Task<IEnumerable<string>>> factory)
    {
        return await _cache.GetOrCreateAsync(key, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24);
            return await factory();
        }) ?? [];
    }
}
