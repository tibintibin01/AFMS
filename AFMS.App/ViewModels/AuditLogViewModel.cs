using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.App.Commands;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class AuditLogViewModel : ManagementViewModelBase
{
    private readonly IAuditQueryService _service;
    private AuditLogDto? _selectedItem;
    private int _page = 1;
    private int _totalPages = 1;
    private int _totalCount;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private UserActivityType? _activityType;
    private string _module = string.Empty;

    public AuditLogViewModel(IAuditQueryService service)
    {
        _service = service; ActivityTypes = [null, .. Enum.GetValues<UserActivityType>().Cast<UserActivityType?>()];
        RefreshCommand = new AsyncRelayCommand(SearchAsync);
        NextCommand = new AsyncRelayCommand(async () => { Page++; await SearchAsync(); }, () => Page < TotalPages);
        PreviousCommand = new AsyncRelayCommand(async () => { Page--; await SearchAsync(); }, () => Page > 1);
    }

    public ObservableCollection<AuditLogDto> Items { get; } = [];
    public ObservableCollection<UserActivityType?> ActivityTypes { get; }
    public AuditLogDto? SelectedItem { get => _selectedItem; set => SetProperty(ref _selectedItem, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public UserActivityType? ActivityType { get => _activityType; set => SetProperty(ref _activityType, value); }
    public string Module { get => _module; set => SetProperty(ref _module, value); }
    public int Page { get => _page; private set { if (SetProperty(ref _page, value)) RaisePaging(); } }
    public int TotalPages { get => _totalPages; private set { if (SetProperty(ref _totalPages, value)) RaisePaging(); } }
    public int TotalCount { get => _totalCount; private set => SetProperty(ref _totalCount, value); }
    public ICommand RefreshCommand { get; }
    public ICommand NextCommand { get; }
    public ICommand PreviousCommand { get; }
    public Task InitializeAsync() { Page = 1; return SearchAsync(); }

    private async Task SearchAsync() => await RunAsync(async () =>
    {
        var result = await _service.SearchAsync(new AuditLogFilter(SearchText,
            FromDate is null ? null : new DateTimeOffset(FromDate.Value.Date),
            ToDate is null ? null : new DateTimeOffset(ToDate.Value.Date.AddDays(1).AddTicks(-1)),
            ActivityType: ActivityType, Module: string.IsNullOrWhiteSpace(Module) ? null : Module.Trim(), Page: Page, PageSize: 100));
        Items.Clear(); foreach (var item in result.Items) Items.Add(item);
        TotalCount = result.TotalCount; TotalPages = result.TotalPages; SelectedItem = Items.FirstOrDefault();
    });

    private void RaisePaging()
    {
        ((AsyncRelayCommand)NextCommand).RaiseCanExecuteChanged(); ((AsyncRelayCommand)PreviousCommand).RaiseCanExecuteChanged();
    }
}
