namespace ReadingQueue.Infrastructure.Sql;

internal static class RefreshTokenQueries
{
    internal const string GetByToken = """
        SELECT Id, UserId, Token, ExpiresAt, CreatedAt, IsRevoked
        FROM RefreshTokens WHERE Token = @Token;
        """;

    internal const string Insert = """
        INSERT INTO RefreshTokens (UserId, Token, ExpiresAt)
        VALUES (@UserId, @Token, @ExpiresAt);
        """;

    internal const string Revoke = """
        UPDATE RefreshTokens SET IsRevoked = 1 WHERE Token = @Token;
        """;
}
