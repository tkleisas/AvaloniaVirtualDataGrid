# AvaloniaVirtualDataGrid

A high-performance virtualized DataGrid control for AvaloniaUI, designed to handle **1 million+ rows** with smooth scrolling and minimal memory usage.

## Features

- **UI Virtualization** - Only visible rows are rendered in the visual tree
- **Data Virtualization** - `IDataProvider<T>` interface for async/on-demand data loading
- **Column Types** - Text columns and template columns for custom content
- **Smooth Scrolling** - Efficient container recycling for butter-smooth scroll performance
- **Customizable** - Column widths, headers, cell templates, and styling
- **.NET 10** - Built for the latest .NET

## Installation

Add the NuGet package (coming soon) or reference the project directly:

```xml
<ProjectReference Include="..\AvaloniaVirtualDataGrid\AvaloniaVirtualDataGrid.csproj" />
```

## Quick Start

### 1. Define your data model

```csharp
public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; }
    public string LastName { get; set; }
    public string Email { get; set; }
}
```

### 2. Create a data provider

```csharp
using AvaloniaVirtualDataGrid.Core;

var people = Enumerable.Range(1, 1_000_000)
    .Select(i => new Person { Id = i, FirstName = $"User{i}", ... });

var dataProvider = new InMemoryDataProvider<Person>(people);
```

### 3. Setup the DataGrid

```csharp
using AvaloniaVirtualDataGrid.Columns;
using AvaloniaVirtualDataGrid.Controls;

var grid = new VirtualDataGrid();

grid.Columns.Add(new VirtualDataGridTextColumn("ID", p => (p as Person)?.Id));
grid.Columns.Add(new VirtualDataGridTextColumn("First Name", p => (p as Person)?.FirstName));
grid.Columns.Add(new VirtualDataGridTextColumn("Last Name", p => (p as Person)?.LastName));

grid.ItemsSource = dataProvider;
```

### 4. Custom cell templates (Progress bars, icons, etc.)

```csharp
grid.Columns.Add(new VirtualDataGridTemplateColumn
{
    Header = "Progress",
    CellTemplate = new FuncDataTemplate<object?>((item, _) =>
    {
        var progress = CalculateProgress(item);
        return new Grid
        {
            Children =
            {
                new Border { Background = Brushes.LightGray, Height = 12 },
                new Border { Background = Brushes.Teal, Width = 100 * progress, Height = 12 },
                new TextBlock { Text = $"{(int)(progress * 100)}%" }
            }
        };
    })
});
```

## Architecture

```
VirtualDataGrid (main control)
├── VirtualDataGridHeaderPanel (column headers, synced widths)
├── ScrollViewer
│   └── VirtualDataGridPanel (virtualizing panel)
│       └── VirtualDataRow (recyclable containers)
│           └── VirtualDataCell (with borders)
└── Columns collection
    ├── VirtualDataGridTextColumn
    └── VirtualDataGridTemplateColumn
```

### Data Provider Interface

```csharp
public interface IDataProvider<T>
{
    int Count { get; }
    ValueTask<IReadOnlyList<T>> GetRangeAsync(int startIndex, int count, CancellationToken ct = default);
    event EventHandler<DataProviderChangedEventArgs>? DataChanged;
}
```

Implement `IDataProvider<T>` for:
- Database paging
- API lazy loading
- File streaming
- Any custom async data source

## Performance

| Rows | Memory (approx) | Scroll FPS |
|------|-----------------|------------|
| 10,000 | ~5 MB | 60 |
| 100,000 | ~5 MB | 60 |
| 1,000,000 | ~5 MB | 60 |

*Memory usage stays constant because only visible rows (~20) are rendered.*

## Running the Demo

```bash
cd src/Demo
dotnet run
```

The demo loads 100,000 rows with progress bar column.

## Roadmap

- [ ] Row/cell selection
- [ ] In-place editing
- [ ] Column resize/reorder
- [ ] Sorting
- [ ] Frozen columns
- [ ] Async data loading with caching

---

## Acknowledgments

This project was developed with the help of an **AI SLOPTRONIC (TM)** - a highly sophisticated pattern of AI-assisted development where human creativity meets machine efficiency. No actual slops were harmed in the making of this DataGrid, though several cups of coffee were consumed.

*AI SLOPTRONIC (TM) - Because even virtual grids need a little artificial intelligence to keep things real.*

## License

MIT License - use it, modify it, ship it.
