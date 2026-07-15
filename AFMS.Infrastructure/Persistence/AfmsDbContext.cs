using AFMS.Application.Abstractions;
using AFMS.Domain.Common;
using AFMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Infrastructure.Persistence;

public sealed class AfmsDbContext(DbContextOptions<AfmsDbContext> options) : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<Collector> Collectors => Set<Collector>();
    public DbSet<Barangay> Barangays => Set<Barangay>();
    public DbSet<AccountableFormType> AccountableFormTypes => Set<AccountableFormType>();
    public DbSet<StockReceipt> StockReceipts => Set<StockReceipt>();
    public DbSet<StockReceiptItem> StockReceiptItems => Set<StockReceiptItem>();
    public DbSet<AccountableFormBooklet> AccountableFormBooklets => Set<AccountableFormBooklet>();
    public DbSet<Issuance> Issuances => Set<Issuance>();
    public DbSet<IssuanceItem> IssuanceItems => Set<IssuanceItem>();
    public DbSet<UsageReport> UsageReports => Set<UsageReport>();
    public DbSet<UsageRange> UsageRanges => Set<UsageRange>();
    public DbSet<Transfer> Transfers => Set<Transfer>();
    public DbSet<ReturnTransaction> ReturnTransactions => Set<ReturnTransaction>();
    public DbSet<FormException> FormExceptions => Set<FormException>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();
    public DbSet<FiscalYear> FiscalYears => Set<FiscalYear>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AfmsDbContext).Assembly);
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
            }

            if (entry.State is EntityState.Added or EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
            }
        }
    }
}
