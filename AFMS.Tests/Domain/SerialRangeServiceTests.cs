using AFMS.Domain.Services;

namespace AFMS.Tests.Domain;

public sealed class SerialRangeServiceTests
{
    private readonly SerialRangeService _service = new();

    [Fact]
    public void Validate_ReturnsRange_WhenBoundsAreValid()
    {
        var range = _service.Validate(100, 199);

        Assert.Equal(100, range.Beginning);
        Assert.Equal(199, range.Ending);
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(1, -10)]
    public void Validate_RejectsNegativeBounds(long beginning, long ending)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => _service.Validate(beginning, ending));
    }

    [Fact]
    public void Validate_RejectsReversedRange()
    {
        Assert.Throws<ArgumentException>(() => _service.Validate(20, 19));
    }

    [Fact]
    public void Count_IsInclusive()
    {
        Assert.Equal(100, _service.Count(new SerialRange(100, 199)));
        Assert.Equal(1, _service.Count(new SerialRange(7, 7)));
    }

    [Theory]
    [InlineData(99, false)]
    [InlineData(100, true)]
    [InlineData(150, true)]
    [InlineData(199, true)]
    [InlineData(200, false)]
    [InlineData(-1, false)]
    public void Contains_ChecksInclusiveMembership(long serial, bool expected)
    {
        Assert.Equal(expected, _service.Contains(new SerialRange(100, 199), serial));
    }

    [Fact]
    public void AreDuplicates_RequiresIdenticalBounds()
    {
        Assert.True(_service.AreDuplicates(new SerialRange(1, 10), new SerialRange(1, 10)));
        Assert.False(_service.AreDuplicates(new SerialRange(1, 10), new SerialRange(2, 10)));
    }

    [Theory]
    [InlineData(1, 10, 10, 20, true)]
    [InlineData(1, 10, 5, 6, true)]
    [InlineData(1, 10, 11, 20, false)]
    [InlineData(20, 30, 1, 19, false)]
    public void Overlaps_DetectsIntersectingRanges(
        long firstBeginning,
        long firstEnding,
        long secondBeginning,
        long secondEnding,
        bool expected)
    {
        Assert.Equal(
            expected,
            _service.Overlaps(
                new SerialRange(firstBeginning, firstEnding),
                new SerialRange(secondBeginning, secondEnding)));
    }

    [Fact]
    public void Split_ReturnsTwoContiguousRanges()
    {
        var result = _service.Split(new SerialRange(100, 199), 149);

        Assert.Equal(new SerialRange(100, 149), result.First);
        Assert.Equal(new SerialRange(150, 199), result.Second);
    }

    [Theory]
    [InlineData(99)]
    [InlineData(199)]
    [InlineData(200)]
    public void Split_RejectsInvalidSplitPoint(long splitAfter)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.Split(new SerialRange(100, 199), splitAfter));
    }

    [Fact]
    public void MergeAdjacent_MergesRangesInEitherOrder()
    {
        var expected = new SerialRange(1, 20);

        Assert.Equal(expected, _service.MergeAdjacent(new SerialRange(1, 10), new SerialRange(11, 20)));
        Assert.Equal(expected, _service.MergeAdjacent(new SerialRange(11, 20), new SerialRange(1, 10)));
    }

    [Fact]
    public void MergeAdjacent_RejectsGapsAndOverlaps()
    {
        Assert.Throws<InvalidOperationException>(
            () => _service.MergeAdjacent(new SerialRange(1, 10), new SerialRange(12, 20)));
        Assert.Throws<InvalidOperationException>(
            () => _service.MergeAdjacent(new SerialRange(1, 10), new SerialRange(10, 20)));
    }

    [Fact]
    public void CalculateRemainingRanges_SubtractsAndCoalescesUsedRanges()
    {
        var remaining = _service.CalculateRemainingRanges(
            new SerialRange(1, 100),
            [new SerialRange(51, 60), new SerialRange(10, 20), new SerialRange(19, 30)]);

        Assert.Equal(
            [new SerialRange(1, 9), new SerialRange(31, 50), new SerialRange(61, 100)],
            remaining);
    }

    [Fact]
    public void CalculateRemainingRanges_ReturnsOriginal_WhenNothingWasUsed()
    {
        var remaining = _service.CalculateRemainingRanges(new SerialRange(1, 10), []);

        Assert.Equal([new SerialRange(1, 10)], remaining);
    }

    [Fact]
    public void CalculateRemainingRanges_ReturnsEmpty_WhenEntireRangeWasUsed()
    {
        var remaining = _service.CalculateRemainingRanges(
            new SerialRange(1, 10),
            [new SerialRange(1, 10)]);

        Assert.Empty(remaining);
    }

    [Fact]
    public void CalculateRemainingRanges_RejectsUsageOutsideOriginalRange()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => _service.CalculateRemainingRanges(
                new SerialRange(10, 20),
                [new SerialRange(9, 12)]));
    }
}
