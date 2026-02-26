using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Layout;

namespace AvaloniaVirtualDataGrid.Controls;

public abstract class VirtualDataGridColumn : AvaloniaObject
{
    public static readonly StyledProperty<string> HeaderProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, string>(nameof(Header));

    public static readonly StyledProperty<double> WidthProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, double>(nameof(Width), double.NaN);

    public static readonly StyledProperty<double> MinWidthProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, double>(nameof(MinWidth), 20);

    public static readonly StyledProperty<double> MaxWidthProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, double>(nameof(MaxWidth), double.PositiveInfinity);

    public static readonly StyledProperty<bool> IsResizableProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, bool>(nameof(IsResizable), true);

    public static readonly StyledProperty<bool> IsSortableProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, bool>(nameof(IsSortable), true);

    public static readonly StyledProperty<bool> IsVisibleProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, bool>(nameof(IsVisible), true);

    public static readonly StyledProperty<HorizontalAlignment> HorizontalContentAlignmentProperty =
        AvaloniaProperty.Register<VirtualDataGridColumn, HorizontalAlignment>(nameof(HorizontalContentAlignment), HorizontalAlignment.Left);

    public string Header
    {
        get => GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public double Width
    {
        get => GetValue(WidthProperty);
        set => SetValue(WidthProperty, value);
    }

    public double MinWidth
    {
        get => GetValue(MinWidthProperty);
        set => SetValue(MinWidthProperty, value);
    }

    public double MaxWidth
    {
        get => GetValue(MaxWidthProperty);
        set => SetValue(MaxWidthProperty, value);
    }

    public bool IsResizable
    {
        get => GetValue(IsResizableProperty);
        set => SetValue(IsResizableProperty, value);
    }

    public bool IsSortable
    {
        get => GetValue(IsSortableProperty);
        set => SetValue(IsSortableProperty, value);
    }

    public bool IsVisible
    {
        get => GetValue(IsVisibleProperty);
        set => SetValue(IsVisibleProperty, value);
    }

    public HorizontalAlignment HorizontalContentAlignment
    {
        get => GetValue(HorizontalContentAlignmentProperty);
        set => SetValue(HorizontalContentAlignmentProperty, value);
    }

    internal VirtualDataGrid? Owner { get; set; }

    private double _actualWidth;
    public double ActualWidth
    {
        get => _actualWidth;
        internal set
        {
            if (Math.Abs(_actualWidth - value) > 0.001)
            {
                _actualWidth = value;
                ActualWidthChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public event EventHandler? ActualWidthChanged;

    internal void CalculateActualWidth(double availableWidth)
    {
        if (double.IsNaN(Width))
        {
            ActualWidth = Math.Max(MinWidth, Math.Min(MaxWidth, 100));
        }
        else
        {
            ActualWidth = Math.Max(MinWidth, Math.Min(MaxWidth, Width));
        }
    }

    public abstract Control CreateCellContent(object? item);
    public virtual Control? CreateEditContent(object? item) => null;
    public virtual void CommitEdit(Control editControl, object? item) { }
}

public class VirtualDataGridColumnCollection : IList<VirtualDataGridColumn>, INotifyCollectionChanged
{
    private readonly List<VirtualDataGridColumn> _items = [];

    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public VirtualDataGridColumn this[int index]
    {
        get => _items[index];
        set
        {
            var oldItem = _items[index];
            _items[index] = value;
            value.Owner = oldItem.Owner;
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, value, oldItem, index));
        }
    }

    public int Count => _items.Count;
    public bool IsReadOnly => false;

    public void Add(VirtualDataGridColumn item)
    {
        _items.Add(item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, _items.Count - 1));
    }

    public void Clear()
    {
        _items.Clear();
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    public bool Contains(VirtualDataGridColumn item) => _items.Contains(item);
    public void CopyTo(VirtualDataGridColumn[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public IEnumerator<VirtualDataGridColumn> GetEnumerator() => _items.GetEnumerator();
    public int IndexOf(VirtualDataGridColumn item) => _items.IndexOf(item);

    public void Insert(int index, VirtualDataGridColumn item)
    {
        _items.Insert(index, item);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, item, index));
    }

    public bool Remove(VirtualDataGridColumn item)
    {
        var index = _items.IndexOf(item);
        if (index >= 0)
        {
            _items.RemoveAt(index);
            OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
            return true;
        }
        return false;
    }

    public void RemoveAt(int index)
    {
        var item = _items[index];
        _items.RemoveAt(index);
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item, index));
    }

    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
    {
        CollectionChanged?.Invoke(this, e);
    }
}
