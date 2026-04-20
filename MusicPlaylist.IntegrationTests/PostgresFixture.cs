using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Infrastructure.Persistence;
using Npgsql;
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
        await MigrateWithRetryAsync();
    }

    private async Task MigrateWithRetryAsync()
    {
        // Testcontainers can report "started" slightly before Postgres is ready to accept connections.
        // In CI this may cause transient "Connection refused" during EF migrations.
        var timeoutAt = DateTimeOffset.UtcNow.AddSeconds(30);
        Exception? last = null;

        while (DateTimeOffset.UtcNow < timeoutAt)
        {
            try
            {
                using var scope = Factory.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
                await db.Database.MigrateAsync();
                return;
            }
            catch (Exception ex) when (IsTransientStartup(ex))
            {
                last = ex;
                await Task.Delay(500);
            }
        }

        throw new InvalidOperationException("PostgreSQL container did not become ready in time.", last);
    }

    private static bool IsTransientStartup(Exception ex)
    {
        if (ex is NpgsqlException)
        {
            return true;
        }

        if (ex is AggregateException ag && ag.InnerExceptions.Any(IsTransientStartup))
        {
            return true;
        }

        return ex.InnerException is not null && IsTransientStartup(ex.InnerException);
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

