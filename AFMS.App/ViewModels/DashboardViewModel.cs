using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;

namespace AFMS.App.ViewModels;

public sealed class DashboardViewModel(IDashboardQueryService dashboard, IBackupService backups) : ManagementViewModelBase
{
    private DashboardDto? _summary;
    private BackupStatusDto? _backup;
    public DashboardDto? Summary { get => _summary; private set => SetProperty(ref _summary, value); }
    public BackupStatusDto? Backup { get => _backup; private set => SetProperty(ref _backup, value); }
    public ICommand RefreshCommand => _refresh ??= new AsyncRelayCommand(InitializeAsync);
    private AsyncRelayCommand? _refresh;
    public async Task InitializeAsync() => await RunAsync(async () =>
    {
        Summary = await dashboard.GetAsync();
        try { Backup = await backups.GetStatusAsync(); } catch (UnauthorizedAccessException) { Backup = null; }
    });
}
