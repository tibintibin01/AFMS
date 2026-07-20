using System.Security.Cryptography;
using System.Text;
using AFMS.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Infrastructure.Persistence;

internal static class AuditIntegrity
{
    private const string DropGuardsSql =
        """
        DROP TRIGGER IF EXISTS TR_AuditLogs_RequireIntegrityHash;
        DROP TRIGGER IF EXISTS TR_AuditLogs_AppendOnly_Delete;
        DROP TRIGGER IF EXISTS TR_AuditLogs_AppendOnly_Update;
        """;

    private const string CreateGuardsSql =
        """
        CREATE TRIGGER TR_AuditLogs_AppendOnly_Update
        BEFORE UPDATE ON AuditLogs
        BEGIN
            SELECT RAISE(ABORT, 'Audit records are append-only and cannot be updated.');
        END;

        CREATE TRIGGER TR_AuditLogs_AppendOnly_Delete
        BEFORE DELETE ON AuditLogs
        BEGIN
            SELECT RAISE(ABORT, 'Audit records are append-only and cannot be deleted.');
        END;

        CREATE TRIGGER TR_AuditLogs_RequireIntegrityHash
        BEFORE INSERT ON AuditLogs
        WHEN NEW.IntegrityHash IS NULL OR length(trim(NEW.IntegrityHash)) <> 64
        BEGIN
            SELECT RAISE(ABORT, 'Audit records require a valid integrity hash.');
        END;
        """;

    public static string Hash(AuditLog entry)
    {
        var payload = string.Join('|', entry.Id, entry.UserId, (int)entry.ActivityType,
            entry.OccurredAt.ToUniversalTime().ToString("O"), entry.Module, entry.EntityName,
            entry.EntityId, entry.ReferenceNumber, entry.Description, entry.Reason,
            entry.OldValues, entry.NewValues, entry.MachineName, entry.PreviousHash);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    public static void Verify(IReadOnlyList<AuditLog> entries)
    {
        string? previousHash = null;
        var chainStarted = false;
        foreach (var entry in entries.OrderBy(value => value.OccurredAt).ThenBy(value => value.CreatedAt).ThenBy(value => value.Id))
        {
            if (string.IsNullOrWhiteSpace(entry.IntegrityHash))
            {
                if (chainStarted)
                    throw new InvalidDataException($"Audit integrity verification failed at record {entry.Id}: an unsigned record follows the protected chain.");
                continue; // Legacy records created before integrity chaining are retained as an unsigned prefix.
            }

            if (!string.Equals(entry.PreviousHash, previousHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Audit integrity verification failed at record {entry.Id}: the chain link is invalid.");
            var expected = Hash(entry);
            if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(expected), Convert.FromHexString(entry.IntegrityHash)))
                throw new InvalidDataException($"Audit integrity verification failed at record {entry.Id}: its contents were changed.");
            previousHash = entry.IntegrityHash;
            chainStarted = true;
        }
    }

    /// <summary>
    /// Upgrades databases created before audit chaining was introduced. Existing
    /// signed records are validated before any rewrite, all historical records
    /// are retained, and the complete chain is rebuilt inside one SQLite
    /// transaction while the append-only guards are temporarily replaced.
    /// </summary>
    public static async Task<int> UpgradeLegacyChainAsync(
        AfmsDbContext context,
        CancellationToken cancellationToken = default)
    {
        var entries = (await context.AuditLogs.AsNoTracking().ToListAsync(cancellationToken))
            .OrderBy(value => value.OccurredAt)
            .ThenBy(value => value.CreatedAt)
            .ThenBy(value => value.Id)
            .ToList();
        if (entries.Count == 0 || entries.All(entry => !string.IsNullOrWhiteSpace(entry.IntegrityHash)))
            return 0;

        // A mixed chain is expected only during the one-time upgrade from the
        // legacy schema. Never use that condition to conceal modified signed data.
        foreach (var entry in entries.Where(entry => !string.IsNullOrWhiteSpace(entry.IntegrityHash)))
            VerifySignedContents(entry);

        await using var transaction = await context.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await context.Database.ExecuteSqlRawAsync(DropGuardsSql, cancellationToken);
            string? previousHash = null;
            foreach (var entry in entries)
            {
                entry.PreviousHash = previousHash;
                entry.IntegrityHash = Hash(entry);
                await context.Database.ExecuteSqlInterpolatedAsync(
                    $"UPDATE AuditLogs SET PreviousHash = {entry.PreviousHash}, IntegrityHash = {entry.IntegrityHash} WHERE Id = {entry.Id}",
                    cancellationToken);
                previousHash = entry.IntegrityHash;
            }

            await context.Database.ExecuteSqlRawAsync(CreateGuardsSql, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return entries.Count;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    private static void VerifySignedContents(AuditLog entry)
    {
        byte[] actual;
        try
        {
            actual = Convert.FromHexString(entry.IntegrityHash!);
        }
        catch (FormatException exception)
        {
            throw new InvalidDataException(
                $"Audit integrity verification failed at record {entry.Id}: its integrity hash is invalid.", exception);
        }

        var expected = Convert.FromHexString(Hash(entry));
        if (actual.Length != expected.Length || !CryptographicOperations.FixedTimeEquals(expected, actual))
            throw new InvalidDataException($"Audit integrity verification failed at record {entry.Id}: its contents were changed.");
    }
}
