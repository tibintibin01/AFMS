using System.Text.Json;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Application.Validation;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class FiscalYearService(
    IFiscalYearRepository years, IAuthorizationService authorization, ICurrentUserSession session,
    IAuditService audit, IUnitOfWork unitOfWork, ITransactionRunner transactions) : IFiscalYearService
{
    public async Task<IReadOnlyList<FiscalYearDto>> ListAsync(CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFiscalYears);
        return (await years.ListAsync(cancellationToken)).Select(Map).ToList();
    }

    public async Task<FiscalYearDto> CreateAsync(int year, DateOnly startDate, DateOnly endDate, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFiscalYears);
        if (year is < 2000 or > 9999 || endDate < startDate || startDate.Year != year || endDate.Year != year)
            throw new ApplicationValidationException(["Enter a valid fiscal year with dates inside that calendar year."]);
        if (await years.YearExistsAsync(year, cancellationToken)) throw new ApplicationValidationException(["The fiscal year already exists."]);
        var entity = new FiscalYear { Year = year, StartDate = startDate, EndDate = endDate, WorkflowStatus = FiscalYearWorkflowStatus.Open };
        await years.AddAsync(entity, cancellationToken);
        await audit.RecordAsync(UserActivityType.RecordCreated, $"Fiscal year {year} created.", nameof(FiscalYear), entity.Id, null,
            new { entity.Year, entity.StartDate, entity.EndDate }, cancellationToken, "Fiscal Year", year.ToString());
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task<FiscalYearCloseCheck> CheckCloseAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFiscalYears);
        return await years.CheckCloseAsync(await Required(id, cancellationToken), cancellationToken);
    }

    public async Task RequestCloseAsync(Guid id, string reason, bool allowDocumentedExceptions, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFiscalYears);
        if (string.IsNullOrWhiteSpace(reason)) throw new ApplicationValidationException(["A fiscal-year closure reason is required."]);
        var entity = await Required(id, cancellationToken);
        if (entity.IsClosed || entity.WorkflowStatus != FiscalYearWorkflowStatus.Open) throw new InvalidOperationException("Only an open fiscal year can be submitted for closure.");
        var check = await years.CheckCloseAsync(entity, cancellationToken);
        if (!check.CanCloseWithoutExceptions && !allowDocumentedExceptions) throw new ApplicationValidationException(check.Issues);
        if (!check.CanCloseWithoutExceptions && reason.Trim().Length < 20) throw new ApplicationValidationException(["Documented exceptions require a detailed reason of at least 20 characters."]);
        var user = Current();
        entity.WorkflowStatus = FiscalYearWorkflowStatus.ClosePending; entity.CloseRequestedByUserId = user.Id;
        entity.CloseRequestedAt = DateTimeOffset.UtcNow; entity.CloseRequestReason = reason.Trim();
        entity.CloseValidationIssues = JsonSerializer.Serialize(check); entity.CloseWithDocumentedExceptions = !check.CanCloseWithoutExceptions;
        await audit.RecordAsync(UserActivityType.FiscalYearCloseRequested, $"Fiscal year {entity.Year} submitted for closure.", nameof(FiscalYear), entity.Id,
            null, new { entity.WorkflowStatus, check.Issues, entity.CloseWithDocumentedExceptions }, cancellationToken, "Fiscal Year", entity.Year.ToString(), reason.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public Task ApproveCloseAsync(Guid id, string comments, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.CloseFiscalYear);
        return transactions.ExecuteAsync(async token =>
        {
            var entity = await Required(id, token); var user = Current(); RequireSupervisor(user);
            if (entity.WorkflowStatus != FiscalYearWorkflowStatus.ClosePending || entity.IsClosed) throw new InvalidOperationException("The fiscal year is not awaiting closure approval.");
            if (entity.CloseRequestedByUserId == user.Id) throw new UnauthorizedAccessException("Separation of duties prevents approving your own fiscal-year closure request.");
            var check = await years.CheckCloseAsync(entity, token);
            if (!check.CanCloseWithoutExceptions && !entity.CloseWithDocumentedExceptions) throw new ApplicationValidationException(check.Issues);
            entity.Snapshots.Clear(); var snapshots = await years.BuildSnapshotAsync(entity, token); await years.AddSnapshotsAsync(snapshots, token);
            entity.CarryForwards.Clear(); var carryForwards = await years.BuildCarryForwardAsync(entity, token); await years.AddCarryForwardsAsync(carryForwards, token);
            entity.IsClosed = true; entity.WorkflowStatus = FiscalYearWorkflowStatus.Closed; entity.ClosedByUserId = user.Id;
            entity.ClosedAt = DateTimeOffset.UtcNow; entity.ClosureReason = string.IsNullOrWhiteSpace(comments) ? entity.CloseRequestReason : comments.Trim();
            entity.SnapshotReference = $"FYS-{entity.Year}-{entity.ClosedAt:yyyyMMddHHmmss}";
            await audit.RecordAsync(UserActivityType.FiscalYearClosed, $"Fiscal year {entity.Year} closed.", nameof(FiscalYear), entity.Id,
                new { Status = FiscalYearWorkflowStatus.ClosePending }, new { entity.WorkflowStatus, entity.SnapshotReference, SnapshotRows = snapshots.Count, CarryForwardRows = carryForwards.Count },
                token, "Fiscal Year", entity.Year.ToString(), entity.ClosureReason);
            await unitOfWork.SaveChangesAsync(token);
        }, cancellationToken);
    }

    public async Task RequestReopenAsync(Guid id, string reason, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.RequestFiscalYearReopen);
        var user = Current();
        if (!user.Roles.Contains("Administrator")) throw new UnauthorizedAccessException("An administrator must request fiscal-year reopening.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length < 20) throw new ApplicationValidationException(["A detailed reopening reason of at least 20 characters is required."]);
        var entity = await Required(id, cancellationToken);
        if (!entity.IsClosed || entity.WorkflowStatus != FiscalYearWorkflowStatus.Closed) throw new InvalidOperationException("Only a closed fiscal year can be submitted for reopening.");
        entity.WorkflowStatus = FiscalYearWorkflowStatus.ReopenPending; entity.ReopenRequestedByUserId = user.Id;
        entity.ReopenRequestedAt = DateTimeOffset.UtcNow; entity.ReopenReason = reason.Trim();
        await audit.RecordAsync(UserActivityType.FiscalYearReopenRequested, $"Fiscal year {entity.Year} reopening requested.", nameof(FiscalYear), entity.Id,
            null, new { entity.WorkflowStatus }, cancellationToken, "Fiscal Year", entity.Year.ToString(), reason.Trim());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ApproveReopenAsync(Guid id, string comments, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ApproveFiscalYearReopen);
        var user = Current(); RequireSupervisor(user);
        var entity = await Required(id, cancellationToken);
        if (entity.WorkflowStatus != FiscalYearWorkflowStatus.ReopenPending || !entity.IsClosed) throw new InvalidOperationException("The fiscal year is not awaiting reopening approval.");
        if (entity.ReopenRequestedByUserId == user.Id) throw new UnauthorizedAccessException("Separation of duties prevents approving your own reopening request.");
        entity.IsClosed = false; entity.WorkflowStatus = FiscalYearWorkflowStatus.Open; entity.ReopenedByUserId = user.Id;
        entity.ReopenedAt = DateTimeOffset.UtcNow; if (!string.IsNullOrWhiteSpace(comments)) entity.ReopenReason = $"{entity.ReopenReason} | Supervisor: {comments.Trim()}";
        await audit.RecordAsync(UserActivityType.FiscalYearReopened, $"Fiscal year {entity.Year} reopened.", nameof(FiscalYear), entity.Id,
            new { Status = FiscalYearWorkflowStatus.ReopenPending }, new { entity.WorkflowStatus }, cancellationToken, "Fiscal Year", entity.Year.ToString(), entity.ReopenReason);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<FiscalYearSnapshotDto>> GetSnapshotAsync(Guid id, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ManageFiscalYears);
        var entity = await Required(id, cancellationToken);
        return entity.Snapshots.Select(x => new FiscalYearSnapshotDto($"{x.AccountableFormType.Code} - {x.AccountableFormType.Name}", x.BeginningQuantity,
            x.Additions, x.Usage, x.Exceptions, x.EndingQuantity, x.OutstandingQuantity)).ToList();
    }

    private async Task<FiscalYear> Required(Guid id, CancellationToken token) => await years.GetAsync(id, token) ?? throw new KeyNotFoundException("Fiscal year was not found.");
    private CurrentUser Current() => session.User ?? throw new UnauthorizedAccessException("Sign in to perform this action.");
    private static void RequireSupervisor(CurrentUser user) { if (!user.Roles.Contains("Supervisor")) throw new UnauthorizedAccessException("A supervisor account must authorize this action."); }
    private static FiscalYearDto Map(FiscalYear x) => new(x.Id, x.Year, x.StartDate, x.EndDate, x.WorkflowStatus, x.IsClosed,
        x.CloseRequestedAt, x.ClosedAt, x.ReopenRequestedAt, x.ReopenedAt, x.ClosureReason, x.ReopenReason, x.SnapshotReference, x.CloseValidationIssues);
}

public sealed class FiscalPeriodGuard(IFiscalYearRepository years) : IFiscalPeriodGuard
{
    public async Task EnsureOpenAsync(DateOnly transactionDate, CancellationToken cancellationToken = default)
    {
        var year = await years.FindForDateAsync(transactionDate, cancellationToken);
        if (year?.IsClosed == true) throw new ApplicationValidationException([$"Fiscal year {year.Year} is closed. Reopen it before recording or changing dated transactions."]);
    }
}
