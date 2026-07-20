using System.Text.Json;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class ReportQueryService(
    IReportDataRepository repository, IReportCatalog catalog, IReportSettingsReader settingsReader,
    IReferenceNumberService references, IAuthorizationService authorization, ICurrentUserSession session,
    ICollectorAccountabilityService collectorLedger) : IReportQueryService
{
    private static readonly AccountableFormStatus[] UsableStatuses =
        [AccountableFormStatus.Available, AccountableFormStatus.Issued, AccountableFormStatus.PartiallyUsed, AccountableFormStatus.Transferred];

    public async Task<ReportDocument> GenerateAsync(ReportType type, ReportFilter filter, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewReports);
        Validate(filter, catalog.Get(type));
        var data = await repository.LoadAsync(filter, cancellationToken);
        var content = await BuildContentAsync(type, filter, data, cancellationToken);
        var settings = await settingsReader.GetAsync(cancellationToken);
        var now = DateTimeOffset.Now;
        return new ReportDocument(type, catalog.Get(type).Title,
            await references.GenerateAsync("RPT", now.Year, cancellationToken), Period(filter), now,
            session.User?.DisplayName ?? "AFMS User", settings.ProvinceName, settings.MunicipalityName,
            settings.OfficeName, settings.OfficeAddress, settings.PreparedBy, settings.CheckedBy,
            settings.CertifiedCorrectBy, content.Columns, content.Rows, content.Summaries, filter);
    }

    private async Task<Content> BuildContentAsync(ReportType type, ReportFilter f, ReportDataSnapshot d, CancellationToken ct) => type switch
    {
        ReportType.StockInventory => Inventory(d.AccountabilityRanges),
        ReportType.AvailableForms => RangeDetail(d.AccountabilityRanges.Where(x => x.Status == AccountableFormStatus.Available), "Storage Location"),
        ReportType.InventoryByFormType => RangeSummary(d.AccountabilityRanges, x => Form(x.AccountableFormBooklet), "Form Type"),
        ReportType.BeginningInventory => BalanceAt(d.Movements, f.FromDate?.AddDays(-1) ?? new DateOnly(f.FiscalYear ?? DateTime.Today.Year, 1, 1).AddDays(-1), "Beginning Balance"),
        ReportType.EndingInventory => BalanceAt(d.Movements, f.ToDate ?? DateOnly.FromDateTime(DateTime.Today), "Ending Balance"),
        ReportType.YearEndInventory => BalanceAt(d.Movements, new DateOnly(f.FiscalYear ?? DateTime.Today.Year, 12, 31), "Year-End Balance"),
        ReportType.StockMovement => MovementReport(PeriodMovements(d.Movements, f)),
        ReportType.InventoryByStorageLocation => RangeSummary(d.AccountabilityRanges.Where(x => x.CurrentCollectorId == null), x => x.StorageLocation ?? x.AccountableFormBooklet.StorageLocation ?? "Unassigned", "Storage Location"),
        ReportType.IssuancePerCollector => IssuanceRows(d.Issuances),
        ReportType.CollectorAccountabilityLedger => await LedgerAsync(f, ct),
        ReportType.OutstandingFormsPerCollector => RangeSummary(d.AccountabilityRanges.Where(x => x.CurrentCollectorId != null), x => x.CurrentCollector?.FullName ?? "Unassigned", "Collector"),
        ReportType.UsedSoldReceipts => UsageRows(d.UsageReports.Where(FinalizedNotReversed)),
        ReportType.UnusedFormsPerCollector => RangeDetail(d.AccountabilityRanges.Where(x => x.CurrentCollectorId != null), "Collector"),
        ReportType.CollectorTransactionHistory => MovementReport(PeriodMovements(d.Movements, f).Where(x => x.FromCollectorId != null || x.ToCollectorId != null)),
        ReportType.AccountabilityByBarangay => RangeSummary(d.AccountabilityRanges.Where(x => x.CurrentCollectorId != null), x => x.CurrentBarangay?.Name ?? "Unassigned", "Barangay"),
        ReportType.CollectorYearEndAccountability => CollectorBalanceAt(d.Movements, d.Collectors, new DateOnly(f.FiscalYear ?? DateTime.Today.Year, 12, 31)),
        ReportType.StockReceivingRegister => ReceivingRows(d.StockReceipts),
        ReportType.IssuanceRegister => IssuanceRows(d.Issuances),
        ReportType.UsageRegister => UsageRows(d.UsageReports),
        ReportType.TransferRegister => TransferRows(d.Transfers),
        ReportType.ReturnRegister => ReturnRows(d.Returns),
        ReportType.ReversalRegister => ReversalRows(d, f),
        ReportType.SpoiledReceipts => ExceptionRows(d.Exceptions.Where(x => x.ExceptionType == ExceptionType.Spoiled)),
        ReportType.CancelledReceipts => ExceptionRows(d.Exceptions.Where(x => x.ExceptionType is ExceptionType.Cancelled or ExceptionType.Invalidated)),
        ReportType.LostMissingForms => ExceptionRows(d.Exceptions.Where(x => x.ExceptionType is ExceptionType.Lost or ExceptionType.Missing)),
        ReportType.DamagedForms => ExceptionRows(d.Exceptions.Where(x => x.ExceptionType == ExceptionType.Damaged)),
        ReportType.SerialNumberGap => GapRows(d),
        ReportType.InventoryVariance => VarianceRows(d.Booklets),
        ReportType.UnresolvedExceptions => ExceptionRows(d.Exceptions.Where(x => x.ResolutionStatus is ExceptionResolutionStatus.Open or ExceptionResolutionStatus.UnderInvestigation)),
        ReportType.AnnualConsolidated or ReportType.BalanceRollForward => RollForward(d.Movements, f),
        ReportType.MonthlyIssuanceUsage => MonthlySummary(PeriodMovements(d.Movements, f), f),
        ReportType.InventoryByBarangay => RangeSummary(d.AccountabilityRanges, x => $"{x.CurrentBarangay?.Name ?? "Unassigned"} | {Form(x.AccountableFormBooklet)}", "Barangay / Form"),
        ReportType.IssuedUsedRemaining => IssuedUsedRemaining(d),
        ReportType.SummaryByFormType => RangeSummary(d.AccountabilityRanges, x => Form(x.AccountableFormBooklet), "Form Type"),
        ReportType.SummaryByCollector => RangeSummary(d.AccountabilityRanges.Where(x => x.CurrentCollectorId != null), x => x.CurrentCollector?.FullName ?? "Unassigned", "Collector"),
        ReportType.AuditActivity => AuditRows(d.AuditLogs),
        ReportType.StockReceivingForm => ReceivingRows(d.StockReceipts),
        ReportType.IssuanceForm => IssuanceRows(d.Issuances),
        ReportType.UsageForm => UsageRows(d.UsageReports),
        ReportType.TransferForm => TransferRows(d.Transfers),
        ReportType.ReturnForm => ReturnRows(d.Returns),
        ReportType.ExceptionForm => ExceptionRows(d.Exceptions),
        ReportType.ReversalForm => ReversalRows(d, f),
        _ => Empty()
    };

    private static Content Inventory(IEnumerable<AccountabilityRange> values)
    {
        var columns = C(("Form Type", ReportValueType.Text), ("Year", ReportValueType.Integer), ("Booklet", ReportValueType.Integer),
            ("Beginning Serial", ReportValueType.Integer), ("Ending Serial", ReportValueType.Integer), ("Total", ReportValueType.Integer),
            ("Used", ReportValueType.Integer), ("Remaining", ReportValueType.Integer), ("Holder", ReportValueType.Text),
            ("Barangay", ReportValueType.Text), ("Status", ReportValueType.Status), ("Storage", ReportValueType.Text));
        var rows = values.OrderBy(x => x.AccountableFormBooklet.AccountableFormType.Code).ThenBy(x => x.BeginningSerial).Select(x =>
        {
            var quantity = Qty(x);
            var used = x.Status == AccountableFormStatus.FullyUsed ? quantity : 0;
            var remaining = UsableStatuses.Contains(x.Status) ? quantity : 0;
            return R(Form(x.AccountableFormBooklet), x.AccountableFormBooklet.SeriesYear, x.AccountableFormBooklet.BookletSequence,
                x.BeginningSerial, x.EndingSerial, quantity, used, remaining, x.CurrentCollector?.FullName ?? "Stock",
                x.CurrentBarangay?.Name ?? "", x.Status, x.StorageLocation ?? x.AccountableFormBooklet.StorageLocation ?? "");
        }).ToList();
        return new(columns, rows, [S("Total receipts", rows.Sum(x => Number(x.Values[5]))), S("Remaining", rows.Sum(x => Number(x.Values[7])))]);
    }

    private static Content RangeDetail(IEnumerable<AccountabilityRange> values, string ownerHeader)
    {
        var columns = C(("Form Type", ReportValueType.Text), ("Beginning Serial", ReportValueType.Integer), ("Ending Serial", ReportValueType.Integer),
            ("Quantity", ReportValueType.Integer), (ownerHeader, ReportValueType.Text), ("Barangay", ReportValueType.Text), ("Status", ReportValueType.Status));
        var rows = values.OrderBy(x => x.AccountableFormBooklet.AccountableFormType.Code).ThenBy(x => x.BeginningSerial)
            .Select(x => R(Form(x.AccountableFormBooklet), x.BeginningSerial, x.EndingSerial, Qty(x),
                ownerHeader == "Collector" ? x.CurrentCollector?.FullName ?? "" : x.StorageLocation ?? x.AccountableFormBooklet.StorageLocation ?? "",
                x.CurrentBarangay?.Name ?? "", x.Status)).ToList();
        return new(columns, rows, [S("Total quantity", rows.Sum(x => Number(x.Values[3])))]);
    }

    private static Content RangeSummary(IEnumerable<AccountabilityRange> values, Func<AccountabilityRange, string> key, string header)
    {
        var rows = values.GroupBy(key).OrderBy(x => x.Key).Select(g => R(g.Key,
            g.Where(x => x.Status == AccountableFormStatus.Available).Sum(Qty),
            g.Where(x => x.CurrentCollectorId != null).Sum(Qty),
            g.Where(x => x.Status == AccountableFormStatus.FullyUsed).Sum(Qty),
            g.Where(x => x.Status is AccountableFormStatus.Lost or AccountableFormStatus.Missing or AccountableFormStatus.Cancelled or AccountableFormStatus.Spoiled).Sum(Qty),
            g.Where(x => UsableStatuses.Contains(x.Status)).Sum(Qty))).ToList();
        return new(C((header, ReportValueType.Text), ("Available", ReportValueType.Integer), ("With Collectors", ReportValueType.Integer),
            ("Used", ReportValueType.Integer), ("Exceptions", ReportValueType.Integer), ("Usable Remaining", ReportValueType.Integer)), rows,
            [S("Usable remaining", rows.Sum(x => Number(x.Values[5])))]);
    }

    private static Content MovementReport(IEnumerable<StockMovement> values)
    {
        var rows = values.OrderBy(x => x.EffectiveDate).ThenBy(x => x.OccurredAt).ThenBy(x => x.ReferenceNumber).Select(x => R(x.OccurredAt, x.ReferenceNumber,
            x.MovementType, Form(x.AccountableFormBooklet), x.BeginningSerial, x.EndingSerial, x.Quantity,
            x.FromCollector?.FullName ?? x.Source ?? "", x.ToCollector?.FullName ?? x.Destination ?? "", x.PerformedByUser.DisplayName)).ToList();
        return new(C(("Date / Time", ReportValueType.DateTime), ("Reference", ReportValueType.Text), ("Movement", ReportValueType.Status),
            ("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer),
            ("Quantity", ReportValueType.Integer), ("Source", ReportValueType.Text), ("Destination", ReportValueType.Text), ("User", ReportValueType.Text)), rows,
            [S("Movement quantity", rows.Sum(x => Number(x.Values[6])))]);
    }

    private static Content ReceivingRows(IEnumerable<StockReceipt> values)
    {
        var rows = values.SelectMany(x => x.Items.DefaultIfEmpty(), (h, i) => R(h.ReceivedDate, h.ReferenceNumber, Form(h.AccountableFormType), h.SeriesYear,
            i?.BeginningSerial ?? 0, i?.EndingSerial ?? 0, i?.Quantity ?? 0, h.Source ?? "", h.SourceDocumentNumber ?? "", h.StorageLocation ?? "", h.Status)).ToList();
        return Register(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Form Type", ReportValueType.Text), ("Year", ReportValueType.Integer),
            ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer), ("Quantity", ReportValueType.Integer), ("Source", ReportValueType.Text),
            ("Source Document", ReportValueType.Text), ("Storage", ReportValueType.Text), ("Status", ReportValueType.Status)), rows, 6);
    }

    private static Content IssuanceRows(IEnumerable<Issuance> values)
    {
        var rows = values.SelectMany(x => x.Items.DefaultIfEmpty(), (h, i) => R(h.IssuedDate, h.ReferenceNumber, h.Collector.FullName,
            h.Barangay?.Name ?? h.CollectionArea ?? "", i is null ? "" : Form(i.AccountableFormBooklet), i?.BeginningSerial ?? 0,
            i?.EndingSerial ?? 0, i?.Quantity ?? 0, h.Purpose, h.Status)).ToList();
        return Register(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Collector", ReportValueType.Text),
            ("Barangay", ReportValueType.Text), ("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer),
            ("Ending", ReportValueType.Integer), ("Quantity", ReportValueType.Integer), ("Purpose", ReportValueType.Text), ("Status", ReportValueType.Status)), rows, 7);
    }

    private static Content UsageRows(IEnumerable<UsageReport> values)
    {
        var rows = values.SelectMany(x => x.Ranges.DefaultIfEmpty(), (h, i) => R(h.ReportingDate, h.ReferenceNumber, h.Collector.FullName,
            Form(h.AccountableFormType), i?.BeginningSerial ?? 0, i?.EndingSerial ?? 0, i?.Quantity ?? 0, h.AmountCollected ?? 0m,
            h.SourceReportNumber ?? "", i?.GapExplanation ?? "", h.Status)).ToList();
        return new(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Collector", ReportValueType.Text), ("Form Type", ReportValueType.Text),
            ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer), ("Quantity", ReportValueType.Integer),
            ("Amount", ReportValueType.Decimal), ("Source Report", ReportValueType.Text), ("Gap Explanation", ReportValueType.Text), ("Status", ReportValueType.Status)), rows,
            [S("Quantity used", rows.Where(x => !Equals(x.Values[10], TransactionStatus.Reversed)).Sum(x => Number(x.Values[6]))), S("Amount collected", rows.Where(x => !Equals(x.Values[10], TransactionStatus.Reversed)).Sum(x => Number(x.Values[7])), "N2")]);
    }

    private static Content TransferRows(IEnumerable<Transfer> values)
    {
        var rows = values.Select(x => R(x.TransferDate, x.ReferenceNumber, x.FromCollector.FullName, x.ToCollector.FullName,
            Form(x.AccountableFormBooklet), x.BeginningSerial, x.EndingSerial, x.Quantity, x.Barangay?.Name ?? "", x.Reason, x.Status)).ToList();
        return Register(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("From Collector", ReportValueType.Text), ("To Collector", ReportValueType.Text),
            ("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer), ("Quantity", ReportValueType.Integer),
            ("Barangay", ReportValueType.Text), ("Reason", ReportValueType.Text), ("Status", ReportValueType.Status)), rows, 7);
    }

    private static Content ReturnRows(IEnumerable<ReturnTransaction> values)
    {
        var rows = values.Select(x => R(x.ReturnDate, x.ReferenceNumber, x.Collector.FullName, Form(x.AccountableFormBooklet),
            x.BeginningSerial, x.EndingSerial, x.Quantity, x.PhysicalCondition, x.Disposition, x.Reason, x.Status)).ToList();
        return Register(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Collector", ReportValueType.Text), ("Form Type", ReportValueType.Text),
            ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer), ("Quantity", ReportValueType.Integer),
            ("Condition", ReportValueType.Text), ("Disposition", ReportValueType.Status), ("Reason", ReportValueType.Text), ("Status", ReportValueType.Status)), rows, 6);
    }

    private static Content ExceptionRows(IEnumerable<FormException> values)
    {
        var rows = values.Select(x => R(x.DateDiscovered, x.ReferenceNumber, x.ExceptionType, x.Collector?.FullName ?? "",
            Form(x.AccountableFormBooklet), x.SerialNumber, x.EndingSerial, x.Quantity, x.Details, x.ActionTaken, x.ResolutionStatus, x.Status)).ToList();
        return Register(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Incident", ReportValueType.Status), ("Collector", ReportValueType.Text),
            ("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer), ("Quantity", ReportValueType.Integer),
            ("Explanation", ReportValueType.Text), ("Action Taken", ReportValueType.Text), ("Resolution", ReportValueType.Status), ("Status", ReportValueType.Status)), rows, 7);
    }

    private static Content ReversalRows(ReportDataSnapshot d, ReportFilter f)
    {
        var rows = PeriodMovements(d.Movements, f).Where(x => x.MovementType == StockMovementType.Reversed).GroupBy(x => new { x.ReferenceNumber, x.ReferenceType, x.ReferenceId, Date = x.EffectiveDate })
            .Select(x => R(x.Key.Date, x.Key.ReferenceNumber, x.Key.ReferenceType ?? "Reversal", x.Sum(y => y.Quantity), string.Join("; ", x.Select(y => y.Remarks).Where(y => !string.IsNullOrWhiteSpace(y)).Distinct()))).ToList();
        return Register(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Reversal Type", ReportValueType.Text), ("Quantity", ReportValueType.Integer), ("Reason", ReportValueType.Text)), rows, 3);
    }

    private static Content GapRows(ReportDataSnapshot d)
    {
        var rows = new List<ReportRow>();
        foreach (var report in d.UsageReports.Where(FinalizedNotReversed))
        foreach (var range in report.Ranges.Where(x => !string.IsNullOrWhiteSpace(x.GapExplanation)))
            rows.Add(R(report.ReportingDate, report.ReferenceNumber, report.Collector.FullName, Form(report.AccountableFormType), range.BeginningSerial, range.EndingSerial, range.GapExplanation ?? ""));
        foreach (var group in d.Booklets.GroupBy(x => new { x.AccountableFormTypeId, x.SeriesYear }).Select(x => x.OrderBy(y => y.BeginningSerial).ToList()))
        for (var i = 1; i < group.Count; i++) if (group[i].BeginningSerial > group[i - 1].EndingSerial + 1)
            rows.Add(R(group[i].DateReceived, "INVENTORY", "", Form(group[i]), group[i - 1].EndingSerial + 1, group[i].BeginningSerial - 1, "Serials absent between received ranges"));
        return new(C(("Date", ReportValueType.Date), ("Reference", ReportValueType.Text), ("Collector", ReportValueType.Text), ("Form Type", ReportValueType.Text),
            ("Gap Beginning", ReportValueType.Integer), ("Gap Ending", ReportValueType.Integer), ("Explanation", ReportValueType.Text)), rows, [S("Gap records", rows.Count)]);
    }

    private static Content VarianceRows(IEnumerable<AccountableFormBooklet> booklets)
    {
        var rows = booklets.Select(x => new { Booklet = x, ActiveRanges = x.AccountabilityRanges.Where(r => r.IsActive).ToList() })
            .Select(x => new { x.Booklet, RangeTotal = x.ActiveRanges.Count == 0 ? x.Booklet.TotalQuantity : x.ActiveRanges.Sum(Qty) })
            .Where(x => x.RangeTotal != x.Booklet.TotalQuantity || x.Booklet.UsedQuantity + x.Booklet.RemainingQuantity != x.Booklet.TotalQuantity)
            .Select(x => R(Form(x.Booklet), x.Booklet.BeginningSerial, x.Booklet.EndingSerial, x.Booklet.TotalQuantity, x.RangeTotal,
                x.Booklet.TotalQuantity - x.RangeTotal, x.Booklet.Status)).ToList();
        return new(C(("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer),
            ("Booklet Total", ReportValueType.Integer), ("Range Total", ReportValueType.Integer), ("Variance", ReportValueType.Integer), ("Status", ReportValueType.Status)), rows,
            [S("Absolute variance", rows.Sum(x => Math.Abs(Number(x.Values[5]))))]);
    }

    private static Content BalanceAt(IEnumerable<StockMovement> movements, DateOnly date, string balanceHeader)
    {
        var rows = movements.Where(x => x.EffectiveDate <= date).GroupBy(x => Form(x.AccountableFormBooklet))
            .Select(x => R(x.Key, x.Sum(SignedGlobal))).Where(x => Number(x.Values[1]) != 0).OrderBy(x => x.Values[0]).ToList();
        return new(C(("Form Type", ReportValueType.Text), (balanceHeader, ReportValueType.Integer)), rows, [S(balanceHeader, rows.Sum(x => Number(x.Values[1])))]);
    }

    private static Content RollForward(IEnumerable<StockMovement> movements, ReportFilter f)
    {
        var from = f.FromDate ?? new DateOnly(f.FiscalYear ?? DateTime.Today.Year, 1, 1); var to = f.ToDate ?? new DateOnly(f.FiscalYear ?? DateTime.Today.Year, 12, 31);
        var list = movements.ToList(); var forms = list.Select(x => Form(x.AccountableFormBooklet)).Distinct().OrderBy(x => x);
        var rows = forms.Select(form => { var all = list.Where(x => Form(x.AccountableFormBooklet) == form).ToList(); var opening = all.Where(x => Day(x) < from).Sum(SignedGlobal);
            var period = all.Where(x => Day(x) >= from && Day(x) <= to).ToList(); var additions = period.Where(x => x.MovementType == StockMovementType.Received).Sum(x => x.Quantity);
            var issued = period.Where(x => x.MovementType == StockMovementType.Issued).Sum(x => x.Quantity); var usage = period.Where(x => x.MovementType == StockMovementType.Used).Sum(x => x.Quantity);
            var returns = period.Where(x => x.MovementType == StockMovementType.Returned).Sum(x => x.Quantity);
            var exceptions = period.Where(x => x.InventoryQuantityDelta < 0 && x.MovementType is not StockMovementType.Used and not StockMovementType.Reversed)
                .Sum(x => -x.InventoryQuantityDelta);
            return R(form, opening, additions, issued, usage, returns, exceptions, period.Where(x => x.MovementType == StockMovementType.Reversed).Sum(SignedGlobal), opening + period.Sum(SignedGlobal)); }).ToList();
        return new(C(("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer), ("Additions", ReportValueType.Integer),
            ("Issued", ReportValueType.Integer), ("Usage", ReportValueType.Integer), ("Returns", ReportValueType.Integer),
            ("Exceptions", ReportValueType.Integer), ("Net Reversals", ReportValueType.Integer), ("Ending", ReportValueType.Integer)), rows,
            [S("Opening", rows.Sum(x => Number(x.Values[1]))), S("Ending", rows.Sum(x => Number(x.Values[8])))]);
    }

    private static Content MonthlySummary(IEnumerable<StockMovement> movements, ReportFilter f)
    {
        var rows = movements.GroupBy(x => new { x.EffectiveDate.Year, x.EffectiveDate.Month, Form = Form(x.AccountableFormBooklet) }).OrderBy(x => x.Key.Year).ThenBy(x => x.Key.Month).ThenBy(x => x.Key.Form)
            .Select(x => R(new DateOnly(x.Key.Year, x.Key.Month, 1), x.Key.Form, x.Where(y => y.MovementType == StockMovementType.Issued).Sum(y => y.Quantity),
                x.Where(y => y.MovementType == StockMovementType.Used).Sum(y => y.Quantity), x.Where(y => y.MovementType == StockMovementType.Returned).Sum(y => y.Quantity))).ToList();
        return new(C(("Month", ReportValueType.Date), ("Form Type", ReportValueType.Text), ("Issued", ReportValueType.Integer), ("Used", ReportValueType.Integer), ("Returned", ReportValueType.Integer)), rows,
            [S("Issued", rows.Sum(x => Number(x.Values[2]))), S("Used", rows.Sum(x => Number(x.Values[3])))]);
    }

    private static Content IssuedUsedRemaining(ReportDataSnapshot d)
    {
        var rows = d.Booklets.GroupBy(Form).Select(g => R(g.Key,
            d.Movements.Where(x => x.AccountableFormTypeId == g.First().AccountableFormTypeId && x.MovementType == StockMovementType.Issued).Sum(x => x.Quantity),
            g.Sum(x => x.UsedQuantity), g.Sum(x => x.RemainingQuantity))).OrderBy(x => x.Values[0]).ToList();
        return new(C(("Form Type", ReportValueType.Text), ("Issued", ReportValueType.Integer), ("Used", ReportValueType.Integer), ("Remaining", ReportValueType.Integer)), rows,
            [S("Issued", rows.Sum(x => Number(x.Values[1]))), S("Used", rows.Sum(x => Number(x.Values[2]))), S("Remaining", rows.Sum(x => Number(x.Values[3])))]);
    }

    private static Content CollectorBalanceAt(IEnumerable<StockMovement> movements, IEnumerable<Collector> collectors, DateOnly date)
    {
        var list = movements.Where(x => Day(x) <= date).ToList(); var rows = collectors.Select(c => R(c.Code, c.FullName,
            list.Where(x => x.ToCollectorId == c.Id).Sum(x => x.Quantity) - list.Where(x => x.FromCollectorId == c.Id).Sum(x => x.Quantity)))
            .Where(x => Number(x.Values[2]) != 0).OrderBy(x => x.Values[1]).ToList();
        return new(C(("Collector Number", ReportValueType.Text), ("Collector", ReportValueType.Text), ("Accountability", ReportValueType.Integer)), rows, [S("Total accountability", rows.Sum(x => Number(x.Values[2])))]);
    }

    private async Task<Content> LedgerAsync(ReportFilter f, CancellationToken ct)
    {
        var entries = await collectorLedger.GetLedgerAsync(new(f.CollectorId!.Value, f.FormTypeId, f.BarangayId, null, f.FiscalYear, f.FromDate, f.ToDate, f.OutstandingOnly), ct);
        var rows = entries.Select(x => R(x.Date, x.ReferenceNumber, x.TransactionType, x.FormType, x.BeginningSerial, x.EndingSerial,
            x.QuantityIn, x.QuantityOut, x.RunningBalance, x.Barangay, x.Status, x.Remarks ?? "")).ToList();
        return new(C(("Date", ReportValueType.DateTime), ("Reference", ReportValueType.Text), ("Transaction", ReportValueType.Status),
            ("Form Type", ReportValueType.Text), ("Beginning", ReportValueType.Integer), ("Ending", ReportValueType.Integer),
            ("Quantity In", ReportValueType.Integer), ("Quantity Out", ReportValueType.Integer), ("Running Balance", ReportValueType.Integer),
            ("Barangay", ReportValueType.Text), ("Status", ReportValueType.Status), ("Remarks", ReportValueType.Text)), rows,
            [S("Ending accountability", rows.LastOrDefault() is { } last ? Number(last.Values[8]) : 0)]);
    }

    private static Content AuditRows(IEnumerable<AuditLog> logs)
    {
        var rows = logs.OrderBy(x => x.OccurredAt).Select(x => R(x.OccurredAt, x.User?.DisplayName ?? "System", x.ActivityType,
            x.EntityName ?? "", x.EntityId?.ToString() ?? "", x.Description, x.MachineName ?? "")).ToList();
        return new(C(("Date / Time", ReportValueType.DateTime), ("User", ReportValueType.Text), ("Activity", ReportValueType.Status),
            ("Entity", ReportValueType.Text), ("Entity ID", ReportValueType.Text), ("Description", ReportValueType.Text), ("Machine", ReportValueType.Text)), rows,
            [S("Audit records", rows.Count)]);
    }

    private static Content Register(IReadOnlyList<ReportColumn> columns, List<ReportRow> rows, int quantityIndex) => new(columns, rows,
        [S("Records", rows.Count), S("Finalized quantity", rows.Where(x => !Equals(x.Values[^1], TransactionStatus.Reversed)).Sum(x => Number(x.Values[quantityIndex])))]);
    private static Content Empty() => new([], [], []);
    private static bool FinalizedNotReversed(UsageReport x) => x.Status == TransactionStatus.Finalized;
    private static string Form(AccountableFormBooklet x) => Form(x.AccountableFormType);
    private static string Form(AccountableFormType x) => $"{x.Code} - {x.Name}";
    private static long Qty(AccountabilityRange x) => checked(x.EndingSerial - x.BeginningSerial + 1);
    private static DateOnly Day(StockMovement x) => x.EffectiveDate;
    private static IEnumerable<StockMovement> PeriodMovements(IEnumerable<StockMovement> values, ReportFilter f)
    {
        var from = f.FromDate ?? (f.FiscalYear is null ? null : new DateOnly(f.FiscalYear.Value, 1, 1));
        var to = f.ToDate ?? (f.FiscalYear is null ? null : new DateOnly(f.FiscalYear.Value, 12, 31));
        return values.Where(x => (from is null || Day(x) >= from) && (to is null || Day(x) <= to));
    }
    internal static long SignedGlobal(StockMovement x) => x.InventoryQuantityDelta;
    private static decimal Number(object? value) => value switch { decimal d => d, long l => l, int i => i, double d => (decimal)d, _ => 0 };
    private static ReportSummary S(string label, decimal value, string format = "N0") => new(label, value, format);
    private static ReportRow R(params object?[] values) => new(values);
    private static IReadOnlyList<ReportColumn> C(params (string Header, ReportValueType Type)[] values) => values.Select((x, i) => new ReportColumn($"C{i}", x.Header, x.Type)).ToList();
    private static string Period(ReportFilter f) => f.FromDate is not null || f.ToDate is not null ? $"{f.FromDate?.ToString("MMM d, yyyy") ?? "Beginning"} to {f.ToDate?.ToString("MMM d, yyyy") ?? "Present"}" : f.FiscalYear is not null ? $"Fiscal Year {f.FiscalYear}" : "As of " + DateTime.Today.ToString("MMMM d, yyyy");
    private static void Validate(ReportFilter f, ReportDefinition definition)
    {
        if (f.FromDate is not null && f.ToDate is not null && f.ToDate < f.FromDate) throw new ApplicationValidationException(["The ending date cannot be earlier than the beginning date."]);
        if (definition.RequiresCollector && f.CollectorId is null) throw new ApplicationValidationException(["Select a collector for this report."]);
        if (definition.RequiresReference && string.IsNullOrWhiteSpace(f.ReferenceNumber)) throw new ApplicationValidationException(["Enter a transaction reference number for this printable form."]);
    }
    private sealed record Content(IReadOnlyList<ReportColumn> Columns, IReadOnlyList<ReportRow> Rows, IReadOnlyList<ReportSummary> Summaries);
}

public sealed class ReportCenterService(
    IReportCatalog catalog, IReportQueryService queries, IReportPdfWriter pdf, IReportExcelWriter excel,
    IApplicationPathService paths, IAuditService audit, IUnitOfWork unitOfWork,
    IAuthorizationService authorization) : IReportCenterService
{
    public IReadOnlyList<ReportDefinition> GetCatalog() => catalog.Definitions;

    public async Task<ReportDocument> PreviewAsync(ReportType type, ReportFilter filter, CancellationToken ct = default)
    {
        var document = await queries.GenerateAsync(type, filter, ct); await Audit(document, UserActivityType.RecordViewed, "generated for preview", null, ct); return document;
    }

    public async Task<string> ExportPdfAsync(ReportType type, ReportFilter filter, string? outputFolder = null, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.ExportReports); var document = await queries.GenerateAsync(type, filter, ct);
        var path = Output(document, outputFolder, ".pdf"); await pdf.WriteAsync(document, path, ct); await Audit(document, UserActivityType.Exported, "exported to PDF", path, ct); return path;
    }

    public async Task<string> ExportExcelAsync(ReportType type, ReportFilter filter, string? outputFolder = null, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.ExportReports); var document = await queries.GenerateAsync(type, filter, ct);
        var path = Output(document, outputFolder, ".xlsx"); await excel.WriteAsync(document, path, ct); await Audit(document, UserActivityType.Exported, "exported to Excel", path, ct); return path;
    }

    public Task RecordPrintAsync(ReportDocument document, CancellationToken ct = default) => Audit(document, UserActivityType.Printed, "sent to print", null, ct);

    private string Output(ReportDocument d, string? folder, string extension)
    {
        folder = string.IsNullOrWhiteSpace(folder) ? Path.Combine(paths.DataDirectory, "Reports") : folder;
        Directory.CreateDirectory(folder); return Path.Combine(folder, $"{Safe(d.Title)}-{d.ReportReference}{extension}");
    }

    private async Task Audit(ReportDocument d, UserActivityType type, string action, string? path, CancellationToken ct)
    {
        await audit.RecordAsync(type, $"Report {d.Title} ({d.ReportReference}) {action}.", nameof(ReportDocument), Guid.NewGuid(), null,
            new { d.Type, d.ReportReference, d.Filter, OutputPath = path, d.GeneratedAt }, ct); await unitOfWork.SaveChangesAsync(ct);
    }
    private static string Safe(string value) => string.Concat(value.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '-' : c)).Replace(' ', '-');
}
