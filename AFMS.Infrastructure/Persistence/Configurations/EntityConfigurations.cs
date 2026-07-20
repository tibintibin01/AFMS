using AFMS.Domain.Common;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
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
        builder.Property(user => user.Username).UseCollation("NOCASE");
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
        builder.Property(user => user.SecurityVersion).HasDefaultValue(1);
        builder.HasIndex(user => user.Username).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_User_FailedLoginCount", "FailedLoginCount >= 0");
            table.HasCheckConstraint("CK_User_SecurityVersion", "SecurityVersion > 0");
        });
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

internal sealed class RolePermissionConfiguration : BaseEntityConfiguration<RolePermission>
{
    public override void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        base.Configure(builder);
        builder.HasIndex(item => new { item.RoleId, item.Permission }).IsUnique();
        builder.HasOne(item => item.Role).WithMany(role => role.Permissions).HasForeignKey(item => item.RoleId);
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
        builder.Property(collector => collector.OfficeOrDepartment).HasMaxLength(200);
        builder.Property(collector => collector.ContactNumber).HasMaxLength(50);
        builder.Property(collector => collector.EmailAddress).HasMaxLength(254);
        builder.Property(collector => collector.AppointmentReference).HasMaxLength(250);
        builder.Property(collector => collector.Remarks).HasMaxLength(2000);
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
        builder.Property(barangay => barangay.Municipality).HasMaxLength(200).IsRequired();
        builder.Property(barangay => barangay.Province).HasMaxLength(200).IsRequired();
        builder.Property(barangay => barangay.Remarks).HasMaxLength(2000);
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
        builder.Property(form => form.Category).HasMaxLength(100);
        builder.Property(form => form.SerialNumberPrefix).HasMaxLength(50);
        builder.Property(form => form.Remarks).HasMaxLength(2000);
        builder.HasIndex(form => form.Code).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AccountableFormType_DefaultReceipts", "DefaultReceiptsPerBooklet > 0");
            table.HasCheckConstraint("CK_AccountableFormType_SerialLength", "SerialNumberLength IS NULL OR SerialNumberLength > 0");
        });
    }
}

internal sealed class FiscalYearConfiguration : BaseEntityConfiguration<FiscalYear>
{
    public override void Configure(EntityTypeBuilder<FiscalYear> builder)
    {
        base.Configure(builder);
        builder.HasIndex(fiscalYear => fiscalYear.Year).IsUnique();
        builder.Property(fiscalYear => fiscalYear.WorkflowStatus).HasDefaultValue(FiscalYearWorkflowStatus.Open).HasSentinel((FiscalYearWorkflowStatus)0);
        builder.HasIndex(fiscalYear => fiscalYear.WorkflowStatus);
        builder.Property(x => x.CloseRequestReason).HasMaxLength(2000);
        builder.Property(x => x.CloseValidationIssues).HasMaxLength(8000);
        builder.Property(x => x.ClosureReason).HasMaxLength(2000);
        builder.Property(x => x.ReopenReason).HasMaxLength(2000);
        builder.Property(x => x.SnapshotReference).HasMaxLength(100);
        builder.ToTable(table => table.HasCheckConstraint("CK_FiscalYear_Dates", "EndDate >= StartDate"));
    }
}

internal sealed class FiscalYearSnapshotConfiguration : BaseEntityConfiguration<FiscalYearSnapshot>
{
    public override void Configure(EntityTypeBuilder<FiscalYearSnapshot> builder)
    {
        base.Configure(builder);
        builder.HasIndex(x => new { x.FiscalYearId, x.AccountableFormTypeId }).IsUnique();
        builder.HasOne(x => x.FiscalYear).WithMany(x => x.Snapshots).HasForeignKey(x => x.FiscalYearId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AccountableFormType).WithMany().HasForeignKey(x => x.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_FiscalYearSnapshot_Quantities", "BeginningQuantity >= 0 AND Additions >= 0 AND Usage >= 0 AND Exceptions >= 0 AND EndingQuantity >= 0 AND OutstandingQuantity >= 0"));
    }
}

internal sealed class FiscalYearCarryForwardConfiguration : BaseEntityConfiguration<FiscalYearCarryForward>
{
    public override void Configure(EntityTypeBuilder<FiscalYearCarryForward> builder)
    {
        base.Configure(builder);
        builder.HasIndex(x => new { x.FiscalYearId, x.AccountableFormBookletId, x.BeginningSerial, x.EndingSerial }).IsUnique();
        builder.HasOne(x => x.FiscalYear).WithMany(x => x.CarryForwards).HasForeignKey(x => x.FiscalYearId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.AccountableFormBooklet).WithMany().HasForeignKey(x => x.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Collector).WithMany().HasForeignKey(x => x.CollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_FiscalYearCarryForward_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
            table.HasCheckConstraint("CK_FiscalYearCarryForward_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");
        });
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
        builder.Property(receipt => receipt.Source).HasMaxLength(250);
        builder.Property(receipt => receipt.SourceDocumentNumber).HasMaxLength(100);
        builder.Property(receipt => receipt.StorageLocation).HasMaxLength(200);
        builder.Property(receipt => receipt.ReceivedBy).HasMaxLength(200);
        builder.Property(receipt => receipt.Remarks).HasMaxLength(2000);
        builder.HasIndex(receipt => receipt.ReferenceNumber).IsUnique();
        builder.HasIndex(receipt => new { receipt.ReceivedDate, receipt.Status, receipt.SeriesYear });
        builder.HasOne(receipt => receipt.AccountableFormType).WithMany().HasForeignKey(receipt => receipt.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.EncodedByUser).WithMany().HasForeignKey(receipt => receipt.EncodedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(receipt => receipt.FiscalYear).WithMany(year => year.StockReceipts).HasForeignKey(receipt => receipt.FiscalYearId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_StockReceipt_SeriesYear", "SeriesYear >= 1900 AND SeriesYear <= 9999"));
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
            table.HasCheckConstraint("CK_StockReceiptItem_Booklets", "NumberOfBooklets > 0 AND ReceiptsPerBooklet > 0");
            table.HasCheckConstraint("CK_StockReceiptItem_CalculatedQuantity", "Quantity = (EndingSerial - BeginningSerial + 1) AND Quantity = (NumberOfBooklets * ReceiptsPerBooklet)");
        });
    }
}

internal sealed class AccountableFormBookletConfiguration : BaseEntityConfiguration<AccountableFormBooklet>
{
    public override void Configure(EntityTypeBuilder<AccountableFormBooklet> builder)
    {
        base.Configure(builder);
        builder.HasIndex(booklet => new { booklet.AccountableFormTypeId, booklet.SeriesYear, booklet.BeginningSerial, booklet.EndingSerial }).IsUnique();
        builder.HasIndex(booklet => new { booklet.StockReceiptItemId, booklet.BookletSequence }).IsUnique();
        builder.HasIndex(booklet => new { booklet.Status, booklet.CurrentCollectorId });
        builder.HasOne(booklet => booklet.StockReceiptItem).WithMany(item => item.Booklets).HasForeignKey(booklet => booklet.StockReceiptItemId);
        builder.HasOne(booklet => booklet.AccountableFormType).WithMany().HasForeignKey(booklet => booklet.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(booklet => booklet.CurrentCollector).WithMany().HasForeignKey(booklet => booklet.CurrentCollectorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(booklet => booklet.CurrentBarangay).WithMany().HasForeignKey(booklet => booklet.CurrentBarangayId).OnDelete(DeleteBehavior.SetNull);
        builder.Property(booklet => booklet.StorageLocation).HasMaxLength(200);
        builder.Property(booklet => booklet.Remarks).HasMaxLength(2000);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_AccountableFormBooklet_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
            table.HasCheckConstraint("CK_AccountableFormBooklet_Quantities", "TotalQuantity > 0 AND UsedQuantity >= 0 AND RemainingQuantity >= 0 AND UsedQuantity + RemainingQuantity = TotalQuantity");
            table.HasCheckConstraint("CK_AccountableFormBooklet_Sequence", "BookletSequence > 0 AND SeriesYear >= 1900 AND SeriesYear <= 9999");
        });
    }
}

internal sealed class IssuanceConfiguration : BaseEntityConfiguration<Issuance>
{
    public override void Configure(EntityTypeBuilder<Issuance> builder)
    {
        base.Configure(builder);
        builder.Property(issuance => issuance.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(issuance => issuance.CollectionArea).HasMaxLength(200);
        builder.Property(issuance => issuance.Purpose).HasMaxLength(500).IsRequired();
        builder.Property(issuance => issuance.ReleasedBy).HasMaxLength(200).IsRequired();
        builder.Property(issuance => issuance.ReceivedBy).HasMaxLength(200).IsRequired();
        builder.Property(issuance => issuance.Remarks).HasMaxLength(2000);
        builder.Property(issuance => issuance.RejectionReason).HasMaxLength(1000);
        builder.Property(issuance => issuance.DecisionComments).HasMaxLength(2000);
        builder.HasIndex(issuance => issuance.ReferenceNumber).IsUnique();
        builder.HasIndex(issuance => new { issuance.CollectorId, issuance.IssuedDate, issuance.Status });
        builder.HasOne(issuance => issuance.Collector).WithMany().HasForeignKey(issuance => issuance.CollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(issuance => issuance.Barangay).WithMany().HasForeignKey(issuance => issuance.BarangayId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(issuance => issuance.IssuedByUser).WithMany().HasForeignKey(issuance => issuance.IssuedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(issuance => issuance.ApprovedByUser).WithMany().HasForeignKey(issuance => issuance.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

internal sealed class IssuanceItemConfiguration : BaseEntityConfiguration<IssuanceItem>
{
    public override void Configure(EntityTypeBuilder<IssuanceItem> builder)
    {
        base.Configure(builder);
        builder.HasIndex(item => new { item.IssuanceId, item.AccountableFormBookletId, item.BeginningSerial, item.EndingSerial }).IsUnique();
        builder.HasIndex(item => new { item.AccountableFormBookletId, item.BeginningSerial, item.EndingSerial }).IsUnique().HasFilter("IsReservationActive = 1");
        builder.HasOne(item => item.Issuance).WithMany(issuance => issuance.Items).HasForeignKey(item => item.IssuanceId);
        builder.HasOne(item => item.AccountableFormBooklet).WithMany(booklet => booklet.IssuanceItems).HasForeignKey(item => item.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_IssuanceItem_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
            table.HasCheckConstraint("CK_IssuanceItem_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");
            table.HasCheckConstraint("CK_IssuanceItem_SeriesYear", "SeriesYear >= 1900 AND SeriesYear <= 9999");
        });
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
        builder.HasOne(report => report.AccountableFormType).WithMany().HasForeignKey(report => report.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(report => report.EncodedByUser).WithMany().HasForeignKey(report => report.EncodedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(report => report.AmountCollected).HasPrecision(18, 2);
        builder.Property(report => report.SourceReportNumber).HasMaxLength(100);
        builder.Property(report => report.Remarks).HasMaxLength(2000);
        builder.Property(report => report.ReversalReason).HasMaxLength(1000);
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
        builder.HasOne(range => range.AccountableFormBooklet).WithMany(booklet => booklet.UsageRanges).HasForeignKey(range => range.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(range => range.GapExplanation).HasMaxLength(1000);
        builder.ToTable(table => { table.HasCheckConstraint("CK_UsageRange_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"); table.HasCheckConstraint("CK_UsageRange_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)"); });
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
        builder.HasOne(transfer => transfer.Barangay).WithMany().HasForeignKey(transfer => transfer.BarangayId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(transfer => transfer.EncodedByUser).WithMany().HasForeignKey(transfer => transfer.EncodedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transfer => transfer.ApprovedByUser).WithMany().HasForeignKey(transfer => transfer.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(transfer => transfer.Reason).HasMaxLength(1000).IsRequired(); builder.Property(transfer => transfer.ReleasedBy).HasMaxLength(200).IsRequired();
        builder.Property(transfer => transfer.ReceivedBy).HasMaxLength(200).IsRequired(); builder.Property(transfer => transfer.Remarks).HasMaxLength(2000); builder.Property(transfer => transfer.ReversalReason).HasMaxLength(1000);
        builder.Property(transfer => transfer.DecisionComments).HasMaxLength(2000);
        builder.ToTable(table => { table.HasCheckConstraint("CK_Transfer_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"); table.HasCheckConstraint("CK_Transfer_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)"); });
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
        builder.HasOne(transaction => transaction.OriginalIssuance).WithMany().HasForeignKey(transaction => transaction.OriginalIssuanceId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.EncodedByUser).WithMany().HasForeignKey(transaction => transaction.EncodedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(transaction => transaction.ApprovedByUser).WithMany().HasForeignKey(transaction => transaction.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(transaction => transaction.PhysicalCondition).HasMaxLength(200).IsRequired(); builder.Property(transaction => transaction.ReceivedBy).HasMaxLength(200).IsRequired();
        builder.Property(transaction => transaction.Reason).HasMaxLength(1000).IsRequired(); builder.Property(transaction => transaction.ReversalReason).HasMaxLength(1000);
        builder.Property(transaction => transaction.DecisionComments).HasMaxLength(2000);
        builder.ToTable(table => { table.HasCheckConstraint("CK_ReturnTransaction_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"); table.HasCheckConstraint("CK_ReturnTransaction_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)"); });
    }
}

internal sealed class FormExceptionConfiguration : BaseEntityConfiguration<FormException>
{
    public override void Configure(EntityTypeBuilder<FormException> builder)
    {
        base.Configure(builder);
        builder.Property(exception => exception.Details).HasMaxLength(2000).IsRequired();
        builder.Property(exception => exception.ReferenceNumber).HasMaxLength(100).IsRequired(); builder.HasIndex(exception => exception.ReferenceNumber).IsUnique();
        builder.Property(exception => exception.ActionTaken).HasMaxLength(2000).IsRequired(); builder.Property(exception => exception.SupportingDocumentReference).HasMaxLength(250);
        builder.Property(exception => exception.Remarks).HasMaxLength(2000); builder.Property(exception => exception.ReversalReason).HasMaxLength(1000);
        builder.Property(exception => exception.DecisionComments).HasMaxLength(2000);
        builder.HasIndex(exception => new { exception.AccountableFormBookletId, exception.SerialNumber, exception.EndingSerial, exception.ExceptionType });
        builder.HasOne(exception => exception.AccountableFormBooklet).WithMany(booklet => booklet.Exceptions).HasForeignKey(exception => exception.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(exception => exception.Collector).WithMany().HasForeignKey(exception => exception.CollectorId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(exception => exception.EncodedByUser).WithMany().HasForeignKey(exception => exception.EncodedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(exception => exception.ApprovedByUser).WithMany().HasForeignKey(exception => exception.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => { table.HasCheckConstraint("CK_FormException_Serial", "SerialNumber >= 0 AND EndingSerial >= SerialNumber"); table.HasCheckConstraint("CK_FormException_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - SerialNumber + 1)"); });
}

internal sealed class AccountabilityRangeConfiguration : BaseEntityConfiguration<AccountabilityRange>
{
    public override void Configure(EntityTypeBuilder<AccountabilityRange> builder)
    {
        base.Configure(builder); builder.HasIndex(x => new { x.AccountableFormBookletId, x.BeginningSerial, x.EndingSerial }).IsUnique().HasFilter("IsActive = 1");
        builder.HasIndex(x => new { x.CurrentCollectorId, x.Status, x.IsActive }); builder.HasIndex(x => new { x.LastTransactionType, x.LastTransactionId });
        builder.HasOne(x => x.AccountableFormBooklet).WithMany(x => x.AccountabilityRanges).HasForeignKey(x => x.AccountableFormBookletId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CurrentCollector).WithMany().HasForeignKey(x => x.CurrentCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CurrentBarangay).WithMany().HasForeignKey(x => x.CurrentBarangayId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(x => x.SourceIssuance).WithMany().HasForeignKey(x => x.SourceIssuanceId).OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.StorageLocation).HasMaxLength(200); builder.Property(x => x.LastTransactionType).HasMaxLength(100); builder.Property(x => x.PreviousStorageLocation).HasMaxLength(200); builder.Property(x => x.PreviousTransactionType).HasMaxLength(100);
        builder.ToTable(table => table.HasCheckConstraint("CK_AccountabilityRange_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial"));
    }
}

internal sealed class TransactionAttachmentConfiguration : BaseEntityConfiguration<TransactionAttachment>
{
    public override void Configure(EntityTypeBuilder<TransactionAttachment> builder)
    {
        base.Configure(builder); builder.Property(x => x.OriginalFileName).HasMaxLength(260).IsRequired(); builder.Property(x => x.StoredFileName).HasMaxLength(260).IsRequired();
        builder.Property(x => x.FileType).HasMaxLength(100).IsRequired(); builder.HasIndex(x => new { x.RelatedTransactionType, x.RelatedTransactionId }); builder.HasIndex(x => x.StoredFileName).IsUnique();
        builder.HasOne(x => x.UploadedByUser).WithMany().HasForeignKey(x => x.UploadedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table => table.HasCheckConstraint("CK_TransactionAttachment_Size", "FileSize > 0"));
    }
}
}

internal sealed class StockMovementConfiguration : BaseEntityConfiguration<StockMovement>
{
    public override void Configure(EntityTypeBuilder<StockMovement> builder)
    {
        base.Configure(builder);
        builder.Property(movement => movement.OccurredAt).HasConversion(
            value => value.UtcDateTime,
            value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        builder.Property(movement => movement.ReferenceNumber).HasMaxLength(100).IsRequired();
        builder.Property(movement => movement.Source).HasMaxLength(250);
        builder.Property(movement => movement.Destination).HasMaxLength(250);
        builder.Property(movement => movement.Remarks).HasMaxLength(2000);
        builder.HasIndex(movement => new { movement.AccountableFormBookletId, movement.OccurredAt });
        builder.HasIndex(movement => new { movement.EffectiveDate, movement.MovementType });
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId });
        builder.HasIndex(movement => new { movement.ReferenceType, movement.ReferenceId, movement.AccountableFormBookletId, movement.MovementType }).IsUnique();
        builder.HasOne(movement => movement.AccountableFormBooklet).WithMany(booklet => booklet.Movements).HasForeignKey(movement => movement.AccountableFormBookletId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.AccountableFormType).WithMany().HasForeignKey(movement => movement.AccountableFormTypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.FromCollector).WithMany().HasForeignKey(movement => movement.FromCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.ToCollector).WithMany().HasForeignKey(movement => movement.ToCollectorId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(movement => movement.Barangay).WithMany().HasForeignKey(movement => movement.BarangayId).OnDelete(DeleteBehavior.SetNull);
        builder.HasOne(movement => movement.PerformedByUser).WithMany().HasForeignKey(movement => movement.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockMovement_Serials", "BeginningSerial >= 0 AND EndingSerial >= BeginningSerial");
            table.HasCheckConstraint("CK_StockMovement_Quantity", "Quantity > 0 AND Quantity = (EndingSerial - BeginningSerial + 1)");
            table.HasCheckConstraint("CK_StockMovement_InventoryDelta", "InventoryQuantityDelta >= -Quantity AND InventoryQuantityDelta <= Quantity");
            table.HasCheckConstraint("CK_StockMovement_EffectiveDate", "EffectiveDate >= '1900-01-01'");
        });
    }
}

internal sealed class ReferenceNumberSequenceConfiguration : BaseEntityConfiguration<ReferenceNumberSequence>
{
    public override void Configure(EntityTypeBuilder<ReferenceNumberSequence> builder)
    {
        base.Configure(builder);
        builder.Property(item => item.Prefix).HasMaxLength(10).IsRequired();
        builder.HasIndex(item => new { item.Prefix, item.Year }).IsUnique();
        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ReferenceNumberSequence_Year", "Year >= 1900 AND Year <= 9999");
            table.HasCheckConstraint("CK_ReferenceNumberSequence_Number", "LastNumber > 0");
        });
    }
}

internal sealed class AuditLogConfiguration : BaseEntityConfiguration<AuditLog>
{
    public override void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        base.Configure(builder);
        builder.Property(audit => audit.Description).HasMaxLength(2000).IsRequired();
        builder.Property(audit => audit.OldValues).HasMaxLength(8000);
        builder.Property(audit => audit.NewValues).HasMaxLength(8000);
        builder.Property(audit => audit.Module).HasMaxLength(150);
        builder.Property(audit => audit.ReferenceNumber).HasMaxLength(150);
        builder.Property(audit => audit.Reason).HasMaxLength(2000);
        builder.Property(audit => audit.PreviousHash).HasMaxLength(64);
        builder.Property(audit => audit.IntegrityHash).HasMaxLength(64);
        builder.Property(audit => audit.OccurredAt).HasConversion(
            value => value.UtcDateTime,
            value => new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc)));
        builder.HasIndex(audit => audit.OccurredAt);
        builder.HasIndex(audit => new { audit.Module, audit.ActivityType, audit.OccurredAt });
        builder.HasIndex(audit => audit.ReferenceNumber);
        builder.HasIndex(audit => audit.IntegrityHash).IsUnique().HasFilter("IntegrityHash IS NOT NULL");
        builder.HasIndex(audit => new { audit.EntityName, audit.EntityId });
        builder.HasOne(audit => audit.User).WithMany(user => user.AuditLogs).HasForeignKey(audit => audit.UserId).OnDelete(DeleteBehavior.SetNull);
    }
}
