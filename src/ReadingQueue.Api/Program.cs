using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ReadingQueue.Api.Endpoints;
using ReadingQueue.Api.Validators;
using ReadingQueue.Application.Services;
using ReadingQueue.Application.UseCases;
using ReadingQueue.Domain.Exceptions;
using ReadingQueue.Domain.Interfaces;
using ReadingQueue.Infrastructure.Auth;
using ReadingQueue.Infrastructure.Data;
using ReadingQueue.Infrastructure.LLM;
using ReadingQueue.Infrastructure.Migrations;

var builder = WebApplication.CreateBuilder(args);

// ── JWT options ──────────────────────────────────────────────────────────────
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

// ── Authentication JWT Bearer ────────────────────────────────────────────────
var jwtSection = builder.Configuration.GetSection("Jwt");
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer           = true,
            ValidateAudience         = true,
            ValidateLifetime         = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer              = jwtSection["Issuer"],
            ValidAudience            = jwtSection["Audience"],
            IssuerSigningKey         = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtSection["SecretKey"]!)),
            ClockSkew                = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

// ── Infraestructura ──────────────────────────────────────────────────────────
builder.Services.AddSingleton<IDbConnectionFactory, SqlConnectionFactory>();
builder.Services.AddScoped<IAuthService,            JwtService>();
builder.Services.AddScoped<IUserRepository,         SqlUserRepository>();
builder.Services.AddScoped<IRefreshTokenRepository, SqlRefreshTokenRepository>();

// ── Use cases ────────────────────────────────────────────────────────────────
builder.Services.AddScoped<RegisterUser>();
builder.Services.AddScoped<LoginUser>();
builder.Services.AddScoped<RefreshAccessToken>();
builder.Services.AddScoped<LogoutUser>();
builder.Services.AddScoped<GetCurrentUser>();

// ── Validadores ──────────────────────────────────────────────────────────────
builder.Services.AddScoped<RegisterRequestValidator>();
builder.Services.AddScoped<LoginRequestValidator>();

// ── Repositorios (Spec-03) ────────────────────────────────────────────────────
builder.Services.AddScoped<IBookRepository,          SqlBookRepository>();
builder.Services.AddScoped<IReferenceDataRepository, SqlReferenceDataRepository>();

// ── Use cases (Spec-03) ───────────────────────────────────────────────────────
builder.Services.AddScoped<GetFilteredBooks>();
builder.Services.AddScoped<GetBookById>();
builder.Services.AddScoped<CreateBook>();
builder.Services.AddScoped<UpdateBook>();
builder.Services.AddScoped<DeleteBook>();
builder.Services.AddScoped<MarkBookAsRead>();
builder.Services.AddScoped<MarkBookAsUnread>();
builder.Services.AddScoped<GetReferenceData>();

// ── Validadores (Spec-03) ─────────────────────────────────────────────────────
builder.Services.AddScoped<CreateBookRequestValidator>();
builder.Services.AddScoped<UpdateBookRequestValidator>();
builder.Services.AddScoped<MarkAsReadRequestValidator>();

// ── Cache (Spec-03) ───────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();

// ── Repositorios (Spec-04) ────────────────────────────────────────────────────
builder.Services.AddScoped<IQueueRepository, SqlQueueRepository>();
builder.Services.AddScoped<IStatsRepository, SqlStatsRepository>();

// ── Servicios de Application (Spec-04) ────────────────────────────────────────
builder.Services.AddScoped<QueueScoringService>();

// ── Spec-05: Claude AI Integration ────────────────────────────────────────────
builder.Services.Configure<ClaudeOptions>(builder.Configuration.GetSection("Claude"));
builder.Services.AddKeyedSingleton("claude-pipeline", (sp, _) =>
{
    var opts   = sp.GetRequiredService<IOptions<ClaudeOptions>>().Value;
    var logger = sp.GetRequiredService<ILogger<ClaudeClient>>();
    return ClaudeResiliencePipeline.Build(logger, opts.TimeoutSeconds, opts.MaxRetries);
});
builder.Services.AddScoped<ILLMClient,              ClaudeClient>();
builder.Services.AddScoped<IAISuggestionRepository, SqlAISuggestionRepository>();

// ── Use cases (Spec-04 + Spec-05) ────────────────────────────────────────────
builder.Services.AddScoped<GenerateQueueWithAI>();
builder.Services.AddScoped<GetQueue>();
builder.Services.AddScoped<ReorderQueue>();
builder.Services.AddScoped<RemoveFromQueue>();
builder.Services.AddScoped<GetSpecialLists>();
builder.Services.AddScoped<GetDashboardStats>();

// ── Serialización JSON con camelCase ──────────────────────────────────────
builder.Services.Configure<JsonOptions>(opts =>
{
    opts.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    opts.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOpenApi();

// ── CORS ─────────────────────────────────────────────────────────────────────
// Soporta string CSV ("a.com,b.com") y variables indexadas (Cors__AllowedOrigins__0)
var originsRaw = builder.Configuration["Cors:AllowedOrigins"];
var allowedOrigins = !string.IsNullOrEmpty(originsRaw)
    ? originsRaw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    : builder.Configuration.GetSection("Cors:AllowedOrigins")
             .GetChildren()
             .Select(c => c.Value!)
             .Where(v => !string.IsNullOrEmpty(v))
             .ToArray();

builder.Services.AddCors(opts => opts.AddDefaultPolicy(policy =>
    policy.WithOrigins(allowedOrigins)
          .AllowAnyHeader()
          .AllowAnyMethod()
          .AllowCredentials()));

var app = builder.Build();

app.Logger.LogInformation("CORS allowed origins: {Origins}", string.Join(", ", allowedOrigins));

// ── Migraciones ──────────────────────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
if (!string.IsNullOrEmpty(connectionString))
    MigrationRunner.Run(connectionString);

// CORS debe ser el primer middleware para que los preflight OPTIONS
// reciban los headers correctos antes de cualquier otro procesamiento.
app.UseCors();

// ── Middleware de excepciones ─────────────────────────────────────────────────
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, body) = ex switch
    {
        UnauthorizedException e => (401, (object)new { error = e.Message }),
        ConflictException     e => (409, (object)new { error = e.Message }),
        BookNotFoundException e => (404, (object)new { error = e.Message }),
        ValidationException   e => (422, (object)new { errors = e.Errors }),
        _                       => (500, (object)new { error = "Error interno del servidor." })
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(body);
}));

app.UseAuthentication();
app.UseAuthorization();

// ── Endpoints ────────────────────────────────────────────────────────────────
HealthEndpoints.Map(app);
AuthEndpoints.Map(app);
BookEndpoints.Map(app);
QueueEndpoints.Map(app);
StatsEndpoints.Map(app);

app.Run();

public partial class Program { }
