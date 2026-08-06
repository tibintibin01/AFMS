using System.ComponentModel;
using System.Runtime.CompilerServices;
using AFMS.App.Services;

namespace AFMS.App.ViewModels;

public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        RaisePropertyChanged(propertyName);
        return true;
    }

    protected void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// Shared plumbing for every screen: a busy flag, one place for status messages,
/// and a wrapper that turns any failure into a readable notice instead of a crash.
/// </summary>
public abstract class PageViewModelBase : ObservableObject
{
    private bool _isBusy;
    private string? _message;
    private bool _isError;

    public bool IsBusy
    {
        get => _isBusy;
        protected set => SetProperty(ref _isBusy, value);
    }

    public string? Message
    {
        get => _message;
        protected set => SetProperty(ref _message, value);
    }

    public bool IsError
    {
        get => _isError;
        protected set => SetProperty(ref _isError, value);
    }

    /// <summary>Runs work with the busy indicator on and reports the outcome.</summary>
    protected async Task RunAsync(Func<Task> operation, string? successMessage = null)
    {
        IsBusy = true;
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
            IsBusy = false;
        }
    }

    protected void ReportError(string message)
    {
        IsError = true;
        Message = message;
    }

    protected void ReportSuccess(string message)
    {
        IsError = false;
        Message = message;
    }
}

/// <summary>Reusable "All / Active / Hidden" filter for the list screens.</summary>
public sealed record ActiveFilter(string Label, bool? Value)
{
    public static IReadOnlyList<ActiveFilter> All { get; } =
    [
        new("All records", null),
        new("Active only", true),
        new("Hidden only", false)
    ];

    public override string ToString() => Label;
}
