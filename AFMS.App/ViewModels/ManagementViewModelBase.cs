using System.Collections.ObjectModel;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public abstract class ManagementViewModelBase : ObservableObject
{
    private bool _isLoading;
    private string? _message;
    private bool _isError;
    private string _searchText = string.Empty;
    private StatusFilter _selectedStatusFilter;

    protected ManagementViewModelBase()
    {
        StatusFilters = [new("All records", null), new("Active", true), new("Inactive", false)];
        _selectedStatusFilter = StatusFilters[0];
    }

    public ObservableCollection<StatusFilter> StatusFilters { get; }

    public bool IsLoading { get => _isLoading; protected set => SetProperty(ref _isLoading, value); }
    public string? Message { get => _message; protected set => SetProperty(ref _message, value); }
    public bool IsError { get => _isError; protected set => SetProperty(ref _isError, value); }
    public string SearchText { get => _searchText; set => SetProperty(ref _searchText, value); }
    public StatusFilter SelectedStatusFilter { get => _selectedStatusFilter; set => SetProperty(ref _selectedStatusFilter, value); }

    protected async Task RunAsync(Func<Task> operation, string? successMessage = null)
    {
        IsLoading = true;
        Message = null;
        try
        {
            await operation();
            if (successMessage is not null)
            {
                IsError = false;
                Message = successMessage;
            }
        }
        catch (Exception exception)
        {
            IsError = true;
            Message = UserFacingError.From(exception);
        }
        finally
        {
            IsLoading = false;
        }
    }
}

public sealed record StatusFilter(string Label, bool? Value)
{
    public override string ToString() => Label;
}
