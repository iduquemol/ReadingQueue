namespace ReadingQueue.Api.Responses;

public sealed record AuthResponse(
    string AccessToken, string RefreshToken, int UserId, string DisplayName);
