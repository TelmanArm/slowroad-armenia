using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SlowRoad.Data;

namespace SlowRoad.Tests;

// Boots the real app but swaps the Npgsql AppDbContext for an in-memory one,
// so tests run with no Postgres available.
public class TestAppFactory : WebApplicationFactory<Program>
{
    // Config the app reads at startup, for a derived factory to adjust before
    // the host is built. The login throttle is effectively off by default so
    // that tests about other things can post the login form freely;
    // LoginHardeningTests pins tight values to exercise it.
    public Dictionary<string, string?> Settings { get; } = new()
    {
        ["Security:Login:PermitLimit"] = "1000",
    };

    // A store of its own per factory instance. The in-memory provider shares a
    // store between hosts that name the same database, so one fixed name let
    // test classes running in parallel seed over each other.
    private readonly string _databaseName = $"SlowRoadTests-{Guid.NewGuid():N}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration(config => config.AddInMemoryCollection(Settings));

        builder.ConfigureServices(services =>
        {
            // EF 9 registers both of these for a DbContext; remove both before
            // re-registering with the in-memory provider.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase(_databaseName));
        });
    }
}
