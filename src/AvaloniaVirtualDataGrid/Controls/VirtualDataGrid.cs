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
using AvaloniaVirtualDataGrid.Services;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGrid : TemplatedControl
{
    private VirtualDataGridPanel? _itemsPanel;
    private ScrollViewer? _scrollViewer;
    private VirtualDataGridHeaderPanel? _headerPanel;
    private readonly SelectionService _selectionService;
    private IList? _items;
    private VirtualDataCell? _editingCell;

    public static readonly StyledProperty<IEnumerable?> ItemsSourceProperty =
        AvaloniaProperty.Register<VirtualDataGrid, IEnumerable?>(nameof(ItemsSource));

    public static readonly DirectProperty<VirtualDataGrid, VirtualDataGridColumnCollection> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<VirtualDataGrid, VirtualDataGridColumnCollection>(
            nameof(Columns),
            o => o.Columns,
            (o, v) => o.Columns = v);

    public static readonly StyledProperty<double> RowHeightProperty =
        AvaloniaProperty.Register<VirtualDataGrid, double>(nameof(RowHeight), 32.0);

    public static readonly StyledProperty<DataGridSelectionMode> SelectionModeProperty =
        AvaloniaProperty.Register<VirtualDataGrid, DataGridSelectionMode>(nameof(SelectionMode), DataGridSelectionMode.Single);

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

    public DataGridSelectionMode SelectionMode
    {
        get => GetValue(SelectionModeProperty);
        set => SetValue(SelectionModeProperty, value);
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

    public IReadOnlySet<int> SelectedIndices => _selectionService.SelectedIndices;

    public event EventHandler<DataGridSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<RoutedEventArgs>? RowDoubleClick;
    public event EventHandler<RoutedEventArgs>? CellClick;
    public event EventHandler<CellEditEventArgs>? CellEditCompleted;
    public event EventHandler<DataGridSortingEventArgs>? Sorting;

    static VirtualDataGrid()
    {
        ItemsSourceProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnItemsSourceChanged(e));
        SelectionModeProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnSelectionModeChanged(e));
        SelectedIndexProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnSelectedIndexChanged(e));
        SelectedItemProperty.Changed.AddClassHandler<VirtualDataGrid>((x, e) => x.OnSelectedItemChanged(e));
    }

    public VirtualDataGrid()
    {
        _columns = new VirtualDataGridColumnCollection();
        _columns.CollectionChanged += (s, e) => OnColumnsCollectionChanged(s, e);
        
        _selectionService = new SelectionService();
        _selectionService.SelectionChanged += OnSelectionServiceChanged;
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        _scrollViewer = e.NameScope.Find<ScrollViewer>("PART_ScrollViewer");
        _itemsPanel = e.NameScope.Find<VirtualDataGridPanel>("PART_ItemsPanel");
        _headerPanel = e.NameScope.Find<VirtualDataGridHeaderPanel>("PART_HeaderPanel");

        if (_scrollViewer != null)
        {
            _scrollViewer.ScrollChanged += OnScrollChanged;
        }

        if (_itemsPanel != null)
        {
            _itemsPanel.ItemTemplate = CreateItemTemplate();
            UpdatePanelItemsSource();
        }

        if (_headerPanel != null)
        {
            _headerPanel.ColumnReordered += OnColumnReordered;
            _headerPanel.HeaderClicked += OnHeaderClicked;
        }

        SyncColumnWidths();
        
        AddHandler(PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnColumnReordered(object? sender, ColumnReorderedEventArgs e)
    {
        if (_itemsPanel != null)
        {
            _itemsPanel.ItemTemplate = CreateItemTemplate();
            _itemsPanel.InvalidateMeasure();
        }
    }

    private void OnHeaderClicked(object? sender, ColumnHeaderClickedEventArgs e)
    {
        OnColumnHeaderClick(e.Column);
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_itemsPanel == null) return;

        var point = e.GetCurrentPoint(_itemsPanel);
        if (point.Properties.IsLeftButtonPressed)
        {
            if (e.ClickCount == 2)
            {
                var hitTest = _itemsPanel.InputHitTest(point.Position);
                var cell = (hitTest as Visual)?.FindAncestorOfType<VirtualDataCell>();
                if (cell != null && cell.Column != null)
                {
                    BeginEdit(cell);
                }
            }
            else
            {
                var hitTest = _itemsPanel.InputHitTest(point.Position);
                var row = (hitTest as Visual)?.FindAncestorOfType<VirtualDataRow>();
                
                if (row != null && row.Index >= 0)
                {
                    var modifiers = e.KeyModifiers;
                    var ctrlPressed = (modifiers & KeyModifiers.Control) != 0;
                    var shiftPressed = (modifiers & KeyModifiers.Shift) != 0;
                    
                    _selectionService.HandleClick(row.Index, ctrlPressed, shiftPressed);
                    UpdateRowSelectionStates();
                }
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);

        if (_editingCell != null)
        {
            if (e.Key == Key.Enter)
            {
                CommitEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Escape)
            {
                CancelEdit();
                e.Handled = true;
            }
            else if (e.Key == Key.Tab)
            {
                CommitEdit();
                MoveToNextCell(e.KeyModifiers.HasFlag(KeyModifiers.Shift) ? -1 : 1);
                e.Handled = true;
            }
        }
        else if (e.Key == Key.F2 && _selectionService.SelectedIndex >= 0)
        {
            var row = FindRowAtIndex(_selectionService.SelectedIndex);
            if (row != null && row.Cells.Count > 0)
            {
                BeginEdit(row.Cells[0]);
                e.Handled = true;
            }
        }
    }

    private VirtualDataRow? FindRowAtIndex(int index)
    {
        if (_itemsPanel == null) return null;
        
        foreach (var child in _itemsPanel.Children)
        {
            if (child is VirtualDataRow row && row.Index == index)
                return row;
        }
        return null;
    }

    private void MoveToNextCell(int direction)
    {
        // TODO: Implement navigation between cells
    }

    public void BeginEdit(VirtualDataCell cell)
    {
        if (_editingCell == cell) return;
        
        CommitEdit();
        
        _editingCell = cell;
        cell.IsEditing = true;
    }

    public void CommitEdit()
    {
        if (_editingCell == null) return;
        
        _editingCell.CommitEdit();
        _editingCell = null;
    }

    public void CancelEdit()
    {
        if (_editingCell == null) return;
        
        _editingCell.CancelEdit();
        _editingCell = null;
    }

    private void OnScrollChanged(object? sender, ScrollChangedEventArgs e)
    {
    }

    private IDataTemplate CreateItemTemplate()
    {
        return new FuncDataTemplate<object?>((item, _) =>
        {
            if (item == null) return null;

            var index = _items?.IndexOf(item) ?? -1;

            var row = new VirtualDataRow
            {
                DataContext = item,
                Index = index,
                IsSelected = _selectionService.IsSelected(index)
            };

            foreach (var column in Columns)
            {
                var cell = new VirtualDataCell
                {
                    Column = column,
                    DataContext = item,
                    OwnerGrid = this
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
        _items = e.NewValue as IList;
        _selectionService.SetGetItemFunc(i => _items != null && i >= 0 && i < _items.Count ? _items[i] : null);
        _selectionService.ClearSelection();
        UpdatePanelItemsSource();
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (VirtualDataGridColumn col in e.NewItems)
            {
                col.Owner = this;
                col.ActualWidthChanged -= OnColumnActualWidthChanged;
                col.ActualWidthChanged += OnColumnActualWidthChanged;
            }
        }
        if (e.OldItems != null)
        {
            foreach (VirtualDataGridColumn col in e.OldItems)
            {
                col.ActualWidthChanged -= OnColumnActualWidthChanged;
            }
        }
        if (_itemsPanel != null)
        {
            _itemsPanel.ItemTemplate = CreateItemTemplate();
        }
        InvalidateMeasure();
    }

    private void OnColumnActualWidthChanged(object? sender, EventArgs e)
    {
        UpdateRowLayouts();
    }

    private void SyncColumnWidths()
    {
        foreach (var column in Columns)
        {
            column.Owner = this;
        }
        UpdateRowLayouts();
    }

    private void UpdateRowLayouts()
    {
        if (_itemsPanel == null) return;

        foreach (var child in _itemsPanel.Children)
        {
            if (child is VirtualDataRow row)
            {
                row.UpdateCellsLayout();
            }
        }
    }

    private void OnSelectionModeChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _selectionService.SelectionMode = SelectionMode;
    }

    private void OnSelectedIndexChanged(AvaloniaPropertyChangedEventArgs e)
    {
        if (!_selectionService.IsSelected((int)e.NewValue!))
        {
            _selectionService.SelectedIndex = (int)e.NewValue!;
            UpdateRowSelectionStates();
        }
    }

    private void OnSelectedItemChanged(AvaloniaPropertyChangedEventArgs e)
    {
        _selectionService.SelectedItem = e.NewValue;
        UpdateRowSelectionStates();
    }

    private void OnSelectionServiceChanged(object? sender, DataGridSelectionChangedEventArgs e)
    {
        SelectedIndex = _selectionService.SelectedIndex;
        SelectedItem = _selectionService.SelectedItem;
        
        SelectionChanged?.Invoke(this, e);
    }

    private void UpdateRowSelectionStates()
    {
        if (_itemsPanel == null) return;

        foreach (var child in _itemsPanel.Children)
        {
            if (child is VirtualDataRow row)
            {
                row.IsSelected = _selectionService.IsSelected(row.Index);
            }
        }
    }

    public void SelectAll()
    {
        var count = (ItemsSource as IDataProvider)?.Count ?? _items?.Count ?? 0;
        _selectionService.SelectAll(count);
        UpdateRowSelectionStates();
    }

    public void ClearSelection()
    {
        _selectionService.ClearSelection();
        UpdateRowSelectionStates();
    }

    internal void OnColumnHeaderClick(VirtualDataGridColumn column)
    {
        if (!column.IsSortable) return;

        var sortMemberPath = (column as Columns.VirtualDataGridTextColumn)?.SortMemberPath ?? column.Header;
        
        var args = new DataGridSortingEventArgs(column, sortMemberPath);
        Sorting?.Invoke(this, args);
        
        if (args.Handled) return;

        Core.ListSortDirection? newDirection = column.SortDirection switch
        {
            null => Core.ListSortDirection.Ascending,
            Core.ListSortDirection.Ascending => Core.ListSortDirection.Descending,
            _ => null
        };

        foreach (var col in Columns)
        {
            col.SortDirection = null;
        }

        column.SortDirection = newDirection;
        _headerPanel?.RebuildHeaders();

        if (ItemsSource is Core.IDataProvider provider && newDirection.HasValue)
        {
            provider.Sort(sortMemberPath, newDirection.Value);
        }
        else if (ItemsSource is Core.IDataProvider providerNoSort)
        {
            providerNoSort.Sort(null, Core.ListSortDirection.Ascending);
        }

        if (_itemsPanel != null)
        {
            _itemsPanel.InvalidateMeasure();
        }
    }

    internal void OnCellEdited(VirtualDataCell cell, object? oldValue, object? newValue)
    {
        if (cell.Column == null || cell.DataContext == null) return;

        var rowIndex = _items?.IndexOf(cell.DataContext) ?? -1;
        if (rowIndex < 0) return;

        CellEditCompleted?.Invoke(this, new CellEditEventArgs(
            rowIndex,
            cell.DataContext,
            cell.Column.Header,
            oldValue,
            newValue
        ));
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        var result = base.ArrangeOverride(finalSize);
        SyncColumnWidths();
        return result;
    }
}

public class CellEditEventArgs : EventArgs
{
    public int RowIndex { get; }
    public object? Item { get; }
    public string ColumnName { get; }
    public object? OldValue { get; }
    public object? NewValue { get; }

    public CellEditEventArgs(int rowIndex, object? item, string columnName, object? oldValue, object? newValue)
    {
        RowIndex = rowIndex;
        Item = item;
        ColumnName = columnName;
        OldValue = oldValue;
        NewValue = newValue;
    }
}

public class DataGridSortingEventArgs : EventArgs
{
    public VirtualDataGridColumn Column { get; }
    public string SortMemberPath { get; }
    public bool Handled { get; set; }

    public DataGridSortingEventArgs(VirtualDataGridColumn column, string sortMemberPath)
    {
        Column = column;
        SortMemberPath = sortMemberPath;
    }
}
