using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Moq;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.Tests.UseCases;

public class GetReferenceDataTests
{
    private readonly Mock<IReferenceDataRepository> _refs = new();
    private readonly IMemoryCache _cache = new MemoryCache(new MemoryCacheOptions());
    private readonly GetReferenceData _sut;

    public GetReferenceDataTests()
    {
        _sut = new GetReferenceData(_refs.Object, _cache);

        _refs.Setup(r => r.GetGenresAsync(default))
             .ReturnsAsync(["Clasico", "Cuentos"]);
        _refs.Setup(r => r.GetMentalEnergyLevelsAsync(default))
             .ReturnsAsync(["Baja - cualquier momento"]);
        _refs.Setup(r => r.GetMoodsAsync(default))
             .ReturnsAsync(["Analitico / quiero aprender algo"]);
        _refs.Setup(r => r.GetRotationCategoriesAsync(default))
             .ReturnsAsync(["Clasico"]);
    }

    [Fact]
    public async Task GetGenresAsync_FirstCall_HitsRepository()
    {
        await _sut.GetGenresAsync();

        _refs.Verify(r => r.GetGenresAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetGenresAsync_SecondCall_ServedFromCache()
    {
        await _sut.GetGenresAsync();
        await _sut.GetGenresAsync();

        _refs.Verify(r => r.GetGenresAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetMentalEnergyLevelsAsync_SecondCall_ServedFromCache()
    {
        await _sut.GetMentalEnergyLevelsAsync();
        await _sut.GetMentalEnergyLevelsAsync();

        _refs.Verify(r => r.GetMentalEnergyLevelsAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetMoodsAsync_SecondCall_ServedFromCache()
    {
        await _sut.GetMoodsAsync();
        await _sut.GetMoodsAsync();

        _refs.Verify(r => r.GetMoodsAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetRotationCategoriesAsync_SecondCall_ServedFromCache()
    {
        await _sut.GetRotationCategoriesAsync();
        await _sut.GetRotationCategoriesAsync();

        _refs.Verify(r => r.GetRotationCategoriesAsync(default), Times.Once);
    }

    [Fact]
    public async Task GetGenresAsync_ReturnsRepositoryValues()
    {
        var result = await _sut.GetGenresAsync();

        result.Should().BeEquivalentTo(["Clasico", "Cuentos"]);
    }
}
