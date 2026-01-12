using Microsoft.EntityFrameworkCore;

namespace PartTracker.Models;

public class AutomationDbContext : DbContext
{
    public DbSet<ActivityForm> Activity { get; set; }
    public DbSet<SplunkEntry> Splunk { get; set; }
    public DbSet<HelperEntry> Helper { get; set; }

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
    }
}
