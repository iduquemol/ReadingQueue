using FluentValidation;
using ReadingQueue.Api.Extensions;
using ReadingQueue.Api.Requests;
using ReadingQueue.Api.Responses;
using ReadingQueue.Api.Validators;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Exceptions;

namespace ReadingQueue.Api.Endpoints;

public static class AuthEndpoints
{
    public static void Map(IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/auth").WithTags("Auth");

        group.MapPost("/register", Register).AllowAnonymous();
        group.MapPost("/login",    Login).AllowAnonymous();
        group.MapPost("/refresh",  Refresh).AllowAnonymous();
        group.MapPost("/logout",   Logout).RequireAuthorization();
        group.MapGet ("/me",       Me).RequireAuthorization();
    }

    private static async Task<IResult> Register(
        RegisterRequest req,
        RegisterUser useCase,
        RegisterRequestValidator validator)
    {
        var validation = await validator.ValidateAsync(req);
        if (!validation.IsValid)
            return Results.UnprocessableEntity(new
            {
                errors = validation.Errors
                    .GroupBy(e => e.PropertyName.ToLower())
                    .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray())
            });

        var result = await useCase.ExecuteAsync(
            new RegisterUser.Command(req.Email, req.Password, req.DisplayName));

        return Results.Created("/api/auth/me",
            new AuthResponse(result.AccessToken, result.RefreshToken,
                             result.UserId, result.DisplayName));
    }

    private static async Task<IResult> Login(
        LoginRequest req,
        LoginUser useCase)
    {
        var result = await useCase.ExecuteAsync(
            new LoginUser.Command(req.Email, req.Password));

        return Results.Ok(new AuthResponse(result.AccessToken, result.RefreshToken,
                                           result.UserId, result.DisplayName));
    }

    private static async Task<IResult> Refresh(
        RefreshRequest req,
        RefreshAccessToken useCase)
    {
        var result = await useCase.ExecuteAsync(
            new RefreshAccessToken.Command(req.RefreshToken));

        return Results.Ok(new TokenResponse(result.AccessToken, result.NewRefreshToken));
    }

    private static async Task<IResult> Logout(
        LogoutRequest req,
        LogoutUser useCase)
    {
        await useCase.ExecuteAsync(new LogoutUser.Command(req.RefreshToken));
        return Results.Ok(new { message = "Sesión cerrada correctamente." });
    }

    private static async Task<IResult> Me(
        HttpContext ctx,
        GetCurrentUser useCase)
    {
        var userId = ctx.GetUserId();
        var result = await useCase.ExecuteAsync(new GetCurrentUser.Query(userId));
        return Results.Ok(new UserProfileResponse(result.Id, result.Email, result.DisplayName));
    }
}
