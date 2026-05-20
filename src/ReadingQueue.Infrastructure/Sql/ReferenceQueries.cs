namespace ReadingQueue.Infrastructure.Sql;

internal static class ReferenceQueries
{
    internal const string GetGenres = """
        SELECT Name FROM Genres ORDER BY Name;
        """;

    internal const string GetMentalEnergyLevels = """
        SELECT Name FROM MentalEnergyLevels ORDER BY SortOrder ASC;
        """;

    internal const string GetMoods = """
        SELECT Name FROM Moods ORDER BY Name;
        """;

    internal const string GetRotationCategories = """
        SELECT Name FROM RotationCategories ORDER BY Name;
        """;
}
