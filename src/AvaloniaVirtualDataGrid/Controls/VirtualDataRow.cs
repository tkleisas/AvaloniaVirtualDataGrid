using System.Collections.ObjectModel;
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

    public ObservableCollection<VirtualDataCell> Cells { get; } = [];

    private readonly Grid _cellsGrid;

    public VirtualDataRow()
    {
        _cellsGrid = new Grid();
        Content = _cellsGrid;

        UpdateCellsLayout();
    }

    internal void UpdateCellsLayout()
    {
        _cellsGrid.ColumnDefinitions.Clear();
        _cellsGrid.Children.Clear();

        foreach (var cell in Cells)
        {
            var colDef = new ColumnDefinition
            {
                Width = cell.Column?.Width > 0 
                    ? new GridLength(cell.Column.Width, GridUnitType.Pixel) 
                    : GridLength.Auto,
                MinWidth = cell.Column?.MinWidth ?? 20,
                MaxWidth = cell.Column?.MaxWidth ?? double.PositiveInfinity
            };
            _cellsGrid.ColumnDefinitions.Add(colDef);

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
            Background = new SolidColorBrush(Color.FromArgb(30, 0, 120, 215));
        }
        else
        {
            Background = null;
        }
    }
}
