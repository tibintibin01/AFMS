namespace AFMS.Domain.Services;

public sealed record GeneratedBookletRange(int Sequence, long BeginningSerial, long EndingSerial, long Quantity);

public sealed class BookletGenerationService
{
    public long CalculateQuantity(long beginningSerial, long endingSerial)
    {
        ValidateSerialRange(beginningSerial, endingSerial);
        return checked(endingSerial - beginningSerial + 1);
    }

    public IReadOnlyList<GeneratedBookletRange> Generate(int numberOfBooklets, int receiptsPerBooklet, long beginningSerial, long endingSerial)
    {
        if (numberOfBooklets <= 0) throw new ArgumentOutOfRangeException(nameof(numberOfBooklets), "Number of booklets must be greater than zero.");
        if (receiptsPerBooklet <= 0) throw new ArgumentOutOfRangeException(nameof(receiptsPerBooklet), "Receipts per booklet must be greater than zero.");
        var quantity = CalculateQuantity(beginningSerial, endingSerial);
        var expectedQuantity = checked((long)numberOfBooklets * receiptsPerBooklet);
        if (quantity != expectedQuantity)
            throw new ArgumentException($"Serial range contains {quantity:N0} receipts but {expectedQuantity:N0} are required for {numberOfBooklets:N0} booklet(s).", nameof(endingSerial));

        var result = new List<GeneratedBookletRange>(numberOfBooklets);
        for (var sequence = 1; sequence <= numberOfBooklets; sequence++)
        {
            var start = checked(beginningSerial + ((long)(sequence - 1) * receiptsPerBooklet));
            var end = checked(start + receiptsPerBooklet - 1);
            result.Add(new GeneratedBookletRange(sequence, start, end, receiptsPerBooklet));
        }

        if (result.Count != numberOfBooklets || result[^1].EndingSerial != endingSerial)
            throw new InvalidOperationException("The generated booklet ranges do not match the supplied receiving range.");
        return result;
    }

    private static void ValidateSerialRange(long beginningSerial, long endingSerial)
    {
        if (beginningSerial < 0 || endingSerial < 0)
            throw new ArgumentOutOfRangeException(nameof(beginningSerial), "Serial numbers cannot be negative.");
        if (beginningSerial > endingSerial)
            throw new ArgumentException("Beginning serial number cannot be greater than ending serial number.", nameof(beginningSerial));
    }
}
