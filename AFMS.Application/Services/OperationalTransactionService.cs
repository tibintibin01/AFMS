using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;
using AFMS.Domain.Services;

namespace AFMS.Application.Services;

public sealed class OperationalTransactionService(
    IOperationalTransactionRepository repository, ICollectorRepository collectors,
    IReferenceNumberService references, ITransactionRunner transactions, IUnitOfWork unitOfWork,
    IAuditService audit, IAuthorizationService authorization, ICurrentUserSession session,
    SerialRangeService serials, IFiscalPeriodGuard fiscalPeriods) : IOperationalTransactionService
{
    private Guid CurrentUserId => (session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.")).Id;

    public async Task<IReadOnlyList<OwnedRangeDto>> ListOwnedRangesAsync(Guid? collectorId = null, Guid? formTypeId = null, CancellationToken ct = default) =>
        (await repository.ListOwnedRangesAsync(collectorId, formTypeId, ct)).Select(r => new OwnedRangeDto(
            r.AccountableFormBookletId, r.AccountableFormBooklet.AccountableFormType.Code,
            r.AccountableFormBooklet.AccountableFormType.Name, r.AccountableFormBooklet.SeriesYear,
            r.AccountableFormBooklet.BeginningSerial, r.AccountableFormBooklet.EndingSerial,
            r.BeginningSerial, r.EndingSerial, r.CurrentCollector?.FullName ?? "Stock",
            r.CurrentBarangay?.Name, r.Status)).ToList();

    public async Task<IReadOnlyList<UsageReportDto>> ListUsageAsync(CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.RecordUsage);
        return (await repository.ListUsageAsync(ct)).Select(Map).ToList();
    }

    public async Task<UsageReportDto> SaveUsageDraftAsync(SaveUsageReportRequest request, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.RecordUsage);
        await fiscalPeriods.EnsureOpenAsync(request.ReportingDate, ct);
        if (request.CollectorId == Guid.Empty || request.FormTypeId == Guid.Empty || request.Ranges.Count == 0 ||
            request.PeriodEnd < request.PeriodStart || request.ReportingDate == default)
            throw new ApplicationValidationException(["Collector, form type, valid reporting period, and at least one usage range are required."]);
        ValidateInputRanges(request.Ranges.Select(x => (x.BookletId, x.BeginningSerial, x.EndingSerial)));
        foreach (var group in request.Ranges.GroupBy(x => x.BookletId))
        {
            var ordered = group.OrderBy(x => x.BeginningSerial).ToList();
            for (var i = 1; i < ordered.Count; i++)
                if (ordered[i].BeginningSerial != ordered[i - 1].EndingSerial + 1 && string.IsNullOrWhiteSpace(ordered[i].GapExplanation))
                    throw Validation("A gap between sequential usage ranges requires an explanation.");
        }
        var collector = await RequiredActiveCollector(request.CollectorId, ct);
        UsageReport entity;
        if (request.Id is null)
        {
            entity = new UsageReport { ReferenceNumber = await references.GenerateAsync("USE", request.ReportingDate.Year, ct),
                CollectorId = collector.Id, AccountableFormTypeId = request.FormTypeId, PeriodStart = request.PeriodStart,
                PeriodEnd = request.PeriodEnd, ReportingDate = request.ReportingDate, SubmittedDate = request.ReportingDate,
                EncodedByUserId = CurrentUserId };
            await repository.AddAsync(entity, ct);
        }
        else
        {
            entity = await RequiredUsage(request.Id.Value, ct);
            EnsureDraft(entity.Status); repository.RemoveRange(entity.Ranges.ToList()); entity.Ranges.Clear();
        }
        entity.CollectorId = request.CollectorId; entity.AccountableFormTypeId = request.FormTypeId;
        entity.PeriodStart = request.PeriodStart; entity.PeriodEnd = request.PeriodEnd; entity.ReportingDate = request.ReportingDate;
        entity.SubmittedDate = request.ReportingDate; entity.AmountCollected = request.AmountCollected;
        entity.SourceReportNumber = Clean(request.SourceReportNumber); entity.Remarks = Clean(request.Remarks);
        foreach (var input in request.Ranges)
        {
            var range = serials.Validate(input.BeginningSerial, input.EndingSerial);
            entity.Ranges.Add(new UsageRange { AccountableFormBookletId = input.BookletId,
                BeginningSerial = range.Beginning, EndingSerial = range.Ending, Quantity = serials.Count(range),
                GapExplanation = Clean(input.GapExplanation) });
        }
        await Record(entity, request.Id is null ? "created" : "edited", ct); await unitOfWork.SaveChangesAsync(ct);
        return Map(await RequiredUsage(entity.Id, ct));
    }

    public Task FinalizeUsageAsync(Guid id, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.RecordUsage);
        return transactions.ExecuteAsync(async token =>
        {
            var entity = await RequiredUsage(id, token); EnsureDraft(entity.Status);
            await fiscalPeriods.EnsureOpenAsync(entity.ReportingDate, token);
            var movements = new List<StockMovement>();
            foreach (var item in entity.Ranges.OrderBy(x => x.BeginningSerial))
            {
                var booklet = await RequiredBooklet(item.AccountableFormBookletId, token);
                if (booklet.AccountableFormTypeId != entity.AccountableFormTypeId) throw Validation("Usage range has the wrong form type.");
                var changed = await ChangeOwnedRange(booklet, entity.CollectorId, item.BeginningSerial, item.EndingSerial,
                    AccountableFormStatus.FullyUsed, null, null, nameof(UsageReport), entity.Id, token);
                movements.Add(Movement(booklet, StockMovementType.Used, entity.ReferenceNumber, item.BeginningSerial,
                    item.EndingSerial, entity.CollectorId, null, "Collector accountability", "Used/Sold", $"{nameof(UsageReport)}:{item.BeginningSerial}", entity.Id,
                    entity.ReportingDate, changed.PreviousBarangayId, -item.Quantity, entity.Remarks));
                RecalculateBooklet(booklet);
            }
            await repository.AddMovementsAsync(movements, token); entity.Status = TransactionStatus.Finalized; entity.FinalizedAt = DateTimeOffset.UtcNow;
            await Record(entity, "finalized", token); await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    public Task ReverseUsageAsync(Guid id, string reason, CancellationToken ct = default) => ReverseAsync(
        SystemPermission.ReverseOperationalTransactions, nameof(UsageReport), id, reason, repository.GetUsageAsync,
        async (entity, token) => { await fiscalPeriods.EnsureOpenAsync(entity.ReportingDate, token); foreach (var range in entity.Ranges) await RestoreRange(range.AccountableFormBookletId, nameof(UsageReport), entity.Id, entity.ReferenceNumber, reason, range.Quantity, entity.ReportingDate, range.Quantity, token); entity.Status = TransactionStatus.Reversed; entity.ReversedAt = DateTimeOffset.UtcNow; entity.ReversalReason = reason.Trim(); }, ct);

    public async Task<IReadOnlyList<TransferDto>> ListTransfersAsync(CancellationToken ct = default)
    { authorization.EnsurePermission(SystemPermission.TransferStock); return (await repository.ListTransfersAsync(ct)).Select(Map).ToList(); }

    public async Task<TransferDto> SaveTransferDraftAsync(SaveTransferRequest request, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.TransferStock); ValidateRange(request.BookletId, request.BeginningSerial, request.EndingSerial);
        await fiscalPeriods.EnsureOpenAsync(request.TransferDate, ct);
        if (request.FromCollectorId == request.ToCollectorId || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ReleasedBy) || string.IsNullOrWhiteSpace(request.ReceivedBy))
            throw Validation("Different collectors, reason, released by, and received by are required.");
        await RequiredCollector(request.FromCollectorId, ct); await RequiredActiveCollector(request.ToCollectorId, ct);
        Transfer entity;
        if (request.Id is null)
        {
            entity = new Transfer { ReferenceNumber = await references.GenerateAsync("TRF", request.TransferDate.Year, ct),
                TransferDate = request.TransferDate, FromCollectorId = request.FromCollectorId, ToCollectorId = request.ToCollectorId,
                AccountableFormBookletId = request.BookletId, Reason = request.Reason.Trim(), ReleasedBy = request.ReleasedBy.Trim(),
                ReceivedBy = request.ReceivedBy.Trim(), EncodedByUserId = CurrentUserId };
            await repository.AddAsync(entity, ct);
        }
        else { entity = await RequiredTransfer(request.Id.Value, ct); EnsureDraft(entity.Status); }
        entity.TransferDate = request.TransferDate; entity.FromCollectorId = request.FromCollectorId; entity.ToCollectorId = request.ToCollectorId;
        entity.BarangayId = request.BarangayId; entity.AccountableFormBookletId = request.BookletId; entity.BeginningSerial = request.BeginningSerial;
        entity.EndingSerial = request.EndingSerial; entity.Quantity = serials.Count(serials.Validate(request.BeginningSerial, request.EndingSerial));
        entity.Reason = request.Reason.Trim(); entity.ReleasedBy = request.ReleasedBy.Trim(); entity.ReceivedBy = request.ReceivedBy.Trim(); entity.Remarks = Clean(request.Remarks);
        await Record(entity, request.Id is null ? "created" : "edited", ct); await unitOfWork.SaveChangesAsync(ct); return Map(await RequiredTransfer(entity.Id, ct));
    }

    public Task SubmitTransferAsync(Guid id, CancellationToken ct = default) => SetSubmitted(id, repository.GetTransferAsync, SystemPermission.TransferStock, ct);
    public Task ApproveTransferAsync(Guid id, CancellationToken ct = default) => SetApproved(id, repository.GetTransferAsync, ct);
    public Task RejectTransferAsync(Guid id, string reason, CancellationToken ct = default) => SetRejected(id, reason, repository.GetTransferAsync, ct);
    public Task FinalizeTransferAsync(Guid id, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.TransferStock);
        return transactions.ExecuteAsync(async token =>
        {
            var entity = await RequiredTransfer(id, token); EnsureApproved(entity.Status); await RequiredActiveCollector(entity.ToCollectorId, token);
            await fiscalPeriods.EnsureOpenAsync(entity.TransferDate, token);
            var booklet = await RequiredBooklet(entity.AccountableFormBookletId, token);
            var changed = await ChangeOwnedRange(booklet, entity.FromCollectorId, entity.BeginningSerial, entity.EndingSerial,
                AccountableFormStatus.Transferred, entity.ToCollectorId, entity.BarangayId, nameof(Transfer), entity.Id, token);
            var movementBarangayId = entity.BarangayId ?? changed.PreviousBarangayId;
            await repository.AddMovementsAsync([
                Movement(booklet, StockMovementType.Transferred, entity.ReferenceNumber, entity.BeginningSerial, entity.EndingSerial, entity.FromCollectorId, null, entity.FromCollector.FullName, "Transfer out", "TransferOut", entity.Id, entity.TransferDate, movementBarangayId, 0, entity.Remarks),
                Movement(booklet, StockMovementType.Transferred, entity.ReferenceNumber, entity.BeginningSerial, entity.EndingSerial, null, entity.ToCollectorId, "Transfer in", entity.ToCollector.FullName, "TransferIn", entity.Id, entity.TransferDate, movementBarangayId, 0, entity.Remarks)], token);
            RecalculateBooklet(booklet); entity.Status = TransactionStatus.Finalized; entity.FinalizedAt = DateTimeOffset.UtcNow;
            await Record(entity, "finalized", token); await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    public Task ReverseTransferAsync(Guid id, string reason, CancellationToken ct = default) => ReverseAsync(
        SystemPermission.ReverseOperationalTransactions, nameof(Transfer), id, reason, repository.GetTransferAsync,
        async (entity, token) => { await fiscalPeriods.EnsureOpenAsync(entity.TransferDate, token); await RestoreRange(entity.AccountableFormBookletId, nameof(Transfer), entity.Id, entity.ReferenceNumber, reason, entity.Quantity, entity.TransferDate, 0, token); entity.Status = TransactionStatus.Reversed; entity.ReversedAt = DateTimeOffset.UtcNow; entity.ReversalReason = reason.Trim(); }, ct);

    public async Task<IReadOnlyList<ReturnDto>> ListReturnsAsync(CancellationToken ct = default)
    { authorization.EnsurePermission(SystemPermission.ReturnStock); return (await repository.ListReturnsAsync(ct)).Select(Map).ToList(); }

    public async Task<ReturnDto> SaveReturnDraftAsync(SaveReturnRequest request, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.ReturnStock); ValidateRange(request.BookletId, request.BeginningSerial, request.EndingSerial);
        await fiscalPeriods.EnsureOpenAsync(request.ReturnDate, ct);
        if (string.IsNullOrWhiteSpace(request.PhysicalCondition) || string.IsNullOrWhiteSpace(request.Reason) || string.IsNullOrWhiteSpace(request.ReceivedBy))
            throw Validation("Physical condition, reason, and received by are required.");
        await RequiredCollector(request.CollectorId, ct); ReturnTransaction entity;
        if (request.Id is null)
        {
            entity = new ReturnTransaction { ReferenceNumber = await references.GenerateAsync("RTN", request.ReturnDate.Year, ct),
                ReturnDate = request.ReturnDate, CollectorId = request.CollectorId, AccountableFormBookletId = request.BookletId,
                PhysicalCondition = request.PhysicalCondition.Trim(), Reason = request.Reason.Trim(), ReceivedBy = request.ReceivedBy.Trim(), EncodedByUserId = CurrentUserId };
            await repository.AddAsync(entity, ct);
        }
        else { entity = await RequiredReturn(request.Id.Value, ct); EnsureDraft(entity.Status); }
        entity.ReturnDate = request.ReturnDate; entity.CollectorId = request.CollectorId; entity.AccountableFormBookletId = request.BookletId;
        entity.BeginningSerial = request.BeginningSerial; entity.EndingSerial = request.EndingSerial; entity.Quantity = serials.Count(serials.Validate(request.BeginningSerial, request.EndingSerial));
        entity.PhysicalCondition = request.PhysicalCondition.Trim(); entity.Reason = request.Reason.Trim(); entity.ReceivedBy = request.ReceivedBy.Trim(); entity.Disposition = request.Disposition; entity.Remarks = Clean(request.Remarks);
        var issuance = await repository.FindSourceIssuanceAsync(request.BookletId, request.CollectorId, ct); entity.OriginalIssuanceId = issuance?.Id;
        await Record(entity, request.Id is null ? "created" : "edited", ct); await unitOfWork.SaveChangesAsync(ct); return Map(await RequiredReturn(entity.Id, ct));
    }

    public Task SubmitReturnAsync(Guid id, CancellationToken ct = default) => SetSubmitted(id, repository.GetReturnAsync, SystemPermission.ReturnStock, ct);
    public Task ApproveReturnAsync(Guid id, CancellationToken ct = default) => SetApproved(id, repository.GetReturnAsync, ct);
    public Task RejectReturnAsync(Guid id, string reason, CancellationToken ct = default) => SetRejected(id, reason, repository.GetReturnAsync, ct);
    public Task FinalizeReturnAsync(Guid id, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.ReturnStock);
        return transactions.ExecuteAsync(async token =>
        {
            var entity = await RequiredReturn(id, token); EnsureApproved(entity.Status); var booklet = await RequiredBooklet(entity.AccountableFormBookletId, token);
            await fiscalPeriods.EnsureOpenAsync(entity.ReturnDate, token);
            var status = entity.Disposition switch { ReturnDisposition.ReturnedToAvailableStock => AccountableFormStatus.Available,
                ReturnDisposition.ReturnedButDamaged => AccountableFormStatus.Spoiled, ReturnDisposition.ForCancellation => AccountableFormStatus.Cancelled,
                ReturnDisposition.Archived => AccountableFormStatus.Archived, _ => AccountableFormStatus.Returned };
            var changed = await ChangeOwnedRange(booklet, entity.CollectorId, entity.BeginningSerial, entity.EndingSerial, status, null, null, nameof(ReturnTransaction), entity.Id, token);
            var movementType = status switch
            {
                AccountableFormStatus.Available => StockMovementType.Returned,
                AccountableFormStatus.Archived => StockMovementType.Archived,
                AccountableFormStatus.Returned => StockMovementType.Adjusted,
                _ => StockMovementType.Cancelled
            };
            var inventoryDelta = ReturnInventoryDelta(entity.Disposition, entity.Quantity);
            await repository.AddMovementsAsync([Movement(booklet, movementType, entity.ReferenceNumber, entity.BeginningSerial, entity.EndingSerial,
                entity.CollectorId, null, entity.Collector.FullName, status == AccountableFormStatus.Available ? booklet.StorageLocation : status.ToString(), nameof(ReturnTransaction), entity.Id,
                entity.ReturnDate, changed.PreviousBarangayId, inventoryDelta, entity.Remarks)], token);
            RecalculateBooklet(booklet); entity.Status = TransactionStatus.Finalized; entity.FinalizedAt = DateTimeOffset.UtcNow;
            await Record(entity, "finalized", token); await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    public Task ReverseReturnAsync(Guid id, string reason, CancellationToken ct = default) => ReverseAsync(
        SystemPermission.ReverseOperationalTransactions, nameof(ReturnTransaction), id, reason, repository.GetReturnAsync,
        async (entity, token) => { await fiscalPeriods.EnsureOpenAsync(entity.ReturnDate, token); await RestoreRange(entity.AccountableFormBookletId, nameof(ReturnTransaction), entity.Id, entity.ReferenceNumber, reason, entity.Quantity, entity.ReturnDate, -ReturnInventoryDelta(entity.Disposition, entity.Quantity), token); entity.Status = TransactionStatus.Reversed; entity.ReversedAt = DateTimeOffset.UtcNow; entity.ReversalReason = reason.Trim(); }, ct);

    public async Task<IReadOnlyList<FormExceptionDto>> ListExceptionsAsync(CancellationToken ct = default)
    { authorization.EnsurePermission(SystemPermission.ManageExceptions); return (await repository.ListExceptionsAsync(ct)).Select(Map).ToList(); }

    public async Task<FormExceptionDto> SaveExceptionDraftAsync(SaveExceptionRequest request, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageExceptions); ValidateRange(request.BookletId, request.BeginningSerial, request.EndingSerial);
        await fiscalPeriods.EnsureOpenAsync(request.DateDiscovered, ct);
        if (string.IsNullOrWhiteSpace(request.Details) || string.IsNullOrWhiteSpace(request.ActionTaken)) throw Validation("Detailed explanation and action taken are required.");
        await RequiredCollector(request.CollectorId, ct); FormException entity;
        if (request.Id is null)
        {
            entity = new FormException { ReferenceNumber = await references.GenerateAsync("EXC", request.DateDiscovered.Year, ct),
                DateDiscovered = request.DateDiscovered, ReportedAt = DateTimeOffset.UtcNow, ExceptionType = request.IncidentType,
                CollectorId = request.CollectorId, AccountableFormBookletId = request.BookletId, Details = request.Details.Trim(),
                ActionTaken = request.ActionTaken.Trim(), EncodedByUserId = CurrentUserId };
            await repository.AddAsync(entity, ct);
        }
        else { entity = await RequiredException(request.Id.Value, ct); EnsureDraft(entity.Status); }
        entity.DateDiscovered = request.DateDiscovered; entity.ExceptionType = request.IncidentType; entity.CollectorId = request.CollectorId;
        entity.AccountableFormBookletId = request.BookletId; entity.SerialNumber = request.BeginningSerial; entity.EndingSerial = request.EndingSerial;
        entity.Quantity = serials.Count(serials.Validate(request.BeginningSerial, request.EndingSerial)); entity.Details = request.Details.Trim();
        entity.SupportingDocumentReference = Clean(request.SupportingDocumentReference); entity.ActionTaken = request.ActionTaken.Trim(); entity.Remarks = Clean(request.Remarks);
        await Record(entity, request.Id is null ? "created" : "edited", ct); await unitOfWork.SaveChangesAsync(ct); return Map(await RequiredException(entity.Id, ct));
    }

    public Task SubmitExceptionAsync(Guid id, CancellationToken ct = default) => SetSubmitted(id, repository.GetExceptionAsync, SystemPermission.ManageExceptions, ct);
    public Task ApproveExceptionAsync(Guid id, CancellationToken ct = default) => SetApproved(id, repository.GetExceptionAsync, ct);
    public Task RejectExceptionAsync(Guid id, string reason, CancellationToken ct = default) => SetRejected(id, reason, repository.GetExceptionAsync, ct);
    public Task FinalizeExceptionAsync(Guid id, CancellationToken ct = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageExceptions);
        return transactions.ExecuteAsync(async token =>
        {
            var entity = await RequiredException(id, token); EnsureApproved(entity.Status); var booklet = await RequiredBooklet(entity.AccountableFormBookletId, token);
            await fiscalPeriods.EnsureOpenAsync(entity.DateDiscovered, token);
            var status = entity.ExceptionType switch { ExceptionType.Lost => AccountableFormStatus.Lost, ExceptionType.Missing => AccountableFormStatus.Missing,
                ExceptionType.Cancelled or ExceptionType.Invalidated => AccountableFormStatus.Cancelled, _ => AccountableFormStatus.Spoiled };
            var changed = await ChangeOwnedRange(booklet, entity.CollectorId!.Value, entity.SerialNumber, entity.EndingSerial, status, null, null, nameof(FormException), entity.Id, token);
            await repository.AddMovementsAsync([Movement(booklet, StockMovementType.Cancelled, entity.ReferenceNumber, entity.SerialNumber, entity.EndingSerial,
                entity.CollectorId, null, entity.Collector?.FullName, status.ToString(), nameof(FormException), entity.Id, entity.DateDiscovered,
                changed.PreviousBarangayId, -entity.Quantity, entity.Remarks)], token);
            RecalculateBooklet(booklet); entity.Status = TransactionStatus.Finalized; entity.FinalizedAt = DateTimeOffset.UtcNow; entity.ResolutionStatus = ExceptionResolutionStatus.Resolved;
            await Record(entity, "finalized", token); await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    public Task ReverseExceptionAsync(Guid id, string reason, CancellationToken ct = default) => ReverseAsync(
        SystemPermission.ReverseOperationalTransactions, nameof(FormException), id, reason, repository.GetExceptionAsync,
        async (entity, token) => { await fiscalPeriods.EnsureOpenAsync(entity.DateDiscovered, token); await RestoreRange(entity.AccountableFormBookletId, nameof(FormException), entity.Id, entity.ReferenceNumber, reason, entity.Quantity, entity.DateDiscovered, entity.Quantity, token); entity.Status = TransactionStatus.Reversed; entity.ReversedAt = DateTimeOffset.UtcNow; entity.ReversalReason = reason.Trim(); entity.ResolutionStatus = ExceptionResolutionStatus.Closed; }, ct);

    private async Task<AccountabilityRange> ChangeOwnedRange(AccountableFormBooklet booklet, Guid ownerId, long beginning, long ending,
        AccountableFormStatus status, Guid? newOwner, Guid? barangayId, string transactionType, Guid transactionId, CancellationToken ct)
    {
        var target = booklet.AccountabilityRanges.SingleOrDefault(r => r.IsActive && r.CurrentCollectorId == ownerId &&
            r.BeginningSerial <= beginning && ending <= r.EndingSerial && r.Status is AccountableFormStatus.Issued or AccountableFormStatus.PartiallyUsed or AccountableFormStatus.Transferred)
            ?? throw Validation("The selected unused range is not currently held by this collector, has already been used, returned, transferred, or excepted.");
        var originalEnd = target.EndingSerial; var originalStatus = target.Status; var originalOwner = target.CurrentCollectorId;
        var originalBarangay = target.CurrentBarangayId; var originalStorage = target.StorageLocation;
        if (target.BeginningSerial < beginning) target.EndingSerial = beginning - 1; else target.IsActive = false;
        // Persist the source-range shrink/deactivation inside the surrounding database
        // transaction before inserting replacement ranges. SQLite overlap triggers are
        // immediate, so this preserves the invariant at every statement boundary.
        await unitOfWork.SaveChangesAsync(ct);
        if (ending < originalEnd) { var suffix = Clone(target, ending + 1, originalEnd, originalStatus, originalOwner, originalBarangay, originalStorage); booklet.AccountabilityRanges.Add(suffix); await repository.AddAsync(suffix, ct); }
        var changed = new AccountabilityRange { AccountableFormBookletId = booklet.Id, BeginningSerial = beginning, EndingSerial = ending,
            Status = status, CurrentCollectorId = newOwner, CurrentBarangayId = barangayId, StorageLocation = newOwner is null ? booklet.StorageLocation : null,
            SourceIssuanceId = target.SourceIssuanceId, LastTransactionType = transactionType, LastTransactionId = transactionId,
            PreviousStatus = originalStatus, PreviousCollectorId = originalOwner, PreviousBarangayId = originalBarangay, PreviousStorageLocation = originalStorage };
        changed.PreviousTransactionType = target.LastTransactionType; changed.PreviousTransactionId = target.LastTransactionId;
        booklet.AccountabilityRanges.Add(changed); await repository.AddAsync(changed, ct);
        return changed;
    }

    private static AccountabilityRange Clone(AccountabilityRange source, long beginning, long ending, AccountableFormStatus status,
        Guid? owner, Guid? barangay, string? storage) => new() { AccountableFormBookletId = source.AccountableFormBookletId,
        BeginningSerial = beginning, EndingSerial = ending, Status = status, CurrentCollectorId = owner, CurrentBarangayId = barangay,
        StorageLocation = storage, SourceIssuanceId = source.SourceIssuanceId, LastTransactionType = source.LastTransactionType,
        LastTransactionId = source.LastTransactionId, PreviousStatus = source.PreviousStatus, PreviousCollectorId = source.PreviousCollectorId,
        PreviousBarangayId = source.PreviousBarangayId, PreviousStorageLocation = source.PreviousStorageLocation,
        PreviousTransactionType = source.PreviousTransactionType, PreviousTransactionId = source.PreviousTransactionId };

    private async Task RestoreRange(Guid bookletId, string type, Guid id, string reference, string reason, long expectedQuantity,
        DateOnly effectiveDate, long reversalInventoryQuantityDelta, CancellationToken ct)
    {
        var booklet = await RequiredBooklet(bookletId, ct);
        var changed = booklet.AccountabilityRanges.Where(r => r.IsActive && r.LastTransactionType == type && r.LastTransactionId == id).ToList();
        if (changed.Count == 0 || changed.Sum(r => r.EndingSerial - r.BeginningSerial + 1) != expectedQuantity)
            throw new InvalidOperationException("This transaction cannot be reversed because all or part of its range was changed by a later transaction.");
        var movements = new List<StockMovement>();
        foreach (var range in changed)
        {
            var restoredTransactionType = range.PreviousTransactionType;
            var restoredTransactionId = range.PreviousTransactionId;
            var lineage = booklet.AccountabilityRanges
                .Where(candidate => candidate.Id != range.Id && candidate.LastTransactionType == restoredTransactionType &&
                    candidate.LastTransactionId == restoredTransactionId && candidate.BeginningSerial <= range.BeginningSerial &&
                    range.EndingSerial <= candidate.EndingSerial)
                .OrderByDescending(candidate => candidate.UpdatedAt)
                .FirstOrDefault();
            var currentOwner = range.CurrentCollectorId;
            range.Status = range.PreviousStatus ?? AccountableFormStatus.Issued; range.CurrentCollectorId = range.PreviousCollectorId;
            range.CurrentBarangayId = range.PreviousBarangayId; range.StorageLocation = range.PreviousStorageLocation;
            range.LastTransactionType = restoredTransactionType; range.LastTransactionId = restoredTransactionId;
            range.PreviousStatus = lineage?.PreviousStatus; range.PreviousCollectorId = lineage?.PreviousCollectorId;
            range.PreviousBarangayId = lineage?.PreviousBarangayId; range.PreviousStorageLocation = lineage?.PreviousStorageLocation;
            range.PreviousTransactionType = lineage?.PreviousTransactionType; range.PreviousTransactionId = lineage?.PreviousTransactionId;
            movements.Add(Movement(booklet, StockMovementType.Reversed, reference, range.BeginningSerial, range.EndingSerial,
                currentOwner, range.CurrentCollectorId, "Reversal", "Restored accountability", $"{type}Reversal:{range.BeginningSerial}", id,
                effectiveDate, range.CurrentBarangayId, reversalInventoryQuantityDelta, reason));
        }
        await repository.AddMovementsAsync(movements, ct); RecalculateBooklet(booklet);
    }

    private static void RecalculateBooklet(AccountableFormBooklet booklet)
    {
        var ranges = booklet.AccountabilityRanges.Where(r => r.IsActive).ToList();
        booklet.UsedQuantity = ranges.Where(r => r.Status == AccountableFormStatus.FullyUsed).Sum(r => r.EndingSerial - r.BeginningSerial + 1);
        booklet.RemainingQuantity = booklet.TotalQuantity - booklet.UsedQuantity;
        var owners = ranges.Where(r => r.CurrentCollectorId != null).Select(r => r.CurrentCollectorId).Distinct().ToList();
        booklet.CurrentCollectorId = owners.Count == 1 && ranges.All(r => r.CurrentCollectorId == owners[0] || r.Status == AccountableFormStatus.FullyUsed) ? owners[0] : null;
        booklet.CurrentBarangayId = booklet.CurrentCollectorId is null ? null : ranges.First(r => r.CurrentCollectorId == booklet.CurrentCollectorId).CurrentBarangayId;
        if (booklet.UsedQuantity == booklet.TotalQuantity) booklet.Status = AccountableFormStatus.FullyUsed;
        else if (booklet.UsedQuantity > 0) booklet.Status = AccountableFormStatus.PartiallyUsed;
        else if (ranges.Select(r => r.Status).Distinct().Count() == 1) booklet.Status = ranges[0].Status;
        else if (ranges.Any(r => r.Status == AccountableFormStatus.Transferred)) booklet.Status = AccountableFormStatus.Transferred;
        else if (ranges.Any(r => r.Status is AccountableFormStatus.Lost or AccountableFormStatus.Missing or AccountableFormStatus.Cancelled or AccountableFormStatus.Spoiled)) booklet.Status = ranges.First(r => r.Status is AccountableFormStatus.Lost or AccountableFormStatus.Missing or AccountableFormStatus.Cancelled or AccountableFormStatus.Spoiled).Status;
        else booklet.Status = AccountableFormStatus.Returned;
    }

    private StockMovement Movement(AccountableFormBooklet b, StockMovementType type, string reference, long begin, long end,
        Guid? from, Guid? to, string? source, string? destination, string referenceType, Guid referenceId, DateOnly effectiveDate,
        Guid? barangayId, long inventoryQuantityDelta, string? remarks) => new()
        { AccountableFormBookletId = b.Id, AccountableFormTypeId = b.AccountableFormTypeId, MovementType = type, ReferenceNumber = reference,
          BeginningSerial = begin, EndingSerial = end, Quantity = end - begin + 1, InventoryQuantityDelta = inventoryQuantityDelta,
          FromCollectorId = from, ToCollectorId = to, BarangayId = barangayId,
          Source = source, Destination = destination, ReferenceType = referenceType, ReferenceId = referenceId,
          PerformedByUserId = CurrentUserId, EffectiveDate = effectiveDate, OccurredAt = DateTimeOffset.UtcNow, Remarks = remarks };

    private static long ReturnInventoryDelta(ReturnDisposition disposition, long quantity) =>
        disposition == ReturnDisposition.ReturnedToAvailableStock ? 0 : -quantity;

    private async Task SetSubmitted<T>(Guid id, Func<Guid, CancellationToken, Task<T?>> getter, SystemPermission permission, CancellationToken ct)
        where T : class, IApprovalWorkflowTransaction
    {
        authorization.EnsurePermission(permission); var entity = await getter(id, ct) ?? throw new KeyNotFoundException("Transaction not found.");
        EnsureDraft(entity.Status);
        entity.Status = TransactionStatus.Submitted;
        entity.SubmittedByUserId = CurrentUserId;
        entity.SubmittedAt = DateTimeOffset.UtcNow;
        await Record(entity, "submitted", ct); await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task SetApproved<T>(Guid id, Func<Guid, CancellationToken, Task<T?>> getter, CancellationToken ct)
        where T : class, IApprovalWorkflowTransaction
    {
        authorization.EnsurePermission(SystemPermission.ApproveTransactions); var entity = await getter(id, ct) ?? throw new KeyNotFoundException("Transaction not found.");
        if (entity.Status != TransactionStatus.Submitted) throw new InvalidOperationException("Only submitted transactions may be approved.");
        if (entity.SubmittedByUserId == CurrentUserId || entity.EncodedByUserId == CurrentUserId)
            throw new UnauthorizedAccessException("Separation of duties prevents approving a transaction you prepared or submitted.");
        entity.Status = TransactionStatus.Approved; entity.ApprovalStatus = ApprovalStatus.Approved;
        entity.ApprovedByUserId = CurrentUserId; entity.ApprovedAt = DateTimeOffset.UtcNow; entity.DecisionAt = entity.ApprovedAt;
        await Record(entity, "approved", ct); await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task SetRejected<T>(Guid id, string reason, Func<Guid, CancellationToken, Task<T?>> getter, CancellationToken ct)
        where T : class, IApprovalWorkflowTransaction
    {
        authorization.EnsurePermission(SystemPermission.ApproveTransactions);
        if (string.IsNullOrWhiteSpace(reason)) throw Validation("A rejection reason is required.");
        var entity = await getter(id, ct) ?? throw new KeyNotFoundException("Transaction not found.");
        if (entity.Status != TransactionStatus.Submitted) throw new InvalidOperationException("Only submitted transactions may be rejected.");
        if (entity.SubmittedByUserId == CurrentUserId || entity.EncodedByUserId == CurrentUserId) throw new UnauthorizedAccessException("Separation of duties prevents rejecting a transaction you prepared or submitted.");
        entity.Status = TransactionStatus.Rejected; entity.ApprovalStatus = ApprovalStatus.Rejected;
        entity.RejectedByUserId = CurrentUserId; entity.DecisionAt = DateTimeOffset.UtcNow; entity.DecisionComments = reason.Trim();
        await Record(entity, $"rejected: {reason.Trim()}", ct); await unitOfWork.SaveChangesAsync(ct);
    }

    private async Task ReverseAsync<T>(SystemPermission permission, string type, Guid id, string reason,
        Func<Guid, CancellationToken, Task<T?>> getter, Func<T, CancellationToken, Task> operation, CancellationToken ct)
        where T : class, IReversibleTransaction
    {
        authorization.EnsurePermission(permission); if (string.IsNullOrWhiteSpace(reason)) throw Validation("A reversal reason is required.");
        var attempt = await getter(id, ct) ?? throw new KeyNotFoundException("Transaction not found.");
        await audit.RecordAsync(UserActivityType.ReversalAttempt, $"{type} reversal requested.", type, id, null,
            new { Reason = reason.Trim() }, ct, "Operational Transactions", attempt.ReferenceNumber, reason.Trim());
        await unitOfWork.SaveChangesAsync(ct);
        var approvedBy = (attempt as IApprovalWorkflowTransaction)?.ApprovedByUserId;
        if (attempt.EncodedByUserId == CurrentUserId || approvedBy == CurrentUserId)
            throw new UnauthorizedAccessException("Separation of duties requires another authorized user to approve this reversal.");
        await transactions.ExecuteAsync(async token =>
        {
            var entity = await getter(id, token) ?? throw new KeyNotFoundException("Transaction not found.");
            if (entity.Status != TransactionStatus.Finalized) throw new InvalidOperationException("Only a finalized, unreversed transaction may be reversed.");
            await operation(entity, token); await Record(entity, $"reversed: {reason.Trim()}", token); await unitOfWork.SaveChangesAsync(token);
        }, ct);
    }

    private async Task<AccountableFormBooklet> RequiredBooklet(Guid id, CancellationToken ct)
    {
        var booklet = await repository.GetBookletAsync(id, ct) ?? throw new KeyNotFoundException("Booklet not found.");
        if (booklet.AccountabilityRanges.Count == 0)
        {
            var range = new AccountabilityRange { AccountableFormBookletId = booklet.Id,
                BeginningSerial = booklet.BeginningSerial, EndingSerial = booklet.EndingSerial, Status = booklet.Status,
                CurrentCollectorId = booklet.CurrentCollectorId, CurrentBarangayId = booklet.CurrentBarangayId,
                StorageLocation = booklet.StorageLocation,
                SourceIssuanceId = booklet.IssuanceItems.Where(x => x.Issuance.Status == TransactionStatus.Finalized).OrderByDescending(x => x.Issuance.FinalizedAt).Select(x => (Guid?)x.IssuanceId).FirstOrDefault() };
            booklet.AccountabilityRanges.Add(range); await repository.AddAsync(range, ct);
        }
        return booklet;
    }

    private async Task<Collector> RequiredActiveCollector(Guid id, CancellationToken ct)
    { var collector = await collectors.GetAsync(id, ct) ?? throw Validation("Collector was not found."); if (!collector.IsActive) throw Validation("Inactive collectors cannot participate in transactions."); return collector; }
    private async Task<Collector> RequiredCollector(Guid id, CancellationToken ct) =>
        await collectors.GetAsync(id, ct) ?? throw Validation("Collector was not found.");
    private Task<UsageReport> RequiredUsage(Guid id, CancellationToken ct) => Required(repository.GetUsageAsync(id, ct));
    private Task<Transfer> RequiredTransfer(Guid id, CancellationToken ct) => Required(repository.GetTransferAsync(id, ct));
    private Task<ReturnTransaction> RequiredReturn(Guid id, CancellationToken ct) => Required(repository.GetReturnAsync(id, ct));
    private Task<FormException> RequiredException(Guid id, CancellationToken ct) => Required(repository.GetExceptionAsync(id, ct));
    private static async Task<T> Required<T>(Task<T?> task) where T : class => await task ?? throw new KeyNotFoundException("Transaction not found.");
    private static void EnsureDraft(TransactionStatus status) { if (status != TransactionStatus.Draft) throw new InvalidOperationException("Only draft transactions may be edited or finalized."); }
    private static void EnsureApproved(TransactionStatus status) { if (status != TransactionStatus.Approved) throw new InvalidOperationException("Supervisor approval is required before finalization."); }
    private void ValidateRange(Guid bookletId, long begin, long end) { if (bookletId == Guid.Empty) throw Validation("Booklet is required."); serials.Validate(begin, end); }
    private void ValidateInputRanges(IEnumerable<(Guid Booklet, long Begin, long End)> values)
    { var ranges = values.Select(v => (v.Booklet, Range: serials.Validate(v.Begin, v.End))).ToList(); foreach (var group in ranges.GroupBy(x => x.Booklet)) { var ordered = group.OrderBy(x => x.Range.Beginning).ToList(); for (var i = 1; i < ordered.Count; i++) if (serials.Overlaps(ordered[i - 1].Range, ordered[i].Range)) throw Validation("Usage ranges cannot overlap."); } }
    private Task Record(object entity, string action, CancellationToken ct)
    {
        var type = entity.GetType(); var id = (Guid)type.GetProperty("Id")!.GetValue(entity)!;
        var reference = Convert.ToString(type.GetProperty("ReferenceNumber")?.GetValue(entity));
        var activity = action.StartsWith("submitted", StringComparison.OrdinalIgnoreCase) ? UserActivityType.Submitted :
            action.StartsWith("approved", StringComparison.OrdinalIgnoreCase) ? UserActivityType.Approved :
            action.StartsWith("rejected", StringComparison.OrdinalIgnoreCase) ? UserActivityType.Rejected :
            action.StartsWith("finalized", StringComparison.OrdinalIgnoreCase) ? UserActivityType.Finalized :
            action.StartsWith("reversed", StringComparison.OrdinalIgnoreCase) ? UserActivityType.Reversed :
            action.StartsWith("created", StringComparison.OrdinalIgnoreCase) ? UserActivityType.RecordCreated : UserActivityType.RecordUpdated;
        return audit.RecordAsync(activity, $"{type.Name} {action}.", type.Name, id, null, new { Action = action }, ct,
            "Operational Transactions", reference, action.StartsWith("reversed", StringComparison.OrdinalIgnoreCase) || action.StartsWith("rejected", StringComparison.OrdinalIgnoreCase) ? action : null);
    }
    private static ApplicationValidationException Validation(string message) => new([message]);
    private static string? Clean(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static UsageReportDto Map(UsageReport e) => new(e.Id, e.ReferenceNumber, e.Collector.FullName, e.AccountableFormType.Code, e.ReportingDate, e.Ranges.Sum(x => x.Quantity), e.AmountCollected, e.Status, e.Remarks);
    private static TransferDto Map(Transfer e) => new(e.Id, e.ReferenceNumber, e.TransferDate, e.FromCollector.FullName, e.ToCollector.FullName, e.AccountableFormBooklet.AccountableFormType.Code, e.BeginningSerial, e.EndingSerial, e.Quantity, e.Status, e.ApprovalStatus);
    private static ReturnDto Map(ReturnTransaction e) => new(e.Id, e.ReferenceNumber, e.ReturnDate, e.Collector.FullName, e.AccountableFormBooklet.AccountableFormType.Code, e.BeginningSerial, e.EndingSerial, e.Quantity, e.Disposition, e.Status, e.ApprovalStatus);
    private static FormExceptionDto Map(FormException e) => new(e.Id, e.ReferenceNumber, e.DateDiscovered, e.ExceptionType, e.Collector?.FullName ?? "Unassigned", e.AccountableFormBooklet.AccountableFormType.Code, e.SerialNumber, e.EndingSerial, e.Quantity, e.Status, e.ApprovalStatus, e.ResolutionStatus);
}
