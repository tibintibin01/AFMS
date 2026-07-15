namespace AFMS.Domain.Services;

public sealed record SerialRange
{
    public SerialRange(long beginning, long ending)
    {
        if (beginning < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(beginning), "Beginning serial cannot be negative.");
        }

        if (ending < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(ending), "Ending serial cannot be negative.");
        }

        if (ending < beginning)
        {
            throw new ArgumentException("Ending serial must be greater than or equal to beginning serial.", nameof(ending));
        }

        Beginning = beginning;
        Ending = ending;
    }

    public long Beginning { get; }
    public long Ending { get; }
}
