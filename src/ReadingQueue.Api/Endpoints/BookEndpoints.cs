using ReadingQueue.Api.Extensions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;
using ReadingQueue.Api.Validators;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.ValueObjects;

namespace ReadingQueue.Api.Endpoints;

public static class BookEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var books = app.MapGroup("/api/books").RequireAuthorization().WithTags("Books");

        books.MapGet("/",                  GetAll);
        books.MapGet("/{id:int}",          GetById);
        books.MapPost("/",                 Create);
        books.MapPut("/{id:int}",          Update);
        books.MapDelete("/{id:int}",       Delete);
        books.MapPost("/{id:int}/read",    MarkAsRead);
        books.MapPost("/{id:int}/unread",  MarkAsUnread);

        var reference = books.MapGroup("/reference").WithTags("Reference");
        reference.MapGet("/genres",              GetGenres);
        reference.MapGet("/mental-energy",       GetMentalEnergy);
        reference.MapGet("/moods",               GetMoods);
        reference.MapGet("/rotation-categories", GetRotationCategories);
        reference.MapGet("/subgenres", GetSubgenresByGenre);
    }

    private static async Task<IResult> GetAll(
        HttpContext ctx,
        GetFilteredBooks useCase,
        string? genre = null, string? country = null, string? mentalEnergy = null,
        string? mood = null, string? rotation = null, int? minPriority = null,
        bool? isRead = null, string? q = null)
    {
        var filter = new BookFilter(genre, country, mentalEnergy, mood, rotation, minPriority, isRead, q);
        var books  = await useCase.ExecuteAsync(new GetFilteredBooks.Query(ctx.GetUserId(), filter));
        return Results.Ok(books.Select(ToResponse));
    }

    private static async Task<IResult> GetById(
        int id, HttpContext ctx, GetBookById useCase)
    {
        var book = await useCase.ExecuteAsync(new GetBookById.Query(id, ctx.GetUserId()));
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> Create(
        CreateBookRequest req,
        HttpContext ctx,
        CreateBook useCase,
        CreateBookRequestValidator validator)
    {
        var validation = await validator.ValidateAsync(req);
        if (!validation.IsValid)
            return Results.UnprocessableEntity(new
            {
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName.ToLower())
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });

        var cmd  = new CreateBook.Command(ctx.GetUserId(), req.Title, req.Author,
            req.Genre, req.Subgenre, req.Country, req.WhyRead, req.Priority,
            req.MentalEnergy, req.RecommendedMood, req.RotationCategory, req.Notes);
        var book = await useCase.ExecuteAsync(cmd);
        return Results.Created($"/api/books/{book.Id}", ToResponse(book));
    }

    private static async Task<IResult> Update(
        int id,
        UpdateBookRequest req,
        HttpContext ctx,
        UpdateBook useCase,
        UpdateBookRequestValidator validator)
    {
        var validation = await validator.ValidateAsync(req);
        if (!validation.IsValid)
            return Results.UnprocessableEntity(new
            {
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName.ToLower())
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });

        var cmd  = new UpdateBook.Command(id, ctx.GetUserId(), req.Title, req.Author,
            req.Genre, req.Subgenre, req.Country, req.WhyRead, req.Priority,
            req.MentalEnergy, req.RecommendedMood, req.RotationCategory, req.Notes);
        var book = await useCase.ExecuteAsync(cmd);
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> Delete(
        int id, HttpContext ctx, DeleteBook useCase)
    {
        await useCase.ExecuteAsync(new DeleteBook.Command(id, ctx.GetUserId()));
        return Results.NoContent();
    }

    private static async Task<IResult> MarkAsRead(
        int id,
        HttpContext ctx,
        MarkBookAsRead useCase,
        MarkAsReadRequest? req = null)
    {
        var cmd  = new MarkBookAsRead.Command(id, ctx.GetUserId(), req?.ReadAt, req?.Notes);
        var book = await useCase.ExecuteAsync(cmd);
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> MarkAsUnread(
        int id, HttpContext ctx, MarkBookAsUnread useCase)
    {
        var book = await useCase.ExecuteAsync(new MarkBookAsUnread.Command(id, ctx.GetUserId()));
        return Results.Ok(ToResponse(book));
    }

    private static async Task<IResult> GetGenres(GetReferenceData useCase)
        => Results.Ok(await useCase.GetGenresAsync());

    private static async Task<IResult> GetMentalEnergy(GetReferenceData useCase)
        => Results.Ok(await useCase.GetMentalEnergyLevelsAsync());

    private static async Task<IResult> GetMoods(GetReferenceData useCase)
        => Results.Ok(await useCase.GetMoodsAsync());

    private static async Task<IResult> GetRotationCategories(GetReferenceData useCase)
        => Results.Ok(await useCase.GetRotationCategoriesAsync());

    private static async Task<IResult> GetSubgenresByGenre(string? genre, GetReferenceData useCase)
    {
        if (string.IsNullOrWhiteSpace(genre))
            return Results.BadRequest(new { error = "genre is required" });

        return Results.Ok(await useCase.GetSubgenresByGenreAsync(genre));
    }

    private static BookResponse ToResponse(Book b) => new(
        b.Id, b.UserId, b.Title, b.Author, b.Genre, b.Subgenre, b.Country, b.WhyRead,
        b.Priority, b.MentalEnergy, b.RecommendedMood, b.RotationCategory,
        b.IsRead, b.ReadAt, b.Notes, b.CreatedAt, b.UpdatedAt);
}
