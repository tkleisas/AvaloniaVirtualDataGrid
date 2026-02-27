using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;
using AvaloniaVirtualDataGrid.Core;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGridHeaderPanel : Grid
{
    private VirtualDataGridColumnCollection? _columns;
    private int _resizingColumnIndex = -1;
    private double _resizeStartX;
    private double _resizeStartWidth;
    private int _draggingColumnIndex = -1;
    private Border? _dragGhost;
    private int _dropTargetIndex = -1;
    private Border? _dropIndicator;
    private Canvas? _overlayCanvas;
    private Point _pressPosition;
    private bool _isDragging;

    public static readonly DirectProperty<VirtualDataGridHeaderPanel, VirtualDataGridColumnCollection?> ColumnsProperty =
        AvaloniaProperty.RegisterDirect<VirtualDataGridHeaderPanel, VirtualDataGridColumnCollection?>(
            nameof(Columns),
            o => o.Columns,
            (o, v) => o.Columns = v);

    public VirtualDataGridColumnCollection? Columns
    {
        get => _columns;
        set
        {
            if (_columns != null)
            {
                _columns.CollectionChanged -= OnColumnsCollectionChanged;
                foreach (var col in _columns)
                {
                    col.ActualWidthChanged -= OnColumnActualWidthChanged;
                }
            }

            SetAndRaise(ColumnsProperty, ref _columns, value);

            if (_columns != null)
            {
                _columns.CollectionChanged += OnColumnsCollectionChanged;
                foreach (var col in _columns)
                {
                    col.ActualWidthChanged += OnColumnActualWidthChanged;
                }
            }

            RebuildHeaders();
        }
    }

    public event EventHandler<ColumnReorderedEventArgs>? ColumnReordered;
    public event EventHandler<ColumnHeaderClickedEventArgs>? HeaderClicked;

    public VirtualDataGridHeaderPanel()
    {
        Height = 32;
        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
    }

    private Canvas GetOverlayCanvas()
    {
        if (_overlayCanvas == null)
        {
            _overlayCanvas = new Canvas
            {
                IsHitTestVisible = false,
                ZIndex = 1000
            };
            Children.Add(_overlayCanvas);
        }
        return _overlayCanvas;
    }

    private void OnColumnsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems != null)
        {
            foreach (VirtualDataGridColumn col in e.NewItems)
            {
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
        RebuildHeaders();
    }

    private void OnColumnActualWidthChanged(object? sender, System.EventArgs e)
    {
        UpdateColumnWidths();
    }

    public void RebuildHeaders()
    {
        ColumnDefinitions.Clear();
        Children.Clear();

        if (_columns == null) return;

        for (int i = 0; i < _columns.Count; i++)
        {
            var column = _columns[i];
            column.CalculateActualWidth(Bounds.Width > 0 ? Bounds.Width : 800);

            var colDef = new ColumnDefinition
            {
                Width = new GridLength(column.ActualWidth, GridUnitType.Pixel),
                MinWidth = column.MinWidth,
                MaxWidth = column.MaxWidth
            };
            ColumnDefinitions.Add(colDef);

            var sortIndicator = CreateSortIndicator(column);
            
            var headerPanel = new DockPanel
            {
                LastChildFill = true
            };

            if (sortIndicator != null)
            {
                DockPanel.SetDock(sortIndicator, Dock.Right);
                headerPanel.Children.Add(sortIndicator);
            }

            var headerText = new TextBlock
            {
                Text = column.Header,
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Left,
                Margin = new Thickness(8, 0),
                FontWeight = FontWeight.SemiBold
            };
            headerPanel.Children.Add(headerText);

            var headerBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = headerPanel,
                Tag = i,
                Cursor = column.IsSortable ? new Cursor(StandardCursorType.Hand) : null
            };

            headerBorder.PointerPressed += OnHeaderPointerPressed;
            headerBorder.PointerMoved += OnHeaderPointerMoved;
            headerBorder.PointerReleased += OnHeaderPointerReleased;

            SetColumn(headerBorder, i);
            Children.Add(headerBorder);

            if (column.IsResizable)
            {
                var gripper = new Border
                {
                    Width = 5,
                    Background = Brushes.Transparent,
                    Cursor = new Cursor(StandardCursorType.SizeWestEast),
                    IsHitTestVisible = true
                };

                gripper.PointerPressed += OnGripperPointerPressed;
                gripper.PointerMoved += OnGripperPointerMoved;
                gripper.PointerReleased += OnGripperPointerReleased;

                SetColumn(gripper, i);
                SetColumnSpan(gripper, 1);
                gripper.HorizontalAlignment = HorizontalAlignment.Right;
                gripper.Margin = new Thickness(0, 0, -2, 0);
                gripper.Tag = i;
                Children.Add(gripper);
            }
        }
    }

    private Control? CreateSortIndicator(VirtualDataGridColumn column)
    {
        if (column.SortDirection == null) return null;

        var arrow = column.SortDirection == ListSortDirection.Ascending ? "▲" : "▼";
        return new TextBlock
        {
            Text = arrow,
            FontSize = 10,
            Foreground = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 8, 0)
        };
    }

    private void OnHeaderPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border header || header.Tag is not int columnIndex)
            return;
        if (_resizingColumnIndex >= 0) return;

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            _draggingColumnIndex = columnIndex;
            _pressPosition = e.GetPosition(this);
            _isDragging = false;
            e.Handled = true;
        }
    }

    private void OnHeaderPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggingColumnIndex < 0 || _columns == null)
            return;

        var position = e.GetPosition(this);
        var delta = position - _pressPosition;

        if (!_isDragging && (Math.Abs(delta.X) > 5 || Math.Abs(delta.Y) > 5))
        {
            _isDragging = true;
        }

        if (!_isDragging) return;

        var canvas = GetOverlayCanvas();
        
        if (_dragGhost == null)
        {
            _dragGhost = new Border
            {
                Background = new SolidColorBrush(Color.FromArgb(200, 240, 240, 240)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(100, 100, 100)),
                BorderThickness = new Thickness(1),
                Child = new TextBlock
                {
                    Text = _columns[_draggingColumnIndex].Header,
                    VerticalAlignment = VerticalAlignment.Center,
                    Margin = new Thickness(8, 0),
                    FontWeight = FontWeight.SemiBold
                },
                IsHitTestVisible = false
            };
            canvas.Children.Add(_dragGhost);
        }

        Canvas.SetLeft(_dragGhost, position.X - _columns[_draggingColumnIndex].ActualWidth / 2);
        Canvas.SetTop(_dragGhost, 0);
        _dragGhost.Width = _columns[_draggingColumnIndex].ActualWidth;
        _dragGhost.Height = 32;

        _dropTargetIndex = GetDropTargetIndex(position.X);
        UpdateDropIndicator();

        e.Handled = true;
    }

    private void OnHeaderPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggingColumnIndex < 0 || _columns == null)
            return;

        if (sender is Border header)
        {
            header.Opacity = 1;
        }

        if (_isDragging)
        {
            if (_dragGhost != null && _overlayCanvas != null)
            {
                _overlayCanvas.Children.Remove(_dragGhost);
                _dragGhost = null;
            }

            if (_dropIndicator != null && _overlayCanvas != null)
            {
                _overlayCanvas.Children.Remove(_dropIndicator);
                _dropIndicator = null;
            }

            if (_dropTargetIndex >= 0 && _dropTargetIndex != _draggingColumnIndex)
            {
                var column = _columns[_draggingColumnIndex];
                _columns.Remove(column);
                _columns.Insert(_dropTargetIndex, column);
                
                ColumnReordered?.Invoke(this, new ColumnReorderedEventArgs(_draggingColumnIndex, _dropTargetIndex));
            }
        }
        else
        {
            var column = _columns[_draggingColumnIndex];
            HeaderClicked?.Invoke(this, new ColumnHeaderClickedEventArgs(column));
        }

        _draggingColumnIndex = -1;
        _dropTargetIndex = -1;
        _isDragging = false;
        e.Handled = true;
    }

    private int GetDropTargetIndex(double x)
    {
        if (_columns == null) return -1;

        double accumulatedWidth = 0;
        for (int i = 0; i < _columns.Count; i++)
        {
            var colWidth = _columns[i].ActualWidth;
            var midPoint = accumulatedWidth + colWidth / 2;

            if (x < midPoint)
                return i;

            accumulatedWidth += colWidth;
        }

        return _columns.Count - 1;
    }

    private void UpdateDropIndicator()
    {
        if (_columns == null) return;
        
        if (_dropIndicator == null)
        {
            _dropIndicator = new Border
            {
                Width = 2,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 215)),
                IsHitTestVisible = false
            };
            GetOverlayCanvas().Children.Add(_dropIndicator);
        }

        double x = 0;
        for (int i = 0; i < _dropTargetIndex; i++)
        {
            x += _columns[i].ActualWidth;
        }
        Canvas.SetLeft(_dropIndicator, x);
        Canvas.SetTop(_dropIndicator, 0);
        _dropIndicator.Height = 32;
    }

    private void OnGripperPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border gripper || gripper.Tag is not int columnIndex)
            return;

        e.Pointer.Capture(gripper);
        _resizingColumnIndex = columnIndex;
        _resizeStartX = e.GetPosition(this).X;
        _resizeStartWidth = _columns![columnIndex].ActualWidth;
        e.Handled = true;
    }

    private void OnGripperPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_resizingColumnIndex < 0 || _columns == null)
            return;

        var currentX = e.GetPosition(this).X;
        var delta = currentX - _resizeStartX;
        var newWidth = _resizeStartWidth + delta;

        var column = _columns[_resizingColumnIndex];
        newWidth = Math.Max(column.MinWidth, Math.Min(column.MaxWidth, newWidth));

        column.Width = newWidth;
        column.ActualWidth = newWidth;

        if (_resizingColumnIndex < ColumnDefinitions.Count)
        {
            ColumnDefinitions[_resizingColumnIndex].Width = new GridLength(newWidth, GridUnitType.Pixel);
        }

        e.Handled = true;
    }

    private void OnGripperPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_resizingColumnIndex >= 0 && sender is Border gripper)
        {
            e.Pointer.Capture(null);
        }
        _resizingColumnIndex = -1;
        e.Handled = true;
    }

    private void UpdateColumnWidths()
    {
        if (_columns == null) return;

        for (int i = 0; i < _columns.Count && i < ColumnDefinitions.Count; i++)
        {
            var column = _columns[i];
            column.CalculateActualWidth(Bounds.Width > 0 ? Bounds.Width : 800);
            ColumnDefinitions[i].Width = new GridLength(column.ActualWidth, GridUnitType.Pixel);
        }
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        UpdateColumnWidths();
        return base.ArrangeOverride(finalSize);
    }
}

public class ColumnReorderedEventArgs : EventArgs
{
    public int OldIndex { get; }
    public int NewIndex { get; }

    public ColumnReorderedEventArgs(int oldIndex, int newIndex)
    {
        OldIndex = oldIndex;
        NewIndex = newIndex;
    }
}

public class ColumnHeaderClickedEventArgs : EventArgs
{
    public VirtualDataGridColumn Column { get; }

    public ColumnHeaderClickedEventArgs(VirtualDataGridColumn column)
    {
        Column = column;
    }
}
