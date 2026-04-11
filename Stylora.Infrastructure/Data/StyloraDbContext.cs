using Microsoft.EntityFrameworkCore;
using Stylora.Domain.Entities;
using Stylora.Domain.Enums;

namespace Stylora.Infrastructure.Data;

public class StyloraDbContext : DbContext
{
    public StyloraDbContext(DbContextOptions<StyloraDbContext> options) : base(options)
    {
    }

    public DbSet<User> Users => Set<User>();
    public DbSet<WardrobeItem> WardrobeItems => Set<WardrobeItem>();
    public DbSet<SeasonAnalysisResult> SeasonAnalysisResults => Set<SeasonAnalysisResult>();
    public DbSet<TryOnSession> TryOnSessions => Set<TryOnSession>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<RecommendedColor> RecommendedColors => Set<RecommendedColor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            entity.Property(e => e.ProfilePicture).HasColumnType("text");
            entity.Property(e => e.Style).HasConversion<string>().HasMaxLength(50);
        });

        // One-to-one: SeasonAnalysisResult owns the FK (UserId → Users.Id).
        // Cascade delete ensures analysis is removed when user is deleted.
        // No FK column on Users table, avoiding circular reference issues.
        modelBuilder.Entity<SeasonAnalysisResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Season).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SubSeason).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.BestMetals).HasMaxLength(200);
            entity.Property(e => e.AnalysisImagePath).HasColumnType("text");
            entity.Property(e => e.ImageData).HasColumnType("text");

            entity.HasOne(e => e.User)
                  .WithOne(u => u.ColorAnalysisResult)
                  .HasForeignKey<SeasonAnalysisResult>(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasIndex(e => e.UserId).IsUnique();
        });

        modelBuilder.Entity<WardrobeItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImagePath).HasColumnType("text").IsRequired();
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.Style).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.WornCount).HasDefaultValue(0);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.WardrobeItems)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Color)
                  .WithMany(c => c.WardrobeItems)
                  .HasForeignKey(e => e.ColorId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Category);
        });

        modelBuilder.Entity<Color>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HexCode).HasMaxLength(7);
        });

        modelBuilder.Entity<RecommendedColor>(entity =>
        {
            entity.HasKey(e => new { e.SeasonAnalysisResultId, e.ColorId });

            entity.HasOne(e => e.SeasonAnalysisResult)
                  .WithMany(s => s.RecommendedColors)
                  .HasForeignKey(e => e.SeasonAnalysisResultId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.Color)
                  .WithMany(c => c.RecommendedColors)
                  .HasForeignKey(e => e.ColorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<TryOnSession>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.PersonImagePath).HasColumnType("text").IsRequired();
            entity.Property(e => e.ClothingImagePath).HasColumnType("text").IsRequired();
            entity.Property(e => e.GeneratedImagePath).HasColumnType("text");
            entity.Property(e => e.ErrorMessage).HasMaxLength(1000);

            entity.HasOne(e => e.User)
                  .WithMany(u => u.TryOnSessions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne(e => e.WardrobeItem)
                  .WithMany(w => w.TryOnSessions)
                  .HasForeignKey(e => e.WardrobeItemId)
                  .OnDelete(DeleteBehavior.SetNull);

            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.CreatedAt);
        });
    }
}
