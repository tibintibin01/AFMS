using System.IO;
using System.Text.Json;
using AFMS.Core;
using Serilog;

namespace AFMS.App.Services;

public interface ISignInPreferences
{
    /// <summary>The username to prefill, or null when the box was left unticked.</summary>
    string? RememberedUsername { get; }

    void Remember(string username);
    void Forget();
}

/// <summary>
/// Remembers the username between sign-ins as a convenience.
///
/// Only the username is ever written. Passwords are never stored anywhere, in any
/// form, so a stolen copy of this file gets someone no closer to the records.
/// </summary>
public sealed class SignInPreferences : ISignInPreferences
{
    private const string FileName = "signin.json";

    private readonly string _path;
    private Preferences _current;

    public SignInPreferences(IApplicationPaths paths)
    {
        _path = Path.Combine(paths.DataDirectory, FileName);
        _current = Read(_path);
    }

    public string? RememberedUsername =>
        string.IsNullOrWhiteSpace(_current.Username) ? null : _current.Username;

    public void Remember(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            Forget();
            return;
        }

        Save(new Preferences { Username = username.Trim() });
    }

    public void Forget() => Save(new Preferences { Username = null });

    private void Save(Preferences preferences)
    {
        _current = preferences;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            File.WriteAllText(_path, JsonSerializer.Serialize(preferences));
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            // A convenience setting is never worth blocking sign-in over.
            Log.Warning(exception, "Could not save the remembered username");
        }
    }

    private static Preferences Read(string path)
    {
        try
        {
            if (!File.Exists(path)) return new Preferences();
            return JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new Preferences();
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            Log.Warning(exception, "Could not read the remembered username; starting fresh");
            return new Preferences();
        }
    }

    private sealed class Preferences
    {
        public string? Username { get; set; }
    }
}
