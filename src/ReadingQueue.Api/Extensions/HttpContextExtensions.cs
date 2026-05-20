using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using ReadingQueue.Domain.Exceptions;

namespace ReadingQueue.Api.Extensions;

public static class HttpContextExtensions
{
    public static int GetUserId(this HttpContext ctx)
    {
        var sub = ctx.User.FindFirstValue(JwtRegisteredClaimNames.Sub)
               ?? ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);

        if (int.TryParse(sub, out var userId))
            return userId;

        throw new UnauthorizedException("UserId no encontrado en el token.");
    }
}
