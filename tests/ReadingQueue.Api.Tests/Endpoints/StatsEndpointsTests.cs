using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using ReadingQueue.Api.Responses;

namespace ReadingQueue.Api.Tests.Endpoints;

public class StatsEndpointsTests : IClassFixture<QueueEndpointsFixture>
{
    private readonly QueueEndpointsFixture _fixture;

    public StatsEndpointsTests(QueueEndpointsFixture fixture) => _fixture = fixture;

    // ── GET /api/stats/dashboard ──────────────────────────────────────────────

    [Fact]
    public async Task Dashboard_TotalBooksIsCorrect()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 3);

        var resp = await client.GetAsync("/api/stats/dashboard");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var stats = await resp.Content.ReadFromJsonAsync<DashboardStatsResponse>(QueueEndpointsFixture.JsonOpts);
        stats!.TotalBooks.Should().Be(3);
    }

    [Fact]
    public async Task Dashboard_ReadPercentage_IsCorrect()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var book = await _fixture.CreateBookAsync(client);
        await _fixture.CreateManyBooksAsync(client, 3);
        await client.PostAsJsonAsync($"/api/books/{book.Id}/read", new { });

        var resp = await client.GetAsync("/api/stats/dashboard");

        var stats = await resp.Content.ReadFromJsonAsync<DashboardStatsResponse>(QueueEndpointsFixture.JsonOpts);
        stats!.ReadPercentage.Should().Be(25.0);
    }

    [Fact]
    public async Task Dashboard_ByMentalEnergy_IsOrderedBySortOrderAsc()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(mentalEnergy: "Alta - concentracion"));
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(mentalEnergy: "Baja - cualquier momento"));
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(mentalEnergy: "Media - tarde tranquila"));

        var resp = await client.GetAsync("/api/stats/dashboard");

        var stats = await resp.Content.ReadFromJsonAsync<DashboardStatsResponse>(QueueEndpointsFixture.JsonOpts);
        var levels = stats!.ByMentalEnergy.Select(m => m.Level).ToList();
        levels.Should().ContainInOrder(
            "Baja - cualquier momento",
            "Media - tarde tranquila",
            "Alta - concentracion");
    }

    [Fact]
    public async Task Dashboard_ByCountry_ReturnsMaximum10()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        var countries = new[]
        {
            "Colombia", "Argentina", "Mexico", "Espana", "Peru",
            "Chile", "Venezuela", "Bolivia", "Ecuador", "Uruguay", "Paraguay", "Cuba"
        };
        foreach (var country in countries)
            await _fixture.CreateBookAsync(client, new(
                Title:            $"Libro {Guid.NewGuid():N}",
                Author:           "Autor",
                Genre:            "Clasico",
                Country:          country,
                WhyRead:          null,
                Priority:         3,
                MentalEnergy:     "Baja - cualquier momento",
                RecommendedMood:  "Solemne / quiero leer algo grande",
                RotationCategory: "Clasico",
                Notes:            null));

        var resp = await client.GetAsync("/api/stats/dashboard");

        var stats = await resp.Content.ReadFromJsonAsync<DashboardStatsResponse>(QueueEndpointsFixture.JsonOpts);
        stats!.ByCountry.Count.Should().Be(10);
    }

    // ── GET /api/stats/special-lists ──────────────────────────────────────────

    [Fact]
    public async Task SpecialLists_Next5_HasMaximum5Elements()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateManyBooksAsync(client, 10);

        var resp = await client.GetAsync("/api/stats/special-lists");

        resp.StatusCode.Should().Be(HttpStatusCode.OK);
        var lists = await resp.Content.ReadFromJsonAsync<SpecialListsResponse>(QueueEndpointsFixture.JsonOpts);
        lists!.Next5.Count.Should().BeInRange(0, 5);
    }

    [Fact]
    public async Task SpecialLists_WhenTired_OnlyContainsBajaEnergyBooks()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(mentalEnergy: "Baja - cualquier momento"));
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(mentalEnergy: "Alta - concentracion"));
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(mentalEnergy: "Baja - cualquier momento"));

        var resp = await client.GetAsync("/api/stats/special-lists");

        var lists = await resp.Content.ReadFromJsonAsync<SpecialListsResponse>(QueueEndpointsFixture.JsonOpts);
        lists!.WhenTired.Should().HaveCount(2);
        lists.WhenTired.Should().AllSatisfy(b => b.MentalEnergy.Should().Be("Baja - cualquier momento"));
    }

    [Fact]
    public async Task SpecialLists_HistoricalDebt_OnlyContainsClasicOrNovelaClasica()
    {
        var (client, _) = await _fixture.RegisterAndLoginAsync();
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(genre: "Clasico"));
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(genre: "Novela clasica"));
        await _fixture.CreateBookAsync(client, QueueEndpointsFixture.DefaultBook(genre: "Novela contemporanea"));

        var resp = await client.GetAsync("/api/stats/special-lists");

        var lists = await resp.Content.ReadFromJsonAsync<SpecialListsResponse>(QueueEndpointsFixture.JsonOpts);
        lists!.HistoricalDebt.Should().HaveCount(2);
        lists.HistoricalDebt.Should().AllSatisfy(
            b => b.Genre.Should().BeOneOf("Clasico", "Novela clasica"));
    }

    // ── Auth required ─────────────────────────────────────────────────────────

    [Theory]
    [InlineData("/api/stats/dashboard")]
    [InlineData("/api/stats/special-lists")]
    public async Task BothEndpoints_WithoutToken_Return401(string path)
    {
        var client = _fixture.CreateClient();

        var resp = await client.GetAsync(path);

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
