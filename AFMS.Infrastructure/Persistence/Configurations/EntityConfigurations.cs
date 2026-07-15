using AFMS.Domain.Common;
using AFMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFMS.Infrastructure.Persistence.Configurations;

internal abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.UpdatedAt).IsRequired();
        builder.Property(entity => entity.IsActive).IsRequired();
        builder.HasIndex(entity => entity.IsActive);
    }
}

internal sealed class UserConfiguration : BaseEntityConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);
        builder.Property(user => user.Username).HasMaxLength(100).IsRequired();
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(user => user.Username).IsUnique();
    }
}

internal sealed class RoleConfiguration : BaseEntityConfiguration<Role>
{
    public override void Configure(EntityTypeBuilder<Role> builder)
    {
        base.Configure(builder);
        builder.Property(role => role.Name).HasMaxLength(100).IsRequired();
        builder.Property(role => role.Description).HasMaxLength(500);
        builder.HasIndex(role => role.Name).IsUnique();
    }
}

internal sealed class UserRoleConfiguration : BaseEntityConfiguration<UserRole>
{
    public override void Configure(EntityTypeBuilder<UserRole> builder)
    {
        base.Configure(builder);
        builder.HasIndex(userRole => new { userRole.UserId, userRole.RoleId }).IsUnique();
        builder.HasOne(userRole => userRole.User).WithMany(user => user.UserRoles).HasForeignKey(userRole => userRole.UserId);
        builder.HasOne(userRole => userRole.Role).WithMany(role => role.UserRoles).HasForeignKey(userRole => userRole.RoleId);
    }
}

internal sealed class CollectorConfiguration : BaseEntityConfiguration<Collector>
{
    public override void Configure(EntityTypeBuilder<Collector> builder)
    {
        base.Configure(builder);
        builder.Property(collector => collector.Code).HasMaxLength(50).IsRequired();
        builder.Property(collector => collector.FullName).HasMaxLength(200).IsRequired();
        builder.Property(collector => collector.EmployeeNumber).HasMaxLength(50);
        builder.Property(collector => collector.Position).HasMaxLength(100);
        builder.HasIndex(collector => collector.Code).IsUnique();
        builder.HasIndex(collector => collector.EmployeeNumber).IsUnique();
        builder.HasOne(collector => collector.User).WithMany().HasForeignKey(collector => collector.UserId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(collector => collector.Barangay).WithMany(barangay => barangay.Collectors).HasForeignKey(collector => collector.BarangayId).OnDelete(DeleteBehavior.SetNull);
    }
}

internal sealed class BarangayConfiguration : BaseEntityConfiguration<Barangay>
{
    public override void Configure(EntityTypeBuilder<Barangay> builder)
    {
        base.Configure(builder);
        builder.Property(barangay => barangay.Code).HasMaxLength(50).IsRequired();
        builder.Property(barangay => barangay.Name).HasMaxLength(200).IsRequired();
        builder.HasIndex(barangay => barangay.Code).IsUnique();
        builder.HasIndex(barangay => barangay.Name).IsUnique();
    }
}

internal sealed class AccountableFormTypeConfiguration : BaseEntityConfiguration<AccountableFormType>
{
    public override void Configure(EntityTypeBuilder<AccountableFormType> builder)
    {
        base.Configure(builder);
        builder.Property(form => form.Code).HasMaxLength(50).IsRequired();
        builder.Property(form => form.Name).HasMaxLength(200).IsRequired();
        builder.Property(form => form.FormNumber).HasMaxLength(50);
        builder.HasIndex(form => form.Code).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_AccountableFormType_DefaultReceipts", "DefaultReceiptsPerBooklet >= 0"));
    }
}

internal sealed class FiscalYearConfiguration : BaseEntityConfiguration<FiscalYear>
{
    public override void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        base.Configure(builder);
        builder.HasIndex(fiscalYear => fiscalYear.Year).IsUnique();
        builder.ToTable(table => table.HasCheckConstraint("CK_FiscalYear_Dates", "EndDate >= StartDate"));
    }
}

internal sealed class SystemSettingConfiguration : BaseEntityConfiguration<SystemSetting>
{
    public override void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        base.Configure(builder);
        builder.Property(setting => setting.Key).HasMaxLength(150).IsRequired();
        builder.Property(setting => setting.Value).HasMaxLength(4000).IsRequired();
        builder.HasIndex(setting => setting.Key).IsUnique();
    }
}

internal sealed class StockReceiptConfiguration : BaseEntityConfiguration<StockReceipt>
{
    public override void Configure(EntityTypeBuilder<StockReceipt> builder)
    {
        base.Configure(builder);
        builder.Property(receipt => receipt.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(receipt => receipt.ReferenceNumber).IsUnique();
        builder.HasIndex(receipt => new { receipt.ReceivedDate, receipt.Status });
        builder.HasOne(receipt => receipt.FiscalYear).WithMany(year => year.StockReceipts).HasForeignKey(receipt => receipt.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class StockReceiptItemConfiguration : BaseEntityConfiguration<StockReceiptItem>
{
    public override void Configure(EntityTypeBuilder<StockReceiptItem> builder)
    {
        base.Configure(builder);
        builder.HasIndex(item => new { item.AccountableFormTypeId, item.BeginningSerial, item.EndingSerial }).IsUnique();
        builder.HasOne(item => item.StockReceipt).WithMany(receipt => receipt.Items).HasForeignKey(item => item.StockReceiptId);
        builder.HasOne(item => item.AccountableFormType).WithMany().HasForeignKey(item => item.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockReceiptItem_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
            table.HasCheckConstraint("CK_StockReceiptItem_Quantity", "Quantity > 0");
        });
    }
}

internal sealed class AccountableFormBookletConfiguration : BaseEntityConfiguration<AccountableFormBooklet>
{
    public override void Configure(EntityTypeBuilder<AccountableFormBooklet> builder)
    {
        base.Configure(builder);
        builder.HasIndex(booklet => new { booklet.AccountableFormTypeId, booklet.BeginningSerial, booklet.EndingSerial }).IsUnique();
        builder.HasIndex(booklet => new { booklet.Status, booklet.CurrentCollectorId });
        builder.HasOne(booklet => booklet.StockReceiptItem).WithMany(item => item.Booklets).HasForeignKey(booklet => booklet.StockReceiptItemId);
        builder.HasOne(booklet => booklet.AccountableFormType).WithMany().HasForeignKey(booklet => booklet.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(booklet => booklet.CurrentCollector).WithMany().HasForeignKey(booklet => booklet.CurrentCollectorId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(table => table.HasCheckConstraint("CK_AccountableFormBooklet_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class IssuanceConfiguration : BaseEntityConfiguration<Issuance>
{
    public override void Configure(EntityTypeBuilder<Issuance> builder)
    {
        base.Configure(builder);
        builder.Property(issuance => issuance.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(issuance => issuance.ReferenceNumber).IsUnique();
        builder.HasIndex(issuance => new { issuance.CollectorId, issuance.IssuedDate, issuance.Status });
        builder.HasOne(issuance => issuance.Collector).WithMany().HasForeignKey(issuance => issuance.CollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(issuance => issuance.IssuedByUser).WithMany().HasForeignKey(issuance => issuance.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(issuance => issuance.ApprovedByUser).WithMany().HasForeignKey(issuance => issuance.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IssuanceItemConfiguration : BaseEntityConfiguration<IssuanceItem>
{
    public override void Configure(EntityTypeBuilder<IssuanceItem> builder)
    {
        base.Configure(builder);
        builder.HasIndex(item => new { item.IssuanceId, item.AccountableFormBookletId }).IsUnique();
        builder.HasOne(item => item.Issuance).WithMany(issuance => issuance.Items).HasForeignKey(item => item.IssuanceId);
        builder.HasOne(item => item.AccountableFormBooklet).WithMany().HasForeignKey(item => item.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_IssuanceItem_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class UsageReportConfiguration : BaseEntityConfiguration<UsageReport>
{
    public override void Configure(EntityTypeBuilder<UsageReport> builder)
    {
        base.Configure(builder);
        builder.Property(report => report.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(report => report.ReferenceNumber).IsUnique();
        builder.HasIndex(report => new { report.CollectorId, report.PeriodStart, report.PeriodEnd });
        builder.HasOne(report => report.Collector).WithMany().HasForeignKey(report => report.CollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_UsageReport_Period", "PeriodEnd >= PeriodStart"));
    }
}

internal sealed class UsageRangeConfiguration : BaseEntityConfiguration<UsageRange>
{
    public override void Configure(EntityTypeBuilder<UsageRange> builder)
    {
        base.Configure(builder);
        builder.HasIndex(range => new { range.UsageReportId, range.AccountableFormBookletId, range.BeginningSerial, range.EndingSerial }).IsUnique();
        builder.HasOne(range => range.UsageReport).WithMany(report => report.Ranges).HasForeignKey(range => range.UsageReportId);
        builder.HasOne(range => range.AccountableFormBooklet).WithMany().HasForeignKey(range => range.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_UsageRange_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class TransferConfiguration : BaseEntityConfiguration<Transfer>
{
    public override void Configure(EntityTypeBuilder<Transfer> builder)
    {
        base.Configure(builder);
        builder.Property(transfer => transfer.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(transfer => transfer.ReferenceNumber).IsUnique();
        builder.HasIndex(transfer => new { transfer.TransferDate, transfer.Status });
        builder.HasOne(transfer => transfer.AccountableFormBooklet).WithMany().HasForeignKey(transfer => transfer.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.FromCollector).WithMany().HasForeignKey(transfer => transfer.FromCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.ToCollector).WithMany().HasForeignKey(transfer => transfer.ToCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_Transfer_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class ReturnTransactionConfiguration : BaseEntityConfiguration<ReturnTransaction>
{
    public override void Configure(EntityTypeBuilder<ReturnTransaction> builder)
    {
        base.Configure(builder);
        builder.Property(transaction => transaction.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(transaction => transaction.ReferenceNumber).IsUnique();
        builder.HasIndex(transaction => new { transaction.ReturnDate, transaction.Status });
        builder.HasOne(transaction => transaction.Collector).WithMany().HasForeignKey(transaction => transaction.CollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.AccountableFormBooklet).WithMany().HasForeignKey(transaction => transaction.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_ReturnTransaction_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class FormExceptionConfiguration : BaseEntityConfiguration<FormException>
{
    public override void Configure(EntityTypeBuilder<FormException> builder)
    {
        base.Configure(builder);
        builder.Property(exception => exception.Details).HasMaxLength(2000).IsRequired();
        builder.HasIndex(exception => new { exception.AccountableFormBookletId, exception.SerialNumber, exception.ExceptionType });
        builder.HasOne(exception => exception.AccountableFormBooklet).WithMany().HasForeignKey(exception => exception.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(exception => exception.Collector).WithMany().HasForeignKey(exception => exception.CollectorId).OnDelete(DeleteBehavior.SetNull);
        builder.ToTable(table => table.HasCheckConstraint("CK_FormException_Serial", "SerialNumber >= 0"));
    }
}

internal sealed class StockMovementConfiguration : BaseEntityConfiguration<StockMovement>
{
    public override void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        base.Configure(builder);
        builder.HasIndex(movement => new { movement.AccountableFormBookletId, movement.OccurredAt });
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId });
        builder.HasOne(movement => movement.AccountableFormBooklet).WithMany(booklet => booklet.Movements).HasForeignKey(movement => movement.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.FromCollector).WithMany().HasForeignKey(movement => movement.FromCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.ToCollector).WithMany().HasForeignKey(movement => movement.ToCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.PerformedByUser).WithMany().HasForeignKey(movement => movement.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_StockMovement_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class AuditLogConfiguration : BaseEntityConfiguration<AuditLog>
{
    public override void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        base.Configure(builder);
        builder.Property(audit => audit.Description).HasMaxLength(2000).IsRequired();
        builder.HasIndex(audit => audit.OccurredAt);
        builder.HasIndex(audit => new { audit.EntityName, audit.EntityId });
        builder.HasOne(audit => audit.User).WithMany(user => user.AuditLogs).HasForeignKey(audit => audit.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
