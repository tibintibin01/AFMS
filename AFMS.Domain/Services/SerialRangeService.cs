namespace AFMS.Domain.Services;

public sealed class SerialRangeService
{
    public SerialRange Validate(long beginning, long ending) => new(beginning, ending);

    public long Count(SerialRange range)
    {
        ArgumentNullException.ThrowIfNull(range);
        return checked(range.Ending - range.Beginning + 1);
    }

    public bool Contains(SerialRange range, long serial)
    {
        ArgumentNullException.ThrowIfNull(range);
        return serial >= 0 && serial >= range.Beginning && serial <= range.Ending;
    }

    public bool AreDuplicates(SerialRange left, SerialRange right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left == right;
    }

    public bool Overlaps(SerialRange left, SerialRange right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);
        return left.Beginning <= right.Ending && right.Beginning <= left.Ending;
    }

    public (SerialRange First, SerialRange Second) Split(SerialRange range, long splitAfter)
    {
        ArgumentNullException.ThrowIfNull(range);
        if (splitAfter < range.Beginning || splitAfter >= range.Ending)
        {
            throw new ArgumentOutOfRangeException(nameof(splitAfter), "Split point must be within the range and before its ending serial.");
        }

        return (new SerialRange(range.Beginning, splitAfter), new SerialRange(checked(splitAfter + 1), range.Ending));
    }

    public SerialRange MergeAdjacent(SerialRange left, SerialRange right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var first = left.Beginning <= right.Beginning ? left : right;
        var second = ReferenceEquals(first, left) ? right : left;
        if (first.Ending == long.MaxValue || first.Ending + 1 != second.Beginning)
        {
            throw new InvalidOperationException("Only adjacent, non-overlapping ranges can be merged.");
        }

        return new SerialRange(first.Beginning, second.Ending);
    }

    public IReadOnlyList<SerialRange> CalculateRemainingRanges(
        SerialRange original,
        IEnumerable<SerialRange> usedRanges)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(usedRanges);

        var used = usedRanges.OrderBy(range => range.Beginning).ToList();
        foreach (var range in used)
        {
            if (range.Beginning < original.Beginning || range.Ending > original.Ending)
            {
                throw new ArgumentOutOfRangeException(nameof(usedRanges), "Every used range must belong to the original range.");
            }
        }

        var merged = new List<SerialRange>();
        foreach (var range in used)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var previous = merged[^1];
            var touchesPrevious = previous.Ending == long.MaxValue || range.Beginning <= previous.Ending + 1;
            if (touchesPrevious)
            {
                merged[^1] = new SerialRange(previous.Beginning, Math.Max(previous.Ending, range.Ending));
            }
            else
            {
                merged.Add(range);
            }
        }

        var remaining = new List<SerialRange>();
        var cursor = original.Beginning;
        foreach (var usedRange in merged)
        {
            if (cursor < usedRange.Beginning)
            {
                remaining.Add(new SerialRange(cursor, usedRange.Beginning - 1));
            }

            if (usedRange.Ending == long.MaxValue)
            {
                return remaining;
            }

            cursor = usedRange.Ending + 1;
        }

        if (cursor <= original.Ending)
        {
            remaining.Add(new SerialRange(cursor, original.Ending));
        }

        return remaining;
    }
}
