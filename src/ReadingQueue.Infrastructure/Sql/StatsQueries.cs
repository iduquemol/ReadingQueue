namespace ReadingQueue.Infrastructure.Sql;

internal static class StatsQueries
{
    internal const string GetCounts = """
        SELECT
            COUNT(*)                                                       AS TotalBooks,
            ISNULL(SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END), 0)        AS ReadBooks,
            ISNULL(SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END), 0)        AS UnreadBooks
        FROM Books
        WHERE UserId = @UserId;
        """;

    internal const string GetByGenre = """
        SELECT
            Genre,
            COUNT(*)                                           AS Total,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END)       AS [Read],
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END)       AS Unread
        FROM Books
        WHERE UserId = @UserId
        GROUP BY Genre
        ORDER BY Total DESC;
        """;

    internal const string GetByRotationCategory = """
        SELECT
            RotationCategory                                   AS Category,
            COUNT(*)                                           AS Total,
            SUM(CASE WHEN IsRead = 1 THEN 1 ELSE 0 END)       AS [Read],
            SUM(CASE WHEN IsRead = 0 THEN 1 ELSE 0 END)       AS Unread
        FROM Books
        WHERE UserId = @UserId
        GROUP BY RotationCategory
        ORDER BY Total DESC;
        """;

    internal const string GetByMentalEnergy = """
        SELECT
            b.MentalEnergy                                     AS Level,
            COUNT(*)                                           AS Total,
            SUM(CASE WHEN b.IsRead = 0 THEN 1 ELSE 0 END)     AS Unread
        FROM Books b
        INNER JOIN MentalEnergyLevels mel ON b.MentalEnergy = mel.Name
        WHERE b.UserId = @UserId
        GROUP BY b.MentalEnergy, mel.SortOrder
        ORDER BY mel.SortOrder ASC;
        """;

    internal const string GetByCountryTop10 = """
        SELECT TOP 10
            Country,
            COUNT(*)                                           AS Total
        FROM Books
        WHERE UserId = @UserId
        GROUP BY Country
        ORDER BY Total DESC;
        """;

    internal const string GetTopUnreadPriority = """
        SELECT TOP 3
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 0
        ORDER BY Priority DESC, CreatedAt ASC;
        """;

    internal const string GetRecentlyRead = """
        SELECT TOP 5
            Id, UserId, Title, Author, Genre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 1
        ORDER BY ReadAt DESC;
        """;
}
