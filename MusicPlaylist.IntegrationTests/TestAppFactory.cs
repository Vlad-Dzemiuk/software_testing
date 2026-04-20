using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;

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
    }
}

