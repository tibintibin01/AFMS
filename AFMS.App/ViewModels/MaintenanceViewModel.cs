using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.App.ViewModels;

public sealed class MaintenanceViewModel : ManagementViewModelBase
{
    private readonly IBackupService _backups;
    private readonly IFiscalYearService _fiscalYears;
    private readonly IUserDialogService _dialogs;
    private readonly IAuthorizationService _authorization;
    private BackupStatusDto? _backupStatus;
    private FiscalYearDto? _selectedYear;
    private BackupHistoryDto? _selectedBackup;
    private int _newYear = DateTime.Today.Year;
    private DateTime _startDate = new(DateTime.Today.Year, 1, 1);
    private DateTime _endDate = new(DateTime.Today.Year, 12, 31);
    private string _reason = string.Empty;
    private bool _allowDocumentedExceptions;
    private string? _restoreFilePath;

    public MaintenanceViewModel(IBackupService backups, IFiscalYearService fiscalYears, IUserDialogService dialogs, IAuthorizationService authorization)
    {
        _backups = backups; _fiscalYears = fiscalYears; _dialogs = dialogs; _authorization = authorization;
        RefreshCommand = new AsyncRelayCommand(RefreshAsync);
        CreateBackupCommand = new AsyncRelayCommand(CreateBackupAsync, () => CanBackupAndRestore);
        BrowseRestoreCommand = new RelayCommand(BrowseRestore, () => CanBackupAndRestore);
        ValidateBackupCommand = new AsyncRelayCommand(ValidateBackupAsync, () => CanBackupAndRestore && HasRestoreSelection());
        RestoreCommand = new AsyncRelayCommand(RestoreAsync, () => CanBackupAndRestore && HasRestoreSelection());
        CreateFiscalYearCommand = new AsyncRelayCommand(CreateFiscalYearAsync, () => CanManageFiscalYears);
        CheckCloseCommand = new AsyncRelayCommand(CheckCloseAsync, () => CanManageFiscalYears && SelectedYear is not null);
        RequestCloseCommand = new AsyncRelayCommand(RequestCloseAsync, () => CanManageFiscalYears && SelectedYear is not null);
        ApproveCloseCommand = new AsyncRelayCommand(ApproveCloseAsync, () => CanManageFiscalYears && SelectedYear is not null);
        RequestReopenCommand = new AsyncRelayCommand(RequestReopenAsync, () => CanManageFiscalYears && SelectedYear is not null);
        ApproveReopenCommand = new AsyncRelayCommand(ApproveReopenAsync, () => CanManageFiscalYears && SelectedYear is not null);
        ViewSnapshotCommand = new AsyncRelayCommand(ViewSnapshotAsync, () => CanManageFiscalYears && SelectedYear is not null);
    }

    public ObservableCollection<BackupHistoryDto> BackupHistory { get; } = [];
    public ObservableCollection<FiscalYearDto> FiscalYears { get; } = [];
    public ObservableCollection<FiscalYearSnapshotDto> Snapshot { get; } = [];
    public bool CanBackupAndRestore => _authorization.HasPermission(AFMS.Domain.Enums.SystemPermission.BackupAndRestore);
    public bool CanManageFiscalYears => _authorization.HasPermission(AFMS.Domain.Enums.SystemPermission.ManageFiscalYears);
    public BackupStatusDto? BackupStatus { get => _backupStatus; private set => SetProperty(ref _backupStatus, value); }
    public BackupHistoryDto? SelectedBackup { get => _selectedBackup; set { if (SetProperty(ref _selectedBackup, value)) { if (value is not null) { _restoreFilePath = null; RaisePropertyChanged(nameof(RestoreFilePath)); } RaiseSelectionCommands(); } } }
    public FiscalYearDto? SelectedYear { get => _selectedYear; set { if (SetProperty(ref _selectedYear, value)) RaiseSelectionCommands(); } }
    public int NewYear { get => _newYear; set { if (SetProperty(ref _newYear, value)) { StartDate = new(value, 1, 1); EndDate = new(value, 12, 31); } } }
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
    public string Reason { get => _reason; set => SetProperty(ref _reason, value); }
    public bool AllowDocumentedExceptions { get => _allowDocumentedExceptions; set => SetProperty(ref _allowDocumentedExceptions, value); }
    public string? RestoreFilePath { get => _restoreFilePath; private set { if (SetProperty(ref _restoreFilePath, value)) RaiseSelectionCommands(); } }
    public ICommand RefreshCommand { get; }
    public ICommand CreateBackupCommand { get; }
    public ICommand BrowseRestoreCommand { get; }
    public ICommand ValidateBackupCommand { get; }
    public ICommand RestoreCommand { get; }
    public ICommand CreateFiscalYearCommand { get; }
    public ICommand CheckCloseCommand { get; }
    public ICommand RequestCloseCommand { get; }
    public ICommand ApproveCloseCommand { get; }
    public ICommand RequestReopenCommand { get; }
    public ICommand ApproveReopenCommand { get; }
    public ICommand ViewSnapshotCommand { get; }
    public event Action? RestartRequested;

    public Task InitializeAsync() => RefreshAsync();

    private async Task RefreshAsync() => await RunAsync(async () =>
    {
        if (CanBackupAndRestore)
        {
            BackupStatus = await _backups.GetStatusAsync();
            BackupHistory.Clear(); foreach (var item in BackupStatus.History) BackupHistory.Add(item);
        }
        if (CanManageFiscalYears)
        {
            FiscalYears.Clear(); foreach (var item in await _fiscalYears.ListAsync()) FiscalYears.Add(item);
        }
    });

    private async Task CreateBackupAsync() => await RunAsync(async () =>
    {
        await _backups.CreateAsync(AFMS.Domain.Enums.BackupKind.Manual);
        await RefreshCoreAsync();
    }, "Database backup created and validated successfully.");

    private async Task ValidateBackupAsync() => await RunAsync(async () =>
    {
        var result = await _backups.ValidateAsync(SelectedRestorePath());
        if (!result.IsValid) throw new InvalidDataException(result.Message);
    }, "The selected backup passed SQLite integrity and AFMS schema validation.");

    private async Task RestoreAsync()
    {
        if (!HasRestoreSelection() || !_dialogs.Confirm("AFMS will create a safety backup, close, and restore this database on the next launch. Continue?", "Confirm database restore")) return;
        await RunAsync(async () =>
        {
            await _backups.ScheduleRestoreAsync(SelectedRestorePath(), Reason);
            RestartRequested?.Invoke();
        }, "Restore scheduled. AFMS is restarting safely.");
    }

    private async Task CreateFiscalYearAsync() => await RunAsync(async () =>
    {
        await _fiscalYears.CreateAsync(NewYear, DateOnly.FromDateTime(StartDate), DateOnly.FromDateTime(EndDate));
        await RefreshCoreAsync();
    }, "Fiscal year created.");

    private async Task CheckCloseAsync() => await RunAsync(async () =>
    {
        var check = await _fiscalYears.CheckCloseAsync(SelectedYear!.Id);
        if (check.Issues.Count > 0) throw new InvalidOperationException(string.Join(Environment.NewLine, check.Issues));
    }, "Pre-close validation passed. No blocking issues were detected.");

    private async Task RequestCloseAsync()
    {
        if (SelectedYear is null || !_dialogs.Confirm($"Submit fiscal year {SelectedYear.Year} for supervisor-authorized closure?", "Fiscal-year closure")) return;
        await RunAsync(async () => { await _fiscalYears.RequestCloseAsync(SelectedYear.Id, Reason, AllowDocumentedExceptions); await RefreshCoreAsync(); }, "Fiscal-year closure request submitted.");
    }

    private async Task ApproveCloseAsync()
    {
        if (SelectedYear is null || !_dialogs.Confirm($"Close fiscal year {SelectedYear.Year}? This locks dated transactions and creates a year-end snapshot.", "Authorize fiscal-year closure")) return;
        await RunAsync(async () => { await _fiscalYears.ApproveCloseAsync(SelectedYear.Id, Reason); await RefreshCoreAsync(); }, "Fiscal year closed and snapshot created.");
    }

    private async Task RequestReopenAsync()
    {
        if (SelectedYear is null || !_dialogs.Confirm($"Request reopening of fiscal year {SelectedYear.Year}? A different supervisor must authorize it.", "Request fiscal-year reopening")) return;
        await RunAsync(async () => { await _fiscalYears.RequestReopenAsync(SelectedYear.Id, Reason); await RefreshCoreAsync(); }, "Fiscal-year reopening request submitted.");
    }

    private async Task ApproveReopenAsync()
    {
        if (SelectedYear is null || !_dialogs.Confirm($"Authorize reopening fiscal year {SelectedYear.Year}?", "Authorize fiscal-year reopening")) return;
        await RunAsync(async () => { await _fiscalYears.ApproveReopenAsync(SelectedYear.Id, Reason); await RefreshCoreAsync(); }, "Fiscal year reopened.");
    }

    private async Task ViewSnapshotAsync() => await RunAsync(async () =>
    {
        Snapshot.Clear(); foreach (var item in await _fiscalYears.GetSnapshotAsync(SelectedYear!.Id)) Snapshot.Add(item);
    });

    private async Task RefreshCoreAsync()
    {
        if (CanBackupAndRestore)
        {
            BackupStatus = await _backups.GetStatusAsync(); BackupHistory.Clear(); foreach (var item in BackupStatus.History) BackupHistory.Add(item);
        }
        if (CanManageFiscalYears)
        {
            FiscalYears.Clear(); foreach (var item in await _fiscalYears.ListAsync()) FiscalYears.Add(item);
        }
    }

    private void RaiseSelectionCommands()
    {
        ((AsyncRelayCommand)ValidateBackupCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)RestoreCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)CheckCloseCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)RequestCloseCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ApproveCloseCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)RequestReopenCommand).RaiseCanExecuteChanged();
        ((AsyncRelayCommand)ApproveReopenCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)ViewSnapshotCommand).RaiseCanExecuteChanged();
    }

    private void BrowseRestore()
    {
        var selected = _dialogs.SelectBackupFile(); if (selected is not null) RestoreFilePath = selected;
    }
    private bool HasRestoreSelection() => SelectedBackup is not null || !string.IsNullOrWhiteSpace(RestoreFilePath);
    private string SelectedRestorePath() => !string.IsNullOrWhiteSpace(RestoreFilePath) ? RestoreFilePath : SelectedBackup!.FilePath;
}
