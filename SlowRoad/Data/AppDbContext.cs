using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using SlowRoad.Models;

namespace SlowRoad.Data;

public class AppDbContext : IdentityDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options){}

    public DbSet<ContentItem> ContentItems => Set<ContentItem>();
    public DbSet<Place> Places => Set<Place>();
    public DbSet<Food> Foods => Set<Food>();
    public DbSet<Section> Sections => Set<Section>();
    public DbSet<SectionItem> SectionItems => Set<SectionItem>();
    public DbSet<Photo> Photos => Set<Photo>();
    public DbSet<PhotoFile> PhotoFiles => Set<PhotoFile>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // One table holds both Places and Foods, told apart by the ItemType column.
        builder.Entity<ContentItem>()
            .HasDiscriminator<string>("ItemType")
            .HasValue<Place>("place")
            .HasValue<Food>("food");

        // Required: SectionItem has no Id field, so EF needs its key spelled out.
        builder.Entity<SectionItem>()
            .HasKey(x => new { x.SectionId, x.ItemId });

        // No two sections with the same key.
        builder.Entity<Section>()
            .HasIndex(x => x.Key)
            .IsUnique();

        // No two items with the same slug (web address).
        builder.Entity<ContentItem>()
            .HasIndex(x => x.Slug)
            .IsUnique();

        // Every page query orders items within a section by SortOrder.
        builder.Entity<SectionItem>()
            .HasIndex(x => new { x.SectionId, x.SortOrder });

        // No two files of the same width for one photo.
        builder.Entity<PhotoFile>()
            .HasIndex(x => new { x.PhotoId, x.Width })
            .IsUnique();

        // Explicit cascade: deleting a content item removes its photos and their files.
        builder.Entity<ContentItem>()
            .HasMany(x => x.Photos)
            .WithOne(x => x.Item)
            .HasForeignKey(x => x.ItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Entity<Photo>()
            .HasMany(x => x.Files)
            .WithOne(x => x.Photo)
            .HasForeignKey(x => x.PhotoId)
            .OnDelete(DeleteBehavior.Cascade);

        // Deleting a section removes its join rows.
        builder.Entity<Section>()
            .HasMany(x => x.Items)
            .WithOne(x => x.Section)
            .HasForeignKey(x => x.SectionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
