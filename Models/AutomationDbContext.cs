using Microsoft.EntityFrameworkCore;

namespace PartTracker.Models;

public class AutomationDbContext : DbContext
{
    public DbSet<ActivityForm> Activity { get; set; }
    public DbSet<SplunkEntry> Splunk { get; set; }
    public DbSet<HelperEntry> Helper { get; set; }
    public DbSet<SpeedUpRequest> SpeedUpRequests { get; set; }
    public DbSet<Premium2025> Premiums2025 { get; set; }
    public DbSet<Premium2024> Premiums2024 { get; set; }
    public DbSet<PartPrice> PartPrices { get; set; }
    public DbSet<SafetyStockFormEntry> SafetyStockForms { get; set; }

    public AutomationDbContext(DbContextOptions<AutomationDbContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        
        modelBuilder.Entity<ActivityForm>().ToTable("activity");
        modelBuilder.Entity<SplunkEntry>()
            .ToTable("splunk")
            .HasKey(s => s.LoadNumber);
        modelBuilder.Entity<HelperEntry>()
            .ToTable("helper")
            .HasKey(h => h.Id);
        modelBuilder.Entity<SpeedUpRequest>()
            .ToTable("ibl_emea_speedup_requests")
            .HasKey(s => s.Id);
        modelBuilder.Entity<Premium2025>()
            .ToTable("premiums2025")
            .HasKey(p => p.Id);
        modelBuilder.Entity<Premium2024>()
            .ToTable("premiums2024")
            .HasKey(p => p.Id);
        modelBuilder.Entity<PartPrice>()
            .ToTable("Part_Price");
        modelBuilder.Entity<SafetyStockFormEntry>()
            .ToTable("safetystockform")
            .HasKey(s => s.Id);
    }
}
