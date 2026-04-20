using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MusicPlaylist.Infrastructure.Persistence;

namespace MusicPlaylist.IntegrationTests;

public sealed class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _connectionString;

    public TestAppFactory(string connectionString)
    {
        _connectionString = connectionString;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            var overrides = new Dictionary<string, string?>
            {
                ["ConnectionStrings:Default"] = _connectionString
            };

            config.AddInMemoryCollection(overrides);
        });

        // In some CI environments configuration overrides may not win consistently.
        // Force the DbContext to use the Testcontainers connection string via DI.
        builder.ConfigureServices(services =>
        {
            // Remove existing AppDbContext registrations.
            var descriptors = services
                .Where(d =>
                    d.ServiceType == typeof(DbContextOptions<AppDbContext>)
                    || d.ServiceType == typeof(AppDbContext))
                .ToList();

            foreach (var d in descriptors)
            {
                services.Remove(d);
            }

            services.AddDbContext<AppDbContext>(options => options.UseNpgsql(_connectionString));
        });
    }
}

