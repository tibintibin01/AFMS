using System.Security.Cryptography;
using System.Text;
using AFMS.Domain.Entities;

namespace AFMS.Infrastructure.Persistence;

internal static class AuditIntegrity
{
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
}
