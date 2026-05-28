namespace ReadingQueue.Infrastructure.Sql;

internal static class BookQueries
{
    internal const string GetByUserFiltered = """
        SELECT
            Id, UserId, Title, Author, Genre, Subgenre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId
          AND (@Genre        IS NULL OR Genre            = @Genre)
          AND (@Country      IS NULL OR Country          = @Country)
          AND (@MentalEnergy IS NULL OR MentalEnergy     = @MentalEnergy)
          AND (@Mood         IS NULL OR RecommendedMood  = @Mood)
          AND (@Rotation     IS NULL OR RotationCategory = @Rotation)
          AND (@MinPriority  IS NULL OR Priority        >= @MinPriority)
          AND (@IsRead       IS NULL OR IsRead           = @IsRead)
          AND (@SearchQuery  IS NULL OR
               Title  LIKE '%' + @SearchQuery + '%' OR
               Author LIKE '%' + @SearchQuery + '%')
        ORDER BY Priority DESC, CreatedAt ASC;
        """;

    internal const string GetById = """
        SELECT
            Id, UserId, Title, Author, Genre, Subgenre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO Books
            (UserId, Title, Author, Genre, Subgenre, Country, WhyRead,
             Priority, MentalEnergy, RecommendedMood, RotationCategory, Notes)
        OUTPUT INSERTED.Id
        VALUES
            (@UserId, @Title, @Author, @Genre, @Subgenre, @Country, @WhyRead,
             @Priority, @MentalEnergy, @RecommendedMood, @RotationCategory, @Notes);
        """;

    internal const string Update = """
        UPDATE Books SET
            Title            = @Title,
            Author           = @Author,
            Genre            = @Genre,
            Subgenre         = @Subgenre,
            Country          = @Country,
            WhyRead          = @WhyRead,
            Priority         = @Priority,
            MentalEnergy     = @MentalEnergy,
            RecommendedMood  = @RecommendedMood,
            RotationCategory = @RotationCategory,
            Notes            = @Notes,
            UpdatedAt        = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string Delete = """
        DELETE FROM Books WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string DeleteFromQueue = """
        DELETE FROM ReadingQueue WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string DeleteFromSuggestions = """
        DELETE FROM AISuggestions WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string MarkAsRead = """
        UPDATE Books SET
            IsRead    = 1,
            ReadAt    = @ReadAt,
            Notes     = COALESCE(@Notes, Notes),
            UpdatedAt = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string RemoveFromQueueIfPresent = """
        DELETE FROM ReadingQueue WHERE BookId = @BookId AND UserId = @UserId;
        """;

    internal const string MarkAsUnread = """
        UPDATE Books SET
            IsRead    = 0,
            ReadAt    = NULL,
            UpdatedAt = GETUTCDATE()
        WHERE Id = @BookId AND UserId = @UserId;
        """;

    internal const string GetUnreadByUser = """
        SELECT
            Id, UserId, Title, Author, Genre, Subgenre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 0
        ORDER BY Priority DESC, CreatedAt ASC;
        """;

    internal const string GetReadByUser = """
        SELECT
            Id, UserId, Title, Author, Genre, Subgenre, Country, WhyRead,
            Priority, MentalEnergy, RecommendedMood, RotationCategory,
            IsRead, ReadAt, Notes, CreatedAt, UpdatedAt
        FROM Books
        WHERE UserId = @UserId AND IsRead = 1
        ORDER BY ReadAt DESC;
        """;
}
