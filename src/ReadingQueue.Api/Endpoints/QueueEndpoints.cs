using ReadingQueue.Api.Extensions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Api.Endpoints;

public static class QueueEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var queue = app.MapGroup("/api/queue").RequireAuthorization().WithTags("Queue");

        queue.MapGet("/",                   GetQueue);
        queue.MapPost("/generate",          GenerateQueue);
        queue.MapGet("/suggestions",        GetSuggestions);
        queue.MapPut("/reorder",            ReorderQueue);
        queue.MapDelete("/{bookId:int}",    RemoveFromQueue);
    }

    private static async Task<IResult> GetQueue(HttpContext ctx, GetQueue useCase)
    {
        var items = await useCase.ExecuteAsync(new GetQueue.Query(ctx.GetUserId()));
        return Results.Ok(items.Select(ToResponse));
    }

    private static async Task<IResult> GenerateQueue(HttpContext ctx, GenerateQueueWithAI useCase)
    {
        var result = await useCase.ExecuteAsync(new GenerateQueueWithAI.Command(ctx.GetUserId()));
        return Results.Ok(new GenerateQueueResponse(
            result.AiContributed,
            result.Queue.Select(qi => ToAIResponse(qi, result.AiReasoningByBookId)).ToList()));
    }

    private static async Task<IResult> GetSuggestions(
        HttpContext ctx, IAISuggestionRepository suggRepo)
    {
        var suggestions = await suggRepo.GetLatestByUserAsync(ctx.GetUserId());
        return Results.Ok(suggestions.Select(s => new AISuggestionResponse(
            s.BookId, s.BookTitle, s.Score, s.Reasoning, s.GeneratedAt, s.WasAccepted)));
    }

    private static async Task<IResult> ReorderQueue(
        ReorderQueueRequest req, HttpContext ctx, ReorderQueue useCase)
    {
        var positions = req.Positions
            .Select(p => new ReorderQueue.QueueItemPosition(p.BookId, p.Position))
            .ToList();
        var items = await useCase.ExecuteAsync(
            new ReorderQueue.Command(ctx.GetUserId(), positions));
        return Results.Ok(items.Select(ToResponse));
    }

    private static async Task<IResult> RemoveFromQueue(
        int bookId, HttpContext ctx, RemoveFromQueue useCase)
    {
        await useCase.ExecuteAsync(new RemoveFromQueue.Command(ctx.GetUserId(), bookId));
        return Results.NoContent();
    }

    private static QueueItemResponse ToResponse(QueueItem qi) => new(
        qi.Position,
        qi.AddedAt,
        qi.Source,
        BookToResponse(qi.Book));

    private static QueueItemWithAIResponse ToAIResponse(
        QueueItem qi, IReadOnlyDictionary<int, string> reasoningMap) => new(
        qi.Position,
        qi.AddedAt,
        qi.Source,
        reasoningMap.TryGetValue(qi.BookId, out var r) ? r : null,
        BookToResponse(qi.Book));

    private static BookResponse BookToResponse(Book b) => new(
        b.Id, b.UserId, b.Title, b.Author, b.Genre, b.Country, b.WhyRead,
        b.Priority, b.MentalEnergy, b.RecommendedMood, b.RotationCategory,
        b.IsRead, b.ReadAt, b.Notes, b.CreatedAt, b.UpdatedAt);
}
