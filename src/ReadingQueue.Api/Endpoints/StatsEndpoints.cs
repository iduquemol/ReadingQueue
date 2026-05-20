using ReadingQueue.Api.Extensions;
using ReadingQueue.Api.Responses;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Api.Endpoints;

public static class StatsEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var stats = app.MapGroup("/api/stats").RequireAuthorization().WithTags("Stats");

        stats.MapGet("/dashboard",     GetDashboard);
        stats.MapGet("/special-lists", GetSpecialLists);
    }

    private static async Task<IResult> GetDashboard(HttpContext ctx, GetDashboardStats useCase)
    {
        var s = await useCase.ExecuteAsync(new GetDashboardStats.Query(ctx.GetUserId()));
        return Results.Ok(new DashboardStatsResponse(
            s.TotalBooks, s.ReadBooks, s.UnreadBooks, s.ReadPercentage,
            s.ByGenre.Select(g => new GenreStatResponse(g.Genre, g.Total, g.Read, g.Unread)).ToList(),
            s.ByRotationCategory.Select(r => new RotationStatResponse(r.Category, r.Total, r.Read, r.Unread)).ToList(),
            s.ByMentalEnergy.Select(m => new MentalEnergyStatResponse(m.Level, m.Total, m.Unread)).ToList(),
            s.ByCountry.Select(c => new CountryStatResponse(c.Country, c.Total)).ToList(),
            s.TopUnreadPriority.Select(BookToResponse).ToList(),
            s.RecentlyRead.Select(BookToResponse).ToList()));
    }

    private static async Task<IResult> GetSpecialLists(HttpContext ctx, GetSpecialLists useCase)
    {
        var lists = await useCase.ExecuteAsync(new GetSpecialLists.Query(ctx.GetUserId()));
        return Results.Ok(new SpecialListsResponse(
            lists.Next5.Select(BookToResponse).ToList(),
            lists.WhenTired.Select(BookToResponse).ToList(),
            lists.HistoricalDebt.Select(BookToResponse).ToList()));
    }

    private static BookResponse BookToResponse(Book b) => new(
        b.Id, b.UserId, b.Title, b.Author, b.Genre, b.Country, b.WhyRead,
        b.Priority, b.MentalEnergy, b.RecommendedMood, b.RotationCategory,
        b.IsRead, b.ReadAt, b.Notes, b.CreatedAt, b.UpdatedAt);
}
