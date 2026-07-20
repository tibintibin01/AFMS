using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;

namespace AFMS.App.ViewModels;

public sealed class SystemSettingsViewModel(ISystemSettingsService service) : ManagementViewModelBase
{
    private SystemSettingsDto _settings = new();
    public SystemSettingsDto Settings { get => _settings; private set => SetProperty(ref _settings, value); }
    public ICommand LoadCommand { get; } = new AsyncRelayCommand(() => Task.CompletedTask);
    public ICommand SaveCommand => _saveCommand ??= new AsyncRelayCommand(SaveAsync);
    private AsyncRelayCommand? _saveCommand;

    public async Task InitializeAsync() => await RunAsync(async () => Settings = await service.GetAsync());
    private async Task SaveAsync() => await RunAsync(() => service.SaveAsync(Settings), "System settings saved.");
}
