using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Pgvector;
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
    public DbSet<ClothingReferenceEmbedding> ClothingReferenceEmbeddings => Set<ClothingReferenceEmbedding>();
    public DbSet<SeasonAnalysisResult> SeasonAnalysisResults => Set<SeasonAnalysisResult>();
    public DbSet<TryOnSession> TryOnSessions => Set<TryOnSession>();
    public DbSet<Color> Colors => Set<Color>();
    public DbSet<RecommendedColor> RecommendedColors => Set<RecommendedColor>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.HasPostgresExtension("vector");

        var vectorConverter = new ValueConverter<float[], Vector>(
            value => new Vector(value),
            value => value.ToArray());

        var vectorComparer = new ValueComparer<float[]>(
            (left, right) => left != null && right != null && left.SequenceEqual(right),
            value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
            value => value.ToArray());

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
            entity.Property(e => e.ArticleTypeLabel).HasMaxLength(100);
            entity.Property(e => e.AudienceTag).HasMaxLength(20);
            entity.Property(e => e.Style).HasConversion<string>().HasMaxLength(50);
            entity.Property(e => e.WornCount).HasDefaultValue(0);
            entity.Property(e => e.ValidationStatus).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.ValidationMessage).HasMaxLength(500);

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
            entity.HasIndex(e => e.ArticleTypeLabel);
        });

        modelBuilder.Entity<ClothingReferenceEmbedding>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Label).HasConversion<string>().HasMaxLength(20);
            entity.Property(e => e.SourceKey).HasMaxLength(260).IsRequired();
            entity.Property(e => e.CategoryHint).HasMaxLength(100);
            entity.Property(e => e.SourceDataset).HasMaxLength(100);
            entity.Property(e => e.GenderTag).HasMaxLength(20);
            entity.Property(e => e.MasterCategory).HasMaxLength(50);
            entity.Property(e => e.SubCategory).HasMaxLength(50);
            entity.Property(e => e.ArticleType).HasMaxLength(100);
            entity.Property(e => e.CategoryGroup).HasMaxLength(50);
            entity.Property(e => e.BaseColour).HasMaxLength(50);
            entity.Property(e => e.ColorFamily).HasMaxLength(50);
            entity.Property(e => e.SeasonTag).HasMaxLength(50);
            entity.Property(e => e.UsageTag).HasMaxLength(50);
            entity.Property(e => e.DisplayName).HasMaxLength(250);
            entity.Property(e => e.IsActive).HasDefaultValue(true);
            entity.Property(e => e.Embedding)
                .HasConversion(vectorConverter)
                .Metadata.SetValueComparer(vectorComparer);
            entity.Property(e => e.Embedding).HasColumnType("vector(512)");

            entity.HasIndex(e => e.SourceKey).IsUnique();
            entity.HasIndex(e => e.Label);
            entity.HasIndex(e => e.GenderTag);
            entity.HasIndex(e => e.CategoryGroup);
            entity.HasIndex(e => e.ArticleType);
            entity.HasIndex(e => e.ColorFamily);
            entity.HasIndex(e => e.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops");
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
