using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;
using AFMS.Core.Domain;

namespace AFMS.App.ViewModels;

/// <summary>Office details for printed forms, plus backups and the audit trail.</summary>
public sealed class SettingsViewModel : PageViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IBackupService _backups;
    private readonly IAuditService _audit;
    private readonly IUserDialogService _dialogs;

    private string _officeName = string.Empty;
    private string _municipality = string.Empty;
    private string _province = string.Empty;
    private string _headOfOffice = string.Empty;
    private string _preparedBy = string.Empty;
    private string _certifiedBy = string.Empty;
    private string _backupFolder = string.Empty;
    private string _reportFolder = string.Empty;
    private int _sessionTimeoutMinutes = 30;
    private bool _dailyBackupEnabled = true;
    private int _backupRetentionDays = 60;

    private BackupStatus? _backupStatus;
    private BackupFileDto? _selectedBackup;
    private string _auditSearch = string.Empty;
    private int _auditPage = 1;
    private AuditPage? _auditResults;

    public SettingsViewModel(
        ISettingsService settings,
        IBackupService backups,
        IAuditService audit,
        IUserDialogService dialogs)
    {
        _settings = settings;
        _backups = backups;
        _audit = audit;
        _dialogs = dialogs;

        SaveCommand = new AsyncRelayCommand(SaveAsync);
        BrowseBackupFolderCommand = new RelayCommand(BrowseBackupFolder);
        BrowseReportFolderCommand = new RelayCommand(BrowseReportFolder);
        BackupNowCommand = new AsyncRelayCommand(BackupNowAsync);
        RestoreCommand = new AsyncRelayCommand(RestoreAsync);
        OpenBackupFolderCommand = new RelayCommand(() => _dialogs.OpenFile(BackupFolder));
        SearchAuditCommand = new AsyncRelayCommand(() => LoadAuditAsync(1));
        NextAuditPageCommand = new AsyncRelayCommand(
            () => LoadAuditAsync(_auditPage + 1), () => _auditResults?.Page < _auditResults?.TotalPages);
        PreviousAuditPageCommand = new AsyncRelayCommand(
            () => LoadAuditAsync(_auditPage - 1), () => _auditPage > 1);
    }

    public event Action? RestartRequested;

    public ObservableCollection<BackupFileDto> Backups { get; } = [];
    public ObservableCollection<AuditEntryDto> AuditEntries { get; } = [];

    public ICommand SaveCommand { get; }
    public ICommand BrowseBackupFolderCommand { get; }
    public ICommand BrowseReportFolderCommand { get; }
    public ICommand BackupNowCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand OpenBackupFolderCommand { get; }
    public ICommand SearchAuditCommand { get; }
    public ICommand NextAuditPageCommand { get; }
    public ICommand PreviousAuditPageCommand { get; }

    public string OfficeName { get => _officeName; set => SetProperty(ref _officeName, value); }
    public string MunicipalityName { get => _municipality; set => SetProperty(ref _municipality, value); }
    public string ProvinceName { get => _province; set => SetProperty(ref _province, value); }
    public string HeadOfOffice { get => _headOfOffice; set => SetProperty(ref _headOfOffice, value); }
    public string PreparedBy { get => _preparedBy; set => SetProperty(ref _preparedBy, value); }
    public string CertifiedBy { get => _certifiedBy; set => SetProperty(ref _certifiedBy, value); }
    public string BackupFolder { get => _backupFolder; set => SetProperty(ref _backupFolder, value); }
    public string ReportFolder { get => _reportFolder; set => SetProperty(ref _reportFolder, value); }
    public bool DailyBackupEnabled { get => _dailyBackupEnabled; set => SetProperty(ref _dailyBackupEnabled, value); }
    public string AuditSearch { get => _auditSearch; set => SetProperty(ref _auditSearch, value); }

    public int SessionTimeoutMinutes
    {
        get => _sessionTimeoutMinutes;
        set => SetProperty(ref _sessionTimeoutMinutes, value);
    }

    public int BackupRetentionDays
    {
        get => _backupRetentionDays;
        set => SetProperty(ref _backupRetentionDays, value);
    }

    public BackupFileDto? SelectedBackup
    {
        get => _selectedBackup;
        set
        {
            if (SetProperty(ref _selectedBackup, value))
                ((AsyncRelayCommand)RestoreCommand).RaiseCanExecuteChanged();
        }
    }

    public string BackupSummary => _backupStatus switch
    {
        null => "Checking backups...",
        { LastBackupAt: null } => "No backup has been made yet. Use 'Back up now'.",
        { BackupOverdue: true } backup =>
            $"Last backup {backup.LastBackupAt:MMM d, yyyy h:mm tt}. That is more than a day ago.",
        var backup => $"Last backup {backup.LastBackupAt:MMM d, yyyy h:mm tt}."
    };

    public bool BackupOverdue => _backupStatus?.BackupOverdue ?? false;

    public string AuditPageLabel => _auditResults is null
        ? string.Empty
        : $"Page {_auditResults.Page} of {_auditResults.TotalPages} ({_auditResults.TotalCount:N0} entries)";

    public Task InitializeAsync() => RunAsync(async () =>
    {
        var settings = await _settings.GetAsync();
        OfficeName = settings.OfficeName;
        MunicipalityName = settings.MunicipalityName;
        ProvinceName = settings.ProvinceName;
        HeadOfOffice = settings.HeadOfOffice;
        PreparedBy = settings.PreparedBy;
        CertifiedBy = settings.CertifiedBy;
        BackupFolder = settings.BackupFolder;
        ReportFolder = settings.ReportFolder;
        SessionTimeoutMinutes = settings.SessionTimeoutMinutes;
        DailyBackupEnabled = settings.DailyBackupEnabled;
        BackupRetentionDays = settings.BackupRetentionDays;

        await LoadBackupsCoreAsync();
        await LoadAuditCoreAsync(1);
    });

    private async Task SaveAsync() => await RunAsync(async () =>
    {
        await _settings.SaveAsync(new OfficeSettings(
            OfficeName, MunicipalityName, ProvinceName, HeadOfOffice, PreparedBy, CertifiedBy,
            BackupFolder, ReportFolder, SessionTimeoutMinutes, DailyBackupEnabled, BackupRetentionDays));

        ReportSuccess("Settings saved. The auto-lock time takes effect at the next sign-in.");
    });

    private void BrowseBackupFolder()
    {
        if (_dialogs.PickFolder("Choose where backups are saved") is { } folder) BackupFolder = folder;
    }

    private void BrowseReportFolder()
    {
        if (_dialogs.PickFolder("Choose where exported reports are saved") is { } folder) ReportFolder = folder;
    }

    private async Task BackupNowAsync() => await RunAsync(async () =>
    {
        var backup = await _backups.CreateAsync(BackupKind.Manual);
        await LoadBackupsCoreAsync();
        ReportSuccess($"Backup saved as {backup.FileName} ({backup.SizeLabel}).");
    });

    private async Task LoadBackupsCoreAsync()
    {
        _backupStatus = await _backups.GetStatusAsync();
        Backups.Clear();
        foreach (var file in _backupStatus.History) Backups.Add(file);

        RaisePropertyChanged(nameof(BackupSummary));
        RaisePropertyChanged(nameof(BackupOverdue));
    }

    /// <summary>
    /// A restore replaces every record, so it needs a written reason, an explicit
    /// confirmation, and a restart to apply.
    /// </summary>
    private async Task RestoreAsync()
    {
        var path = SelectedBackup?.FilePath ?? _dialogs.PickBackupFile();
        if (string.IsNullOrWhiteSpace(path)) return;

        var check = await _backups.CheckAsync(path);
        if (!check.IsValid)
        {
            ReportError(check.Message);
            return;
        }

        var confirmed = _dialogs.Confirm(
            $"Restore from {System.IO.Path.GetFileName(path)}?\n\n" +
            "Every record entered after that backup will be replaced. A safety copy of " +
            "the current data is taken first, and AFMS will restart to finish.",
            "Confirm restore");
        if (!confirmed) return;

        var reason = _dialogs.AskForText(
            "Why is this restore needed? (at least 10 characters, kept in the audit trail)",
            "Reason for restore");
        if (string.IsNullOrWhiteSpace(reason)) return;

        await RunAsync(async () =>
        {
            await _backups.ScheduleRestoreAsync(path, reason);
            _dialogs.Inform(
                "The restore is ready. AFMS will now restart to apply it.",
                "Restart required");
            RestartRequested?.Invoke();
        });
    }

    private async Task LoadAuditAsync(int page) => await RunAsync(() => LoadAuditCoreAsync(page));

    private async Task LoadAuditCoreAsync(int page)
    {
        _auditPage = Math.Max(1, page);
        _auditResults = await _audit.SearchAsync(new AuditFilter(
            SearchText: AuditSearch, Page: _auditPage, PageSize: 100));

        AuditEntries.Clear();
        foreach (var entry in _auditResults.Entries) AuditEntries.Add(entry);

        RaisePropertyChanged(nameof(AuditPageLabel));
        ((AsyncRelayCommand)NextAuditPageCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)PreviousAuditPageCommand).RaiseCanExecuteChanged();
    }
}
