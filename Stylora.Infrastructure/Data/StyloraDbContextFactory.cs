using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Pgvector.EntityFrameworkCore;

namespace Stylora.Infrastructure.Data;

public class StyloraDbContextFactory : IDesignTimeDbContextFactory<StyloraDbContext>
{
    public StyloraDbContext CreateDbContext(string[] args)
    {
        // Mirror Program.cs: load .env two levels above the startup project if it exists
        var envPath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", ".env");
        if (File.Exists(envPath))
        {
            foreach (var line in File.ReadAllLines(envPath))
            {
                var trimmed = line.Trim();
                if (string.IsNullOrEmpty(trimmed) || trimmed.StartsWith('#')) continue;
                var idx = trimmed.IndexOf('=');
                if (idx <= 0) continue;
                Environment.SetEnvironmentVariable(trimmed[..idx].Trim(), trimmed[(idx + 1)..].Trim());
            }
        }

        var password = Environment.GetEnvironmentVariable("DATABASE_PASSWORD")
            ?? throw new InvalidOperationException(
                "DATABASE_PASSWORD environment variable is not set. " +
                "Set it before running 'dotnet ef database update'.");

        var options = new DbContextOptionsBuilder<StyloraDbContext>()
            .UseNpgsql($"Host=localhost;Database=stylora;Username=postgres;Password={password}",
                o => o.UseVector())
            .Options;
        return new StyloraDbContext(options);
    }
}
