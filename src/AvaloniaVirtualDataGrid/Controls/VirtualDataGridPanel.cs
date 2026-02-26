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
using AvaloniaVirtualDataGrid.Core;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGridPanel : Panel
{
    private int _firstVisibleIndex;
    private int _lastVisibleIndex = -1;
    private readonly Dictionary<int, Control> _containers = [];
    private IEnumerable? _itemsSource;
    private IList? _items;
    private ScrollViewer? _scrollViewer;

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

            _containers.Clear();
            Children.Clear();
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    public VirtualDataGridPanel()
    {
        Background = Avalonia.Media.Brushes.White;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        
        if (_scrollViewer == null)
        {
            FindScrollViewer();
        }
    }

    private void FindScrollViewer()
    {
        _scrollViewer = this.GetVisualAncestors().OfType<ScrollViewer>().FirstOrDefault();
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        
        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged -= OnScrollChanged;
            _scrollViewer = null;
        }
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
        InvalidateArrange();
    }

    private void OnItemsSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        InvalidateMeasure();
        InvalidateArrange();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (_itemsSource is not IDataProvider provider)
        {
            return new Size(0, 0);
        }

        var itemCount = provider.Count;
        var extentHeight = itemCount * RowHeight;
        var extentWidth = CalculateExtentWidth();

        if (double.IsNaN(extentWidth) || extentWidth <= 0)
            extentWidth = 600;

        RecycleContainers();

        if (itemCount == 0)
            return new Size(extentWidth, 0);

        var offsetY = _scrollViewer?.Offset.Y ?? 0;
        var viewportHeight = _scrollViewer?.Viewport.Height ?? availableSize.Height;
        
        if (double.IsInfinity(viewportHeight) || viewportHeight <= 0)
            viewportHeight = 500;

        var firstVisible = (int)(offsetY / RowHeight);
        var visibleCount = (int)Math.Ceiling(viewportHeight / RowHeight) + 2;

        _firstVisibleIndex = Math.Max(0, firstVisible - 1);
        _lastVisibleIndex = Math.Min(itemCount - 1, firstVisible + visibleCount);

        for (var i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var container = GetOrCreateContainer(i);
            if (container != null)
            {
                container.Measure(new Size(extentWidth, RowHeight));
            }
        }

        return new Size(extentWidth, extentHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        if (_itemsSource is not IDataProvider provider)
            return finalSize;

        var arrangedWidth = Math.Max(finalSize.Width, CalculateExtentWidth());

        for (var i = _firstVisibleIndex; i <= _lastVisibleIndex; i++)
        {
            var container = GetContainerFromIndex(i);
            if (container != null)
            {
                var y = i * RowHeight;
                var rect = new Rect(0, y, arrangedWidth, RowHeight);
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
                column.CalculateActualWidth(_scrollViewer?.Viewport.Width ?? 600);
                width += column.ActualWidth;
            }
            if (width > 0)
                return width;
        }
        
        return 600;
    }

    private Control? GetOrCreateContainer(int index)
    {
        var container = GetContainerFromIndex(index);
        if (container != null)
            return container;

        var item = GetItemAtIndex(index);
        if (item == null) 
            return null;

        if (ItemTemplate != null)
        {
            container = ItemTemplate.Build(item);
            if (container != null)
            {
                container.DataContext = item;
            }
        }

        if (container == null)
        {
            container = new ContentControl { Content = item };
        }

        Children.Add(container);
        _containers[index] = container;
        
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
                Children.Remove(container);
                _containers.Remove(index);
            }
        }
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
}
