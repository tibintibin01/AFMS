namespace AFMS.Core.Files;

/// <summary>
/// Everything the app writes lives under %LOCALAPPDATA%\AFMS, so an app upgrade
/// or reinstall never touches the records.
/// </summary>
public sealed class ApplicationPaths : IApplicationPaths
{
    public ApplicationPaths()
    {
        DataDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "AFMS");
        DatabasePath = Path.Combine(DataDirectory, "inventory.db");
        LogDirectory = Path.Combine(DataDirectory, "Logs");
        DefaultBackupDirectory = Path.Combine(DataDirectory, "Backups");
        DefaultReportDirectory = Path.Combine(DataDirectory, "Reports");
    }

    public string DataDirectory { get; }
    public string DatabasePath { get; }
    public string LogDirectory { get; }
    public string DefaultBackupDirectory { get; }
    public string DefaultReportDirectory { get; }

    public void EnsureDirectoriesExist()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(LogDirectory);
        Directory.CreateDirectory(DefaultBackupDirectory);
        Directory.CreateDirectory(DefaultReportDirectory);
    }
}

public sealed class BCryptPasswordHasher : IPasswordHasher
{
    private const int WorkFactor = 12;

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.HashPassword(password, WorkFactor);
    }

    public bool Verify(string password, string passwordHash)
    {
        ArgumentNullException.ThrowIfNull(password);
        ArgumentException.ThrowIfNullOrWhiteSpace(passwordHash);
        return BCrypt.Net.BCrypt.Verify(password, passwordHash);
    }
}
