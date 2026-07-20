namespace AFMS.Application.Models;

public sealed class InitialDataSet
{
    public string Version { get; set; } = "1";
    public Dictionary<string, string> SystemSettings { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public List<InitialBarangay> Barangays { get; set; } = [];
    public List<InitialFormType> AccountableFormTypes { get; set; } = [];
    public List<InitialCollector> Collectors { get; set; } = [];
}

public sealed record InitialBarangay(string Code, string Name, string Municipality, string Province, string? Remarks = null);
public sealed record InitialFormType(string Code, string Name, int ReceiptsPerBooklet, string? Category = null, string? Remarks = null, string? PreviousCode = null);
public sealed record InitialCollector(string CollectorNumber, string FullName, string? Remarks = null);
