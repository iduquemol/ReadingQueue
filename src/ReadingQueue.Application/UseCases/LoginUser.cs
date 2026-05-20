using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class LoginUser
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public LoginUser(IUserRepository users, IRefreshTokenRepository tokens, IAuthService auth)
    {
        _users  = users;
        _tokens = tokens;
        _auth   = auth;
    }

    public record Command(string Email, string Password);
    public record Result(string AccessToken, string RefreshToken, int UserId, string DisplayName);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var user = await _users.GetByEmailAsync(cmd.Email, ct);

        if (user is null || !_auth.VerifyPassword(cmd.Password, user.PasswordHash))
            throw new UnauthorizedException("Credenciales inválidas.");

        if (!user.IsActive)
            throw new UnauthorizedException("Cuenta desactivada.");

        var accessToken  = _auth.GenerateAccessToken(user);
        var refreshToken = _auth.GenerateRefreshToken();

        await _tokens.CreateAsync(user.Id, refreshToken, DateTime.UtcNow.AddDays(7), ct);

        return new Result(accessToken, refreshToken, user.Id, user.DisplayName);
    }
}
