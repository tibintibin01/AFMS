using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Windows.Input;
using AFMS.App.Commands;
using AFMS.App.Services;
using AFMS.Application.Abstractions;
using AFMS.Application.Models;
using AFMS.Domain.Enums;

namespace AFMS.App.ViewModels;

public sealed class ReportCenterViewModel : ManagementViewModelBase
{
    private readonly IReportCenterService _reports;
    private readonly IStockLookupService _lookups;
    private readonly IUserDialogService _dialogs;
    private ReportCategory _selectedCategory;
    private ReportDefinition? _selectedReport;
    private LookupOption<Guid?>? _formType;
    private LookupOption<Guid?>? _collector;
    private LookupOption<Guid?>? _barangay;
    private AccountableFormStatus? _status;
    private TransactionStatus? _transactionStatus;
    private DateTime? _fromDate = new(DateTime.Today.Year, 1, 1);
    private DateTime? _toDate = DateTime.Today;
    private string _fiscalYear = DateTime.Today.Year.ToString();
    private string _referenceNumber = string.Empty;
    private string _outputFolder = string.Empty;
    private bool _outstandingOnly;
    private bool _includeDrafts;
    private DataView? _preview;
    private ReportDocument? _currentDocument;
    private string? _currentFile;

    public ReportCenterViewModel(IReportCenterService reports, IStockLookupService lookups, IUserDialogService dialogs, IAuthorizationService authorization)
    {
        _reports = reports;
        _lookups = lookups;
        _dialogs = dialogs;
        CanExport = authorization.HasPermission(SystemPermission.ExportReports);
        Categories = new(Enum.GetValues<ReportCategory>());
        _selectedCategory = Categories[0];
        RefreshDefinitions();
        GenerateCommand = new AsyncRelayCommand(GenerateAsync);
        ExportPdfCommand = new AsyncRelayCommand(ExportPdfAsync, () => CanExport);
        ExportExcelCommand = new AsyncRelayCommand(ExportExcelAsync, () => CanExport);
        PreviewPdfCommand = new AsyncRelayCommand(PreviewPdfAsync, () => CanExport);
        PrintCommand = new AsyncRelayCommand(PrintAsync, () => CanExport);
        OpenFileCommand = new RelayCommand(OpenCurrentFile);
        SelectFolderCommand = new RelayCommand(SelectFolder);
        ClearFiltersCommand = new RelayCommand(ClearFilters);
    }

    public ObservableCollection<ReportCategory> Categories { get; }
    public ObservableCollection<ReportDefinition> ReportDefinitions { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> FormTypes { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> Collectors { get; } = [];
    public ObservableCollection<LookupOption<Guid?>> Barangays { get; } = [];
    public Array Statuses => Enum.GetValues<AccountableFormStatus>();
    public Array TransactionStatuses => Enum.GetValues<TransactionStatus>();
    public bool CanExport { get; }
    public ICommand GenerateCommand { get; }
    public ICommand ExportPdfCommand { get; }
    public ICommand ExportExcelCommand { get; }
    public ICommand PreviewPdfCommand { get; }
    public ICommand PrintCommand { get; }
    public ICommand OpenFileCommand { get; }
    public ICommand SelectFolderCommand { get; }
    public ICommand ClearFiltersCommand { get; }

    public ReportCategory SelectedCategory { get => _selectedCategory; set { if (SetProperty(ref _selectedCategory, value)) RefreshDefinitions(); } }
    public ReportDefinition? SelectedReport { get => _selectedReport; set => SetProperty(ref _selectedReport, value); }
    public LookupOption<Guid?>? SelectedFormType { get => _formType; set => SetProperty(ref _formType, value); }
    public LookupOption<Guid?>? SelectedCollector { get => _collector; set => SetProperty(ref _collector, value); }
    public LookupOption<Guid?>? SelectedBarangay { get => _barangay; set => SetProperty(ref _barangay, value); }
    public AccountableFormStatus? SelectedStatus { get => _status; set => SetProperty(ref _status, value); }
    public TransactionStatus? SelectedTransactionStatus { get => _transactionStatus; set => SetProperty(ref _transactionStatus, value); }
    public DateTime? FromDate { get => _fromDate; set => SetProperty(ref _fromDate, value); }
    public DateTime? ToDate { get => _toDate; set => SetProperty(ref _toDate, value); }
    public string FiscalYear { get => _fiscalYear; set => SetProperty(ref _fiscalYear, value); }
    public string ReferenceNumber { get => _referenceNumber; set => SetProperty(ref _referenceNumber, value); }
    public string OutputFolder { get => _outputFolder; set => SetProperty(ref _outputFolder, value); }
    public bool OutstandingOnly { get => _outstandingOnly; set => SetProperty(ref _outstandingOnly, value); }
    public bool IncludeDrafts { get => _includeDrafts; set => SetProperty(ref _includeDrafts, value); }
    public DataView? Preview { get => _preview; private set => SetProperty(ref _preview, value); }
    public ReportDocument? CurrentDocument { get => _currentDocument; private set => SetProperty(ref _currentDocument, value); }
    public string? CurrentFile { get => _currentFile; private set => SetProperty(ref _currentFile, value); }
    public string PreviewTitle => CurrentDocument?.Title ?? "Generate a report to preview its data";
    public string PreviewReference => CurrentDocument is null ? string.Empty : $"{CurrentDocument.ReportReference} | {CurrentDocument.ReportingPeriod} | {CurrentDocument.Rows.Count:N0} rows";

    public async Task InitializeAsync()
    {
        if (FormTypes.Count != 0) return;
        FormTypes.Add(new(null, "All form types"));
        foreach (var x in await _lookups.ListFormTypesAsync()) FormTypes.Add(new(x.Id, $"{x.Code} - {x.Name}"));
        Collectors.Add(new(null, "All collectors"));
        foreach (var x in await _lookups.ListCollectorsAsync()) Collectors.Add(new(x.Id, x.Name));
        Barangays.Add(new(null, "All barangays"));
        foreach (var x in await _lookups.ListBarangaysAsync()) Barangays.Add(new(x.Id, x.Name));
        SelectedFormType = FormTypes[0];
        SelectedCollector = Collectors[0];
        SelectedBarangay = Barangays[0];
    }

    private void RefreshDefinitions()
    {
        ReportDefinitions.Clear();
        foreach (var x in _reports.GetCatalog().Where(x => x.Category == SelectedCategory)) ReportDefinitions.Add(x);
        SelectedReport = ReportDefinitions.FirstOrDefault();
    }

    private ReportFilter Filter() => new(
        FromDate is null ? null : DateOnly.FromDateTime(FromDate.Value),
        ToDate is null ? null : DateOnly.FromDateTime(ToDate.Value),
        int.TryParse(FiscalYear, out var year) ? year : null,
        SelectedFormType?.Value,
        SelectedCollector?.Value,
        SelectedBarangay?.Value,
        SelectedStatus,
        string.IsNullOrWhiteSpace(ReferenceNumber) ? null : ReferenceNumber.Trim(),
        OutstandingOnly,
        IncludeDrafts,
        SelectedTransactionStatus);

    private async Task GenerateAsync() => await RunAsync(async () =>
    {
        var definition = SelectedReport ?? throw new InvalidOperationException("Select a report.");
        CurrentDocument = await _reports.PreviewAsync(definition.Type, Filter());
        Preview = ToTable(CurrentDocument).DefaultView;
        RaisePropertyChanged(nameof(PreviewTitle));
        RaisePropertyChanged(nameof(PreviewReference));
        IsError = false;
        Message = CurrentDocument.IsEmpty ? "No records matched the selected filters." : $"Report preview generated with {CurrentDocument.Rows.Count:N0} row(s).";
    });

    private async Task ExportPdfAsync() => await RunAsync(async () =>
    {
        var definition = SelectedReport ?? throw new InvalidOperationException("Select a report.");
        CurrentFile = await _reports.ExportPdfAsync(definition.Type, Filter(), Folder());
    }, "PDF report created.");

    private async Task ExportExcelAsync() => await RunAsync(async () =>
    {
        var definition = SelectedReport ?? throw new InvalidOperationException("Select a report.");
        CurrentFile = await _reports.ExportExcelAsync(definition.Type, Filter(), Folder());
    }, "Excel report created.");

    private async Task PreviewPdfAsync() => await RunAsync(async () =>
    {
        var definition = SelectedReport ?? throw new InvalidOperationException("Select a report.");
        CurrentFile = await _reports.ExportPdfAsync(definition.Type, Filter(), Folder());
        OpenCurrentFile();
    }, "PDF preview opened.");

    private async Task PrintAsync() => await RunAsync(async () =>
    {
        var definition = SelectedReport ?? throw new InvalidOperationException("Select a report.");
        CurrentDocument = await _reports.PreviewAsync(definition.Type, Filter());
        Preview = ToTable(CurrentDocument).DefaultView;
        RaisePropertyChanged(nameof(PreviewTitle));
        RaisePropertyChanged(nameof(PreviewReference));
        CurrentFile = await _reports.ExportPdfAsync(definition.Type, Filter(), Folder());
        var file = CurrentFile ?? throw new InvalidOperationException("The PDF file could not be generated.");
        _ = Process.Start(new ProcessStartInfo(file) { UseShellExecute = true, Verb = "print" })
            ?? throw new InvalidOperationException("No PDF printing application is registered.");
        await _reports.RecordPrintAsync(CurrentDocument);
    }, "Report sent to the registered PDF printer.");

    private void OpenCurrentFile()
    {
        var file = CurrentFile;
        if (string.IsNullOrWhiteSpace(file) || !File.Exists(file))
        {
            IsError = true;
            Message = "Generate a PDF or Excel file first.";
            return;
        }
        Process.Start(new ProcessStartInfo(file) { UseShellExecute = true });
    }

    private void SelectFolder()
    {
        var folder = _dialogs.SelectFolder();
        if (folder is not null) OutputFolder = folder;
    }

    private string? Folder() => string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder.Trim();

    private void ClearFilters()
    {
        FromDate = new DateTime(DateTime.Today.Year, 1, 1);
        ToDate = DateTime.Today;
        FiscalYear = DateTime.Today.Year.ToString();
        SelectedFormType = FormTypes.FirstOrDefault();
        SelectedCollector = Collectors.FirstOrDefault();
        SelectedBarangay = Barangays.FirstOrDefault();
        SelectedStatus = null;
        SelectedTransactionStatus = null;
        ReferenceNumber = string.Empty;
        OutstandingOnly = false;
        IncludeDrafts = false;
    }

    private static DataTable ToTable(ReportDocument document)
    {
        var table = new DataTable();
        foreach (var c in document.Columns) table.Columns.Add(c.Header, typeof(string));
        foreach (var row in document.Rows) table.Rows.Add(document.Columns.Select((c, i) => Format(i < row.Values.Count ? row.Values[i] : null, c.Type)).ToArray());
        return table;
    }

    private static string Format(object? value, ReportValueType type) => value switch
    {
        null => string.Empty,
        DateOnly d => d.ToString("MMM d, yyyy"),
        DateTimeOffset d => d.ToString("MMM d, yyyy h:mm tt"),
        decimal d => d.ToString("N2"),
        long l => l.ToString("N0"),
        int i => i.ToString("N0"),
        _ => value.ToString() ?? string.Empty
    };
}
