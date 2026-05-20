using ReadingQueue.Domain.Entities;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class RegisterUser
{
    private readonly IUserRepository _users;
    private readonly IRefreshTokenRepository _tokens;
    private readonly IAuthService _auth;

    public RegisterUser(IUserRepository users, IRefreshTokenRepository tokens, IAuthService auth)
    {
        _users  = users;
        _tokens = tokens;
        _auth   = auth;
    }

    public record Command(string Email, string Password, string DisplayName);
    public record Result(string AccessToken, string RefreshToken, int UserId, string DisplayName);

    public async Task<Result> ExecuteAsync(Command cmd, CancellationToken ct = default)
    {
        var existing = await _users.GetByEmailAsync(cmd.Email, ct);
        if (existing is not null)
            throw new ConflictException("El email ya está registrado.");

        var hash   = _auth.HashPassword(cmd.Password);
        var userId = await _users.CreateAsync(cmd.Email, hash, cmd.DisplayName, ct);

        var user         = new User(userId, cmd.Email, hash, cmd.DisplayName, DateTime.UtcNow, true);
        var accessToken  = _auth.GenerateAccessToken(user);
        var refreshToken = _auth.GenerateRefreshToken();

        await _tokens.CreateAsync(userId, refreshToken, DateTime.UtcNow.AddDays(7), ct);

        return new Result(accessToken, refreshToken, userId, cmd.DisplayName);
    }
}
