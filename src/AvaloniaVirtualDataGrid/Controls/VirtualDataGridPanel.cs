using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.VisualTree;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGridPanel : VirtualizingPanel, IScrollable
{
    private double _extentHeight;
    private double _extentWidth;
    private double _offsetX;
    private double _offsetY;
    private Size _viewport;
    private int _firstVisibleIndex;
    private int _lastVisibleIndex;
    private readonly Dictionary<int, Control> _containers = [];
    private IEnumerable? _itemsSource;
    private IList? _items;

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<VirtualDataGridPanel, double>(nameof(RowHeight), 32.0);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<VirtualDataGridPanel, IDataTemplate?>(nameof(ItemTemplate));

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public IDataTemplate? ItemTemplate
    {
        get => GetValue(ItemTemplateProperty);
        set => SetValue(ItemTemplateProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => _itemsSource;
        set
        {
            if (_itemsSource is INotifyCollectionChanged oldCollection)
            {
                oldCollection.CollectionChanged -= OnItemsSourceCollectionChanged;
            }

            _itemsSource = value;
            _items = value as IList;

            if (_itemsSource is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnItemsSourceCollectionChanged;
            }

            InvalidateMeasure();
        }
    }

    public Size Viewport => _viewport;

    public Size Extent => new(_extentWidth, _extentHeight);

    public Vector Offset
    {
        get => new(_offsetX, _offsetY);
        set
        {
            var oldOffsetY = _offsetY;
            _offsetX = Math.Max(0, Math.Min(value.X, _extentWidth - _viewport.Width));
            _offsetY = Math.Max(0, Math.Min(value.Y, _extentHeight - _viewport.Height));

            if (Math.Abs(oldOffsetY - _offsetY) > 0.5)
            {
                InvalidateArrange();
                RaiseScrollInvalidated(EventArgs.Empty);
            }
        }
    }

    public bool CanHorizontallyScroll
    {
        get => true;
        set { }
    }

    public bool CanVerticallyScroll
    {
        get => true;
        set { }
    }

    public event EventHandler? ScrollInvalidated;

    public VirtualDataGridPanel()
    {
        AddHandler(PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
    }

    private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        var deltaY = -e.Delta.Y * RowHeight * 3;
        var deltaX = -e.Delta.X * 50;

        Offset = new Vector(_offsetX + deltaX, _offsetY + deltaY);
        e.Handled = true;
    }

    protected void RaiseScrollInvalidated(EventArgs e)
    {
        ScrollInvalidated?.Invoke(this, e);
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        _viewport = availableSize;

        if (_itemsSource is not IDataProvider provider)
        {
            _extentHeight = 0;
            _extentWidth = availableSize.Width;
            return base.MeasureOverride(availableSize);
        }

        var itemCount = provider.Count;
        _extentHeight = itemCount * RowHeight;
        _extentWidth = CalculateExtentWidth();

        RecycleContainers();

        var firstVisible = (int)(_offsetY / RowHeight);
        var visibleCount = (int)Math.Ceiling(availableSize.Height / RowHeight) + 2;

        _firstVisibleIndex = Math.Max(0, firstVisible - 1);
        _lastVisibleIndex = Math.Min(itemCount - 1, firstVisible + visibleCount);

        for (var i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var container = GetOrCreateContainer(i);
            if (container != null)
            {
                container.Measure(new Size(_extentWidth, RowHeight));
            }
        }

        return availableSize;
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_itemsSource is not IDataProvider provider)
            return finalSize;

        var arrangedWidth = Math.Max(finalSize.Width, _extentWidth);

        for (var i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var container = GetContainerFromIndex(i);
            if (container != null)
            {
                var y = i * RowHeight - _offsetY;
                var rect = new Rect(-_offsetX, y, arrangedWidth, RowHeight);
                container.Arrange(rect);
            }
        }

        return finalSize;
    }

    protected virtual double CalculateExtentWidth()
    {
        if (this.GetVisualParent() is VirtualDataGrid grid)
        {
            double width = 0;
            foreach (var column in grid.Columns)
            {
                width += column.ActualWidth;
            }
            return Math.Max(width, _viewport.Width);
        }
        return _viewport.Width;
    }

    private Control? GetOrCreateContainer(int index)
    {
        var container = GetContainerFromIndex(index);
        if (container != null)
            return container;

        container = CreateContainerForItem(index);
        if (container != null)
        {
            AddInternalChild(container);
            _containers[index] = container;
        }
        return container;
    }

    private void RecycleContainers()
    {
        var toRemove = new List<int>();

        foreach (var kvp in _containers)
        {
            if (kvp.Key < _firstVisibleIndex || kvp.Key > _lastVisibleIndex)
            {
                toRemove.Add(kvp.Key);
            }
        }

        foreach (var index in toRemove)
        {
            if (_containers.TryGetValue(index, out var container))
            {
                ClearContainerForItem(container);
                RemoveInternalChild(container);
                _containers.Remove(index);
            }
        }
    }

    protected virtual Control? CreateContainerForItem(int index)
    {
        var item = GetItemAtIndex(index);
        if (item == null) return null;

        if (ItemTemplate != null)
        {
            var container = ItemTemplate.Build(item);
            if (container != null)
            {
                container.DataContext = item;
                return container;
            }
        }

        return new ContentControl { Content = item };
    }

    protected virtual void ClearContainerForItem(Control container)
    {
        container.DataContext = null;
    }

    private object? GetItemAtIndex(int index)
    {
        if (_itemsSource is IDataProvider provider && index >= 0 && index < provider.Count)
        {
            if (_items != null && index < _items.Count)
            {
                return _items[index];
            }
        }
        return null;
    }

    private Control? GetContainerFromIndex(int index)
    {
        return _containers.TryGetValue(index, out var container) ? container : null;
    }

    protected override Control? ScrollIntoView(int index)
    {
        if (index < 0) return null;

        var targetOffset = index * RowHeight;
        var maxOffset = Math.Max(0, _extentHeight - _viewport.Height);

        if (targetOffset < _offsetY)
        {
            Offset = new Vector(_offsetX, targetOffset);
        }
        else if (targetOffset + RowHeight > _offsetY + _viewport.Height)
        {
            Offset = new Vector(_offsetX, Math.Min(targetOffset, maxOffset));
        }

        return GetContainerFromIndex(index);
    }

    protected override Control? ContainerFromIndex(int index)
    {
        return GetContainerFromIndex(index);
    }

    protected override IEnumerable<Control> GetRealizedContainers()
    {
        return _containers.Values;
    }

    protected override int IndexFromContainer(Control container)
    {
        foreach (var kvp in _containers)
        {
            if (kvp.Value == container)
                return kvp.Key;
        }
        return -1;
    }

    protected override Control? GetControl(NavigationDirection direction, IInputElement? from, bool wrap)
    {
        return null;
    }
}

internal interface IDataProvider
{
    int Count { get; }
}
