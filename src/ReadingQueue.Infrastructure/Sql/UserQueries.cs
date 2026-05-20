namespace ReadingQueue.Infrastructure.Sql;

internal static class UserQueries
{
    internal const string GetByEmail = """
        SELECT Id, Email, PasswordHash, DisplayName, CreatedAt, IsActive
        FROM Users WHERE Email = @Email;
        """;

    internal const string GetById = """
        SELECT Id, Email, PasswordHash, DisplayName, CreatedAt, IsActive
        FROM Users WHERE Id = @UserId;
        """;

    internal const string Insert = """
        INSERT INTO Users (Email, PasswordHash, DisplayName)
        OUTPUT INSERTED.Id
        VALUES (@Email, @PasswordHash, @DisplayName);
        """;
}
