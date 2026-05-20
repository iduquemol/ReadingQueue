using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class LogoutUser
{
    private readonly IRefreshTokenRepository _tokens;

    public LogoutUser(IRefreshTokenRepository tokens) => _tokens = tokens;

    public record Command(string RefreshToken);

    public async Task ExecuteAsync(Command cmd, CancellationToken ct = default)
        => await _tokens.RevokeAsync(cmd.RefreshToken, ct);
}
