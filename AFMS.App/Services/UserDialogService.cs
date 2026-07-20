using System.Windows;

namespace AFMS.App.Services;

public interface IUserDialogService
{
    bool Confirm(string message, string title);
    string? SelectAttachment();
    string? SelectFolder();
    string? SelectBackupFile();
}

public sealed class UserDialogService : IUserDialogService
{
    public bool Confirm(string message, string title) =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) == MessageBoxResult.Yes;

    public string? SelectAttachment()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select supporting attachment",
            Filter = "Documents and images|*.pdf;*.doc;*.docx;*.xls;*.xlsx;*.jpg;*.jpeg;*.png;*.tif;*.tiff;*.txt|All files|*.*",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? SelectFolder()
    {
        var dialog = new Microsoft.Win32.OpenFolderDialog { Title = "Select report output folder", Multiselect = false };
        return dialog.ShowDialog() == true ? dialog.FolderName : null;
    }

    public string? SelectBackupFile()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Select a validated AFMS database backup",
            Filter = "AFMS database backups|*.afmsbak|All files|*.*",
            CheckFileExists = true
        };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
