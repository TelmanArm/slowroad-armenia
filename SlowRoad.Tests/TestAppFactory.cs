using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SlowRoad.Data;

namespace SlowRoad.Tests;

// Boots the real app but swaps the Npgsql AppDbContext for an in-memory one,
// so tests run with no Postgres available.
public class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // EF 9 registers both of these for a DbContext; remove both before
            // re-registering with the in-memory provider.
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();

            services.AddDbContext<AppDbContext>(o =>
                o.UseInMemoryDatabase("SlowRoadTests"));
        });
    }
}
