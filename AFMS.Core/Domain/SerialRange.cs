namespace AFMS.Core.Domain;

/// <summary>
/// An inclusive run of serial numbers, such as receipt booklet 1001 through 1050.
/// The constructor validates the range, so an invalid range cannot be created.
/// </summary>
public sealed record SerialRange
{
    public SerialRange(long from, long to)
    {
        if (from < 0)
            throw new ArgumentOutOfRangeException(nameof(from), "A serial number cannot be negative.");
        if (to < from)
            throw new ArgumentOutOfRangeException(nameof(to), "The ending serial must not be lower than the beginning serial.");

        From = from;
        To = to;
    }

    public long From { get; }

    public long To { get; }

    /// <summary>How many serial numbers this range covers.</summary>
    public long Count => checked(To - From + 1);

    public bool Contains(long serial) => serial >= From && serial <= To;

    public bool Overlaps(SerialRange other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return From <= other.To && other.From <= To;
    }

    public override string ToString() => From == To ? From.ToString() : $"{From}-{To}";
}
