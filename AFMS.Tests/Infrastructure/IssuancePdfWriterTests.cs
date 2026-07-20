using AFMS.Application.Models;
using AFMS.Infrastructure.Files;

namespace AFMS.Tests.Infrastructure;

public sealed class IssuancePdfWriterTests
{
    [Fact]
    public async Task Writer_CreatesRenderReadySampleReceipt()
    {
        var path = Environment.GetEnvironmentVariable("AFMS_SAMPLE_PDF_PATH")
            ?? Path.Combine(Path.GetTempPath(), "AFMS-Issuance-Receipt-Sample.pdf");
        var document = new IssuanceReceiptDocument(
            "Aurora", "Dipaculao", "Municipal Treasury Office", string.Empty, "ISS-2026-000001",
            new DateOnly(2026, 7, 16), "SHELLA MAY I. VELASCO", "North Poblacion",
            "Collection of municipal revenues", "Received in good order and condition.",
            "Kevin Macalinao", "Accountable Forms Custodian", "Maria Elena P. Chavez", "SHELLA MAY I. VELASCO",
            [new("AF51 - LOCAL", 2026, 1, 100001, 100050, 50), new("BIR-0016 - CEDULA", 2026, 2, 200001, 200050, 50)]);

        await new IssuancePdfWriter().WriteAsync(document, path);

        var bytes = await File.ReadAllBytesAsync(path);
        Assert.True(bytes.Length > 1_000);
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
