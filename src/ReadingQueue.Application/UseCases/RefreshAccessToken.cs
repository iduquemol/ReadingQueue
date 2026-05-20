using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class RefreshAccessToken
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public RefreshAccessToken(IUserRepository users, IRefreshTokenRepository tokens, IAuthService auth)
    {
        _users  = users;
        _tokens = tokens;
        _auth   = auth;
    }

    public record Command(string RefreshToken);
    public record Result(string AccessToken, string NewRefreshToken);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var stored = await _tokens.GetByTokenAsync(cmd.RefreshToken, ct);

        if (stored is null || !stored.IsValid(DateTime.UtcNow))
            throw new UnauthorizedException("Token de renovación inválido.");

        var user = await _users.GetByIdAsync(stored.UserId, ct)
            ?? throw new UnauthorizedException("Token de renovación inválido.");

        await _tokens.RevokeAsync(cmd.RefreshToken, ct);

        var newAccessToken  = _auth.GenerateAccessToken(user);
        var newRefreshToken = _auth.GenerateRefreshToken();

        await _tokens.CreateAsync(user.Id, newRefreshToken, DateTime.UtcNow.AddDays(7), ct);

        return new Result(newAccessToken, newRefreshToken);
    }
}
