using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Tests.Infrastructure;

public sealed class Phase5OperationalWorkflowTests
{
    [Fact]
    public async Task CompleteWorkflow_PartialUseTransferAndReturn_PreservesBalancesAndRanges()
    {
        await using var f = await Fixture.CreateAsync(510001); var service = f.Service<IOperationalTransactionService>();
        var usage = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, 1200m, "RCD-1", null,
            [new(f.BookletId, 510001, 510010)]));
        await service.FinalizeUsageAsync(usage.Id);

        var transfer = await service.SaveTransferDraftAsync(new(null, f.Today, f.CollectorA, f.CollectorB, f.BarangayId, f.BookletId,
            510011, 510030, "Reassignment", "Custodian", "Collector B", null));
        await service.SubmitTransferAsync(transfer.Id); await f.ApproveAsync(() => service.ApproveTransferAsync(transfer.Id)); await service.FinalizeTransferAsync(transfer.Id);

        var returned = await service.SaveReturnDraftAsync(new(null, f.Today, f.CollectorB, f.BookletId, 510011, 510020,
            "Good", "Excess forms", "Custodian", ReturnDisposition.ReturnedToAvailableStock, null));
        await service.SubmitReturnAsync(returned.Id); await f.ApproveAsync(() => service.ApproveReturnAsync(returned.Id)); await service.FinalizeReturnAsync(returned.Id);

        var ranges = await f.Service<AfmsDbContext>().AccountabilityRanges.Where(x => x.IsActive).OrderBy(x => x.BeginningSerial).ToListAsync();
        Assert.Collection(ranges,
            x => { Assert.Equal(510001, x.BeginningSerial); Assert.Equal(AccountableFormStatus.FullyUsed, x.Status); },
            x => { Assert.Equal(510011, x.BeginningSerial); Assert.Equal(510020, x.EndingSerial); Assert.Equal(AccountableFormStatus.Available, x.Status); },
            x => { Assert.Equal(510021, x.BeginningSerial); Assert.Equal(f.CollectorB, x.CurrentCollectorId); },
            x => { Assert.Equal(510031, x.BeginningSerial); Assert.Equal(f.CollectorA, x.CurrentCollectorId); });
        var ledger = f.Service<ICollectorAccountabilityService>();
        Assert.Equal(20, (await ledger.GetLedgerAsync(new(f.CollectorA))).Last().RunningBalance);
        Assert.Equal(10, (await ledger.GetLedgerAsync(new(f.CollectorB))).Last().RunningBalance);
        await Assert.ThrowsAsync<InvalidOperationException>(() => f.ReverseAsync(() => service.ReverseTransferAsync(transfer.Id, "Cannot bypass later return")));
        await f.ReverseAsync(() => service.ReverseReturnAsync(returned.Id, "Return entry corrected"));
        await f.ReverseAsync(() => service.ReverseTransferAsync(transfer.Id, "Transfer entry corrected"));
        Assert.Equal(40, (await ledger.GetLedgerAsync(new(f.CollectorA))).Last().RunningBalance);
        Assert.Equal(0, (await ledger.GetLedgerAsync(new(f.CollectorB))).Last().RunningBalance);
    }

    [Fact]
    public async Task Usage_MultipleRangesUpdatesBookletAndRejectsDuplicateOrWrongCollector()
    {
        await using var f = await Fixture.CreateAsync(520001); var service = f.Service<IOperationalTransactionService>();
        var usage = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, null, null, null,
            [new(f.BookletId, 520001, 520005), new(f.BookletId, 520010, 520014, "Void gap documented in source report")]));
        await service.FinalizeUsageAsync(usage.Id);
        var booklet = await f.Service<AfmsDbContext>().AccountableFormBooklets.SingleAsync(x => x.Id == f.BookletId);
        Assert.Equal(10, booklet.UsedQuantity); Assert.Equal(40, booklet.RemainingQuantity); Assert.Equal(AccountableFormStatus.PartiallyUsed, booklet.Status);
        var duplicate = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, null, null, null, [new(f.BookletId, 520003, 520006)]));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.FinalizeUsageAsync(duplicate.Id));
        var wrong = await service.SaveUsageDraftAsync(new(null, f.CollectorB, f.FormTypeId, f.Today, f.Today, f.Today, null, null, null, [new(f.BookletId, 520020, 520021)]));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.FinalizeUsageAsync(wrong.Id));
    }

    [Fact]
    public async Task FullBookletUsage_PreservesInactiveRangeHistoryAndCreatesFinalActiveRange()
    {
        await using var f = await Fixture.CreateAsync(525001);
        var service = f.Service<IOperationalTransactionService>();
        var usage = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, null, "RCD-525", null,
            [new(f.BookletId, 525001, 525050)]));

        await service.FinalizeUsageAsync(usage.Id);

        var context = f.Service<AfmsDbContext>();
        var ranges = await context.AccountabilityRanges
            .Where(range => range.AccountableFormBookletId == f.BookletId)
            .ToListAsync();
        Assert.Equal(2, ranges.Count);
        Assert.Single(ranges, range => !range.IsActive && range.Status == AccountableFormStatus.Issued);
        var finalRange = Assert.Single(ranges, range => range.IsActive);
        Assert.Equal(AccountableFormStatus.FullyUsed, finalRange.Status);
        Assert.Equal(525001, finalRange.BeginningSerial);
        Assert.Equal(525050, finalRange.EndingSerial);
        Assert.Null(finalRange.CurrentCollectorId);
        var booklet = await context.AccountableFormBooklets.SingleAsync(booklet => booklet.Id == f.BookletId);
        Assert.Equal(AccountableFormStatus.FullyUsed, booklet.Status);
        Assert.Equal(0, booklet.RemainingQuantity);
    }

    [Fact]
    public async Task BarangayFilteredLedger_IncludesUsageMovementAndCorrectBalance()
    {
        await using var f = await Fixture.CreateAsync(527001);
        var service = f.Service<IOperationalTransactionService>();
        var usage = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, null, null, null,
            [new(f.BookletId, 527001, 527005)]));
        await service.FinalizeUsageAsync(usage.Id);

        var ledger = await f.Service<ICollectorAccountabilityService>()
            .GetLedgerAsync(new(f.CollectorA, BarangayId: f.BarangayId));
        Assert.Equal(2, ledger.Count);
        Assert.Equal(AccountabilityTransactionType.Usage, ledger[^1].TransactionType);
        Assert.Equal(45, ledger[^1].RunningBalance);
    }

    [Fact]
    public async Task InactiveCollector_CanReturnOutstandingFormsUnderApproval()
    {
        await using var f = await Fixture.CreateAsync(528001);
        var context = f.Service<AfmsDbContext>();
        var collector = await context.Collectors.SingleAsync(value => value.Id == f.CollectorA);
        collector.IsActive = false;
        await context.SaveChangesAsync();

        var service = f.Service<IOperationalTransactionService>();
        var returned = await service.SaveReturnDraftAsync(new(null, f.Today, f.CollectorA, f.BookletId, 528001, 528010,
            "Good", "Accountability clearance after deactivation", "Custodian", ReturnDisposition.ReturnedToAvailableStock, null));
        await service.SubmitReturnAsync(returned.Id);
        await f.ApproveAsync(() => service.ApproveReturnAsync(returned.Id));
        await service.FinalizeReturnAsync(returned.Id);

        var activeRange = await context.AccountabilityRanges.SingleAsync(range => range.IsActive && range.BeginningSerial == 528001);
        Assert.Equal(AccountableFormStatus.Available, activeRange.Status);
        Assert.Null(activeRange.CurrentCollectorId);
    }

    [Fact]
    public async Task ReturnedPartialRange_CanBeReissuedWithoutChangingRemainingCollectorOwnership()
    {
        await using var f = await Fixture.CreateAsync(529001);
        var operations = f.Service<IOperationalTransactionService>();
        var returned = await operations.SaveReturnDraftAsync(new(null, f.Today, f.CollectorA, f.BookletId, 529001, 529010,
            "Good", "Returned for reassignment", "Custodian", ReturnDisposition.ReturnedToAvailableStock, null));
        await operations.SubmitReturnAsync(returned.Id);
        await f.ApproveAsync(() => operations.ApproveReturnAsync(returned.Id));
        await operations.FinalizeReturnAsync(returned.Id);

        var issuance = f.Service<IIssuanceService>();
        var available = Assert.Single(await issuance.ListAvailableBookletsAsync(new()));
        Assert.Equal(529001, available.BeginningSerial);
        Assert.Equal(529010, available.EndingSerial);
        var draft = await issuance.SaveDraftAsync(new(null, f.Today, f.CollectorB, f.BarangayId, null,
            "Reissue returned receipts", "Custodian", "Collector B", null, [available.Id]));
        await issuance.SubmitAsync(draft.Id);
        await f.ApproveAsync(() => issuance.ApproveAsync(draft.Id));
        await issuance.FinalizeAsync(draft.Id);

        var ranges = await f.Service<AfmsDbContext>().AccountabilityRanges.Where(range => range.IsActive)
            .OrderBy(range => range.BeginningSerial).ToListAsync();
        Assert.Collection(ranges,
            range => { Assert.Equal(529001, range.BeginningSerial); Assert.Equal(529010, range.EndingSerial); Assert.Equal(f.CollectorB, range.CurrentCollectorId); Assert.Equal(AccountableFormStatus.Issued, range.Status); },
            range => { Assert.Equal(529011, range.BeginningSerial); Assert.Equal(529050, range.EndingSerial); Assert.Equal(f.CollectorA, range.CurrentCollectorId); });
        Assert.Equal(40, (await f.Service<ICollectorAccountabilityService>().GetLedgerAsync(new(f.CollectorA))).Last().RunningBalance);
        Assert.Equal(10, (await f.Service<ICollectorAccountabilityService>().GetLedgerAsync(new(f.CollectorB))).Last().RunningBalance);
    }

    [Fact]
    public async Task TransferUsedReceiptsAndReturnUnownedReceipts_AreRejectedAtomically()
    {
        await using var f = await Fixture.CreateAsync(530001); var service = f.Service<IOperationalTransactionService>();
        var usage = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, null, null, null, [new(f.BookletId, 530001, 530010)])); await service.FinalizeUsageAsync(usage.Id);
        var transfer = await service.SaveTransferDraftAsync(new(null, f.Today, f.CollectorA, f.CollectorB, null, f.BookletId, 530005, 530015, "Invalid test", "A", "B", null));
        await service.SubmitTransferAsync(transfer.Id); await f.ApproveAsync(() => service.ApproveTransferAsync(transfer.Id));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.FinalizeTransferAsync(transfer.Id));
        var returned = await service.SaveReturnDraftAsync(new(null, f.Today, f.CollectorB, f.BookletId, 530020, 530025, "Good", "Invalid test", "A", ReturnDisposition.ReturnedToAvailableStock, null));
        await service.SubmitReturnAsync(returned.Id); await f.ApproveAsync(() => service.ApproveReturnAsync(returned.Id));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => service.FinalizeReturnAsync(returned.Id));
        Assert.Empty(await f.Service<AfmsDbContext>().StockMovements.Where(x => x.ReferenceId == transfer.Id || x.ReferenceId == returned.Id).ToListAsync());
    }

    [Fact]
    public async Task LostExceptionRequiresApprovalAndReversalRestoresAccountability()
    {
        await using var f = await Fixture.CreateAsync(540001); var service = f.Service<IOperationalTransactionService>();
        var incident = await service.SaveExceptionDraftAsync(new(null, f.Today, ExceptionType.Lost, f.CollectorA, f.BookletId, 540001, 540003,
            "Forms lost during field collection", "POLICE-1", "Reported to supervisor", null));
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FinalizeExceptionAsync(incident.Id));
        await service.SubmitExceptionAsync(incident.Id); await f.ApproveAsync(() => service.ApproveExceptionAsync(incident.Id)); await service.FinalizeExceptionAsync(incident.Id);
        Assert.Equal(AccountableFormStatus.Lost, (await f.Service<AfmsDbContext>().AccountabilityRanges.SingleAsync(x => x.IsActive && x.BeginningSerial == 540001)).Status);
        await Assert.ThrowsAsync<UnauthorizedAccessException>(() => service.ReverseExceptionAsync(incident.Id, "Self reversal must be rejected"));
        await f.ReverseAsync(() => service.ReverseExceptionAsync(incident.Id, "Forms recovered and verified"));
        var restored = await f.Service<AfmsDbContext>().AccountabilityRanges.SingleAsync(x => x.IsActive && x.BeginningSerial == 540001);
        Assert.Equal(AccountableFormStatus.Issued, restored.Status); Assert.Equal(f.CollectorA, restored.CurrentCollectorId);
        Assert.Equal(50, (await f.Service<ICollectorAccountabilityService>().GetLedgerAsync(new(f.CollectorA))).Last().RunningBalance);
    }

    [Fact]
    public async Task FullLifecycle_ClosesFiscalYear_WithVerifiedBalancesAndMovementHistory()
    {
        await using var f = await Fixture.CreateAsync(550001); var service = f.Service<IOperationalTransactionService>();
        var usage = await service.SaveUsageDraftAsync(new(null, f.CollectorA, f.FormTypeId, f.Today, f.Today, f.Today, null, "RCD-550", null,
            [new(f.BookletId, 550001, 550005)]));
        await service.FinalizeUsageAsync(usage.Id);
        var transfer = await service.SaveTransferDraftAsync(new(null, f.Today, f.CollectorA, f.CollectorB, f.BarangayId, f.BookletId,
            550006, 550020, "Authorized reassignment", "Custodian", "Collector B", null));
        await service.SubmitTransferAsync(transfer.Id); await f.ApproveAsync(() => service.ApproveTransferAsync(transfer.Id)); await service.FinalizeTransferAsync(transfer.Id);
        var returned = await service.SaveReturnDraftAsync(new(null, f.Today, f.CollectorB, f.BookletId, 550006, 550010,
            "Good", "Unused at period end", "Custodian", ReturnDisposition.ReturnedToAvailableStock, null));
        await service.SubmitReturnAsync(returned.Id); await f.ApproveAsync(() => service.ApproveReturnAsync(returned.Id)); await service.FinalizeReturnAsync(returned.Id);
        var cancelled = await service.SaveExceptionDraftAsync(new(null, f.Today, ExceptionType.Cancelled, f.CollectorA, f.BookletId, 550021, 550021,
            "Receipt invalidated after documented encoding error", "MEMO-550", "Cancelled under supervisor control", null));
        await service.SubmitExceptionAsync(cancelled.Id); await f.ApproveAsync(() => service.ApproveExceptionAsync(cancelled.Id)); await service.FinalizeExceptionAsync(cancelled.Id);

        var years = f.Service<IFiscalYearService>(); var year = await years.CreateAsync(2026, new(2026, 1, 1), new(2026, 12, 31));
        await years.RequestCloseAsync(year.Id, "Annual close with one documented and approved cancellation exception", true);
        await f.ApproveAsync(() => years.ApproveCloseAsync(year.Id, "Supervisor verified the documented exception and final balances"));

        var ledger = f.Service<ICollectorAccountabilityService>();
        Assert.Equal(29, (await ledger.GetLedgerAsync(new(f.CollectorA))).Last().RunningBalance);
        Assert.Equal(10, (await ledger.GetLedgerAsync(new(f.CollectorB))).Last().RunningBalance);
        var db = f.Service<AfmsDbContext>();
        Assert.Contains(await db.StockMovements.ToListAsync(), x => x.MovementType == StockMovementType.Cancelled);
        Assert.True((await years.ListAsync()).Single(x => x.Id == year.Id).IsClosed);
        Assert.NotEmpty(await years.GetSnapshotAsync(year.Id));
        await Assert.ThrowsAsync<ApplicationValidationException>(() => f.Service<IFiscalPeriodGuard>().EnsureOpenAsync(f.Today));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path; private readonly ServiceProvider _provider; private readonly IServiceScope _scope;
        private Guid AdminId { get; init; } private Guid SupervisorId { get; init; } private Guid ReviewerId { get; init; }
        public Guid CollectorA { get; private init; } public Guid CollectorB { get; private init; } public Guid BarangayId { get; private init; }
        public Guid FormTypeId { get; private init; } public Guid BookletId { get; private init; } public DateOnly Today => new(2026, 7, 16);
        private Fixture(string path, ServiceProvider provider, IServiceScope scope) { _path = path; _provider = provider; _scope = scope; }
        public T Service<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();
        public static async Task<Fixture> CreateAsync(long beginning)
        {
            var path = Path.Combine(Path.GetTempPath(), $"afms-phase5-{Guid.NewGuid():N}.db"); var services = new ServiceCollection();
            services.AddLogging(); services.AddApplication(); services.AddInfrastructure($"Data Source={path};Pooling=False");
            var provider = services.BuildServiceProvider(); var scope = provider.CreateScope(); await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var db = scope.ServiceProvider.GetRequiredService<AfmsDbContext>(); var user = new User { Username = $"admin{beginning}", DisplayName = "Administrator", PasswordHash = "test" };
            var supervisor = new User { Username = $"supervisor{beginning}", DisplayName = "Supervisor", PasswordHash = "test" };
            var reviewer = new User { Username = $"reviewer{beginning}", DisplayName = "Reversal Administrator", PasswordHash = "test" };
            var form = new AccountableFormType { Code = $"AF-{beginning}", Name = "Local", DefaultReceiptsPerBooklet = 50 };
            var barangay = new Barangay { Code = $"B-{beginning}", Name = $"Barangay {beginning}", Municipality = "Dipaculao", Province = "Aurora" };
            var a = new Collector { Code = $"A-{beginning}", FullName = "Collector A" }; var b = new Collector { Code = $"B-{beginning}", FullName = "Collector B" };
            db.AddRange(user, supervisor, reviewer, form, barangay, a, b); await db.SaveChangesAsync();
            scope.ServiceProvider.GetRequiredService<ICurrentUserSession>().Begin(new CurrentUser(user.Id, user.Username, user.DisplayName, new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            var receiving = scope.ServiceProvider.GetRequiredService<IStockReceivingService>(); var receipt = await receiving.SaveDraftAsync(new(null, new(2026, 7, 16), form.Id, "BLGF", "DR-5", 2026, "Vault", "Treasurer", null, [new(1, 50, beginning, beginning + 49)]));
            await receiving.SubmitAsync(receipt.Id); await receiving.FinalizeAsync(receipt.Id); db.ChangeTracker.Clear(); var booklet = await db.AccountableFormBooklets.SingleAsync();
            var issuance = scope.ServiceProvider.GetRequiredService<IIssuanceService>(); var issue = await issuance.SaveDraftAsync(new(null, new(2026, 7, 16), a.Id, barangay.Id, null, "Collection", "Custodian", "Collector A", null, [booklet.Id]));
            await issuance.SubmitAsync(issue.Id);
            var session = scope.ServiceProvider.GetRequiredService<ICurrentUserSession>();
            session.Begin(new CurrentUser(supervisor.Id, supervisor.Username, supervisor.DisplayName, new HashSet<string> { "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            await issuance.ApproveAsync(issue.Id);
            session.Begin(new CurrentUser(user.Id, user.Username, user.DisplayName, new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            await issuance.FinalizeAsync(issue.Id);
            db.ChangeTracker.Clear();
            return new Fixture(path, provider, scope) { AdminId = user.Id, SupervisorId = supervisor.Id, ReviewerId = reviewer.Id, CollectorA = a.Id, CollectorB = b.Id, BarangayId = barangay.Id, FormTypeId = form.Id, BookletId = booklet.Id };
        }
        public async Task ApproveAsync(Func<Task> action)
        {
            var session = Service<ICurrentUserSession>(); session.Begin(new CurrentUser(SupervisorId, "phase5.supervisor", "Supervisor", new HashSet<string> { "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            try { await action(); } finally { session.Begin(new CurrentUser(AdminId, "phase5.admin", "Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false)); }
        }
        public async Task ReverseAsync(Func<Task> action)
        {
            var session = Service<ICurrentUserSession>(); session.Begin(new CurrentUser(ReviewerId, "phase5.reviewer", "Reversal Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            try { await action(); } finally { session.Begin(new CurrentUser(AdminId, "phase5.admin", "Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false)); }
        }
        public async ValueTask DisposeAsync() { _scope.Dispose(); await _provider.DisposeAsync(); Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools(); if (File.Exists(_path)) File.Delete(_path); }
    }
}
