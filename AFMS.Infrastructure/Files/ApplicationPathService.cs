using AFMS.Application.Abstractions;

namespace AFMS.Infrastructure.Files;

public sealed class ApplicationPathService : IApplicationPathService
{
    public ApplicationPathService()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AFMS");
        DatabasePath = Path.Combine(DataDirectory, "afms.db");
        LogDirectory = Path.Combine(DataDirectory, "Logs");
    }

    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string LogDirectory { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
    }
}
