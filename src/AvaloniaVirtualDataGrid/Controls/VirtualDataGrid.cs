using System.Collections;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using AvaloniaVirtualDataGrid.Core;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGrid : TemplatedControl
{
    private VirtualDataGridPanel? _itemsPanel;
    private ScrollViewer? _scrollViewer;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VirtualDataGrid, IEnumerable?>(nameof(ItemsSource));

    public static readonly DirectProperty<VirtualDataGrid, VirtualDataGridColumnCollection> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<VirtualDataGrid, VirtualDataGridColumnCollection>(
            nameof(Columns),
            o => o.Columns,
            (o, v) => o.Columns = v);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<VirtualDataGrid, double>(nameof(RowHeight), 32.0);

    public static readonly StyledProperty<int> SelectedIndexProperty =
        AvaloniaProperty.Register<VirtualDataGrid, int>(nameof(SelectedIndex), -1);

    public static readonly StyledProperty<object?> SelectedItemProperty =
        AvaloniaProperty.Register<VirtualDataGrid, object?>(nameof(SelectedItem));

    private VirtualDataGridColumnCollection _columns = [];

    public IEnumerable? ItemsSource
    {
        get => GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public VirtualDataGridColumnCollection Columns
    {
        get => _columns;
        set => SetAndRaise(ColumnsProperty, ref _columns, value);
    }

    public double RowHeight
    {
        get => GetValue(RowHeightProperty);
        set => SetValue(RowHeightProperty, value);
    }

    public int SelectedIndex
    {
        get => GetValue(SelectedIndexProperty);
        set => SetValue(SelectedIndexProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }

    public event EventHandler<SelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<RoutedEventArgs>? RowDoubleClick;
    public event EventHandler<RoutedEventArgs>? CellClick;

    static VirtualDataGrid()
    {
        ItemsSourceProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnItemsSourceChanged(e));
        SelectedIndexProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnSelectedIndexChanged(e));
        SelectedItemProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnSelectedItemChanged(e));
    }

    public VirtualDataGrid()
    {
        _columns = new VirtualDataGridColumnCollection();
        _columns.CollectionChanged += OnColumnsCollectionChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _itemsPanel = e.NameScope.Find<VirtualDataGridPanel>("PART_ItemsPanel");

        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        if (_itemsPanel != null)
        {
            _itemsPanel.ItemTemplate = CreateItemTemplate();
            UpdatePanelItemsSource();
        }

        SyncColumnWidths();
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
    }

    private IDataTemplate CreateItemTemplate()
    {
        return new FuncDataTemplate<object?>((item, _) =>
        {
            if (item == null) return null;

            var row = new VirtualDataRow
            {
                DataContext = item
            };

            foreach (var column in Columns)
            {
                var cell = new VirtualDataCell
                {
                    Column = column,
                    DataContext = item
                };

                var content = column.CreateCellContent(item);
                cell.Content = content;

                row.Cells.Add(cell);
            }

            return row;
        });
    }

    private void UpdatePanelItemsSource()
    {
        if (_itemsPanel != null)
        {
            _itemsPanel.ItemsSource = ItemsSource;
        }
    }

    private void OnItemsSourceChanged(AvaloniaPropertyChangedEventArgs e)
    {
        UpdatePanelItemsSource();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        SyncColumnWidths();
        if (_itemsPanel != null)
        {
            _itemsPanel.ItemTemplate = CreateItemTemplate();
        }
        InvalidateMeasure();
    }

    private void SyncColumnWidths()
    {
        foreach (var column in Columns)
        {
            column.Owner = this;
        }
    }

    private void OnSelectedIndexChanged(AvaloniaPropertyChangedEventArgs e)
    {
    }

    private void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
    {
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        SyncColumnWidths();
        return result;
    }
}

public class SelectionChangedEventArgs : RoutedEventArgs
{
    public IList AddedItems { get; }
    public IList RemovedItems { get; }

    public SelectionChangedEventArgs(IList addedItems, IList removedItems)
    {
        AddedItems = addedItems;
        RemovedItems = removedItems;
    }
}
