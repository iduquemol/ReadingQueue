using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Api.Endpoints;

public static class HealthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        app.MapGet("/health", (IDbConnectionFactory factory) =>
        {
            try
            {
                using var conn = factory.Create();
                conn.Open();

                return Results.Ok(new
                {
                    status = "ok",
                    database = "reachable",
                    timestamp = DateTimeOffset.UtcNow
                });
            }
            catch
            {
                return Results.Json(
                    new
                    {
                        status = "degraded",
                        database = "unreachable",
                        timestamp = DateTimeOffset.UtcNow
                    },
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        })
        .WithName("GetHealth")
        .WithSummary("Verifica estado del servidor y la base de datos")
        .WithTags("Health")
        .AllowAnonymous();
    }
}
