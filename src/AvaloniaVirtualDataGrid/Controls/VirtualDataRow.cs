using System.Collections.ObjectModel;
using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Media;

namespace AvaloniaVirtualDataGrid.Controls;

public class VirtualDataRow : ContentControl
{
    public static readonly StyledProperty<bool> IsSelectedProperty =
        AvaloniaProperty.Register<VirtualDataRow, bool>(nameof(IsSelected));

    public static readonly StyledProperty<int> IndexProperty =
        AvaloniaProperty.Register<VirtualDataRow, int>(nameof(Index), -1);

    public bool IsSelected
    {
        get => GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    public int Index
    {
        get => GetValue(IndexProperty);
        set => SetValue(IndexProperty, value);
    }

    public ObservableCollection<VirtualDataCell> Cells { get; }

    private readonly Grid _cellsGrid;

    public VirtualDataRow()
    {
        _cellsGrid = new Grid();
        Content = _cellsGrid;
        Background = Brushes.Transparent;
        BorderBrush = ThemeColors.Separator();
        BorderThickness = new Thickness(0, 0, 0, 1);

        Cells = [];
        Cells.CollectionChanged += OnCellsCollectionChanged;
    }

    private void OnCellsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        UpdateCellsLayout();
    }

    internal void UpdateCellsLayout()
    {
        _cellsGrid.ColumnDefinitions.Clear();
        _cellsGrid.Children.Clear();

        var borderColor = ThemeColors.Separator();

        foreach (var cell in Cells)
        {
            var colDef = new ColumnDefinition
            {
                Width = new GridLength(cell.Column?.ActualWidth ?? 100, GridUnitType.Pixel),
                MinWidth = cell.Column?.MinWidth ?? 20,
                MaxWidth = cell.Column?.MaxWidth ?? double.PositiveInfinity
            };
            _cellsGrid.ColumnDefinitions.Add(colDef);

            cell.BorderBrush = borderColor;
            cell.BorderThickness = new Thickness(0, 0, 1, 0);

            Grid.SetColumn(cell, _cellsGrid.ColumnDefinitions.Count - 1);
            _cellsGrid.Children.Add(cell);
        }
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == IsSelectedProperty)
        {
            UpdateVisualState();
        }
    }

    private void UpdateVisualState()
    {
        if (IsSelected)
        {
            Background = ThemeColors.SelectionBrush();
        }
        else
        {
            Background = Brushes.Transparent;
        }
    }
}
