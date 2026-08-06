using AFMS.Core;

namespace AFMS.App.ViewModels;

/// <summary>
/// One editable line on the stock-in or release form. Keeps quantity and the
/// serial range in step, and can ask the service whether a serial range is still
/// free while the user is typing.
/// </summary>
public sealed class StockLineEditor : ObservableObject
{
    private readonly Func<StockLineEditor, Task>? _checkSerials;
    private ItemOption? _item;
    private long _quantity;
    private long? _serialFrom;
    private long? _serialTo;
    private string? _remarks;
    private string? _warning;

    public StockLineEditor(
        IReadOnlyList<ItemOption> itemOptions,
        Func<StockLineEditor, Task>? checkSerials = null)
    {
        ItemOptions = itemOptions;
        _checkSerials = checkSerials;
    }

    public IReadOnlyList<ItemOption> ItemOptions { get; }

    public ItemOption? Item
    {
        get => _item;
        set
        {
            if (!SetProperty(ref _item, value)) return;

            // Serial boxes only make sense for serial-tracked items.
            if (value?.TracksSerials != true)
            {
                _serialFrom = null;
                _serialTo = null;
                RaisePropertyChanged(nameof(SerialFrom));
                RaisePropertyChanged(nameof(SerialTo));
            }

            Warning = null;
            RaisePropertyChanged(nameof(SupportsSerials));
            RaisePropertyChanged(nameof(Unit));
            RaisePropertyChanged(nameof(OnHandLabel));
            QueueSerialCheck();
        }
    }

    public bool SupportsSerials => Item?.TracksSerials == true;

    public string Unit => Item?.Unit ?? string.Empty;

    public string OnHandLabel => Item is null ? string.Empty : $"{Item.OnHand:N0} on hand";

    public long Quantity
    {
        get => _quantity;
        set => SetProperty(ref _quantity, value);
    }

    public long? SerialFrom
    {
        get => _serialFrom;
        set
        {
            if (!SetProperty(ref _serialFrom, value)) return;
            SyncQuantityFromSerials();
            QueueSerialCheck();
        }
    }

    public long? SerialTo
    {
        get => _serialTo;
        set
        {
            if (!SetProperty(ref _serialTo, value)) return;
            SyncQuantityFromSerials();
            QueueSerialCheck();
        }
    }

    public string? Remarks
    {
        get => _remarks;
        set => SetProperty(ref _remarks, value);
    }

    /// <summary>Live notice shown beside the line, such as a serial already out with someone.</summary>
    public string? Warning
    {
        get => _warning;
        set
        {
            if (SetProperty(ref _warning, value)) RaisePropertyChanged(nameof(HasWarning));
        }
    }

    public bool HasWarning => !string.IsNullOrWhiteSpace(Warning);

    public bool IsComplete => Item is not null && Quantity > 0;

    public StockLineInput ToInput() => new(
        Item?.Id ?? Guid.Empty,
        Quantity,
        SupportsSerials ? SerialFrom : null,
        SupportsSerials ? SerialTo : null,
        Remarks);

    public static StockLineEditor FromLine(
        IReadOnlyList<ItemOption> options,
        Guid itemId,
        long quantity,
        long? serialFrom,
        long? serialTo,
        string? remarks,
        Func<StockLineEditor, Task>? checkSerials = null)
    {
        var editor = new StockLineEditor(options, checkSerials)
        {
            _item = options.FirstOrDefault(option => option.Id == itemId),
            _quantity = quantity,
            _serialFrom = serialFrom,
            _serialTo = serialTo,
            _remarks = remarks
        };
        return editor;
    }

    /// <summary>A serial range implies its own quantity, so keep them consistent.</summary>
    private void SyncQuantityFromSerials()
    {
        if (SerialFrom is not { } from || SerialTo is not { } to || to < from) return;
        Quantity = to - from + 1;
    }

    private void QueueSerialCheck()
    {
        if (_checkSerials is null) return;
        if (!SupportsSerials || SerialFrom is null || SerialTo is null) return;
        _ = _checkSerials(this);
    }
}
