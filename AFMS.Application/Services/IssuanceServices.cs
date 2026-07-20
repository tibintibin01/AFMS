using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class IssuanceService(
    IIssuanceRepository issuances,
    ICollectorRepository collectors,
    IBarangayRepository barangays,
    IReferenceNumberService references,
    ITransactionRunner transactions,
    IUnitOfWork unitOfWork,
    IAuditService audit,
    IAuthorizationService authorization,
    ICurrentUserSession session,
    IFiscalPeriodGuard fiscalPeriods) : IIssuanceService
{
    public async Task<IReadOnlyList<IssuanceDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewIssuances);
        return (await issuances.ListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<IssuanceDto?> GetAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewIssuances);
        var entity = await issuances.GetAsync(id, cancellationToken);
        return entity is null ? null : Map(entity);
    }

    public async Task<IReadOnlyList<AvailableBookletDto>> ListAvailableBookletsAsync(AvailableBookletFilter filter, Guid? excludingIssuanceId = null, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.IssueStock);
        return (await issuances.ListAvailableBookletsAsync(filter, excludingIssuanceId, cancellationToken)).Select(MapAvailable).ToList();
    }

    public async Task<IssuanceDto> SaveDraftAsync(SaveIssuanceDraftRequest request, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.IssueStock);
        await fiscalPeriods.EnsureOpenAsync(request.DateIssued, cancellationToken);
        ValidateDraft(request);
        var collector = await collectors.GetAsync(request.CollectorId, cancellationToken)
            ?? throw new ApplicationValidationException(["Collector was not found."]);
        if (!collector.IsActive) throw new ApplicationValidationException(["Inactive collectors cannot receive accountable forms."]);
        if (request.BarangayId is not null)
        {
            var barangay = await barangays.GetAsync(request.BarangayId.Value, cancellationToken)
                ?? throw new ApplicationValidationException(["Assigned barangay was not found."]);
            if (!barangay.IsActive) throw new ApplicationValidationException(["The selected barangay is inactive."]);
        }

        var selectedIds = request.BookletIds.Distinct().ToList();
        if (selectedIds.Count != request.BookletIds.Count) throw new ApplicationValidationException(["The same available serial range cannot be selected twice."]);
        var available = await issuances.ListAvailableBookletsAsync(new AvailableBookletFilter(), request.Id, cancellationToken);
        var selectedRanges = new List<AccountabilityRange>();
        foreach (var selectedId in selectedIds)
        {
            var matches = available.Where(range => range.Id == selectedId ||
                (range.AccountableFormBookletId == selectedId && range.BeginningSerial == range.AccountableFormBooklet.BeginningSerial &&
                 range.EndingSerial == range.AccountableFormBooklet.EndingSerial)).ToList();
            if (matches.Count != 1)
                throw new ApplicationValidationException(["One or more selected serial ranges are unavailable, reserved, issued, or belong to a closed fiscal period."]);
            selectedRanges.Add(matches[0]);
        }

        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.");
        Issuance entity;
        object? oldValues = null;
        if (request.Id is null)
        {
            entity = new Issuance
            {
                ReferenceNumber = await references.GenerateAsync("ISS", request.DateIssued.Year, cancellationToken),
                IssuedDate = request.DateIssued,
                CollectorId = request.CollectorId,
                Purpose = request.Purpose.Trim(),
                ReleasedBy = request.ReleasedBy.Trim(),
                ReceivedBy = request.ReceivedBy.Trim(),
                IssuedByUserId = currentUser.Id
            };
            await issuances.AddAsync(entity, cancellationToken);
        }
        else
        {
            entity = await GetRequiredAsync(request.Id.Value, cancellationToken);
            if (entity.Status != TransactionStatus.Draft) throw new InvalidOperationException("Only draft issuances may be edited.");
            oldValues = Snapshot(entity);
            issuances.RemoveItems(entity.Items.ToList());
            entity.Items.Clear();
        }

        Apply(entity, request);
        foreach (var range in selectedRanges)
        {
            var booklet = range.AccountableFormBooklet;
            entity.Items.Add(new IssuanceItem
            {
                AccountableFormBookletId = booklet.Id,
                BeginningSerial = range.BeginningSerial,
                EndingSerial = range.EndingSerial,
                Quantity = checked(range.EndingSerial - range.BeginningSerial + 1),
                SeriesYear = booklet.SeriesYear,
                IsReservationActive = true
            });
        }

        await audit.RecordAsync(request.Id is null ? UserActivityType.RecordCreated : UserActivityType.RecordUpdated,
            request.Id is null ? $"Issuance draft {entity.ReferenceNumber} created." : $"Issuance draft {entity.ReferenceNumber} edited.",
            nameof(Issuance), entity.Id, oldValues, Snapshot(entity), cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map((await GetRequiredAsync(entity.Id, cancellationToken)));
    }

    public async Task SubmitAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.IssueStock);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await fiscalPeriods.EnsureOpenAsync(entity.IssuedDate, cancellationToken);
        if (entity.Status != TransactionStatus.Draft) throw new InvalidOperationException("Only draft issuances may be submitted.");
        await ValidateForActionAsync(entity, cancellationToken);
        entity.Status = TransactionStatus.Submitted; entity.ApprovalStatus = ApprovalStatus.Pending; entity.SubmittedAt = DateTimeOffset.UtcNow;
        entity.SubmittedByUserId = session.User?.Id;
        await AuditStatusAsync(entity, "submitted", TransactionStatus.Draft, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ApproveTransactions);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await fiscalPeriods.EnsureOpenAsync(entity.IssuedDate, cancellationToken);
        if (entity.Status != TransactionStatus.Submitted) throw new InvalidOperationException("Only submitted issuances may be approved.");
        await ValidateForActionAsync(entity, cancellationToken);
        var user = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.");
        if (entity.SubmittedByUserId == user.Id || entity.IssuedByUserId == user.Id)
            throw new UnauthorizedAccessException("Separation of duties prevents approving an issuance you prepared or submitted.");
        entity.Status = TransactionStatus.Approved; entity.ApprovalStatus = ApprovalStatus.Approved;
        entity.ApprovedByUserId = user.Id; entity.ApprovedAt = DateTimeOffset.UtcNow; entity.DecisionAt = entity.ApprovedAt;
        await AuditStatusAsync(entity, "approved", TransactionStatus.Submitted, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RejectAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ApproveTransactions);
        if (string.IsNullOrWhiteSpace(reason)) throw new ApplicationValidationException(["A rejection reason is required."]);
        var entity = await GetRequiredAsync(id, cancellationToken);
        await fiscalPeriods.EnsureOpenAsync(entity.IssuedDate, cancellationToken);
        if (entity.Status != TransactionStatus.Submitted) throw new InvalidOperationException("Only submitted issuances may be rejected.");
        var user = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.");
        if (entity.SubmittedByUserId == user.Id || entity.IssuedByUserId == user.Id)
            throw new UnauthorizedAccessException("Separation of duties prevents rejecting an issuance you prepared or submitted.");
        entity.Status = TransactionStatus.Rejected; entity.ApprovalStatus = ApprovalStatus.Rejected;
        entity.RejectionReason = reason.Trim(); entity.DecisionComments = reason.Trim(); entity.RejectedAt = DateTimeOffset.UtcNow;
        entity.DecisionAt = entity.RejectedAt; entity.RejectedByUserId = user.Id;
        foreach (var item in entity.Items) item.IsReservationActive = false;
        await AuditStatusAsync(entity, "rejected", TransactionStatus.Submitted, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task FinalizeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.IssueStock);
        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.");
        await transactions.ExecuteAsync(async token =>
        {
            var entity = await GetRequiredAsync(id, token);
            await fiscalPeriods.EnsureOpenAsync(entity.IssuedDate, token);
            if (entity.Status != TransactionStatus.Approved) throw new InvalidOperationException("Only approved issuances may be finalized.");
            await ValidateForActionAsync(entity, token);
            var movements = new List<StockMovement>();
            foreach (var item in entity.Items)
            {
                var booklet = item.AccountableFormBooklet;
                if (!item.IsReservationActive || !await issuances.IsRangeIssuableAsync(booklet.Id, item.BeginningSerial, item.EndingSerial, entity.Id, token))
                    throw new ApplicationValidationException([$"Serial range {item.BeginningSerial}-{item.EndingSerial} is no longer issuable."]);
                if (item.Quantity != checked(item.EndingSerial - item.BeginningSerial + 1))
                    throw new ApplicationValidationException(["An issuance item has an invalid serial-range quantity."]);
                var range = booklet.AccountabilityRanges.SingleOrDefault(candidate => candidate.IsActive &&
                    candidate.BeginningSerial == item.BeginningSerial && candidate.EndingSerial == item.EndingSerial &&
                    candidate.Status == AccountableFormStatus.Available && candidate.CurrentCollectorId is null)
                    ?? throw new ApplicationValidationException([$"Serial range {item.BeginningSerial}-{item.EndingSerial} is not available."]);
                range.PreviousStatus = range.Status;
                range.PreviousCollectorId = range.CurrentCollectorId;
                range.PreviousBarangayId = range.CurrentBarangayId;
                range.PreviousStorageLocation = range.StorageLocation;
                range.PreviousTransactionType = range.LastTransactionType;
                range.PreviousTransactionId = range.LastTransactionId;
                range.Status = AccountableFormStatus.Issued;
                range.CurrentCollectorId = entity.CollectorId;
                range.CurrentBarangayId = entity.BarangayId;
                range.SourceIssuanceId = entity.Id;
                range.StorageLocation = null;
                range.LastTransactionType = nameof(Issuance);
                range.LastTransactionId = entity.Id;
                UpdateBookletSummary(booklet, entity.IssuedDate);
                item.IsReservationActive = false;
                movements.Add(new StockMovement
                {
                    AccountableFormBookletId = booklet.Id,
                    AccountableFormTypeId = booklet.AccountableFormTypeId,
                    MovementType = StockMovementType.Issued,
                    ReferenceNumber = entity.ReferenceNumber,
                    BeginningSerial = item.BeginningSerial,
                    EndingSerial = item.EndingSerial,
                    Quantity = item.Quantity,
                    InventoryQuantityDelta = 0,
                    Source = booklet.StorageLocation,
                    Destination = entity.Collector.FullName,
                    ToCollectorId = entity.CollectorId,
                    BarangayId = entity.BarangayId,
                    ReferenceType = nameof(Issuance),
                    ReferenceId = entity.Id,
                    PerformedByUserId = currentUser.Id,
                    EffectiveDate = entity.IssuedDate,
                    OccurredAt = DateTimeOffset.UtcNow,
                    Remarks = entity.Remarks
                });
            }
            await issuances.AddMovementsAsync(movements, token);
            entity.Status = TransactionStatus.Finalized; entity.FinalizedAt = DateTimeOffset.UtcNow;
            await AuditStatusAsync(entity, "finalized", TransactionStatus.Approved, token);
            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task ReverseAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ReverseIssuance);
        if (string.IsNullOrWhiteSpace(reason)) throw new ApplicationValidationException(["A reversal reason is required."]);
        var currentUser = session.User ?? throw new UnauthorizedAccessException("A signed-in user is required.");
        var attempt = await GetRequiredAsync(id, cancellationToken);
        await audit.RecordAsync(UserActivityType.ReversalAttempt, $"Issuance reversal requested for {attempt.ReferenceNumber}.", nameof(Issuance), attempt.Id,
            null, new { Reason = reason.Trim() }, cancellationToken, "Issuance", attempt.ReferenceNumber, reason.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        if (attempt.IssuedByUserId == currentUser.Id || attempt.ApprovedByUserId == currentUser.Id)
            throw new UnauthorizedAccessException("Separation of duties requires another authorized user to approve this reversal.");
        await transactions.ExecuteAsync(async token =>
        {
            var entity = await GetRequiredAsync(id, token);
            await fiscalPeriods.EnsureOpenAsync(entity.IssuedDate, token);
            if (entity.Status != TransactionStatus.Finalized) throw new InvalidOperationException("Only finalized issuances may be reversed.");
            var movements = new List<StockMovement>();
            foreach (var item in entity.Items)
            {
                var booklet = item.AccountableFormBooklet;
                var issuedRange = booklet.AccountabilityRanges.SingleOrDefault(range => range.IsActive && range.SourceIssuanceId == entity.Id &&
                    range.BeginningSerial == item.BeginningSerial && range.EndingSerial == item.EndingSerial);
                if (issuedRange is null || issuedRange.Status != AccountableFormStatus.Issued || issuedRange.CurrentCollectorId != entity.CollectorId)
                    throw new InvalidOperationException("Issuance cannot be reversed because one or more issued serial ranges changed accountability or usage.");
                movements.Add(new StockMovement
                {
                    AccountableFormBookletId = booklet.Id, AccountableFormTypeId = booklet.AccountableFormTypeId,
                    MovementType = StockMovementType.Reversed, ReferenceNumber = entity.ReferenceNumber,
                    BeginningSerial = item.BeginningSerial, EndingSerial = item.EndingSerial, Quantity = item.Quantity,
                    InventoryQuantityDelta = 0,
                    Source = entity.Collector.FullName, Destination = booklet.StorageLocation, FromCollectorId = entity.CollectorId,
                    BarangayId = entity.BarangayId, ReferenceType = "IssuanceReversal", ReferenceId = entity.Id,
                    PerformedByUserId = currentUser.Id, EffectiveDate = entity.IssuedDate, OccurredAt = DateTimeOffset.UtcNow, Remarks = reason.Trim()
                });
                issuedRange.Status = issuedRange.PreviousStatus ?? AccountableFormStatus.Available;
                issuedRange.CurrentCollectorId = issuedRange.PreviousCollectorId;
                issuedRange.CurrentBarangayId = issuedRange.PreviousBarangayId;
                issuedRange.StorageLocation = issuedRange.PreviousStorageLocation ?? booklet.StorageLocation;
                issuedRange.SourceIssuanceId = null;
                issuedRange.LastTransactionType = "IssuanceReversal";
                issuedRange.LastTransactionId = entity.Id;
                UpdateBookletSummary(booklet, null);
            }
            await issuances.AddMovementsAsync(movements, token);
            entity.Status = TransactionStatus.Reversed; entity.ReversedAt = DateTimeOffset.UtcNow; entity.ReversalReason = reason.Trim();
            await AuditStatusAsync(entity, "reversed", TransactionStatus.Finalized, token);
            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    private async Task ValidateForActionAsync(Issuance entity, CancellationToken cancellationToken)
    {
        var errors = new List<string>();
        if (!entity.Collector.IsActive) errors.Add("Inactive collectors cannot receive accountable forms.");
        if (entity.Items.Count == 0) errors.Add("At least one booklet is required.");
        if (string.IsNullOrWhiteSpace(entity.Purpose)) errors.Add("Purpose is required.");
        if (string.IsNullOrWhiteSpace(entity.ReleasedBy)) errors.Add("Released by is required.");
        if (string.IsNullOrWhiteSpace(entity.ReceivedBy)) errors.Add("Received by is required.");
        foreach (var group in entity.Items.GroupBy(item => item.AccountableFormBookletId))
        {
            var ordered = group.OrderBy(item => item.BeginningSerial).ToList();
            for (var index = 1; index < ordered.Count; index++)
                if (ordered[index].BeginningSerial <= ordered[index - 1].EndingSerial)
                    errors.Add("Issuance serial ranges cannot overlap.");
        }
        foreach (var item in entity.Items)
            if (!await issuances.IsRangeIssuableAsync(item.AccountableFormBookletId, item.BeginningSerial, item.EndingSerial, entity.Id, cancellationToken)) errors.Add($"Serial range {item.BeginningSerial}-{item.EndingSerial} is unavailable or reserved.");
        if (errors.Count > 0) throw new ApplicationValidationException(errors.Distinct().ToList());
    }

    private static void ValidateDraft(SaveIssuanceDraftRequest request)
    {
        var errors = new List<string>();
        if (request.CollectorId == Guid.Empty) errors.Add("Collector is required.");
        if (request.DateIssued == default) errors.Add("Date issued is required.");
        if (string.IsNullOrWhiteSpace(request.Purpose)) errors.Add("Purpose is required.");
        if (string.IsNullOrWhiteSpace(request.ReleasedBy)) errors.Add("Released by is required.");
        if (string.IsNullOrWhiteSpace(request.ReceivedBy)) errors.Add("Received by is required.");
        if (request.BookletIds.Count == 0) errors.Add("Select at least one available booklet.");
        if (errors.Count > 0) throw new ApplicationValidationException(errors);
    }

    private static void Apply(Issuance entity, SaveIssuanceDraftRequest request)
    {
        entity.IssuedDate = request.DateIssued; entity.CollectorId = request.CollectorId; entity.BarangayId = request.BarangayId;
        entity.CollectionArea = Normalize(request.CollectionArea); entity.Purpose = request.Purpose.Trim(); entity.ReleasedBy = request.ReleasedBy.Trim();
        entity.ReceivedBy = request.ReceivedBy.Trim(); entity.Remarks = Normalize(request.Remarks);
    }

    private async Task<Issuance> GetRequiredAsync(Guid id, CancellationToken cancellationToken) =>
        await issuances.GetAsync(id, cancellationToken) ?? throw new KeyNotFoundException("The issuance transaction no longer exists.");

    private Task AuditStatusAsync(Issuance entity, string action, TransactionStatus oldStatus, CancellationToken cancellationToken) =>
        audit.RecordAsync(action switch { "submitted" => UserActivityType.Submitted, "approved" => UserActivityType.Approved,
                "rejected" => UserActivityType.Rejected, "finalized" => UserActivityType.Finalized, "reversed" => UserActivityType.Reversed, _ => UserActivityType.RecordUpdated },
            $"Issuance {entity.ReferenceNumber} {action}.", nameof(Issuance), entity.Id, new { Status = oldStatus },
            new { entity.Status, entity.ApprovalStatus }, cancellationToken, "Issuance", entity.ReferenceNumber,
            action == "rejected" ? entity.RejectionReason : action == "reversed" ? entity.ReversalReason : null);

    private static object Snapshot(Issuance entity) => new { entity.ReferenceNumber, entity.IssuedDate, entity.CollectorId, entity.BarangayId, entity.CollectionArea, entity.Purpose, entity.ReleasedBy, entity.ReceivedBy, entity.Remarks, Items = entity.Items.Select(item => item.AccountableFormBookletId).ToList() };
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static AvailableBookletDto MapAvailable(AccountabilityRange range)
    {
        var item = range.AccountableFormBooklet;
        return new(range.Id, item.Id, item.AccountableFormTypeId, $"{item.AccountableFormType.Code} - {item.AccountableFormType.Name}",
            item.SeriesYear, item.BookletSequence, range.BeginningSerial, range.EndingSerial,
            checked(range.EndingSerial - range.BeginningSerial + 1), range.StorageLocation ?? item.StorageLocation, item.DateReceived);
    }

    private static void UpdateBookletSummary(AccountableFormBooklet booklet, DateOnly? issuedDate)
    {
        var ranges = booklet.AccountabilityRanges.Where(range => range.IsActive).ToList();
        booklet.UsedQuantity = ranges.Where(range => range.Status == AccountableFormStatus.FullyUsed)
            .Sum(range => checked(range.EndingSerial - range.BeginningSerial + 1));
        booklet.RemainingQuantity = booklet.TotalQuantity - booklet.UsedQuantity;
        var owners = ranges.Where(range => range.CurrentCollectorId is not null).Select(range => range.CurrentCollectorId).Distinct().ToList();
        booklet.CurrentCollectorId = owners.Count == 1 && ranges.All(range => range.CurrentCollectorId == owners[0] || range.Status == AccountableFormStatus.FullyUsed)
            ? owners[0] : null;
        booklet.CurrentBarangayId = booklet.CurrentCollectorId is null ? null : ranges.First(range => range.CurrentCollectorId == booklet.CurrentCollectorId).CurrentBarangayId;
        if (owners.Count > 0 && issuedDate is not null) booklet.DateIssued = issuedDate;
        else if (owners.Count == 0) booklet.DateIssued = null;
        if (booklet.UsedQuantity == booklet.TotalQuantity) booklet.Status = AccountableFormStatus.FullyUsed;
        else if (booklet.UsedQuantity > 0) booklet.Status = AccountableFormStatus.PartiallyUsed;
        else if (ranges.All(range => range.Status == AccountableFormStatus.Available)) booklet.Status = AccountableFormStatus.Available;
        else if (ranges.Any(range => range.Status is AccountableFormStatus.Lost or AccountableFormStatus.Missing or AccountableFormStatus.Cancelled or AccountableFormStatus.Spoiled))
            booklet.Status = ranges.First(range => range.Status is AccountableFormStatus.Lost or AccountableFormStatus.Missing or AccountableFormStatus.Cancelled or AccountableFormStatus.Spoiled).Status;
        else if (ranges.Any(range => range.CurrentCollectorId is not null))
            booklet.Status = ranges.Any(range => range.Status == AccountableFormStatus.Transferred) ? AccountableFormStatus.Transferred : AccountableFormStatus.Issued;
        else if (ranges.Select(range => range.Status).Distinct().Count() == 1) booklet.Status = ranges[0].Status;
        else booklet.Status = AccountableFormStatus.Returned;
    }

    internal static IssuanceDto Map(Issuance entity) => new(entity.Id, entity.ReferenceNumber, entity.IssuedDate, entity.CollectorId,
        entity.Collector.FullName, entity.BarangayId, entity.Barangay?.Name ?? entity.CollectionArea ?? string.Empty,
        entity.Purpose, entity.ReleasedBy, entity.ApprovedByUser?.DisplayName ?? string.Empty, entity.ReceivedBy, entity.Remarks,
        entity.Status, entity.ApprovalStatus, entity.RejectionReason,
        entity.Items.OrderBy(item => item.AccountableFormBooklet.BeginningSerial).Select(item => new IssuanceItemDto(item.Id,
            item.AccountableFormBookletId, $"{item.AccountableFormBooklet.AccountableFormType.Code} - {item.AccountableFormBooklet.AccountableFormType.Name}",
            item.AccountableFormBooklet.BookletSequence, item.BeginningSerial, item.EndingSerial, item.Quantity, item.SeriesYear,
            item.AccountableFormBooklet.StorageLocation)).ToList());
}

public sealed class CollectorAccountabilityService(
    ICollectorAccountabilityRepository ledger,
    ICollectorRepository collectors,
    IAuthorizationService authorization) : ICollectorAccountabilityService
{
    public async Task<IReadOnlyList<CollectorLedgerEntryDto>> GetLedgerAsync(CollectorLedgerFilter filter, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewCollectorAccountability);
        var movements = await ledger.ListCollectorMovementsAsync(filter.CollectorId, filter.FormTypeId, filter.BarangayId, filter.OutstandingOnly, cancellationToken);
        var from = filter.FromDate;
        var to = filter.ToDate;
        if (filter.Year is not null && filter.Month is not null)
        {
            from = new DateOnly(filter.Year.Value, filter.Month.Value, 1); to = from.Value.AddMonths(1).AddDays(-1);
        }
        else if (filter.Year is not null)
        {
            from = new DateOnly(filter.Year.Value, 1, 1); to = new DateOnly(filter.Year.Value, 12, 31);
        }

        long running = 0;
        var result = new List<CollectorLedgerEntryDto>();
        foreach (var movement in movements.Where(item => from is not null && item.EffectiveDate < from))
            running = ApplyBalance(running, movement, filter.CollectorId);
        if (from is not null && running != 0)
            result.Add(new CollectorLedgerEntryDto(new DateTimeOffset(from.Value.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), "BEGINNING", AccountabilityTransactionType.BeginningBalance,
                "All forms", 0, 0, running, 0, running, string.Empty, "Opening", null));

        foreach (var movement in movements.Where(item => (from is null || item.EffectiveDate >= from) && (to is null || item.EffectiveDate <= to)))
        {
            var quantityIn = movement.ToCollectorId == filter.CollectorId ? movement.Quantity : 0;
            var quantityOut = movement.FromCollectorId == filter.CollectorId ? movement.Quantity : 0;
            running = checked(running + quantityIn - quantityOut);
            if (running < 0) throw new InvalidOperationException("Collector accountability cannot become negative.");
            result.Add(new CollectorLedgerEntryDto(movement.OccurredAt, movement.ReferenceNumber, MapType(movement, filter.CollectorId),
                $"{movement.AccountableFormType.Code} - {movement.AccountableFormType.Name}", movement.BeginningSerial, movement.EndingSerial,
                quantityIn, quantityOut, running, movement.Barangay?.Name ?? string.Empty,
                movement.AccountableFormBooklet.Status.ToString(), movement.Remarks));
        }
        return result;
    }

    public async Task<CollectorAccountabilityDetailDto?> GetCollectorDetailAsync(Guid collectorId, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewCollectorAccountability);
        var collector = await collectors.GetAsync(collectorId, cancellationToken); if (collector is null) return null;
        var booklets = await ledger.ListCollectorBookletsAsync(collectorId, cancellationToken);
        var outstanding = booklets.Where(item => item.AccountabilityRanges.Any(range => range.IsActive && range.CurrentCollectorId == collectorId)).Select(InventoryService.MapBooklet).ToList();
        var fullyUsed = booklets.Where(item => item.Status == AccountableFormStatus.FullyUsed).Select(InventoryService.MapBooklet).ToList();
        var returned = booklets.Where(item => item.Status == AccountableFormStatus.Returned).Select(InventoryService.MapBooklet).ToList();
        var history = await GetLedgerAsync(new CollectorLedgerFilter(collectorId), cancellationToken);
        var total = history.LastOrDefault()?.RunningBalance ?? 0;
        return new CollectorAccountabilityDetailDto(collector.Id, collector.Code, collector.FullName, collector.EmployeeNumber, collector.Position,
            collector.OfficeOrDepartment, collector.IsActive, total, outstanding, fullyUsed, returned, history,
            history.GroupBy(item => item.FormType).Select(group => new AccountabilitySummaryDto(group.Key, group.Sum(item => item.QuantityIn - item.QuantityOut))).Where(x => x.Quantity > 0).ToList(),
            history.GroupBy(item => string.IsNullOrWhiteSpace(item.Barangay) ? "Unassigned" : item.Barangay).Select(group => new AccountabilitySummaryDto(group.Key, group.Sum(item => item.QuantityIn - item.QuantityOut))).Where(x => x.Quantity > 0).ToList());
    }

    private static long ApplyBalance(long balance, StockMovement movement, Guid collectorId) => checked(balance + (movement.ToCollectorId == collectorId ? movement.Quantity : 0) - (movement.FromCollectorId == collectorId ? movement.Quantity : 0));
    private static AccountabilityTransactionType MapType(StockMovement movement, Guid collectorId) => movement.MovementType switch
    {
        StockMovementType.Issued => AccountabilityTransactionType.Issuance,
        StockMovementType.Transferred when movement.ToCollectorId == collectorId => AccountabilityTransactionType.TransferIn,
        StockMovementType.Transferred => AccountabilityTransactionType.TransferOut,
        StockMovementType.Used => AccountabilityTransactionType.Usage,
        StockMovementType.Returned => AccountabilityTransactionType.Return,
        StockMovementType.Cancelled => AccountabilityTransactionType.CancelledOrSpoiled,
        StockMovementType.Reversed => AccountabilityTransactionType.Reversal,
        _ => AccountabilityTransactionType.Adjustment
    };
}

public sealed class IssuanceReceiptService(
    IIssuanceRepository issuances,
    IReportSettingsReader settingsService,
    IIssuancePdfWriter writer,
    IApplicationPathService paths,
    IAuditService audit,
    IUnitOfWork unitOfWork,
    IAuthorizationService authorization) : IIssuanceReceiptService
{
    public async Task<string> GeneratePdfAsync(Guid issuanceId, string? outputFolder = null, bool forPrintPreview = false, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewIssuances);
        var issuance = await issuances.GetAsync(issuanceId, cancellationToken) ?? throw new KeyNotFoundException("Issuance was not found.");
        if (issuance.Status is not (TransactionStatus.Approved or TransactionStatus.Finalized or TransactionStatus.Reversed))
            throw new InvalidOperationException("Only approved or finalized issuances can be printed.");
        var settings = await settingsService.GetAsync(cancellationToken);
        var folder = !string.IsNullOrWhiteSpace(outputFolder) ? outputFolder :
            !string.IsNullOrWhiteSpace(settings.ReportOutputFolder) ? settings.ReportOutputFolder : Path.Combine(paths.DataDirectory, "Reports");
        Directory.CreateDirectory(folder);
        var outputPath = Path.Combine(folder, $"Issuance-{issuance.ReferenceNumber}.pdf");
        var document = new IssuanceReceiptDocument(settings.ProvinceName, settings.MunicipalityName, settings.OfficeName, settings.OfficeAddress,
            issuance.ReferenceNumber, issuance.IssuedDate, issuance.Collector.FullName, issuance.Barangay?.Name ?? issuance.CollectionArea ?? string.Empty,
            issuance.Purpose, issuance.Remarks ?? string.Empty, settings.PreparedBy, issuance.ReleasedBy,
            issuance.ApprovedByUser?.DisplayName ?? settings.CheckedBy, issuance.ReceivedBy,
            issuance.Items.OrderBy(item => item.AccountableFormBooklet.BeginningSerial).Select(item => new IssuanceReceiptLine(
                $"{item.AccountableFormBooklet.AccountableFormType.Code} - {item.AccountableFormBooklet.AccountableFormType.Name}", item.SeriesYear,
                item.AccountableFormBooklet.BookletSequence, item.BeginningSerial, item.EndingSerial, item.Quantity)).ToList());
        await writer.WriteAsync(document, outputPath, cancellationToken);
        await audit.RecordAsync(forPrintPreview ? UserActivityType.Printed : UserActivityType.Exported,
            forPrintPreview ? $"Issuance receipt {issuance.ReferenceNumber} opened for print preview." : $"Issuance receipt {issuance.ReferenceNumber} exported to PDF.",
            nameof(Issuance), issuance.Id, null, new { OutputPath = outputPath }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return outputPath;
    }
}

public sealed class ReportSettingsReader(ISystemSettingRepository repository, IAuthorizationService authorization) : IReportSettingsReader
{
    public async Task<SystemSettingsDto> GetAsync(CancellationToken cancellationToken = default)
    {
        if (!authorization.HasPermission(SystemPermission.ViewIssuances) && !authorization.HasPermission(SystemPermission.ViewReports))
            throw new UnauthorizedAccessException("The current user cannot read report settings.");
        var stored = (await repository.ListAsync(cancellationToken)).ToDictionary(item => item.Key, item => item.Value, StringComparer.OrdinalIgnoreCase);
        string Get(string key, string fallback = "") => stored.TryGetValue(key, out var value) ? value : fallback;
        return new SystemSettingsDto
        {
            MunicipalityName = Get("MunicipalityName"), ProvinceName = Get("ProvinceName"), OfficeName = Get("OfficeName", "Municipal Treasury Office"),
            OfficeAddress = Get("OfficeAddress"), MunicipalTreasurerName = Get("MunicipalTreasurerName"), PreparedBy = Get("PreparedBy"),
            CheckedBy = Get("CheckedBy"), CertifiedCorrectBy = Get("CertifiedCorrectBy"), DefaultFiscalYear = Get("DefaultFiscalYear"),
            ReferenceNumberPrefixes = Get("ReferenceNumberPrefixes"), DatabaseBackupFolder = Get("DatabaseBackupFolder"), ReportOutputFolder = Get("ReportOutputFolder")
        };
    }
}
