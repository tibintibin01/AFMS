using System.IO;
using System.Text.Json;
using System.Windows;
using AFMS.Core;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Serilog;

namespace AFMS.App.Services;

/// <summary>
/// Turns any exception into one sentence the user can act on, and writes the
/// technical detail to the log instead of showing it on screen.
/// </summary>
public static class UserFacingError
{
    public static string From(Exception exception)
    {
        Log.Error(exception, "Operation failed");

        return exception switch
        {
            // These already carry a message written for the user.
            ValidationException validation => string.Join(Environment.NewLine, validation.Errors),
            UnauthorizedAccessException => exception.Message,
            KeyNotFoundException => exception.Message,
            InvalidOperationException => exception.Message,
            InvalidDataException => exception.Message,

            IOException => "AFMS could not read or write that file or folder. " +
                           "Check the location and that it is not open in another program.",
            DbUpdateConcurrencyException => "That record changed while you were editing. " +
                                            "Refresh the screen and try again.",
            DbUpdateException or SqliteException =>
                "The database refused the change, so nothing was saved. " +
                "Check for duplicate codes or reference numbers.",
            OperationCanceledException => "The operation was cancelled.",
            _ => "That did not go through. The details were written to the AFMS log."
        };
    }
}

public interface IUserDialogService
{
    bool Confirm(string message, string title);
    string? AskForText(string prompt, string title, string initialValue = "");
    string? PickFolder(string title);
    string? PickBackupFile();
    string? PickSaveLocation(string suggestedFileName, string filter);
    void Inform(string message, string title);
    void OpenFile(string path);
}

public sealed class UserDialogService : IUserDialogService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No)
            == MessageBoxResult.Yes;

    public void Inform(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public string? AskForText(string prompt, string title, string initialValue = "")
    {
        var window = new Views.TextPromptWindow(prompt, title, initialValue);
        return window.ShowDialog() == true ? window.EnteredText : null;
    }

    public string? PickFolder(string title)
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = title, Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? PickBackupFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Choose an AFMS backup file",
            Filter = "AFMS backups|*.afmsbak|All files|*.*",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? PickSaveLocation(string suggestedFileName, string filter)
    {
        var dialog = new Microsoft.Win32.SaveFileDialog
        {
            FileName = suggestedFileName,
            Filter = filter,
            OverwritePrompt = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public void OpenFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(
                new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
        }
        catch (Exception exception)
        {
            Log.Warning(exception, "Could not open {Path}", path);
            MessageBox.Show(
                $"The file was saved but could not be opened automatically.\n\n{path}",
                "File saved", MessageBoxButton.OK, MessageBoxImage.Information);
        }
    }
}

/// <summary>
/// Loads optional starter records from starter-data.json next to the program, so a
/// brand new database can begin with the office's usual items and people already in.
/// </summary>
public sealed class JsonStarterDataProvider : IStarterDataProvider
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public async Task<StarterData?> GetAsync(CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "starter-data.json");
        if (!File.Exists(path)) return null;

        try
        {
            await using var stream = File.OpenRead(path);
            var file = await JsonSerializer.DeserializeAsync<StarterFile>(stream, Options, cancellationToken);
            if (file is null || string.IsNullOrWhiteSpace(file.Version)) return null;

            return new StarterData(
                file.Version.Trim(),
                file.Settings ?? new Dictionary<string, string>(),
                (file.Items ?? []).Select(item => new StarterItem(
                    item.Code ?? string.Empty,
                    item.Name ?? string.Empty,
                    item.Unit ?? "pcs",
                    item.Category,
                    item.TracksSerials,
                    item.ReorderLevel)).ToList(),
                (file.People ?? []).Select(person => new StarterRecipient(
                    person.Code ?? string.Empty,
                    person.Name ?? string.Empty,
                    person.Position,
                    person.Office)).ToList());
        }
        catch (Exception exception) when (exception is JsonException or IOException)
        {
            Log.Warning(exception, "starter-data.json could not be read and was skipped");
            return null;
        }
    }

    private sealed class StarterFile
    {
        public string? Version { get; set; }
        public Dictionary<string, string>? Settings { get; set; }
        public List<StarterItemFile>? Items { get; set; }
        public List<StarterPersonFile>? People { get; set; }
    }

    private sealed class StarterItemFile
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Unit { get; set; }
        public string? Category { get; set; }
        public bool TracksSerials { get; set; }
        public long ReorderLevel { get; set; }
    }

    private sealed class StarterPersonFile
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Position { get; set; }
        public string? Office { get; set; }
    }
}
