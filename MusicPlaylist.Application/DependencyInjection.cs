using Microsoft.Extensions.DependencyInjection;

namespace MusicPlaylist.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Marker registration to keep DI wiring stable before adding real services.
        services.AddSingleton<ApplicationMarker>();
        return services;
    }

    private sealed class ApplicationMarker;
}

