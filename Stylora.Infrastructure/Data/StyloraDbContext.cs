using Microsoft.EntityFrameworkCore;
using Stylora.Domain.Entities;

namespace Stylora.Infrastructure.Data;

public class StyloraDbContext : DbContext
{
    public StyloraDbContext(DbContextOptions<StyloraDbContext> options) : base(options)
    {
    }

    // Main entities
    public DbSet<User> Users => Set<User>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<WardrobeItem> WardrobeItems => Set<WardrobeItem>();
    public DbSet<OutfitSuggestion> OutfitSuggestions => Set<OutfitSuggestion>();
    public DbSet<SeasonAnalysisResult> SeasonAnalysisResults => Set<SeasonAnalysisResult>();
    public DbSet<TryOnSession> TryOnSessions => Set<TryOnSession>();
    
    // Normalized lookup entities
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<Color> Colors => Set<Color>();
    
    // Junction tables
    public DbSet<WardrobeItemTag> WardrobeItemTags => Set<WardrobeItemTag>();
    public DbSet<OutfitItem> OutfitItems => Set<OutfitItem>();
    public DbSet<RecommendedColor> RecommendedColors => Set<RecommendedColor>();
    public DbSet<UserPaletteColor> UserPaletteColors => Set<UserPaletteColor>();
    public DbSet<WearLog> WearLogs => Set<WearLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Email).IsUnique();
            entity.Property(e => e.Email).HasMaxLength(256).IsRequired();
            entity.Property(e => e.PasswordHash).HasMaxLength(512).IsRequired();
            entity.Property(e => e.FirstName).HasMaxLength(100);
            entity.Property(e => e.LastName).HasMaxLength(100);
            
            entity.HasOne(e => e.Profile)
                  .WithOne(p => p.User)
                  .HasForeignKey<UserProfile>(p => p.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // UserProfile configuration
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Season).HasMaxLength(50);
            entity.Property(e => e.SubSeason).HasMaxLength(50);
            entity.Property(e => e.PreferredStyle).HasMaxLength(100);
            entity.Property(e => e.AvatarPath).HasMaxLength(500);
        });

        // WardrobeItem configuration
        modelBuilder.Entity<WardrobeItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.ImagePath).HasColumnType("text").IsRequired();
            entity.Property(e => e.Description).HasMaxLength(500);
            entity.Property(e => e.Brand).HasMaxLength(100);
            entity.Property(e => e.Category).HasConversion<string>().HasMaxLength(50);
            
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

        // Tag configuration
        modelBuilder.Entity<Tag>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
        });

        // WardrobeItemTag junction table (many-to-many)
        modelBuilder.Entity<WardrobeItemTag>(entity =>
        {
            entity.HasKey(e => new { e.WardrobeItemId, e.TagId });
            
            entity.HasOne(e => e.WardrobeItem)
                  .WithMany(w => w.WardrobeItemTags)
                  .HasForeignKey(e => e.WardrobeItemId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.Tag)
                  .WithMany(t => t.WardrobeItemTags)
                  .HasForeignKey(e => e.TagId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // Color configuration
        modelBuilder.Entity<Color>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.HasIndex(e => e.Name).IsUnique();
            entity.Property(e => e.Name).HasMaxLength(50).IsRequired();
            entity.Property(e => e.HexCode).HasMaxLength(7);
        });

        // OutfitSuggestion configuration
        modelBuilder.Entity<OutfitSuggestion>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Reasoning).HasMaxLength(1000);
            entity.Property(e => e.StyleTip).HasMaxLength(500);
            entity.Property(e => e.Occasion).HasMaxLength(100);
            entity.Property(e => e.Weather).HasMaxLength(50);
            
            entity.HasOne(e => e.User)
                  .WithMany(u => u.OutfitSuggestions)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.IsFavorite);
        });

        // OutfitItem junction table (many-to-many)
        modelBuilder.Entity<OutfitItem>(entity =>
        {
            entity.HasKey(e => new { e.OutfitSuggestionId, e.WardrobeItemId });
            entity.Property(e => e.ItemRole).HasMaxLength(50).IsRequired();
            
            entity.HasOne(e => e.OutfitSuggestion)
                  .WithMany(o => o.OutfitItems)
                  .HasForeignKey(e => e.OutfitSuggestionId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.WardrobeItem)
                  .WithMany(w => w.OutfitItems)
                  .HasForeignKey(e => e.WardrobeItemId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // SeasonAnalysisResult configuration
        modelBuilder.Entity<SeasonAnalysisResult>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Season).HasMaxLength(50).IsRequired();
            entity.Property(e => e.SubSeason).HasMaxLength(50).IsRequired();
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.BestMetals).HasMaxLength(200);
            entity.Property(e => e.AnalysisImagePath).HasColumnType("text");
            
            entity.HasOne(e => e.User)
                  .WithMany(u => u.SeasonAnalyses)
                  .HasForeignKey(e => e.UserId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasIndex(e => e.UserId);
        });

        // RecommendedColor junction table (many-to-many)
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

        // UserPaletteColor junction table (many-to-many)
        modelBuilder.Entity<UserPaletteColor>(entity =>
        {
            entity.HasKey(e => new { e.UserProfileId, e.ColorId });
            
            entity.HasOne(e => e.UserProfile)
                  .WithMany(p => p.PaletteColors)
                  .HasForeignKey(e => e.UserProfileId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.Color)
                  .WithMany(c => c.UserPaletteColors)
                  .HasForeignKey(e => e.ColorId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // TryOnSession configuration
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

        // WearLog configuration
        modelBuilder.Entity<WearLog>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Notes).HasMaxLength(500);
            entity.Property(e => e.Occasion).HasMaxLength(100);
            
            entity.HasOne(e => e.WardrobeItem)
                  .WithMany(w => w.WearLogs)
                  .HasForeignKey(e => e.WardrobeItemId)
                  .OnDelete(DeleteBehavior.Cascade);
                  
            entity.HasOne(e => e.OutfitSuggestion)
                  .WithMany(o => o.WearLogs)
                  .HasForeignKey(e => e.OutfitSuggestionId)
                  .OnDelete(DeleteBehavior.SetNull);
                  
            entity.HasIndex(e => e.WardrobeItemId);
            entity.HasIndex(e => e.WornAt);
        });
    }
}
