using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AvaloniaVirtualDataGrid.Columns;
using AvaloniaVirtualDataGrid.Controls;
using Demo.Data;

namespace Demo.Views;

public partial class MainWindow : Window
{
    private SqliteDataProvider? _sqliteProvider;
    private AvaloniaVirtualDataGrid.Core.InMemoryDataProvider<PersonRecord>? _memoryProvider;
    private readonly VirtualDataGrid _dataGrid;
    private bool _useSqlite = true;
    private string _dbPath = "people.db";

    public MainWindow()
    {
        InitializeComponent();

        _dataGrid = this.FindControl<VirtualDataGrid>("DataGrid")!;
        
        if (_useSqlite)
        {
            InitializeSqlite();
        }
        else
        {
            InitializeMemoryData();
        }
    }

    private void InitializeSqlite()
    {
        var dbExists = File.Exists(_dbPath);
        _sqliteProvider = new SqliteDataProvider(_dbPath);

        if (!dbExists)
        {
            _sqliteProvider.PopulateData(100000);
        }

        SetupColumns();
        _dataGrid.ItemsSource = _sqliteProvider;
        _dataGrid.CellEditCompleted += OnCellEditCompleted;

        var countText = this.FindControl<TextBlock>("CountText")!;
        countText.Text = $"SQLite: {_sqliteProvider.Count:N0} rows (WAL mode)";
    }

    private void OnCellEditCompleted(object? sender, CellEditEventArgs e)
    {
        if (_sqliteProvider != null && e.Item is PersonRecord record)
        {
            // Map column header to property name
            var propertyName = e.ColumnName.Replace(" ", "");
            _sqliteProvider.Update(e.RowIndex, propertyName, e.NewValue);
            Console.WriteLine($"Updated row {e.RowIndex}, column {e.ColumnName}: {e.OldValue} -> {e.NewValue}");
        }
    }

    private void InitializeMemoryData()
    {
        var people = GenerateMemoryData(100000);
        _memoryProvider = new AvaloniaVirtualDataGrid.Core.InMemoryDataProvider<PersonRecord>(people);
        
        SetupColumns();
        _dataGrid.ItemsSource = _memoryProvider;

        var countText = this.FindControl<TextBlock>("CountText")!;
        countText.Text = $"Memory: {_memoryProvider.Count:N0} rows";
    }

    private static IEnumerable<PersonRecord> GenerateMemoryData(int count)
    {
        var firstNames = new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin" };
        var cities = new[] { "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose" };

        var random = new Random(42);

        for (int i = 1; i <= count; i++)
        {
            yield return new PersonRecord
            {
                Id = i,
                FirstName = firstNames[random.Next(firstNames.Length)],
                LastName = lastNames[random.Next(lastNames.Length)],
                Email = $"user{i}@example.com",
                Age = random.Next(18, 80),
                City = cities[random.Next(cities.Length)],
                Progress = random.NextDouble()
            };
        }
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void SetupColumns()
    {
        _dataGrid.Columns.Add(new VirtualDataGridTextColumn("ID", p => (p as PersonRecord)?.Id));
        _dataGrid.Columns.Add(VirtualDataGridTextColumn.Create<PersonRecord>("First Name", p => p.FirstName));
        _dataGrid.Columns.Add(VirtualDataGridTextColumn.Create<PersonRecord>("Last Name", p => p.LastName));
        _dataGrid.Columns.Add(VirtualDataGridTextColumn.Create<PersonRecord>("Email", p => p.Email));
        _dataGrid.Columns.Add(VirtualDataGridTextColumn.Create<PersonRecord>("Age", p => p.Age));
        _dataGrid.Columns.Add(VirtualDataGridTextColumn.Create<PersonRecord>("City", p => p.City));
        
        _dataGrid.Columns.Add(new VirtualDataGridTemplateColumn
        {
            Header = "Progress",
            CellTemplate = new FuncDataTemplate<object?>((item, _) =>
            {
                if (item is not PersonRecord person) return null;
                
                var progress = person.Progress;
                
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

    protected override void OnClosing(WindowClosingEventArgs e)
    {
        _sqliteProvider?.Close();
        base.OnClosing(e);
    }
}
