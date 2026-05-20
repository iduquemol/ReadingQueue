using System.Security.Claims;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using ReadingQueue.Api.Extensions;
using ReadingQueue.Domain.Exceptions;

namespace ReadingQueue.Api.Tests.Extensions;

public class HttpContextExtensionsTests
{
    private static HttpContext ContextWithClaims(params Claim[] claims)
    {
        var ctx = new DefaultHttpContext();
        ctx.User = new ClaimsPrincipal(new ClaimsIdentity(claims));
        return ctx;
    }

    [Fact]
    public void GetUserId_SubClaim_ReturnsCorrectInt()
    {
        var ctx = ContextWithClaims(new Claim("sub", "42"));

        ctx.GetUserId().Should().Be(42);
    }

    [Fact]
    public void GetUserId_NameIdentifierFallback_ReturnsCorrectInt()
    {
        var ctx = ContextWithClaims(
            new Claim(ClaimTypes.NameIdentifier, "99"));

        ctx.GetUserId().Should().Be(99);
    }

    [Fact]
    public void GetUserId_NoIdentityClaim_ThrowsUnauthorizedException()
    {
        var ctx = ContextWithClaims(new Claim("email", "x@x.com"));

        ctx.Invoking(c => c.GetUserId())
           .Should().Throw<UnauthorizedException>();
    }

    [Fact]
    public void GetUserId_SubNotParsableAsInt_ThrowsUnauthorizedException()
    {
        var ctx = ContextWithClaims(new Claim("sub", "not-an-int"));

        ctx.Invoking(c => c.GetUserId())
           .Should().Throw<UnauthorizedException>();
    }
}
