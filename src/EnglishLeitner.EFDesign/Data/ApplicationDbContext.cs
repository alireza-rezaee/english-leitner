using EnglishLeitner.EFDesign.Models;
using Microsoft.EntityFrameworkCore;

namespace EnglishLeitner.EFDesign.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : DbContext(options)
{
    public DbSet<Word> Words { get; set; }
    public DbSet<Pronunciation> Pronunciations { get; set; }
    public DbSet<MeaningGroup> MeaningGroups { get; set; }
    public DbSet<MeaningItem> MeaningItems { get; set; }
    public DbSet<Example> Examples { get; set; }
    public DbSet<Review> Reviews { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<MeaningItem>(entity =>
        {
            entity.Property(e => e.Refs)
                .HasConversion(
                    v => string.Join('\n', v.Where(x => !string.IsNullOrWhiteSpace(x))),
                    v => v.Split("\n")
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList() ?? new List<string>()
                );

            entity.Property(e => e.Topics)
                .HasConversion(
                    v => string.Join('\n', v.Where(x => !string.IsNullOrWhiteSpace(x))),
                    v => v.Split("\n")
                        .Where(x => !string.IsNullOrWhiteSpace(x))
                        .ToList() ?? new List<string>()
                );
        });
    }
}