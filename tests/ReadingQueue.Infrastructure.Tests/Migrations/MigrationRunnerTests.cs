using DotNet.Testcontainers.Configurations;
using FluentAssertions;
using Microsoft.Data.SqlClient;
using ReadingQueue.Infrastructure.Migrations;
using Testcontainers.MsSql;

namespace ReadingQueue.Infrastructure.Tests.Migrations;

public class MigrationRunnerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public string ConnectionString { get; private set; } = null!;

    public MigrationRunnerFixture()
    {
        // Ryuk (resource reaper) falla con Docker Desktop en Windows via named pipe
        TestcontainersSettings.ResourceReaperEnabled = false;

#pragma warning disable CS0618
        _container = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();
#pragma warning restore CS0618
    }

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        MigrationRunner.Run(ConnectionString);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}

public class MigrationRunnerTests : IClassFixture<MigrationRunnerFixture>
{
    private readonly string _connectionString;

    public MigrationRunnerTests(MigrationRunnerFixture fixture)
    {
        _connectionString = fixture.ConnectionString;
    }

    [Fact]
    public void Run_AgainstEmptyDatabase_DoesNotThrow()
    {
        // La migración ya corrió en el fixture — verificamos que no haya lanzado
        var tables = GetTableNames();
        tables.Should().NotBeEmpty();
    }

    [Fact]
    public void Run_CreatesAllBusinessTables()
    {
        var tables = GetTableNames();

        tables.Should().Contain(new[]
        {
            "Users", "Books", "ReadingQueue", "AISuggestions", "RefreshTokens"
        });
    }

    [Fact]
    public void Run_CreatesAllReferenceTables()
    {
        var tables = GetTableNames();

        tables.Should().Contain(new[]
        {
            "Genres", "MentalEnergyLevels", "Moods", "RotationCategories"
        });
    }

    [Fact]
    public void Run_SeedsGenres_SevenValues()
    {
        var count = QueryScalar<int>("SELECT COUNT(*) FROM Genres");

        count.Should().Be(7);
    }

    [Fact]
    public void Run_SeedsMentalEnergyLevels_FiveValues()
    {
        var count = QueryScalar<int>("SELECT COUNT(*) FROM MentalEnergyLevels");

        count.Should().Be(5);
    }

    [Fact]
    public void Run_SeedsMoods_SevenValues()
    {
        var count = QueryScalar<int>("SELECT COUNT(*) FROM Moods");

        count.Should().Be(7);
    }

    [Fact]
    public void Run_SeedsRotationCategories_FiveValues()
    {
        var count = QueryScalar<int>("SELECT COUNT(*) FROM RotationCategories");

        count.Should().Be(5);
    }

    [Fact]
    public void Run_CalledTwice_IsIdempotent()
    {
        var act = () => MigrationRunner.Run(_connectionString);

        act.Should().NotThrow();
    }

    [Fact]
    public void Run_CalledTwice_DoesNotDuplicateSeedData()
    {
        MigrationRunner.Run(_connectionString);

        var count = QueryScalar<int>("SELECT COUNT(*) FROM Genres");

        count.Should().Be(7);
    }

    private List<string> GetTableNames()
    {
        var tables = new List<string>();
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_TYPE = 'BASE TABLE'";
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
            tables.Add(reader.GetString(0));
        return tables;
    }

    private T QueryScalar<T>(string sql)
    {
        using var conn = new SqlConnection(_connectionString);
        conn.Open();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        return (T)cmd.ExecuteScalar()!;
    }
}
