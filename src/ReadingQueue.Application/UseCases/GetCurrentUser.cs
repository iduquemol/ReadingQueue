using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Application.UseCases;

public sealed class GetCurrentUser
{
    private readonly IUserRepository _users;

    public GetCurrentUser(IUserRepository users) => _users = users;

    public record Query(int UserId);
    public record Result(int Id, string Email, string DisplayName);

    public async Task<Result> ExecuteAsync(Query query, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(query.UserId, ct)
            ?? throw new UnauthorizedException("Usuario no encontrado.");

        return new Result(user.Id, user.Email, user.DisplayName);
    }
}
