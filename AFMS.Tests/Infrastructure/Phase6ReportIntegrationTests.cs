using AFMS.Application;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Infrastructure;
using AFMS.Infrastructure.Persistence;
using System.IO.Compression;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFMS.Tests.Infrastructure;

public sealed class Phase6ReportIntegrationTests
{
    [Fact]
    public async Task OpeningAndEndingBalances_UseMovementHistory()
    {
        await using var f = await Fixture.CreateAsync(610001);
        await f.UseAsync(610001, 610010);

        var report = await f.ReportAsync(ReportType.BalanceRollForward);

        Assert.Single(report.Rows);
        Assert.Equal(100m, Value(report, 0, "Beginning"));
        Assert.Equal(10m, Value(report, 0, "Usage"));
        Assert.Equal(90m, Value(report, 0, "Ending"));
    }

    [Fact]
    public async Task AnnualConsolidatedTotals_AgreeWithRollForward()
    {
        await using var f = await Fixture.CreateAsync(620001);
        await f.UseAsync(620001, 620004);
        var annual = await f.ReportAsync(ReportType.AnnualConsolidated);
        var rollForward = await f.ReportAsync(ReportType.BalanceRollForward);

        Assert.Equal(Value(rollForward, 0, "Ending"), Value(annual, 0, "Ending"));
        Assert.Equal(96m, annual.Summaries.Single(x => x.Label == "Ending").Value);
    }

    [Fact]
    public async Task CollectorLedger_CalculatesRunningBalance()
    {
        await using var f = await Fixture.CreateAsync(630001);
        await f.UseAsync(630001, 630009);

        var report = await f.ReportAsync(ReportType.CollectorAccountabilityLedger, new(f.From, f.To, 2026, CollectorId: f.CollectorA));

        Assert.NotEmpty(report.Rows);
        Assert.Equal(41m, Value(report, report.Rows.Count - 1, "Running Balance"));
    }

    [Fact]
    public async Task DateRangeFilter_ExcludesEarlierMovements()
    {
        await using var f = await Fixture.CreateAsync(640001);
        var report = await f.ReportAsync(ReportType.StockMovement, new(f.To, f.To));

        Assert.DoesNotContain(report.Rows, x => Equals(x.Values[2], StockMovementType.Received));
        Assert.Contains(report.Rows, x => Equals(x.Values[2], StockMovementType.Issued));
    }

    [Fact]
    public async Task ReversedUsage_RestoresEndingBalanceAndIsReported()
    {
        await using var f = await Fixture.CreateAsync(650001);
        var usageId = await f.UseAsync(650001, 650010);
        await f.ReverseAsync(() => f.Service<IOperationalTransactionService>().ReverseUsageAsync(usageId, "Encoding correction"));

        var balances = await f.ReportAsync(ReportType.BalanceRollForward);
        var reversals = await f.ReportAsync(ReportType.ReversalRegister);

        Assert.Equal(100m, Value(balances, 0, "Ending"));
        Assert.Equal(10m, Value(balances, 0, "Net Reversals"));
        Assert.Single(reversals.Rows);
    }

    [Fact]
    public async Task ReturnedToStock_ReducesCollectorBalanceAndAppearsAsAvailable()
    {
        await using var f = await Fixture.CreateAsync(660001);
        await f.ReturnAsync(660001, 660010);

        var collector = await f.ReportAsync(ReportType.CollectorAccountabilityLedger, new(f.From, f.To, 2026, CollectorId: f.CollectorA));
        var available = await f.ReportAsync(ReportType.AvailableForms);

        Assert.Equal(40m, Value(collector, collector.Rows.Count - 1, "Running Balance"));
        Assert.Contains(available.Rows, x => Convert.ToInt64(x.Values[1]) == 660001 && Convert.ToInt64(x.Values[2]) == 660010);
        Assert.Equal(60m, available.Summaries.Single(x => x.Label == "Total quantity").Value);
    }

    [Fact]
    public async Task DamagedReturn_ReducesGlobalInventory_AndReversalRestoresIt()
    {
        await using var f = await Fixture.CreateAsync(665001);
        var returnId = await f.ReturnAsync(665001, 665010, ReturnDisposition.ReturnedButDamaged);

        var reduced = await f.ReportAsync(ReportType.BalanceRollForward);
        Assert.Equal(10m, Value(reduced, 0, "Exceptions"));
        Assert.Equal(90m, Value(reduced, 0, "Ending"));

        await f.ReverseAsync(() => f.Service<IOperationalTransactionService>().ReverseReturnAsync(returnId, "Physical condition was recorded incorrectly"));
        var restored = await f.ReportAsync(ReportType.BalanceRollForward);
        Assert.Equal(100m, Value(restored, 0, "Ending"));
        var deltas = await f.Service<AfmsDbContext>().StockMovements
            .Where(movement => movement.ReferenceId == returnId)
            .Select(movement => movement.InventoryQuantityDelta)
            .ToListAsync();
        Assert.Contains(-10, deltas);
        Assert.Contains(10, deltas);
    }

    [Fact]
    public async Task ExceptionTotals_ExcludeFormsFromEndingInventory()
    {
        await using var f = await Fixture.CreateAsync(670001);
        await f.LoseAsync(670001, 670002);

        var balances = await f.ReportAsync(ReportType.BalanceRollForward);
        var exceptions = await f.ReportAsync(ReportType.LostMissingForms);

        Assert.Equal(2m, Value(balances, 0, "Exceptions"));
        Assert.Equal(98m, Value(balances, 0, "Ending"));
        Assert.Equal(2m, exceptions.Summaries.Single(x => x.Label == "Finalized quantity").Value);
    }

    [Fact]
    public async Task EmptyReport_ExportsValidPdfAndExcelSamples()
    {
        await using var f = await Fixture.CreateAsync(680001);
        var report = await f.ReportAsync(ReportType.DamagedForms);
        Assert.True(report.IsEmpty);

        var output = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "output", "reports"));
        Directory.CreateDirectory(output);
        var pdfPath = Path.Combine(output, "phase6-sample-empty-exception-report.pdf");
        var excelPath = Path.Combine(output, "phase6-sample-empty-exception-report.xlsx");
        await f.Service<IReportPdfWriter>().WriteAsync(report, pdfPath);
        await f.Service<IReportExcelWriter>().WriteAsync(report, excelPath);

        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(await File.ReadAllBytesAsync(pdfPath), 0, 4));
        using var workbook = ZipFile.OpenRead(excelPath);
        Assert.NotNull(workbook.GetEntry("xl/workbook.xml"));
        var worksheet = workbook.GetEntry("xl/worksheets/sheet1.xml") ?? throw new InvalidOperationException("The report worksheet is missing.");
        using var reader = new StreamReader(worksheet.Open());
        Assert.Contains("ySplit=\"11\"", await reader.ReadToEndAsync());

        var inventory = await f.ReportAsync(ReportType.StockInventory);
        Assert.NotEmpty(inventory.Rows);
        await f.Service<IReportPdfWriter>().WriteAsync(inventory, Path.Combine(output, "phase6-sample-stock-inventory.pdf"));
        await f.Service<IReportExcelWriter>().WriteAsync(inventory, Path.Combine(output, "phase6-sample-stock-inventory.xlsx"));
    }

    private static decimal Value(ReportDocument report, int row, string column) =>
        Convert.ToDecimal(report.Rows[row].Values[report.Columns.ToList().FindIndex(x => x.Header == column)]);

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _path;
        private readonly ServiceProvider _provider;
        private readonly IServiceScope _scope;
        private Guid AdminId { get; init; }
        private Guid SupervisorId { get; init; }
        private Guid ReviewerId { get; init; }
        public Guid CollectorA { get; private init; }
        public Guid FormTypeId { get; private init; }
        public Guid BookletId { get; private init; }
        public DateOnly From => new(2026, 7, 16);
        public DateOnly To => new(2026, 7, 16);

        private Fixture(string path, ServiceProvider provider, IServiceScope scope)
        {
            _path = path;
            _provider = provider;
            _scope = scope;
        }

        public T Service<T>() where T : notnull => _scope.ServiceProvider.GetRequiredService<T>();

        public static async Task<Fixture> CreateAsync(long beginning)
        {
            var path = Path.Combine(Path.GetTempPath(), $"afms-phase6-{Guid.NewGuid():N}.db");
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddApplication();
            services.AddInfrastructure($"Data Source={path};Pooling=False");
            var provider = services.BuildServiceProvider();
            var scope = provider.CreateScope();
            await scope.ServiceProvider.GetRequiredService<IDatabaseInitializer>().InitializeAsync();
            var db = scope.ServiceProvider.GetRequiredService<AfmsDbContext>();
            var user = new User { Username = $"admin{beginning}", DisplayName = "Report Administrator", PasswordHash = "test" };
            var supervisor = new User { Username = $"supervisor{beginning}", DisplayName = "Report Supervisor", PasswordHash = "test" };
            var reviewer = new User { Username = $"reviewer{beginning}", DisplayName = "Reversal Administrator", PasswordHash = "test" };
            var form = new AccountableFormType { Code = $"AF-{beginning}", Name = "Local", DefaultReceiptsPerBooklet = 50 };
            var barangay = new Barangay { Code = $"B-{beginning}", Name = $"Barangay {beginning}", Municipality = "Dipaculao", Province = "Aurora" };
            var collector = new Collector { Code = $"C-{beginning}", FullName = "Collector A" };
            db.AddRange(user, supervisor, reviewer, form, barangay, collector,
                new SystemSetting { Key = "ProvinceName", Value = "Aurora" },
                new SystemSetting { Key = "MunicipalityName", Value = "Dipaculao" },
                new SystemSetting { Key = "OfficeName", Value = "Municipal Treasury Office" },
                new SystemSetting { Key = "OfficeAddress", Value = "Dipaculao, Aurora" });
            await db.SaveChangesAsync();
            var session = scope.ServiceProvider.GetRequiredService<ICurrentUserSession>();
            session.Begin(new CurrentUser(user.Id, user.Username, user.DisplayName,
                new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));

            var receiving = scope.ServiceProvider.GetRequiredService<IStockReceivingService>();
            var receipt = await receiving.SaveDraftAsync(new(null, new(2026, 7, 15), form.Id, "BLGF", "DR-6", 2026, "Vault", "Treasurer", null,
                [new(2, 50, beginning, beginning + 99)]));
            await receiving.SubmitAsync(receipt.Id);
            await receiving.FinalizeAsync(receipt.Id);
            db.ChangeTracker.Clear();
            var booklet = await db.AccountableFormBooklets.OrderBy(x => x.BeginningSerial).FirstAsync();
            var issuance = scope.ServiceProvider.GetRequiredService<IIssuanceService>();
            var issue = await issuance.SaveDraftAsync(new(null, new(2026, 7, 16), collector.Id, barangay.Id, null, "Collection", "Custodian", "Collector A", null, [booklet.Id]));
            await issuance.SubmitAsync(issue.Id);
            session.Begin(new CurrentUser(supervisor.Id, supervisor.Username, supervisor.DisplayName, new HashSet<string> { "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            await issuance.ApproveAsync(issue.Id);
            session.Begin(new CurrentUser(user.Id, user.Username, user.DisplayName, new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            await issuance.FinalizeAsync(issue.Id);

            var receivedMovements = await db.StockMovements.Where(x => x.MovementType == StockMovementType.Received).ToListAsync();
            foreach (var movement in receivedMovements) movement.OccurredAt = new DateTimeOffset(2026, 7, 15, 10, 0, 0, TimeSpan.FromHours(8));
            var issuedMovements = await db.StockMovements.Where(x => x.MovementType == StockMovementType.Issued).ToListAsync();
            foreach (var movement in issuedMovements) movement.OccurredAt = new DateTimeOffset(2026, 7, 16, 9, 0, 0, TimeSpan.FromHours(8));
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();
            return new Fixture(path, provider, scope) { AdminId = user.Id, SupervisorId = supervisor.Id, ReviewerId = reviewer.Id, CollectorA = collector.Id, FormTypeId = form.Id, BookletId = booklet.Id };
        }

        public Task<ReportDocument> ReportAsync(ReportType type, ReportFilter? filter = null) =>
            Service<IReportQueryService>().GenerateAsync(type, filter ?? new(From, To, 2026));

        public async Task<Guid> UseAsync(long beginning, long ending)
        {
            var service = Service<IOperationalTransactionService>();
            var usage = await service.SaveUsageDraftAsync(new(null, CollectorA, FormTypeId, From, To, To, null, null, null,
                [new(BookletId, beginning, ending)]));
            await service.FinalizeUsageAsync(usage.Id);
            return usage.Id;
        }

        public async Task<Guid> ReturnAsync(long beginning, long ending, ReturnDisposition disposition = ReturnDisposition.ReturnedToAvailableStock)
        {
            var service = Service<IOperationalTransactionService>();
            var transaction = await service.SaveReturnDraftAsync(new(null, To, CollectorA, BookletId, beginning, ending,
                disposition == ReturnDisposition.ReturnedButDamaged ? "Damaged" : "Good", "Excess forms", "Custodian", disposition, null));
            await service.SubmitReturnAsync(transaction.Id);
            await ApproveAsync(() => service.ApproveReturnAsync(transaction.Id));
            await service.FinalizeReturnAsync(transaction.Id);
            return transaction.Id;
        }

        public async Task LoseAsync(long beginning, long ending)
        {
            var service = Service<IOperationalTransactionService>();
            var incident = await service.SaveExceptionDraftAsync(new(null, To, ExceptionType.Lost, CollectorA, BookletId, beginning, ending,
                "Forms lost during collection", "POLICE-6", "Reported to supervisor", null));
            await service.SubmitExceptionAsync(incident.Id);
            await ApproveAsync(() => service.ApproveExceptionAsync(incident.Id));
            await service.FinalizeExceptionAsync(incident.Id);
        }

        private async Task ApproveAsync(Func<Task> action)
        {
            var session = Service<ICurrentUserSession>(); session.Begin(new CurrentUser(SupervisorId, "phase6.supervisor", "Report Supervisor", new HashSet<string> { "Supervisor" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            try { await action(); } finally { session.Begin(new CurrentUser(AdminId, "phase6.admin", "Report Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false)); }
        }
        public async Task ReverseAsync(Func<Task> action)
        {
            var session = Service<ICurrentUserSession>(); session.Begin(new CurrentUser(ReviewerId, "phase6.reviewer", "Reversal Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false));
            try { await action(); } finally { session.Begin(new CurrentUser(AdminId, "phase6.admin", "Report Administrator", new HashSet<string> { "Administrator" }, Enum.GetValues<SystemPermission>().ToHashSet(), false)); }
        }

        public async ValueTask DisposeAsync()
        {
            _scope.Dispose();
            await _provider.DisposeAsync();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (File.Exists(_path)) File.Delete(_path);
        }
    }
}
