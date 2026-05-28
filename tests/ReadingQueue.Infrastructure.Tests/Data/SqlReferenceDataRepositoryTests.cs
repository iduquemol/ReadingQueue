using FluentAssertions;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlReferenceDataRepositoryTests : IClassFixture<BookRepositoryFixture>
{
    private readonly SqlReferenceDataRepository _sut;

    public SqlReferenceDataRepositoryTests(BookRepositoryFixture fixture)
        => _sut = new SqlReferenceDataRepository(fixture.Factory);

    [Fact]
    public async Task GetGenresAsync_Returns7Genres()
    {
        var result = await _sut.GetGenresAsync();

        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetGenresAsync_ContainsClasico()
    {
        var result = await _sut.GetGenresAsync();

        result.Should().Contain("Clasico");
    }

    [Fact]
    public async Task GetMentalEnergyLevelsAsync_Returns5Levels()
    {
        var result = await _sut.GetMentalEnergyLevelsAsync();

        result.Should().HaveCount(5);
    }

    [Fact]
    public async Task GetMentalEnergyLevelsAsync_FirstElementIsLowest()
    {
        var result = (await _sut.GetMentalEnergyLevelsAsync()).ToList();

        result.First().Should().Be("Baja - cualquier momento");
    }

    [Fact]
    public async Task GetMentalEnergyLevelsAsync_LastElementIsHighest()
    {
        var result = (await _sut.GetMentalEnergyLevelsAsync()).ToList();

        result.Last().Should().Be("Maxima - modo lector");
    }

    [Fact]
    public async Task GetMoodsAsync_Returns7Moods()
    {
        var result = await _sut.GetMoodsAsync();

        result.Should().HaveCount(7);
    }

    [Fact]
    public async Task GetRotationCategoriesAsync_Returns10Categories()
    {
        var result = await _sut.GetRotationCategoriesAsync();

        result.Should().HaveCount(10);
    }
}
