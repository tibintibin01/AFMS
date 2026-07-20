using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Domain.Services;

namespace AFMS.Application.Services;

public sealed class ReferenceNumberService(IReferenceNumberSequenceRepository sequences) : IReferenceNumberService
{
    private static readonly IReadOnlyDictionary<string, string> Prefixes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["RCV"] = "RCV", ["ISS"] = "ISS", ["USE"] = "USE", ["TRF"] = "TRF", ["RTN"] = "RTN", ["EXC"] = "EXC", ["RPT"] = "RPT"
    };

    public async Task<string> GenerateAsync(string transactionCode, int year, CancellationToken cancellationToken = default)
    {
        if (!Prefixes.TryGetValue(transactionCode.Trim(), out var prefix))
            throw new ArgumentException("Unsupported AFMS transaction reference type.", nameof(transactionCode));
        if (year is < 1900 or > 9999) throw new ArgumentOutOfRangeException(nameof(year));
        var next = await sequences.ReserveNextAsync(prefix, year, cancellationToken);
        return $"{prefix}-{year:0000}-{next:000000}";
    }
}

public sealed class StockReceivingService(
    IStockReceiptRepository receipts,
    IReferenceNumberService references,
    ITransactionRunner transactions,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    IAuthorizationService authorization,
    ICurrentUserSession session,
    BookletGenerationService bookletGenerator,
    IFiscalPeriodGuard fiscalPeriods) : IStockReceivingService
{
    public async Task<IReadOnlyList<StockReceiptDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ReceiveStock);
        return (await receipts.ListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<StockReceiptDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ReceiveStock);
        var entity = await receipts.GetAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<StockReceiptDto> SaveDraftAsync(SaveStockReceiptDraftRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ReceiveStock);
        await fiscalPeriods.EnsureOpenAsync(request.DateReceived, cancellationToken);
        ValidateDraft(request);
        await ValidateItemsAndOverlapsAsync(request, cancellationToken);
        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required to receive stock.");
        StockReceipt entity;
        object? oldValues = null;

        if (request.Id is null)
        {
            entity = new StockReceipt
            {
                ReferenceNumber = await references.GenerateAsync("RCV", request.SeriesYear, cancellationToken),
                ReceivedDate = request.DateReceived,
                AccountableFormTypeId = request.AccountableFormTypeId,
                EncodedByUserId = currentUser.Id
            };
            await receipts.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity = await receipts.GetAsync(request.Id.Value, cancellationToken)
                ?? throw new KeyNotFoundException("The stock-receiving draft no longer exists.");
            if (entity.Status != TransactionStatus.Draft)
                throw new InvalidOperationException("Only draft receiving transactions may be edited.");
            oldValues = Snapshot(entity);
            receipts.RemoveItems(entity.Items.ToList());
            entity.Items.Clear();
        }

        ApplyHeader(entity, request);
        foreach (var item in request.Items)
        {
            entity.Items.Add(new StockReceiptItem
            {
                AccountableFormTypeId = request.AccountableFormTypeId,
                NumberOfBooklets = item.NumberOfBooklets,
                ReceiptsPerBooklet = item.ReceiptsPerBooklet,
                BeginningSerial = item.BeginningSerial,
                EndingSerial = item.EndingSerial,
                Quantity = bookletGenerator.CalculateQuantity(item.BeginningSerial, item.EndingSerial)
            });
        }

        await audit.RecordAsync(
            request.Id is null ? UserActivityType.RecordCreated : UserActivityType.RecordUpdated,
            request.Id is null ? $"Receiving draft {entity.ReferenceNumber} created." : $"Receiving draft {entity.ReferenceNumber} edited.",
            nameof(StockReceipt), entity.Id, oldValues, Snapshot(entity), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return Map((await receipts.GetAsync(entity.Id, cancellationToken))!);
    }

    public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ReceiveStock);
        var receipt = await GetRequiredAsync(id, cancellationToken);
        await fiscalPeriods.EnsureOpenAsync(receipt.ReceivedDate, cancellationToken);
        if (receipt.Status != TransactionStatus.Draft) throw new InvalidOperationException("Only draft receiving transactions may be submitted.");
        await ValidateForFinalizationAsync(receipt, cancellationToken);
        receipt.Status = TransactionStatus.Submitted;
        receipt.SubmittedAt = DateTimeOffset.UtcNow;
        await audit.RecordAsync(UserActivityType.Submitted, $"Receiving transaction {receipt.ReferenceNumber} submitted.", nameof(StockReceipt), receipt.Id,
            new { Status = TransactionStatus.Draft }, new { receipt.Status, receipt.SubmittedAt }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task FinalizeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ReceiveStock);
        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required to finalize stock.");
        await transactions.ExecuteAsync(async transactionToken =>
        {
            var receipt = await GetRequiredAsync(id, transactionToken);
            await fiscalPeriods.EnsureOpenAsync(receipt.ReceivedDate, transactionToken);
            if (receipt.Status != TransactionStatus.Submitted)
                throw new InvalidOperationException("Only submitted receiving transactions may be finalized.");
            await ValidateForFinalizationAsync(receipt, transactionToken);

            var booklets = new List<AccountableFormBooklet>();
            var movements = new List<StockMovement>();
            var globalSequence = 1;
            foreach (var item in receipt.Items.OrderBy(item => item.BeginningSerial))
            {
                if (await receipts.HasOverlappingBookletRangeAsync(receipt.AccountableFormTypeId, receipt.SeriesYear, item.BeginningSerial, item.EndingSerial, transactionToken))
                    throw new ApplicationValidationException([$"Serial range {item.BeginningSerial}-{item.EndingSerial} already exists in finalized inventory."]);

                foreach (var range in bookletGenerator.Generate(item.NumberOfBooklets, item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial))
                {
                    var booklet = new AccountableFormBooklet
                    {
                        StockReceiptItemId = item.Id,
                        AccountableFormTypeId = receipt.AccountableFormTypeId,
                        SeriesYear = receipt.SeriesYear,
                        BookletSequence = globalSequence++,
                        BeginningSerial = range.BeginningSerial,
                        EndingSerial = range.EndingSerial,
                        TotalQuantity = range.Quantity,
                        UsedQuantity = 0,
                        RemainingQuantity = range.Quantity,
                        Status = AccountableFormStatus.Available,
                        DateReceived = receipt.ReceivedDate,
                        StorageLocation = receipt.StorageLocation,
                        Remarks = receipt.Remarks
                    };
                    booklet.AccountabilityRanges.Add(new AccountabilityRange
                    {
                        AccountableFormBooklet = booklet,
                        AccountableFormBookletId = booklet.Id,
                        BeginningSerial = range.BeginningSerial,
                        EndingSerial = range.EndingSerial,
                        Status = AccountableFormStatus.Available,
                        StorageLocation = receipt.StorageLocation,
                        LastTransactionType = nameof(StockReceipt),
                        LastTransactionId = receipt.Id
                    });
                    booklets.Add(booklet);
                    movements.Add(new StockMovement
                    {
                        AccountableFormBooklet = booklet,
                        AccountableFormBookletId = booklet.Id,
                        AccountableFormTypeId = receipt.AccountableFormTypeId,
                        MovementType = StockMovementType.Received,
                        ReferenceNumber = receipt.ReferenceNumber,
                        BeginningSerial = range.BeginningSerial,
                        EndingSerial = range.EndingSerial,
                        Quantity = range.Quantity,
                        InventoryQuantityDelta = range.Quantity,
                        Source = receipt.Source,
                        Destination = receipt.StorageLocation,
                        ReferenceType = nameof(StockReceipt),
                        ReferenceId = receipt.Id,
                        PerformedByUserId = currentUser.Id,
                        EffectiveDate = receipt.ReceivedDate,
                        OccurredAt = DateTimeOffset.UtcNow,
                        Remarks = receipt.Remarks
                    });
                }
            }

            if (booklets.Count != receipt.Items.Sum(item => item.NumberOfBooklets))
                throw new InvalidOperationException("Generated booklet count does not match the receiving transaction.");

            await receipts.AddBookletsAsync(booklets, transactionToken);
            await receipts.AddMovementsAsync(movements, transactionToken);
            receipt.Status = TransactionStatus.Finalized;
            receipt.FinalizedAt = DateTimeOffset.UtcNow;
            await audit.RecordAsync(UserActivityType.Finalized, $"Receiving transaction {receipt.ReferenceNumber} finalized with {booklets.Count} booklet(s).",
                nameof(StockReceipt), receipt.Id, new { Status = TransactionStatus.Submitted }, new { receipt.Status, receipt.FinalizedAt, BookletCount = booklets.Count }, transactionToken);
            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);
    }

    public async Task ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ApproveTransactions);
        var receipt = await GetRequiredAsync(id, cancellationToken);
        await fiscalPeriods.EnsureOpenAsync(receipt.ReceivedDate, cancellationToken);
        await audit.RecordAsync(UserActivityType.ReversalAttempt, $"Reversal requested for {receipt.ReferenceNumber}.", nameof(StockReceipt), receipt.Id, null, new { Reason = reason }, cancellationToken,
            "Stock Receiving", receipt.ReferenceNumber, reason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(reason)) throw new ApplicationValidationException(["A reversal reason is required."]);
        if (receipt.Status != TransactionStatus.Finalized) throw new InvalidOperationException("Only finalized receiving transactions may be reversed.");

        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required to reverse stock.");
        if (receipt.EncodedByUserId == currentUser.Id)
            throw new UnauthorizedAccessException("Separation of duties requires a different authorized user to reverse stock receiving prepared by you.");
        await transactions.ExecuteAsync(async transactionToken =>
        {
            receipt = await GetRequiredAsync(id, transactionToken);
            var booklets = receipt.Items.SelectMany(item => item.Booklets).ToList();
            if (booklets.Any(item => item.Status != AccountableFormStatus.Available || item.UsedQuantity != 0 || item.CurrentCollectorId is not null))
                throw new InvalidOperationException("This receipt cannot be reversed because one or more booklets are no longer untouched and available.");

            var movements = booklets.Select(booklet => new StockMovement
            {
                AccountableFormBookletId = booklet.Id,
                AccountableFormTypeId = booklet.AccountableFormTypeId,
                MovementType = StockMovementType.Reversed,
                ReferenceNumber = receipt.ReferenceNumber,
                BeginningSerial = booklet.BeginningSerial,
                EndingSerial = booklet.EndingSerial,
                Quantity = booklet.TotalQuantity,
                InventoryQuantityDelta = -booklet.TotalQuantity,
                Source = receipt.StorageLocation,
                Destination = "Reversed receiving transaction",
                ReferenceType = "StockReceiptReversal",
                ReferenceId = receipt.Id,
                PerformedByUserId = currentUser.Id,
                EffectiveDate = receipt.ReceivedDate,
                OccurredAt = DateTimeOffset.UtcNow,
                Remarks = reason.Trim()
            }).ToList();
            foreach (var booklet in booklets) booklet.Status = AccountableFormStatus.Archived;
            await receipts.AddMovementsAsync(movements, transactionToken);
            receipt.Status = TransactionStatus.Reversed;
            receipt.ReversedAt = DateTimeOffset.UtcNow;
            await audit.RecordAsync(UserActivityType.Reversed, $"Receiving transaction {receipt.ReferenceNumber} reversed.", nameof(StockReceipt), receipt.Id,
                new { Status = TransactionStatus.Finalized }, new { receipt.Status, receipt.ReversedAt, Reason = reason.Trim() }, transactionToken,
                "Stock Receiving", receipt.ReferenceNumber, reason.Trim());
            await unitOfWork.SaveChangesAsync(transactionToken);
        }, cancellationToken);
    }

    private async Task<StockReceipt> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await receipts.GetAsync(id, cancellationToken) ?? throw new KeyNotFoundException("The stock-receiving transaction no longer exists.");

    private void ValidateDraft(SaveStockReceiptDraftRequest request)
    {
        var errors = new List<string>();
        if (request.AccountableFormTypeId == Guid.Empty) errors.Add("Accountable form type is required.");
        if (request.SeriesYear is < 1900 or > 9999) errors.Add("Series year must be a four-digit year.");
        if (request.DateReceived == default) errors.Add("Date received is required.");
        if (request.Items.Count > 100) errors.Add("A receiving transaction cannot contain more than 100 ranges.");
        if (errors.Count > 0) throw new ApplicationValidationException(errors);
    }

    private async Task ValidateItemsAndOverlapsAsync(SaveStockReceiptDraftRequest request, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        for (var index = 0; index < request.Items.Count; index++)
        {
            var item = request.Items[index];
            try { bookletGenerator.Generate(item.NumberOfBooklets, item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial); }
            catch (Exception exception) when (exception is ArgumentException or OverflowException) { errors.Add($"Item {index + 1}: {exception.Message}"); continue; }

            if (request.Items.Where((_, otherIndex) => otherIndex != index).Any(other => item.BeginningSerial <= other.EndingSerial && other.BeginningSerial <= item.EndingSerial))
                errors.Add($"Item {index + 1}: the serial range overlaps another item in this transaction.");
            else if (await receipts.HasOverlappingReceiptRangeAsync(request.AccountableFormTypeId, request.SeriesYear, item.BeginningSerial, item.EndingSerial, request.Id, cancellationToken))
                errors.Add($"Item {index + 1}: serial range {item.BeginningSerial}-{item.EndingSerial} overlaps an existing receiving transaction.");
        }
        if (errors.Count > 0) throw new ApplicationValidationException(errors.Distinct().ToList());
    }

    private async Task ValidateForFinalizationAsync(StockReceipt receipt, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(receipt.Source)) errors.Add("Source or issuing office is required before submission or finalization.");
        if (string.IsNullOrWhiteSpace(receipt.StorageLocation)) errors.Add("Storage location is required before submission or finalization.");
        if (string.IsNullOrWhiteSpace(receipt.ReceivedBy)) errors.Add("Received by is required before submission or finalization.");
        if (receipt.Items.Count == 0) errors.Add("At least one stock-receipt item is required.");
        foreach (var item in receipt.Items)
        {
            try { bookletGenerator.Generate(item.NumberOfBooklets, item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial); }
            catch (Exception exception) when (exception is ArgumentException or OverflowException) { errors.Add(exception.Message); }
        }
        var itemList = receipt.Items.ToList();
        for (var index = 0; index < itemList.Count; index++)
            if (itemList.Skip(index + 1).Any(other => itemList[index].BeginningSerial <= other.EndingSerial && other.BeginningSerial <= itemList[index].EndingSerial))
                errors.Add("Receiving item serial ranges overlap each other.");
        if (errors.Count > 0) throw new ApplicationValidationException(errors.Distinct().ToList());
    }

    private static void ApplyHeader(StockReceipt entity, SaveStockReceiptDraftRequest request)
    {
        entity.ReceivedDate = request.DateReceived;
        entity.AccountableFormTypeId = request.AccountableFormTypeId;
        entity.Source = Normalize(request.SourceOrIssuingOffice);
        entity.SourceDocumentNumber = Normalize(request.SourceDocumentNumber);
        entity.SeriesYear = request.SeriesYear;
        entity.StorageLocation = Normalize(request.StorageLocation);
        entity.ReceivedBy = Normalize(request.ReceivedBy);
        entity.Remarks = Normalize(request.Remarks);
    }

    private static object Snapshot(StockReceipt entity) => new
    {
        entity.ReferenceNumber, entity.ReceivedDate, entity.AccountableFormTypeId, entity.Source,
        entity.SourceDocumentNumber, entity.SeriesYear, entity.StorageLocation, entity.ReceivedBy,
        entity.Remarks, entity.Status,
        Items = entity.Items.Select(item => new { item.NumberOfBooklets, item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial, item.Quantity }).ToList()
    };

    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static StockReceiptDto Map(StockReceipt entity) => new(
        entity.Id, entity.ReferenceNumber, entity.ReceivedDate, entity.AccountableFormTypeId,
        $"{entity.AccountableFormType.Code} - {entity.AccountableFormType.Name}", entity.Source,
        entity.SourceDocumentNumber, entity.SeriesYear, entity.StorageLocation, entity.ReceivedBy,
        entity.EncodedByUser.DisplayName, entity.Remarks, entity.Status, entity.SubmittedAt, entity.FinalizedAt,
        entity.Items.OrderBy(item => item.BeginningSerial).Select(item => new StockReceiptItemDto(item.Id, item.NumberOfBooklets,
            item.ReceiptsPerBooklet, item.BeginningSerial, item.EndingSerial, item.Quantity)).ToList());
}

public sealed class InventoryService(IInventoryRepository inventory, IAuthorizationService authorization) : IInventoryService
{
    public async Task<IReadOnlyList<InventoryBookletDto>> SearchAsync(InventoryFilter filter, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewInventory);
        return (await inventory.ListAsync(filter, cancellationToken)).Select(MapBooklet).ToList();
    }

    public async Task<InventoryDetailDto?> GetDetailAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewInventory);
        var booklet = await inventory.GetDetailAsync(id, cancellationToken);
        if (booklet is null) return null;
        var receipt = booklet.StockReceiptItem.StockReceipt;
        return new InventoryDetailDto(
            MapBooklet(booklet), receipt.ReferenceNumber, receipt.ReceivedDate, receipt.Source, receipt.SourceDocumentNumber,
            CurrentHolder(booklet, true),
            booklet.Movements.OrderByDescending(item => item.OccurredAt).Select(MapMovement).ToList(),
            booklet.UsageRanges.OrderBy(item => item.BeginningSerial).Select(item => new UsageRangeSummary(item.BeginningSerial, item.EndingSerial)).ToList(),
            booklet.Exceptions.OrderByDescending(item => item.ReportedAt).Select(item => new ExceptionSummary(item.SerialNumber, item.ExceptionType, item.Details, item.ReportedAt)).ToList());
    }

    public async Task<IReadOnlyList<StockMovementDto>> SearchMovementsAsync(MovementHistoryFilter filter, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewStockMovements);
        return (await inventory.ListMovementsAsync(filter, cancellationToken)).Select(MapMovement).ToList();
    }

    internal static InventoryBookletDto MapBooklet(AccountableFormBooklet item)
    {
        var active = item.AccountabilityRanges.Where(range => range.IsActive).ToList();
        var barangays = active.Where(range => range.CurrentBarangay is not null).Select(range => range.CurrentBarangay!.Name)
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return new(item.Id, $"{item.AccountableFormType.Code} - {item.AccountableFormType.Name}", item.SeriesYear, item.BookletSequence,
            item.BeginningSerial, item.EndingSerial, item.TotalQuantity, item.UsedQuantity, item.RemainingQuantity,
            CurrentHolder(item, false), barangays.Count switch { 0 => string.Empty, 1 => barangays[0], _ => "Multiple barangays" },
            item.DateReceived, item.DateIssued, item.Status, item.StorageLocation,
            item.Movements.Count == 0 ? null : item.Movements.Max(movement => movement.OccurredAt), item.Remarks);
    }

    private static string CurrentHolder(AccountableFormBooklet item, bool includeStorage)
    {
        var holders = item.AccountabilityRanges.Where(range => range.IsActive && range.CurrentCollector is not null)
            .Select(range => range.CurrentCollector!.FullName).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return holders.Count switch
        {
            1 => holders[0],
            > 1 => "Multiple collectors",
            _ => includeStorage ? item.StorageLocation ?? "Municipal Treasury Office" : "Treasury stock"
        };
    }

    internal static StockMovementDto MapMovement(StockMovement item) => new(
        item.Id, item.OccurredAt, item.MovementType, item.ReferenceNumber,
        $"{item.AccountableFormType.Code} - {item.AccountableFormType.Name}", item.BeginningSerial,
        item.EndingSerial, item.Quantity, item.Source, item.Destination, item.PerformedByUser.DisplayName, item.Remarks);
}

public sealed class StockLookupService(
    IAccountableFormTypeRepository formTypes,
    ICollectorRepository collectors,
    IBarangayRepository barangays,
    IAuthorizationService authorization) : IStockLookupService
{
    public async Task<IReadOnlyList<StockFormTypeLookup>> ListFormTypesAsync(bool activeOnly = false, CancellationToken cancellationToken = default)
    {
        EnsureStockReadPermission();
        return (await formTypes.ListAsync(null, activeOnly ? true : null, cancellationToken))
            .Select(item => new StockFormTypeLookup(item.Id, item.Code, item.Name, item.DefaultReceiptsPerBooklet)).ToList();
    }

    public async Task<IReadOnlyList<StockLookup>> ListCollectorsAsync(CancellationToken cancellationToken = default)
    {
        EnsureStockReadPermission();
        return (await collectors.ListAsync(null, null, null, cancellationToken)).Select(item => new StockLookup(item.Id, item.FullName, item.IsActive)).ToList();
    }

    public async Task<IReadOnlyList<StockLookup>> ListBarangaysAsync(CancellationToken cancellationToken = default)
    {
        EnsureStockReadPermission();
        return (await barangays.ListAsync(null, null, cancellationToken)).Select(item => new StockLookup(item.Id, item.Name, item.IsActive)).ToList();
    }

    private void EnsureStockReadPermission()
    {
        if (!authorization.HasPermission(SystemPermission.ReceiveStock) &&
            !authorization.HasPermission(SystemPermission.ViewInventory) &&
            !authorization.HasPermission(SystemPermission.ViewStockMovements) &&
            !authorization.HasPermission(SystemPermission.ViewIssuances) &&
            !authorization.HasPermission(SystemPermission.ViewCollectorAccountability))
            throw new UnauthorizedAccessException("The current user cannot access stock reference data.");
    }
}
