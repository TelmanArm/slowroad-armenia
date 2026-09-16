using Microsoft.EntityFrameworkCore;
using SlowRoad.Models;

namespace SlowRoad.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){}
    public DbSet<Place> Places => Set<Place>();

}