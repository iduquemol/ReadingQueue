namespace ReadingQueue.Infrastructure.Sql;

internal static class AISuggestionQueries
{
    internal const string Insert = """
        INSERT INTO AISuggestions (UserId, BookId, Reasoning, Score, WasAccepted)
        VALUES (@UserId, @BookId, @Reasoning, @Score, @WasAccepted);
        """;

    internal const string GetLatestByUser = """
        SELECT TOP (@Take)
            s.Id, s.UserId, s.BookId, s.Reasoning, s.Score, s.GeneratedAt, s.WasAccepted,
            b.Title AS BookTitle
        FROM AISuggestions s
        INNER JOIN Books b ON s.BookId = b.Id
        WHERE s.UserId = @UserId
        ORDER BY s.GeneratedAt DESC;
        """;

    internal const string HasGeneratedToday = """
        SELECT CASE WHEN EXISTS (
            SELECT 1 FROM AISuggestions
            WHERE UserId = @UserId
              AND CAST(GeneratedAt AS DATE) = CAST(GETUTCDATE() AS DATE)
        ) THEN 1 ELSE 0 END;
        """;
}
