using System.Collections.ObjectModel;
using System.Data;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>Picks a report, previews the rows on screen, then saves it as PDF or Excel.</summary>
public sealed class ReportsViewModel : PageViewModelBase
{
    private readonly IReportService _reports;
    private readonly IItemService _items;
    private readonly IRecipientService _people;
    private readonly IUserDialogService _dialogs;

    private ReportOption _report = ReportOption.All[0];
    private ItemOption? _item;
    private RecipientOption? _person;
    private DateTime? _fromDate;
    private DateTime? _toDate;
    private ReportDocument? _preview;

    public ReportsViewModel(
        IReportService reports,
        IItemService items,
        IRecipientService people,
        IUserDialogService dialogs)
    {
        _reports = reports;
        _items = items;
        _people = people;
        _dialogs = dialogs;

        PreviewCommand = new AsyncRelayCommand(PreviewAsync);
        ExportPdfCommand = new AsyncRelayCommand(() => ExportAsync(pdf: true), () => Preview is not null);
        ExportExcelCommand = new AsyncRelayCommand(() => ExportAsync(pdf: false), () => Preview is not null);
    }

    public ObservableCollection<ItemOption> Items { get; } = [];
    public ObservableCollection<RecipientOption> People { get; } = [];
    public ObservableCollection<TotalDisplay> PreviewTotals { get; } = [];
    public IReadOnlyList<ReportOption> Reports => ReportOption.All;

    /// <summary>
    /// A real table for the DataGrid to bind to, so the preview lines up in proper
    /// columns instead of one long piped-together string.
    /// </summary>
    public DataTable PreviewTable { get; } = new();

    /// <summary>One report total, pre-formatted with its own number format (counts vs money).</summary>
    public sealed record TotalDisplay(string Label, string Value);

    public ICommand PreviewCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ExportExcelCommand { get; }

    public ReportOption SelectedReport
    {
        get => _report;
        set
        {
            if (!SetProperty(ref _report, value)) return;
            foreach (var property in new[]
                     {
                         nameof(NeedsItem), nameof(NeedsPerson),
                         nameof(SupportsDateRange), nameof(Description)
                     })
            {
                RaisePropertyChanged(property);
            }
        }
    }

    public ItemOption? SelectedItem { get => _item; set => SetProperty(ref _item, value); }
    public RecipientOption? SelectedPerson { get => _person; set => SetProperty(ref _person, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }

    public bool NeedsItem => SelectedReport.Kind is ReportKind.StockCard;
    public bool NeedsPerson => SelectedReport.Kind is
        ReportKind.ReleasesByRecipient or ReportKind.OutstandingAccountability;
    public bool SupportsDateRange => SelectedReport.Kind is not
        (ReportKind.StockOnHand or ReportKind.OutstandingAccountability);

    public string Description => SelectedReport.Description;

    public ReportDocument? Preview
    {
        get => _preview;
        private set
        {
            if (!SetProperty(ref _preview, value)) return;
            ((AsyncRelayCommand)ExportPdfCommand).RaiseCanExecuteChanged();
            ((AsyncRelayCommand)ExportExcelCommand).RaiseCanExecuteChanged();
            RaisePropertyChanged(nameof(HasPreview));
            RaisePropertyChanged(nameof(PreviewHeading));
        }
    }

    public bool HasPreview => Preview is not null;

    public string PreviewHeading => Preview is null
        ? string.Empty
        : $"{Preview.Title} - {Preview.Rows.Count:N0} row(s)";

    public Task InitializeAsync() => RunAsync(async () =>
    {
        if (Items.Count == 0)
        {
            foreach (var item in await _items.ListOptionsAsync()) Items.Add(item);
            SelectedItem = Items.FirstOrDefault();
        }

        if (People.Count == 0)
        {
            People.Add(new RecipientOption(Guid.Empty, "ALL", "Everyone"));
            foreach (var person in await _people.ListOptionsAsync()) People.Add(person);
            SelectedPerson = People[0];
        }
    });

    private async Task PreviewAsync() => await RunAsync(async () =>
    {
        var document = await _reports.BuildAsync(new ReportRequest(
            SelectedReport.Kind,
            FromDate is { } from && SupportsDateRange ? DateOnly.FromDateTime(from) : null,
            ToDate is { } to && SupportsDateRange ? DateOnly.FromDateTime(to) : null,
            NeedsItem ? SelectedItem?.Id : null,
            NeedsPerson && SelectedPerson is { } person && person.Id != Guid.Empty ? person.Id : null));

        Preview = document;
        FillPreviewTable(document);

        PreviewTotals.Clear();
        foreach (var total in document.Totals)
            PreviewTotals.Add(new TotalDisplay(total.Label, total.Value.ToString(total.Format)));

        const int previewLimit = 500;
        ReportSuccess(document.Rows.Count > previewLimit
            ? $"Showing the first {previewLimit:N0} of {document.Rows.Count:N0} rows. Export to see them all."
            : $"{document.Rows.Count:N0} row(s) ready to export.");
    });

    /// <summary>
    /// Rebuilds the preview table's columns to match the report, then fills it. A
    /// DataTable lets the DataGrid line values up under their own header instead of
    /// running them together as one string.
    /// </summary>
    private void FillPreviewTable(ReportDocument document)
    {
        const int previewLimit = 500;

        PreviewTable.Clear();
        PreviewTable.Columns.Clear();
        foreach (var column in document.Columns)
            PreviewTable.Columns.Add(column.Header, typeof(string));

        foreach (var row in document.Rows.Take(previewLimit))
        {
            var values = new string[document.Columns.Count];
            for (var index = 0; index < document.Columns.Count; index++)
                values[index] = Format(index < row.Values.Count ? row.Values[index] : null);
            PreviewTable.Rows.Add(values);
        }

        RaisePropertyChanged(nameof(PreviewTable));
    }

    private async Task ExportAsync(bool pdf) => await RunAsync(async () =>
    {
        if (Preview is not { } document) return;

        var path = pdf
            ? await _reports.ExportPdfAsync(document)
            : await _reports.ExportExcelAsync(document);

        ReportSuccess($"Saved to {path}");
        _dialogs.OpenFile(path);
    });

    private static string Format(object? value) => value switch
    {
        null => string.Empty,
        DateOnly date => date.ToString("MMM d, yyyy"),
        DateTimeOffset moment => moment.ToString("MMM d, yyyy h:mm tt"),
        long number => number.ToString("N0"),
        int number => number.ToString("N0"),
        decimal number => number.ToString("N2"),
        _ => value.ToString() ?? string.Empty
    };
}

public sealed record ReportOption(string Label, ReportKind Kind, string Description)
{
    public static IReadOnlyList<ReportOption> All { get; } =
    [
        new("Stock on hand", ReportKind.StockOnHand,
            "Every item with its total received, total released, and what is left."),
        new("Stock card for one item", ReportKind.StockCard,
            "The full in-and-out history of a single item with a running balance."),
        new("Releases to recipients", ReportKind.ReleasesByRecipient,
            "What went out, to whom, and with which serial numbers."),
        new("Outstanding accountability", ReportKind.OutstandingAccountability,
            "What each person still holds and has not returned."),
        new("Stock in and out summary", ReportKind.InOutSummary,
            "Totals received and released per item over a date range.")
    ];

    public override string ToString() => Label;
}
