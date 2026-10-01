using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace SlowRoad.Data;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        // real local DB when the file is there; dummy in CI, where nothing connects
        var cs = new ConfigurationBuilder()
                     .SetBasePath(Directory.GetCurrentDirectory())
                     .AddJsonFile("appsettings.Development.json", optional: true)
                     .Build()
                     .GetConnectionString("Default")
                 ?? "Host=x;Database=x;Username=x;Password=x";

        var services = new ServiceCollection();
        services.Configure<IdentityOptions>(o =>
            o.Stores.SchemaVersion = IdentitySchemaVersions.Version2);

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(cs)
            .UseApplicationServiceProvider(services.BuildServiceProvider())
            .Options;

        return new AppDbContext(options);
    }
}