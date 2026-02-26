using Avalonia.Controls;
using Avalonia.Markup.Xaml;
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
    }
}
