namespace ReadingQueue.Infrastructure.Sql;

internal static class QueueQueries
{
    internal const string GetByUser = """
        SELECT
            rq.Id,
            rq.UserId,
            rq.BookId,
            rq.Position,
            rq.AddedAt,
            rq.Source,
            b.Id               AS BookId2,
            b.UserId           AS UserId2,
            b.Title,
            b.Author,
            b.Genre,
            b.Country,
            b.WhyRead,
            b.Priority,
            b.MentalEnergy,
            b.RecommendedMood,
            b.RotationCategory,
            b.IsRead,
            b.ReadAt,
            b.Notes,
            b.CreatedAt        AS BookCreatedAt,
            b.UpdatedAt        AS BookUpdatedAt
        FROM ReadingQueue rq
        INNER JOIN Books b ON rq.BookId = b.Id
        WHERE rq.UserId = @UserId
          AND b.IsRead  = 0
        ORDER BY rq.Position ASC;
        """;

    internal const string DeleteByUser = """
        DELETE FROM ReadingQueue WHERE UserId = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO ReadingQueue (UserId, BookId, Position, Source)
        VALUES (@UserId, @BookId, @Position, @Source);
        """;

    internal const string UpdatePosition = """
        UPDATE ReadingQueue
        SET Position = @Position
        WHERE UserId = @UserId AND BookId = @BookId;
        """;

    internal const string DeleteByBook = """
        DELETE FROM ReadingQueue
        WHERE UserId = @UserId AND BookId = @BookId;
        """;

    internal const string ExistsBook = """
        SELECT COUNT(1)
        FROM ReadingQueue
        WHERE UserId = @UserId AND BookId = @BookId;
        """;
}
