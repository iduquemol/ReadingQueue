using System.Data;
using System.Net;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using ReadingQueue.Domain.Interfaces;

namespace ReadingQueue.Api.Tests.Endpoints;

public class HealthEndpointsTests
{
    private static HttpClient BuildClient(bool databaseReachable)
    {
        var factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    var descriptor = services.SingleOrDefault(
                        d => d.ServiceType == typeof(IDbConnectionFactory));
                    if (descriptor is not null)
                        services.Remove(descriptor);

                    var mockConn = new Mock<IDbConnection>();
                    var mockFactory = new Mock<IDbConnectionFactory>();

                    if (databaseReachable)
                        mockFactory.Setup(f => f.Create()).Returns(mockConn.Object);
                    else
                        mockFactory.Setup(f => f.Create())
                                   .Throws(new InvalidOperationException("Database unreachable"));

                    services.AddSingleton(mockFactory.Object);
                });
            });

        return factory.CreateClient();
    }

    [Fact]
    public async Task GetHealth_WithReachableDatabase_Returns200()
    {
        var client = BuildClient(databaseReachable: true);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetHealth_WithReachableDatabase_ReturnsStatusOk()
    {
        var client = BuildClient(databaseReachable: true);

        var response = await client.GetAsync("/health");
        var body = await ParseBody(response);

        body.GetProperty("status").GetString().Should().Be("ok");
    }

    [Fact]
    public async Task GetHealth_WithReachableDatabase_ReturnsDatabaseReachable()
    {
        var client = BuildClient(databaseReachable: true);

        var response = await client.GetAsync("/health");
        var body = await ParseBody(response);

        body.GetProperty("database").GetString().Should().Be("reachable");
    }

    [Fact]
    public async Task GetHealth_WithReachableDatabase_ReturnsValidTimestamp()
    {
        var client = BuildClient(databaseReachable: true);

        var response = await client.GetAsync("/health");
        var body = await ParseBody(response);

        var timestamp = body.GetProperty("timestamp").GetString();
        DateTimeOffset.TryParse(timestamp, out _).Should().BeTrue();
    }

    [Fact]
    public async Task GetHealth_DoesNotRequireAuthorization()
    {
        var client = BuildClient(databaseReachable: true);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().NotBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetHealth_WithUnreachableDatabase_Returns503()
    {
        var client = BuildClient(databaseReachable: false);

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
    }

    [Fact]
    public async Task GetHealth_WithUnreachableDatabase_ReturnsStatusDegraded()
    {
        var client = BuildClient(databaseReachable: false);

        var response = await client.GetAsync("/health");
        var body = await ParseBody(response);

        body.GetProperty("status").GetString().Should().Be("degraded");
        body.GetProperty("database").GetString().Should().Be("unreachable");
    }

    private static async Task<JsonElement> ParseBody(HttpResponseMessage response)
    {
        var json = await response.Content.ReadAsStringAsync();
        return JsonDocument.Parse(json).RootElement;
    }
}
