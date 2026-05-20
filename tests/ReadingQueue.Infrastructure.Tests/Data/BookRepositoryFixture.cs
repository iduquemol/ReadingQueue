using Dapper;
using DotNet.Testcontainers.Configurations;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using ReadingQueue.Infrastructure.Data;
using ReadingQueue.Infrastructure.Migrations;
using Testcontainers.MsSql;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class BookRepositoryFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public string             ConnectionString { get; private set; } = null!;
    public SqlConnectionFactory Factory        { get; private set; } = null!;
    public int                UserId          { get; private set; }

    public BookRepositoryFixture()
    {
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

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = ConnectionString
            })
            .Build();
        Factory = new SqlConnectionFactory(config);

        using var conn = new SqlConnection(ConnectionString);
        UserId = await conn.ExecuteScalarAsync<int>("""
            INSERT INTO Users (Email, PasswordHash, DisplayName)
            OUTPUT INSERTED.Id
            VALUES ('booktest@example.com', 'hash', 'Book Test User');
            """);
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
