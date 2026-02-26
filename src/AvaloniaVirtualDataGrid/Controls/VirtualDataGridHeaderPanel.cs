using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataGridHeaderPanel : Grid
{
    private VirtualDataGridColumnCollection? _columns;

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

    public VirtualDataGridHeaderPanel()
    {
        Height = 32;
        Background = new SolidColorBrush(Color.FromRgb(240, 240, 240));
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

    private void RebuildHeaders()
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

            var headerBorder = new Border
            {
                Background = Brushes.Transparent,
                BorderBrush = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                BorderThickness = new Thickness(0, 0, 1, 0),
                Child = new TextBlock
                {
                    Text = column.Header,
                    VerticalAlignment = VerticalAlignment.Center,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    Margin = new Thickness(8, 0),
                    FontWeight = FontWeight.SemiBold
                }
            };

            SetColumn(headerBorder, i);
            Children.Add(headerBorder);
        }
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
