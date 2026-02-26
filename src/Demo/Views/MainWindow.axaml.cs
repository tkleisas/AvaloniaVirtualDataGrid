using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaVirtualDataGrid.Columns;
using AvaloniaVirtualDataGrid.Controls;
using AvaloniaVirtualDataGrid.Core;
using Demo.Models;

namespace Demo.Views;

public partial class MainWindow : Window
{
    private readonly InMemoryDataProvider<Person> _dataProvider;
    private readonly VirtualDataGrid _dataGrid;

    public MainWindow()
    {
        InitializeComponent();

        _dataProvider = new InMemoryDataProvider<Person>(Person.GenerateSampleData(100000));
        
        _dataGrid = this.FindControl<VirtualDataGrid>("DataGrid")!;
        SetupColumns();
        _dataGrid.ItemsSource = _dataProvider;

        var countText = this.FindControl<TextBlock>("CountText")!;
        countText.Text = $"Showing {_dataProvider.Count:N0} rows";
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupColumns()
    {
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("ID", p => (p as Person)?.Id));
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("First Name", p => (p as Person)?.FirstName));
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("Last Name", p => (p as Person)?.LastName));
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("Email", p => (p as Person)?.Email));
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("Age", p => (p as Person)?.Age));
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("City", p => (p as Person)?.City));
        
        // Progress bar column using template
        _dataGrid.Columns.Add(new VirtualDataGridTemplateColumn
        {
            Header = "Progress",
            CellTemplate = new FuncDataTemplate<object?>((item, _) =>
            {
                if (item is not Person person) return null;
                
                var progress = (person.Age - 18) / 62.0; // Age 18-80 maps to 0-1
                
                return new Grid
                {
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                    Margin = new Thickness(4, 0),
                    Children =
                    {
                        new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(230, 230, 230)),
                            Height = 12,
                            CornerRadius = new CornerRadius(6)
                        },
                        new Border
                        {
                            Background = new SolidColorBrush(Color.FromRgb(0, 150, 136)),
                            Height = 12,
                            CornerRadius = new CornerRadius(6),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left,
                            Width = 100 * progress,
                            Clip = new RectangleGeometry(new Rect(0, 0, 100 * progress, 12))
                        },
                        new TextBlock
                        {
                            Text = $"{(int)(progress * 100)}%",
                            FontSize = 10,
                            FontWeight = FontWeight.SemiBold,
                            Foreground = new SolidColorBrush(progress > 0.5 ? Colors.White : Colors.Gray),
                            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                        }
                    }
                };
            })
        });
    }
}
