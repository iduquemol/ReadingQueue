namespace ReadingQueue.Api.Requests;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
