using System.Data;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Moq;
using ReadingQueue.Infrastructure.Data;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class SqlConnectionFactoryTests
{
    [Fact]
    public void Constructor_WithValidConnectionString_DoesNotThrow()
    {
        var config = BuildConfig("Server=localhost;Database=Test;TrustServerCertificate=True;");

        var act = () => new SqlConnectionFactory(config);

        act.Should().NotThrow();
    }

    [Fact]
    public void Constructor_MissingConnectionString_ThrowsInvalidOperationException()
    {
        var config = new Mock<IConfiguration>();
        config.Setup(c => c.GetSection("ConnectionStrings"))
              .Returns(new Mock<IConfigurationSection>().Object);

        var act = () => new SqlConnectionFactory(config.Object);

        act.Should().Throw<InvalidOperationException>()
           .WithMessage("*ConnectionStrings:DefaultConnection*");
    }

    [Fact]
    public void Create_ReturnsNonNullConnection()
    {
        var factory = BuildFactory();

        var connection = factory.Create();

        connection.Should().NotBeNull();
        connection.Dispose();
    }

    [Fact]
    public void Create_ReturnsNewInstanceOnEachCall()
    {
        var factory = BuildFactory();

        var conn1 = factory.Create();
        var conn2 = factory.Create();

        conn1.Should().NotBeSameAs(conn2);
        conn1.Dispose();
        conn2.Dispose();
    }

    [Fact]
    public void Create_ReturnsIDbConnection()
    {
        var factory = BuildFactory();

        var connection = factory.Create();

        connection.Should().BeAssignableTo<IDbConnection>();
        connection.Dispose();
    }

    private static SqlConnectionFactory BuildFactory()
        => new(BuildConfig("Server=localhost;Database=Test;TrustServerCertificate=True;"));

    private static IConfiguration BuildConfig(string connectionString)
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["ConnectionStrings:DefaultConnection"] = connectionString
        };
        return new ConfigurationBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();
    }
}
