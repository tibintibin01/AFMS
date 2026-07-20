using System.IO;
using System.Text.Json;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.App.Services;

public sealed class JsonInitialDataProvider : IInitialDataProvider
{
    private static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public async Task<InitialDataSet?> GetAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "initial-data.json");
        if (!File.Exists(path)) return null;
        await using var stream = File.OpenRead(path);
        return await JsonSerializer.DeserializeAsync<InitialDataSet>(stream, Options, cancellationToken)
            ?? throw new InvalidDataException("initial-data.json did not contain a valid AFMS initial-data object.");
    }
}
