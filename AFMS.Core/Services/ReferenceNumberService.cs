using AFMS.Core.Domain;
using AFMS.Core.Persistence;
using Microsoft.EntityFrameworkCore;

namespace AFMS.Core.Services;

/// <summary>
/// Hands out reference numbers such as IN-2026-0001 and OUT-2026-0001, one
/// counter per prefix and year. A unique index on the counter row plus a short
/// retry keeps numbers from being reused even if two saves land together.
/// </summary>
public sealed class ReferenceNumberService(InventoryDbContext context) : IReferenceNumberService
{
    private const int MaxAttempts = 5;

    public async Task<string> NextAsync(string prefix, int year, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(prefix);
        var normalized = prefix.Trim().ToUpperInvariant();

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                var sequence = await context.ReferenceSequences.SingleOrDefaultAsync(
                    row => row.Prefix == normalized && row.Year == year, cancellationToken);

                if (sequence is null)
                {
                    sequence = new ReferenceSequence { Prefix = normalized, Year = year, LastNumber = 1 };
                    context.ReferenceSequences.Add(sequence);
                }
                else
                {
                    sequence.LastNumber = checked(sequence.LastNumber + 1);
                }

                return $"{normalized}-{year:D4}-{sequence.LastNumber:D4}";
            }
            catch (Exception exception) when (attempt < MaxAttempts &&
                exception is DbUpdateException or Microsoft.Data.Sqlite.SqliteException)
            {
                context.ChangeTracker.Clear();
                await Task.Delay(TimeSpan.FromMilliseconds(20 * attempt), cancellationToken);
            }
        }

        throw new InvalidOperationException("A reference number could not be generated. Try saving again.");
    }
}
