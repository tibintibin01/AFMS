using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Files;
using AFMS.Infrastructure.Persistence;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Tests.Infrastructure;

public sealed class Phase7HardeningIntegrationTests
{
    [Fact]
    public async Task Backup_IsValidated_RestoresOnlyAfterShutdown_AndCreatesSafetyCopy()
    {
        var root = Path.Combine(Path.GetTempPath(), $"afms-phase7-backup-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root); var paths = new TestPaths(root); paths.EnsureDirectoriesExist();
        ServiceProvider? provider = null; IServiceScope? scope = null; BackupHistoryDto backup;
        Guid requesterId = Guid.Empty;
        try
        {
            var services = new ServiceCollection(); services.AddLogging(); services.AddApplication();
            services.AddInfrastructure($"Data Source={paths.DatabasePath};Pooling=False"); services.AddSingleton<IApplicationPathService>(paths);
            provider = services.BuildServiceProvider(); scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var db = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
            var user = new User { Username = "backup.admin", DisplayName = "Backup Administrator", PasswordHash = "test" };
            requesterId = user.Id;
            db.Users.Add(user); db.SystemSettings.Add(new SystemSetting { Key = "DatabaseBackupFolder", Value = paths.BackupDirectory }); await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<ICurrentUserSession>().Begin(Admin(user));
            var service = scope.ServiceProvider.GetRequiredService<IBackupService>();
            var attachment = Path.Combine(paths.DataDirectory, "Attachments", "FormException", Guid.NewGuid().ToString("N"), "evidence.pdf");
            Directory.CreateDirectory(Path.GetDirectoryName(attachment)!);
            await File.WriteAllTextAsync(attachment, "original supporting evidence");
            backup = await service.CreateAsync(BackupKind.Manual);
            Assert.True((await service.ValidateAsync(backup.FilePath)).IsValid);
            var tampered = await service.CreateAsync(BackupKind.Manual);
            await File.AppendAllTextAsync(tampered.FilePath, "tampered");
            Assert.False((await service.ValidateAsync(tampered.FilePath)).IsValid);
            db.SystemSettings.Add(new SystemSetting { Key = "TransientAfterBackup", Value = "must disappear after restore" }); await db.SaveChangesAsync();
            await File.WriteAllTextAsync(attachment, "changed after backup");
            await service.ScheduleRestoreAsync(backup.FilePath, "Integration verification restore");
            Assert.True(service.HasPendingRestore);
            Assert.Contains(Directory.EnumerateFiles(paths.BackupDirectory, "*PreRestore*.afmsbak"), _ => true);
            scope.Dispose(); scope = null; await provider.DisposeAsync(); provider = null; SqliteConnection.ClearAllPools();

            BackupService.ApplyPendingRestore(paths);
            await using var connection = new SqliteConnection($"Data Source={paths.DatabasePath};Mode=ReadOnly;Pooling=False");
            await connection.OpenAsync(); await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM SystemSettings WHERE Key='TransientAfterBackup';";
            Assert.Equal(0L, Convert.ToInt64(await command.ExecuteScalarAsync()));
            Assert.Equal("original supporting evidence", await File.ReadAllTextAsync(attachment));
            await connection.CloseAsync();

            services = new ServiceCollection(); services.AddLogging(); services.AddApplication();
            services.AddInfrastructure($"Data Source={paths.DatabasePath};Pooling=False"); services.AddSingleton<IApplicationPathService>(paths);
            provider = services.BuildServiceProvider(); scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            await scope.ServiceProvider.GetRequiredService<IBackupService>().CompletePendingRestoreAuditAsync();
            var restoredAudit = await scope.ServiceProvider.GetRequiredService<AfmsDbContext>().AuditLogs
                .SingleAsync(log => log.ActivityType == UserActivityType.Restored);
            Assert.Equal(requesterId, restoredAudit.UserId);
            Assert.Contains("backup.admin", restoredAudit.Description, StringComparison.OrdinalIgnoreCase);
            var restoreLedger = await File.ReadAllLinesAsync(Path.Combine(paths.DataDirectory, "restore-ledger.jsonl"));
            Assert.Contains(restoreLedger, line => line.Contains("\"Event\":\"Scheduled\"", StringComparison.Ordinal));
            Assert.Contains(restoreLedger, line => line.Contains("\"Event\":\"Applied\"", StringComparison.Ordinal));
            Assert.Contains(restoreLedger, line => line.Contains("\"Event\":\"Completed\"", StringComparison.Ordinal));

            await File.WriteAllTextAsync(Path.Combine(root, "corrupt.afmsbak"), "not a database");
            Assert.False((await BackupService.ValidateDatabaseFileAsync(Path.Combine(root, "corrupt.afmsbak"))).IsValid);
        }
        finally
        {
            scope?.Dispose(); if (provider is not null) await provider.DisposeAsync(); SqliteConnection.ClearAllPools();
            try { Directory.Delete(root, true); } catch { }
        }
    }

    [Fact]
    public async Task FiscalYear_CloseAndReopen_RequireDifferentAuthorizedUsers_AndLockTransactions()
    {
        await using var fixture = await FiscalFixture.CreateAsync();
        var years = fixture.Service<IFiscalYearService>(); var session = fixture.Service<ICurrentUserSession>();
        var year = await years.CreateAsync(2026, new(2026, 1, 1), new(2026, 12, 31));
        await years.RequestCloseAsync(year.Id, "Annual records reviewed for authorized closure", false);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => years.ApproveCloseAsync(year.Id, "self approval must fail"));

        session.Begin(fixture.Supervisor);
        await years.ApproveCloseAsync(year.Id, "Supervisor verified year-end balances");
        Assert.True((await years.ListAsync()).Single().IsClosed);
        await Assert.ThrowsAsync<AFMS.Application.Validation.ApplicationValidationException>(() =>
            fixture.Service<IFiscalPeriodGuard>().EnsureOpenAsync(new(2026, 7, 16)));

        session.Begin(fixture.Administrator);
        await years.RequestReopenAsync(year.Id, "Correction requested after year-end reconciliation review");
        session.Begin(fixture.Supervisor);
        await years.ApproveReopenAsync(year.Id, "Supporting reconciliation was verified");
        Assert.False((await years.ListAsync()).Single().IsClosed);

        var audits = await fixture.Service<IAuditQueryService>().SearchAsync(new(Module: "Fiscal Year", PageSize: 100));
        Assert.Contains(audits.Items, x => x.ActivityType == UserActivityType.FiscalYearClosed && !string.IsNullOrWhiteSpace(x.IntegrityHash));
        Assert.Contains(audits.Items, x => x.ActivityType == UserActivityType.FiscalYearReopened);
    }

    [Fact]
    public async Task FiscalYearSnapshot_ReconstructsOwnershipAtYearEnd_NotCurrentState()
    {
        await using var fixture = await FiscalFixture.CreateAsync();
        var db = fixture.Service<AfmsDbContext>();
        var userId = fixture.Administrator.Id;
        var form = new AccountableFormType { Code = "FY-ASOF", Name = "As-of Test", DefaultReceiptsPerBooklet = 50 };
        var collectorA = new Collector { Code = "FY-A", FullName = "Year-end Collector" };
        var collectorB = new Collector { Code = "FY-B", FullName = "Later Collector" };
        var receipt = new StockReceipt
        {
            ReferenceNumber = "RCV-2026-ASOF", ReceivedDate = new(2026, 12, 1), AccountableFormType = form,
            AccountableFormTypeId = form.Id, EncodedByUserId = userId, SeriesYear = 2026, Status = TransactionStatus.Finalized
        };
        var item = new StockReceiptItem
        {
            StockReceipt = receipt, StockReceiptId = receipt.Id, AccountableFormType = form, AccountableFormTypeId = form.Id,
            BeginningSerial = 900001, EndingSerial = 900050, Quantity = 50, NumberOfBooklets = 1, ReceiptsPerBooklet = 50
        };
        var booklet = new AccountableFormBooklet
        {
            StockReceiptItem = item, StockReceiptItemId = item.Id, AccountableFormType = form, AccountableFormTypeId = form.Id,
            SeriesYear = 2026, BookletSequence = 1, BeginningSerial = 900001, EndingSerial = 900050,
            TotalQuantity = 50, RemainingQuantity = 50, DateReceived = new(2026, 12, 1), DateIssued = new(2026, 12, 15),
            Status = AccountableFormStatus.Transferred, CurrentCollector = collectorB, CurrentCollectorId = collectorB.Id
        };
        booklet.AccountabilityRanges.Add(new AccountabilityRange
        {
            AccountableFormBooklet = booklet, AccountableFormBookletId = booklet.Id, BeginningSerial = 900001, EndingSerial = 900050,
            Status = AccountableFormStatus.Transferred, CurrentCollector = collectorB, CurrentCollectorId = collectorB.Id
        });
        item.Booklets.Add(booklet); receipt.Items.Add(item);
        db.AddRange(form, collectorA, collectorB, receipt);
        var issuanceId = Guid.NewGuid(); var transferId = Guid.NewGuid();
        db.StockMovements.AddRange(
            Movement(StockMovementType.Received, "RCV-2026-ASOF", "StockReceipt", receipt.Id, new(2026, 12, 1), null, null, 50),
            Movement(StockMovementType.Issued, "ISS-2026-ASOF", "Issuance", issuanceId, new(2026, 12, 15), null, collectorA.Id, 0),
            Movement(StockMovementType.Transferred, "TRF-2027-ASOF", "TransferOut", transferId, new(2027, 1, 2), collectorA.Id, null, 0),
            Movement(StockMovementType.Transferred, "TRF-2027-ASOF", "TransferIn", transferId, new(2027, 1, 2), null, collectorB.Id, 0));
        await db.SaveChangesAsync();

        var years = fixture.Service<IFiscalYearService>();
        var year = await years.CreateAsync(2026, new(2026, 1, 1), new(2026, 12, 31));
        await years.RequestCloseAsync(year.Id, "Retroactive year-end ownership reconstruction test", false);
        fixture.Service<ICurrentUserSession>().Begin(fixture.Supervisor);
        await years.ApproveCloseAsync(year.Id, "Supervisor verified the as-of movement reconstruction");

        var snapshot = Assert.Single(await years.GetSnapshotAsync(year.Id));
        Assert.Equal(50, snapshot.OutstandingQuantity);
        var carryForward = await db.FiscalYearCarryForwards.SingleAsync(row => row.FiscalYearId == year.Id);
        Assert.Equal(collectorA.Id, carryForward.CollectorId);

        StockMovement Movement(StockMovementType type, string reference, string referenceType, Guid referenceId, DateOnly date,
            Guid? from, Guid? to, long inventoryDelta) => new()
        {
            AccountableFormBooklet = booklet, AccountableFormBookletId = booklet.Id, AccountableFormType = form, AccountableFormTypeId = form.Id,
            MovementType = type, ReferenceNumber = reference, BeginningSerial = 900001, EndingSerial = 900050, Quantity = 50,
            InventoryQuantityDelta = inventoryDelta, FromCollectorId = from, ToCollectorId = to, ReferenceType = referenceType,
            ReferenceId = referenceId, PerformedByUserId = userId, EffectiveDate = date,
            OccurredAt = new DateTimeOffset(date.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero)
        };
    }

    [Fact]
    public async Task FiscalYearClose_IsBlockedBySubmittedStockReceipt()
    {
        await using var fixture = await FiscalFixture.CreateAsync();
        var db = fixture.Service<AfmsDbContext>();
        var form = new AccountableFormType { Code = "FY-PENDING", Name = "Pending Test", DefaultReceiptsPerBooklet = 50 };
        db.AddRange(form, new StockReceipt
        {
            ReferenceNumber = "RCV-2026-PENDING", ReceivedDate = new(2026, 12, 20), AccountableFormType = form,
            AccountableFormTypeId = form.Id, SeriesYear = 2026, EncodedByUserId = fixture.Administrator.Id,
            Status = TransactionStatus.Submitted
        });
        await db.SaveChangesAsync();
        var years = fixture.Service<IFiscalYearService>();
        var year = await years.CreateAsync(2026, new(2026, 1, 1), new(2026, 12, 31));

        var check = await years.CheckCloseAsync(year.Id);

        Assert.Equal(1, check.UnapprovedTransactions);
        await Assert.ThrowsAsync<AFMS.Application.Validation.ApplicationValidationException>(() =>
            years.RequestCloseAsync(year.Id, "Attempted close with submitted stock receipt", false));
    }

    [Fact]
    public async Task AuditLog_IsAppendOnly_AndSupportsServerPagedFiltering()
    {
        await using var fixture = await FiscalFixture.CreateAsync(); var audit = fixture.Service<IAuditService>();
        for (var i = 0; i < 35; i++) await audit.RecordAsync(UserActivityType.Exported, $"Export {i}", "Report", Guid.NewGuid(), null, new { i }, module: "Reports", referenceNumber: $"RPT-{i:000}");
        await fixture.Service<IUnitOfWork>().SaveChangesAsync();
        var page = await fixture.Service<IAuditQueryService>().SearchAsync(new(Module: "Reports", Page: 2, PageSize: 25));
        Assert.Equal(35, page.TotalCount); Assert.Equal(10, page.Items.Count); Assert.All(page.Items, x => Assert.False(string.IsNullOrWhiteSpace(x.IntegrityHash)));
        var db = fixture.Service<AfmsDbContext>(); var item = await db.AuditLogs.FirstAsync(); item.Description = "tampered";
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync("UPDATE AuditLogs SET Description='direct tamper' WHERE Id={0}", item.Id));

        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER TR_AuditLogs_AppendOnly_Update;");
        await db.Database.ExecuteSqlRawAsync("UPDATE AuditLogs SET Description='direct tamper' WHERE Id={0}", item.Id);
        await Assert.ThrowsAsync<InvalidDataException>(() => fixture.Service<IDatabaseInitializer>().InitializeAsync());
    }

    private static CurrentUser Admin(User user) => new(user.Id, user.Username, user.DisplayName,
        new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false);

    private sealed class FiscalFixture : IAsyncDisposable
    {
        private readonly string _path; private readonly ServiceProvider _provider; private readonly IServiceScope _scope;
        public CurrentUser Administrator { get; } public CurrentUser Supervisor { get; }
        private FiscalFixture(string path, ServiceProvider provider, IServiceScope scope, CurrentUser administrator, CurrentUser supervisor)
        { _path = path; _provider = provider; _scope = scope; Administrator = administrator; Supervisor = supervisor; }
        public T Service<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
        public static async Task<FiscalFixture> CreateAsync()
        {
            var path = Path.Combine(Path.GetTempPath(), $"afms-phase7-fiscal-{Guid.NewGuid():N}.db"); var services = new ServiceCollection();
            services.AddLogging(); services.AddApplication(); services.AddInfrastructure($"Data Source={path};Pooling=False");
            var provider = services.BuildServiceProvider(); var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var db = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
            var admin = new User { Username = "phase7.admin", DisplayName = "Phase 7 Administrator", PasswordHash = "test" };
            var supervisor = new User { Username = "phase7.supervisor", DisplayName = "Phase 7 Supervisor", PasswordHash = "test" };
            db.AddRange(admin, supervisor); await db.SaveChangesAsync();
            var adminUser = new CurrentUser(admin.Id, admin.Username, admin.DisplayName, new HashSet<string> { "Administrator", "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false);
            var supervisorUser = new CurrentUser(supervisor.Id, supervisor.Username, supervisor.DisplayName, new HashSet<string> { "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false);
            scope.ServiceProvider.GetRequiredService<ICurrentUserSession>().Begin(adminUser);
            return new(path, provider, scope, adminUser, supervisorUser);
        }
        public async ValueTask DisposeAsync() { _scope.Dispose(); await _provider.DisposeAsync(); SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path); }
    }

    private sealed class TestPaths(string root) : IApplicationPathService
    {
        public string DataDirectory { get; } = Path.Combine(root, "data");
        public string DatabasePath { get; } = Path.Combine(root, "data", "afms.db");
        public string LogDirectory { get; } = Path.Combine(root, "logs");
        public string BackupDirectory { get; } = Path.Combine(root, "backups");
        public void EnsureDirectoriesExist() { Directory.CreateDirectory(DataDirectory); Directory.CreateDirectory(LogDirectory); Directory.CreateDirectory(BackupDirectory); }
    }
}
