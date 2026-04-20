using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace MusicPlaylist.IntegrationTests;

public sealed class PostgresFixture : IAsyncLifetime
{
    private PostgreSqlContainer? _container;

    public string ConnectionString => _container!.GetConnectionString();

    public TestAppFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        _container = new PostgreSqlBuilder("postgres:16")
            .WithDatabase("musicplaylist")
            .WithUsername("musicplaylist")
            .WithPassword("LocalDev_ChangeMe")
            .Build();

        await _container.StartAsync();

        Factory = new TestAppFactory(ConnectionString);

        // Ensure database schema exists (migrations from Infrastructure).
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}

[CollectionDefinition(nameof(IntegrationTestCollection))]
public sealed class IntegrationTestCollection : ICollectionFixture<PostgresFixture>;

