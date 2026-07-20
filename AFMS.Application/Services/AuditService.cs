using System.Text.Json;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Entities;
using AFMS.Domain.Enums;

namespace AFMS.Application.Services;

public sealed class AuditService(
    IAuditLogRepository auditLogs,
    ICurrentUserSession session,
    IAuthorizationService authorization) : IAuditService, IAuditQueryService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    public Task RecordAsync(
        UserActivityType activityType,
        string description,
        string entityName,
        Guid entityId,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken = default,
        string? module = null,
        string? referenceNumber = null,
        string? reason = null) =>
        auditLogs.AddAsync(new AuditLog
        {
            UserId = session.User?.Id,
            ActivityType = activityType,
            Description = description,
            EntityName = entityName,
            EntityId = entityId,
            Module = module ?? entityName,
            ReferenceNumber = referenceNumber,
            Reason = reason,
            MachineName = Environment.MachineName,
            OldValues = Serialize(oldValues),
            NewValues = Serialize(newValues)
        }, cancellationToken);

    public async Task<IReadOnlyList<AuditLogDto>> ListAsync(
        int limit = 250,
        CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewAuditLogs);
        var entries = await auditLogs.ListAsync(null, limit, cancellationToken);
        return entries.Select(Map).ToList();
    }

    public async Task<AuditLogPage> SearchAsync(AuditLogFilter filter, CancellationToken cancellationToken = default)
    {
        authorization.EnsurePermission(SystemPermission.ViewAuditLogs);
        return await auditLogs.QueryAsync(filter, cancellationToken);
    }

    internal static AuditLogDto Map(AuditLog audit) => new(
        audit.OccurredAt,
        audit.User?.Username ?? "System",
        audit.ActivityType,
        audit.Description,
        audit.EntityName,
        audit.EntityId,
        audit.OldValues,
        audit.NewValues,
        audit.MachineName,
        audit.Module,
        audit.ReferenceNumber,
        audit.Reason,
        audit.IntegrityHash);

    private static string? Serialize(object? value) =>
        value is null ? null : JsonSerializer.Serialize(value, JsonOptions);
}
