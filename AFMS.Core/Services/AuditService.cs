using System.Text.Json;
using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

public sealed class AuditService(InventoryDbContext context, ICurrentUser session) : IAuditService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = false };

    /// <summary>
    /// Queues an audit entry on the change tracker. The caller saves it in the
    /// same transaction as the change being described, so the record and its
    /// history are always written together or not at all.
    /// </summary>
    public Task RecordAsync(
        ActivityType activity,
        string description,
        string? entityName = null,
        Guid? entityId = null,
        string? referenceNumber = null,
        object? details = null,
        CancellationToken cancellationToken = default)
    {
        var user = session.User;
        context.AuditLogs.Add(new AuditLog
        {
            OccurredAt = DateTimeOffset.UtcNow,
            UserId = user?.Id,
            Username = user?.Username,
            Activity = activity,
            Description = description,
            EntityName = entityName,
            EntityId = entityId,
            ReferenceNumber = referenceNumber,
            Details = details is null ? null : Truncate(JsonSerializer.Serialize(details, JsonOptions)),
            MachineName = Environment.MachineName
        });

        return Task.CompletedTask;
    }

    public async Task<AuditPage> SearchAsync(AuditFilter filter, CancellationToken cancellationToken = default)
    {
        var query = context.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(filter.SearchText))
        {
            var term = filter.SearchText.Trim();
            query = query.Where(entry =>
                EF.Functions.Like(entry.Description, $"%{term}%") ||
                EF.Functions.Like(entry.ReferenceNumber ?? string.Empty, $"%{term}%") ||
                EF.Functions.Like(entry.Username ?? string.Empty, $"%{term}%"));
        }

        if (filter.Activity is { } activity)
            query = query.Where(entry => entry.Activity == activity);

        // Dates are compared in UTC because that is how they are stored.
        if (filter.FromDate is { } from)
        {
            var start = new DateTimeOffset(from.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(entry => entry.OccurredAt >= start);
        }

        if (filter.ToDate is { } to)
        {
            var end = new DateTimeOffset(to.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
            query = query.Where(entry => entry.OccurredAt < end);
        }

        var total = await query.CountAsync(cancellationToken);
        var pageSize = Math.Clamp(filter.PageSize, 10, 500);
        var page = Math.Max(1, filter.Page);

        var entries = await query
            .OrderByDescending(entry => entry.OccurredAt)
            .ThenByDescending(entry => entry.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(entry => new AuditEntryDto(
                entry.OccurredAt,
                entry.Username ?? "System",
                entry.Activity,
                entry.Description,
                entry.EntityName,
                entry.ReferenceNumber,
                entry.Details,
                entry.MachineName))
            .ToListAsync(cancellationToken);

        return new AuditPage(entries, total, page, pageSize);
    }

    private static string Truncate(string value) => value.Length <= 4000 ? value : value[..4000];
}
