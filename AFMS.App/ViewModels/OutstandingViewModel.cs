using System.Collections.ObjectModel;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>Who still holds what, including the exact serial ranges.</summary>
public sealed class OutstandingViewModel : PageViewModelBase
{
    private readonly IAccountabilityService _accountability;
    private readonly IRecipientService _people;
    private readonly IReportService _reports;
    private readonly IUserDialogService _dialogs;

    private OutstandingByRecipient? _selectedPerson;
    private RecipientOption? _filterPerson;

    public OutstandingViewModel(
        IAccountabilityService accountability,
        IRecipientService people,
        IReportService reports,
        IUserDialogService dialogs)
    {
        _accountability = accountability;
        _people = people;
        _reports = reports;
        _dialogs = dialogs;

        RefreshCommand = new AsyncRelayCommand(InitializeAsync);
        PrintCommand = new AsyncRelayCommand(PrintAsync);
    }

    public ObservableCollection<OutstandingByRecipient> People { get; } = [];
    public ObservableCollection<OutstandingItem> SelectedPersonItems { get; } = [];
    public ObservableCollection<RecipientOption> PeopleFilter { get; } = [];

    public ICommand RefreshCommand { get; }
    public ICommand PrintCommand { get; }

    public OutstandingByRecipient? SelectedPerson
    {
        get => _selectedPerson;
        set
        {
            if (!SetProperty(ref _selectedPerson, value)) return;

            SelectedPersonItems.Clear();
            foreach (var item in value?.Items ?? []) SelectedPersonItems.Add(item);

            RaisePropertyChanged(nameof(SelectedPersonHeading));
            RaisePropertyChanged(nameof(HasSelection));
        }
    }

    public bool HasSelection => SelectedPerson is not null;

    public string SelectedPersonHeading => SelectedPerson is null
        ? "Pick a person to see the details"
        : $"{SelectedPerson.RecipientName} - {SelectedPerson.TotalQuantity:N0} item(s) still out";

    public RecipientOption? FilterPerson
    {
        get => _filterPerson;
        set
        {
            if (SetProperty(ref _filterPerson, value)) _ = InitializeAsync();
        }
    }

    public long GrandTotal => People.Sum(person => person.TotalQuantity);

    public bool IsEmpty => People.Count == 0 && !IsBusy;

    public Task InitializeAsync() => RunAsync(async () =>
    {
        if (PeopleFilter.Count == 0)
        {
            PeopleFilter.Add(new RecipientOption(Guid.Empty, "ALL", "Everyone"));
            foreach (var person in await _people.ListOptionsAsync()) PeopleFilter.Add(person);
            _filterPerson = PeopleFilter[0];
            RaisePropertyChanged(nameof(FilterPerson));
        }

        var selectedId = SelectedPerson?.RecipientId;
        var filter = FilterPerson is { } chosen && chosen.Id != Guid.Empty ? chosen.Id : (Guid?)null;
        var rows = await _accountability.ListOutstandingAsync(filter);

        People.Clear();
        foreach (var row in rows) People.Add(row);

        // Keep the same person highlighted across a refresh where possible.
        SelectedPerson = People.FirstOrDefault(row => row.RecipientId == selectedId)
            ?? People.FirstOrDefault();

        RaisePropertyChanged(nameof(GrandTotal));
        RaisePropertyChanged(nameof(IsEmpty));
    });

    private async Task PrintAsync() => await RunAsync(async () =>
    {
        var filter = FilterPerson is { } person && person.Id != Guid.Empty ? person.Id : (Guid?)null;
        var document = await _reports.BuildAsync(
            new ReportRequest(ReportKind.OutstandingAccountability, RecipientId: filter));
        var path = await _reports.ExportPdfAsync(document);

        ReportSuccess($"Saved to {path}");
        _dialogs.OpenFile(path);
    });
}
