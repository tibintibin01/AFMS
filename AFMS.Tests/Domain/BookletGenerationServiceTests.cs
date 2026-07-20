using AFMS.Domain.Services;

namespace AFMS.Tests.Domain;

public sealed class BookletGenerationServiceTests
{
    private readonly BookletGenerationService _service = new();

    [Fact]
    public void Generate_CreatesExpectedConsecutiveBooklets()
    {
        var result = _service.Generate(10, 50, 100001, 100500);
        Assert.Equal(10, result.Count);
        Assert.Equal(new GeneratedBookletRange(1, 100001, 100050, 50), result[0]);
        Assert.Equal(new GeneratedBookletRange(10, 100451, 100500, 50), result[^1]);
    }

    [Fact]
    public void CalculateQuantity_IsInclusive() => Assert.Equal(500, _service.CalculateQuantity(100001, 100500));

    [Theory]
    [InlineData(0, 50, 1, 50)]
    [InlineData(1, 0, 1, 50)]
    [InlineData(1, 50, 50, 1)]
    [InlineData(1, 50, -1, 48)]
    [InlineData(2, 50, 1, 50)]
    public void Generate_RejectsInvalidOrMismatchedInput(int books, int perBook, long beginning, long ending) =>
        Assert.ThrowsAny<ArgumentException>(() => _service.Generate(books, perBook, beginning, ending));
}
