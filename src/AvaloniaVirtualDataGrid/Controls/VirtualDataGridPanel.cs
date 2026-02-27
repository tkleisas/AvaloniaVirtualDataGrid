using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.VisualTree;
using AvaloniaVirtualDataGrid.Core;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGridPanel : Panel
{
    private int _firstVisibleIndex;
    private int _lastVisibleIndex = -1;
    private readonly Dictionary<int, Control> _containers = [];
    private readonly HashSet<int> _loadingIndices = [];
    private IEnumerable? _itemsSource;
    private IList? _items;
    private ScrollViewer? _scrollViewer;
    private int _prefetchBuffer = 50;

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<VirtualDataGridPanel, double>(nameof(RowHeight), 32.0);

    public static readonly StyledProperty<IDataTemplate?> ItemTemplateProperty =
        AvaloniaProperty.Register<VirtualDataGridPanel, IDataTemplate?>(nameof(ItemTemplate));

    public static readonly StyledProperty<IDataTemplate?> LoadingTemplateProperty =
        AvaloniaProperty.Register<VirtualDataGridPanel, IDataTemplate?>(nameof(LoadingTemplate));

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

    public IDataTemplate? LoadingTemplate
    {
        get => GetValue(LoadingTemplateProperty);
        set => SetValue(LoadingTemplateProperty, value);
    }

    public int PrefetchBuffer
    {
        get => _prefetchBuffer;
        set => _prefetchBuffer = value;
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
            if (_itemsSource is IDataProvider oldProvider)
            {
                oldProvider.DataChanged -= OnDataProviderDataChanged;
            }

            _itemsSource = value;
            _items = value as IList;

            if (_itemsSource is INotifyCollectionChanged newCollection)
            {
                newCollection.CollectionChanged += OnItemsSourceCollectionChanged;
            }
            if (_itemsSource is IDataProvider newProvider)
            {
                newProvider.DataChanged += OnDataProviderDataChanged;
            }

            _containers.Clear();
            Children.Clear();
            InvalidateMeasure();
            InvalidateArrange();
        }
    }

    private void OnDataProviderDataChanged(object? sender, DataProviderChangedEventArgs e)
    {
        _containers.Clear();
        _loadingIndices.Clear();
        Children.Clear();
        _firstVisibleIndex = 0;
        _lastVisibleIndex = -1;
        InvalidateMeasure();
        InvalidateArrange();
    }

    public VirtualDataGridPanel()
    {
        Background = Brushes.White;
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
        InvalidateMeasure();
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

        PrefetchData(firstVisible, visibleCount);

        return new Size(extentWidth, extentHeight);
    }

    private void PrefetchData(int firstVisible, int visibleCount)
    {
        if (_itemsSource is not AsyncDataProvider<object> asyncProvider)
            return;

        var prefetchStart = Math.Max(0, firstVisible + visibleCount);
        var prefetchEnd = Math.Min(asyncProvider.Count - 1, prefetchStart + _prefetchBuffer);

        if (prefetchStart <= prefetchEnd)
        {
            asyncProvider.Prefetch(prefetchStart, prefetchEnd - prefetchStart + 1);
        }
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
        {
            if (container is VirtualDataRow row)
            {
                row.Index = index;
                UpdateRowSelectionState(row, index);
            }
            return container;
        }

        var item = GetItemAtIndex(index);
        
        if (item == null)
        {
            if (_loadingIndices.Contains(index))
            {
                return CreateLoadingContainer(index);
            }

            if (_itemsSource is IDataProvider provider && index >= 0 && index < provider.Count)
            {
                TriggerAsyncLoad(index);
                return CreateLoadingContainer(index);
            }

            return null;
        }

        if (ItemTemplate != null)
        {
            container = ItemTemplate.Build(item);
            if (container != null)
            {
                container.DataContext = item;
                if (container is VirtualDataRow row)
                {
                    row.Index = index;
                    UpdateRowSelectionState(row, index);
                }
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

    private Control CreateLoadingContainer(int index)
    {
        Control container;
        
        if (LoadingTemplate != null)
        {
            container = LoadingTemplate.Build(index);
        }
        else
        {
            container = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(245, 245, 245)),
                Child = new TextBlock
                {
                    Text = "Loading...",
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    Foreground = new SolidColorBrush(Color.FromRgb(150, 150, 150))
                }
            };
        }

        if (container is VirtualDataRow row)
        {
            row.Index = index;
        }

        Children.Add(container);
        _containers[index] = container;
        
        return container;
    }

    private void TriggerAsyncLoad(int index)
    {
        if (_loadingIndices.Contains(index))
            return;

        _loadingIndices.Add(index);

        Task.Run(async () =>
        {
            try
            {
                if (_itemsSource is IDataProvider provider && _items != null)
                {
                    var count = Math.Min(50, provider.Count - index);
                    if (count > 0 && _items is IDataProvider<object> asyncProvider)
                    {
                        await asyncProvider.GetRangeAsync(index, count);
                    }
                }
            }
            finally
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    _loadingIndices.Remove(index);
                    if (_containers.TryGetValue(index, out var container))
                    {
                        Children.Remove(container);
                        _containers.Remove(index);
                    }
                    InvalidateMeasure();
                    InvalidateArrange();
                });
            }
        });
    }

    private void UpdateRowSelectionState(VirtualDataRow row, int index)
    {
        if (this.GetVisualParent() is VirtualDataGrid grid)
        {
            row.IsSelected = grid.SelectedIndices.Contains(index);
        }
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
