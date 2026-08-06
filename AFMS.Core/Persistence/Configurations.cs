using AFMS.Core.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace AFMS.Core.Persistence;

internal abstract class BaseConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
    where TEntity : BaseEntity
{
    public virtual void Configure(EntityTypeBuilder<TEntity> builder)
    {
        builder.HasKey(entity => entity.Id);
        builder.Property(entity => entity.Id).ValueGeneratedOnAdd();
        builder.Property(entity => entity.CreatedAt).IsRequired();
        builder.Property(entity => entity.UpdatedAt).IsRequired();
    }
}

internal sealed class UserConfiguration : BaseConfiguration<User>
{
    public override void Configure(EntityTypeBuilder<User> builder)
    {
        base.Configure(builder);
        builder.Property(user => user.Username).HasMaxLength(100).IsRequired().UseCollation("NOCASE");
        builder.Property(user => user.DisplayName).HasMaxLength(200).IsRequired();
        builder.Property(user => user.PasswordHash).HasMaxLength(200).IsRequired();
        builder.HasIndex(user => user.Username).IsUnique();
        builder.HasIndex(user => user.IsActive);
        builder.ToTable(table =>
            table.HasCheckConstraint("CK_User_FailedSignInCount", "FailedSignInCount >= 0"));
    }
}

internal sealed class ItemConfiguration : BaseConfiguration<Item>
{
    public override void Configure(EntityTypeBuilder<Item> builder)
    {
        base.Configure(builder);
        builder.Property(item => item.Code).HasMaxLength(50).IsRequired().UseCollation("NOCASE");
        builder.Property(item => item.Name).HasMaxLength(200).IsRequired();
        builder.Property(item => item.Unit).HasMaxLength(30).IsRequired();
        builder.Property(item => item.Category).HasMaxLength(100);
        builder.Property(item => item.Remarks).HasMaxLength(2000);
        builder.HasIndex(item => item.Code).IsUnique();
        builder.HasIndex(item => item.IsActive);
        builder.HasIndex(item => item.Name);
        builder.ToTable(table =>
            table.HasCheckConstraint("CK_Item_ReorderLevel", "ReorderLevel >= 0"));
    }
}

internal sealed class RecipientConfiguration : BaseConfiguration<Recipient>
{
    public override void Configure(EntityTypeBuilder<Recipient> builder)
    {
        base.Configure(builder);
        builder.Property(recipient => recipient.Code).HasMaxLength(50).IsRequired().UseCollation("NOCASE");
        builder.Property(recipient => recipient.Name).HasMaxLength(200).IsRequired();
        builder.Property(recipient => recipient.Position).HasMaxLength(150);
        builder.Property(recipient => recipient.Office).HasMaxLength(200);
        builder.Property(recipient => recipient.ContactNumber).HasMaxLength(50);
        builder.Property(recipient => recipient.Remarks).HasMaxLength(2000);
        builder.HasIndex(recipient => recipient.Code).IsUnique();
        builder.HasIndex(recipient => recipient.IsActive);
        builder.HasIndex(recipient => recipient.Name);
    }
}

internal sealed class StockInConfiguration : BaseConfiguration<StockIn>
{
    public override void Configure(EntityTypeBuilder<StockIn> builder)
    {
        base.Configure(builder);
        builder.Property(entry => entry.ReferenceNumber).HasMaxLength(40).IsRequired();
        builder.Property(entry => entry.SourceName).HasMaxLength(200);
        builder.Property(entry => entry.DocumentNumber).HasMaxLength(100);
        builder.Property(entry => entry.ReceivedBy).HasMaxLength(200);
        builder.Property(entry => entry.Remarks).HasMaxLength(2000);
        builder.Property(entry => entry.VoidReason).HasMaxLength(1000);

        builder.HasIndex(entry => entry.ReferenceNumber).IsUnique();
        builder.HasIndex(entry => entry.EntryDate);
        builder.HasIndex(entry => entry.Status);
        builder.HasIndex(entry => new { entry.Source, entry.ReturnedByRecipientId });

        builder.HasOne(entry => entry.ReturnedByRecipient)
            .WithMany()
            .HasForeignKey(entry => entry.ReturnedByRecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(entry => entry.RecordedByUser)
            .WithMany()
            .HasForeignKey(entry => entry.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(entry => entry.Lines)
            .WithOne(line => line.StockIn)
            .HasForeignKey(line => line.StockInId)
            .OnDelete(DeleteBehavior.Cascade);

        // A return has to name the person handing stock back, otherwise their
        // accountability can never be cleared.
        builder.ToTable(table => table.HasCheckConstraint(
            "CK_StockIn_ReturnNeedsRecipient",
            "Source <> 3 OR ReturnedByRecipientId IS NOT NULL"));
    }
}

internal sealed class StockInLineConfiguration : BaseConfiguration<StockInLine>
{
    public override void Configure(EntityTypeBuilder<StockInLine> builder)
    {
        base.Configure(builder);
        builder.Property(line => line.Remarks).HasMaxLength(500);
        builder.Ignore(line => line.Serials);

        builder.HasIndex(line => line.StockInId);
        builder.HasIndex(line => new { line.ItemId, line.SerialFrom, line.SerialTo });

        builder.HasOne(line => line.Item)
            .WithMany()
            .HasForeignKey(line => line.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_StockInLine_Quantity", "Quantity > 0");
            table.HasCheckConstraint(
                "CK_StockInLine_Serials",
                "(SerialFrom IS NULL AND SerialTo IS NULL) OR (SerialFrom IS NOT NULL AND SerialTo IS NOT NULL AND SerialFrom >= 0 AND SerialTo >= SerialFrom)");
        });
    }
}

internal sealed class ReleaseConfiguration : BaseConfiguration<Release>
{
    public override void Configure(EntityTypeBuilder<Release> builder)
    {
        base.Configure(builder);
        builder.Property(release => release.ReferenceNumber).HasMaxLength(40).IsRequired();
        builder.Property(release => release.Purpose).HasMaxLength(500);
        builder.Property(release => release.ReleasedBy).HasMaxLength(200).IsRequired();
        builder.Property(release => release.ReceivedBy).HasMaxLength(200).IsRequired();
        builder.Property(release => release.Remarks).HasMaxLength(2000);
        builder.Property(release => release.VoidReason).HasMaxLength(1000);

        builder.HasIndex(release => release.ReferenceNumber).IsUnique();
        builder.HasIndex(release => release.ReleaseDate);
        builder.HasIndex(release => release.Status);
        builder.HasIndex(release => new { release.RecipientId, release.Status });

        builder.HasOne(release => release.Recipient)
            .WithMany()
            .HasForeignKey(release => release.RecipientId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(release => release.RecordedByUser)
            .WithMany()
            .HasForeignKey(release => release.RecordedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(release => release.Lines)
            .WithOne(line => line.Release)
            .HasForeignKey(line => line.ReleaseId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

internal sealed class ReleaseLineConfiguration : BaseConfiguration<ReleaseLine>
{
    public override void Configure(EntityTypeBuilder<ReleaseLine> builder)
    {
        base.Configure(builder);
        builder.Property(line => line.Remarks).HasMaxLength(500);
        builder.Ignore(line => line.Serials);

        builder.HasIndex(line => line.ReleaseId);
        builder.HasIndex(line => new { line.ItemId, line.SerialFrom, line.SerialTo });

        builder.HasOne(line => line.Item)
            .WithMany()
            .HasForeignKey(line => line.ItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(table =>
        {
            table.HasCheckConstraint("CK_ReleaseLine_Quantity", "Quantity > 0");
            table.HasCheckConstraint(
                "CK_ReleaseLine_Serials",
                "(SerialFrom IS NULL AND SerialTo IS NULL) OR (SerialFrom IS NOT NULL AND SerialTo IS NOT NULL AND SerialFrom >= 0 AND SerialTo >= SerialFrom)");
        });
    }
}

internal sealed class AuditLogConfiguration : BaseConfiguration<AuditLog>
{
    public override void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        base.Configure(builder);
        builder.Property(entry => entry.Username).HasMaxLength(100);
        builder.Property(entry => entry.Description).HasMaxLength(1000).IsRequired();
        builder.Property(entry => entry.EntityName).HasMaxLength(100);
        builder.Property(entry => entry.ReferenceNumber).HasMaxLength(100);
        builder.Property(entry => entry.Details).HasMaxLength(4000);
        builder.Property(entry => entry.MachineName).HasMaxLength(100);

        builder.HasIndex(entry => entry.OccurredAt);
        builder.HasIndex(entry => entry.Activity);
        builder.HasIndex(entry => entry.ReferenceNumber);
    }
}

internal sealed class SystemSettingConfiguration : BaseConfiguration<SystemSetting>
{
    public override void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        base.Configure(builder);
        builder.Property(setting => setting.Key).HasMaxLength(100).IsRequired().UseCollation("NOCASE");
        builder.Property(setting => setting.Value).HasMaxLength(4000).IsRequired();
        builder.Property(setting => setting.Description).HasMaxLength(500);
        builder.HasIndex(setting => setting.Key).IsUnique();
    }
}

internal sealed class ReferenceSequenceConfiguration : BaseConfiguration<ReferenceSequence>
{
    public override void Configure(EntityTypeBuilder<ReferenceSequence> builder)
    {
        base.Configure(builder);
        builder.Property(sequence => sequence.Prefix).HasMaxLength(20).IsRequired();
        builder.HasIndex(sequence => new { sequence.Prefix, sequence.Year }).IsUnique();
        builder.ToTable(table =>
            table.HasCheckConstraint("CK_ReferenceSequence_LastNumber", "LastNumber >= 0"));
    }
}
