using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Application.Songs;
using MusicPlaylist.Infrastructure.Persistence;
using MusicPlaylist.Infrastructure.Seeding;

namespace MusicPlaylist.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Default");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Default' is not configured.");
        }

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

        services.AddScoped<SongsRepository>();
        services.AddScoped<ISongsReadRepository>(sp => sp.GetRequiredService<SongsRepository>());
        services.AddScoped<ISongsWriteRepository>(sp => sp.GetRequiredService<SongsRepository>());

        services.AddScoped<IPlaylistRepository, PlaylistRepository>();

        services.AddHostedService<DatabaseSeedingHostedService>();

        return services;
    }
}

