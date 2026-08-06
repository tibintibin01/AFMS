namespace AFMS.Core.Domain;

/// <summary>
/// Set arithmetic over serial ranges. This is what lets the app answer
/// "is serial 1001 still out with someone?" without tracking every serial
/// as its own row.
/// </summary>
public static class SerialMath
{
    /// <summary>
    /// Sorts ranges and joins any that overlap or sit next to each other, so
    /// 1-10 and 11-20 become a single 1-20.
    /// </summary>
    public static IReadOnlyList<SerialRange> Merge(IEnumerable<SerialRange> ranges)
    {
        ArgumentNullException.ThrowIfNull(ranges);

        var ordered = ranges.OrderBy(range => range.From).ThenBy(range => range.To).ToList();
        var merged = new List<SerialRange>();

        foreach (var range in ordered)
        {
            if (merged.Count == 0)
            {
                merged.Add(range);
                continue;
            }

            var previous = merged[^1];

            // Touching means the next range starts at or before previous.To + 1.
            // Guard against overflow when a range already ends at long.MaxValue.
            var touches = previous.To == long.MaxValue || range.From <= previous.To + 1;
            if (touches)
                merged[^1] = new SerialRange(previous.From, Math.Max(previous.To, range.To));
            else
                merged.Add(range);
        }

        return merged;
    }

    /// <summary>
    /// Removes <paramref name="remove"/> from <paramref name="source"/> and returns
    /// what is left. Used to work out which released serials are still outstanding
    /// after returns are taken into account.
    /// </summary>
    public static IReadOnlyList<SerialRange> Subtract(
        IEnumerable<SerialRange> source,
        IEnumerable<SerialRange> remove)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(remove);

        var remaining = Merge(source).ToList();
        var cuts = Merge(remove);

        foreach (var cut in cuts)
        {
            var next = new List<SerialRange>(remaining.Count);
            foreach (var range in remaining)
            {
                if (!range.Overlaps(cut))
                {
                    next.Add(range);
                    continue;
                }

                // Keep the part of the range that sits before the cut.
                if (range.From < cut.From)
                    next.Add(new SerialRange(range.From, cut.From - 1));

                // Keep the part that sits after the cut.
                if (range.To > cut.To)
                    next.Add(new SerialRange(cut.To + 1, range.To));
            }

            remaining = next;
        }

        return remaining;
    }

    /// <summary>Returns every range in <paramref name="candidates"/> that touches <paramref name="target"/>.</summary>
    public static IReadOnlyList<SerialRange> Intersecting(SerialRange target, IEnumerable<SerialRange> candidates)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidates);
        return candidates.Where(target.Overlaps).ToList();
    }

    /// <summary>
    /// Returns only the shared parts of <paramref name="target"/> and the candidates,
    /// clipped to the target. Used so a warning names the exact serials in conflict
    /// rather than the whole range they happen to sit inside.
    /// </summary>
    public static IReadOnlyList<SerialRange> Intersect(SerialRange target, IEnumerable<SerialRange> candidates)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(candidates);

        var shared = candidates
            .Where(target.Overlaps)
            .Select(candidate => new SerialRange(
                Math.Max(target.From, candidate.From),
                Math.Min(target.To, candidate.To)));

        return Merge(shared);
    }

    /// <summary>Total serial count across ranges, after merging so overlaps are not counted twice.</summary>
    public static long TotalCount(IEnumerable<SerialRange> ranges) =>
        Merge(ranges).Sum(range => range.Count);

    /// <summary>Formats ranges for display, for example "1001-1050, 2001-2010".</summary>
    public static string Describe(IEnumerable<SerialRange> ranges)
    {
        var text = string.Join(", ", Merge(ranges).Select(range => range.ToString()));
        return text.Length == 0 ? "-" : text;
    }
}
