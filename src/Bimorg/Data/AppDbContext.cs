using Bimorg.Models;
using Microsoft.EntityFrameworkCore;

namespace Bimorg.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<BattleMap> BattleMaps => Set<BattleMap>();
    public DbSet<MapKeyword> MapKeywords => Set<MapKeyword>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var map = modelBuilder.Entity<BattleMap>();
        map.ToTable("BattleMaps");
        map.HasIndex(e => e.FilePath).IsUnique();
        map.Property(e => e.FilePath).HasMaxLength(4096).IsRequired();
        map.Property(e => e.FileName).HasMaxLength(512).IsRequired();
        map.Property(e => e.Description).HasMaxLength(8192).IsRequired();
        map.HasMany(e => e.Keywords)
            .WithOne(k => k.BattleMap!)
            .HasForeignKey(k => k.BattleMapId)
            .OnDelete(DeleteBehavior.Cascade);

        var kw = modelBuilder.Entity<MapKeyword>();
        kw.ToTable("Keywords");
        kw.Property(k => k.Word).HasMaxLength(256).IsRequired();
        kw.HasIndex(k => new { k.BattleMapId, k.Word });
    }
}