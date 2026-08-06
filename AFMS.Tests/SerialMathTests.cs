using AFMS.Core.Domain;

namespace AFMS.Tests;

/// <summary>
/// The range arithmetic behind "is this serial still out with someone?".
/// Everything else about serial tracking rests on these being right.
/// </summary>
public sealed class SerialMathTests
{
    [Fact]
    public void Range_rejects_an_ending_serial_below_the_beginning()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialRange(50, 49));
    }

    [Fact]
    public void Range_rejects_negative_serials()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new SerialRange(-1, 10));
    }

    [Fact]
    public void Range_counts_inclusively()
    {
        Assert.Equal(50, new SerialRange(1001, 1050).Count);
        Assert.Equal(1, new SerialRange(7, 7).Count);
    }

    [Theory]
    [InlineData(1, 10, 5, 15, true)]    // partial overlap
    [InlineData(1, 10, 10, 20, true)]   // touching at one serial
    [InlineData(1, 10, 11, 20, false)]  // adjacent but distinct
    [InlineData(1, 100, 40, 50, true)]  // fully contained
    [InlineData(20, 30, 1, 10, false)]  // clearly apart
    public void Overlaps_answers_correctly(long aFrom, long aTo, long bFrom, long bTo, bool expected)
    {
        var left = new SerialRange(aFrom, aTo);
        var right = new SerialRange(bFrom, bTo);

        Assert.Equal(expected, left.Overlaps(right));
        Assert.Equal(expected, right.Overlaps(left));
    }

    [Fact]
    public void Merge_joins_adjacent_and_overlapping_ranges()
    {
        var merged = SerialMath.Merge(
        [
            new SerialRange(11, 20),
            new SerialRange(1, 10),
            new SerialRange(18, 25),
            new SerialRange(40, 45)
        ]);

        Assert.Equal(2, merged.Count);
        Assert.Equal(new SerialRange(1, 25), merged[0]);
        Assert.Equal(new SerialRange(40, 45), merged[1]);
    }

    [Fact]
    public void Subtract_removes_a_slice_from_the_middle()
    {
        var remaining = SerialMath.Subtract(
            [new SerialRange(1, 100)],
            [new SerialRange(40, 50)]);

        Assert.Equal(2, remaining.Count);
        Assert.Equal(new SerialRange(1, 39), remaining[0]);
        Assert.Equal(new SerialRange(51, 100), remaining[1]);
    }

    [Fact]
    public void Subtract_trims_the_leading_and_trailing_edges()
    {
        var leading = SerialMath.Subtract([new SerialRange(1, 50)], [new SerialRange(1, 10)]);
        Assert.Equal(new SerialRange(11, 50), Assert.Single(leading));

        var trailing = SerialMath.Subtract([new SerialRange(1, 50)], [new SerialRange(41, 50)]);
        Assert.Equal(new SerialRange(1, 40), Assert.Single(trailing));
    }

    [Fact]
    public void Subtract_returns_nothing_when_everything_is_removed()
    {
        var remaining = SerialMath.Subtract(
            [new SerialRange(1001, 1050)],
            [new SerialRange(1000, 1100)]);

        Assert.Empty(remaining);
    }

    [Fact]
    public void Subtract_leaves_untouched_ranges_alone()
    {
        var remaining = SerialMath.Subtract(
            [new SerialRange(1, 10), new SerialRange(100, 110)],
            [new SerialRange(200, 300)]);

        Assert.Equal(2, remaining.Count);
    }

    [Fact]
    public void Subtract_handles_many_cuts_across_many_ranges()
    {
        var remaining = SerialMath.Subtract(
            [new SerialRange(1, 100), new SerialRange(200, 300)],
            [new SerialRange(10, 20), new SerialRange(50, 60), new SerialRange(250, 260)]);

        Assert.Equal(
            new[]
            {
                new SerialRange(1, 9),
                new SerialRange(21, 49),
                new SerialRange(61, 100),
                new SerialRange(200, 249),
                new SerialRange(261, 300)
            },
            remaining);
    }

    [Fact]
    public void Merge_does_not_overflow_at_the_top_of_the_range()
    {
        var merged = SerialMath.Merge(
        [
            new SerialRange(long.MaxValue - 5, long.MaxValue),
            new SerialRange(long.MaxValue - 10, long.MaxValue - 6)
        ]);

        Assert.Equal(new SerialRange(long.MaxValue - 10, long.MaxValue), Assert.Single(merged));
    }

    [Fact]
    public void Total_count_does_not_double_count_overlaps()
    {
        var total = SerialMath.TotalCount(
            [new SerialRange(1, 10), new SerialRange(5, 15)]);

        Assert.Equal(15, total);
    }

    [Fact]
    public void Describe_formats_ranges_for_the_screen()
    {
        Assert.Equal("1001-1050, 2001-2010", SerialMath.Describe(
            [new SerialRange(2001, 2010), new SerialRange(1001, 1050)]));
        Assert.Equal("7", SerialMath.Describe([new SerialRange(7, 7)]));
        Assert.Equal("-", SerialMath.Describe([]));
    }

    [Fact]
    public void Intersecting_returns_only_the_ranges_that_touch_the_target()
    {
        var hits = SerialMath.Intersecting(
            new SerialRange(45, 55),
            [new SerialRange(1, 40), new SerialRange(41, 50), new SerialRange(51, 60)]);

        Assert.Equal(2, hits.Count);
    }
}
