using DotNet.Testcontainers.Configurations;
using ReadingQueue.Infrastructure.Migrations;
using Testcontainers.MsSql;

namespace ReadingQueue.Infrastructure.Tests.Data;

public class AuthContainerFixture : IAsyncLifetime
{
    private readonly MsSqlContainer _container;

    public string ConnectionString { get; private set; } = null!;

    public AuthContainerFixture()
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
    }

    public async Task DisposeAsync() => await _container.DisposeAsync();
}
