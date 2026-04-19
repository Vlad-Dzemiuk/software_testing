using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Application.Playlists;
using MusicPlaylist.Application.Songs;

namespace MusicPlaylist.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<ISongService, SongService>();
        services.AddScoped<IPlaylistService, PlaylistService>();
        return services;
    }
}

