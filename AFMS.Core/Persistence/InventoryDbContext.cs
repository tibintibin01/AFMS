using AFMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace AFMS.Core.Persistence;

public sealed class InventoryDbContext(DbContextOptions<InventoryDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Item> Items => Set<Item>();
    public DbSet<Recipient> Recipients => Set<Recipient>();
    public DbSet<StockIn> StockIns => Set<StockIn>();
    public DbSet<StockInLine> StockInLines => Set<StockInLine>();
    public DbSet<Release> Releases => Set<Release>();
    public DbSet<ReleaseLine> ReleaseLines => Set<ReleaseLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<ReferenceSequence> ReferenceSequences => Set<ReferenceSequence>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(InventoryDbContext).Assembly);
    }

    /// <summary>
    /// SQLite cannot sort or compare a DateTimeOffset column, so every timestamp is
    /// stored as UTC ticks. That keeps ordering and date filters running in SQL
    /// instead of silently falling back to loading whole tables into memory.
    /// </summary>
    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTimeOffset>().HaveConversion<UtcTicksConverter>();
    }

    private sealed class UtcTicksConverter() : ValueConverter<DateTimeOffset, long>(
        moment => moment.UtcDateTime.Ticks,
        ticks => new DateTimeOffset(ticks, TimeSpan.Zero));

    /// <summary>
    /// Blocked on purpose. Every write in this app goes through the async path so a
    /// long save can never freeze the window.
    /// </summary>
    public override int SaveChanges() =>
        throw new NotSupportedException("Use SaveChangesAsync so the user interface stays responsive.");

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        StampTimestamps();
        GuardAuditTrail();
        return base.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Audit entries are added only. Editing or removing one is a bug, so fail loudly.</summary>
    private void GuardAuditTrail()
    {
        var tampered = ChangeTracker.Entries<AuditLog>()
            .Any(entry => entry.State is EntityState.Modified or EntityState.Deleted);
        if (tampered)
            throw new InvalidOperationException("Audit entries cannot be changed or removed once written.");
    }

    private void StampTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
                entry.Entity.CreatedAt = now;
            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Entity.UpdatedAt = now;
        }
    }
}
