using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MusicPlaylist.Infrastructure.Persistence;

namespace MusicPlaylist.Infrastructure.Seeding;

public sealed class DatabaseSeedingHostedService : IHostedService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<DatabaseSeedingHostedService> _logger;

    public DatabaseSeedingHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<DatabaseSeedingHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var enabled = string.Equals(
            _configuration["SEED_ON_STARTUP"],
            "true",
            StringComparison.OrdinalIgnoreCase);

        if (!enabled)
        {
            return;
        }

        await using var scope = _scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        if (await db.Songs.AnyAsync(cancellationToken))
        {
            _logger.LogInformation("Database seed skipped: Songs table is not empty.");
            return;
        }

        await ProductionLikeDataSeeder.SeedAsync(db, cancellationToken);

        _logger.LogInformation("seed completed");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
