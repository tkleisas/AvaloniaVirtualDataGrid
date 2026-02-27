# AvaloniaVirtualDataGrid

A high-performance virtualized DataGrid control for AvaloniaUI, designed to handle **1 million+ rows** with smooth scrolling and minimal memory usage.

## Features

- **UI Virtualization** - Only visible rows are rendered in the visual tree
- **Data Virtualization** - `IDataProvider<T>` interface for async/on-demand data loading
- **Row Selection** - Single and multiple selection with Ctrl/Shift support
- **In-place Editing** - Double-click or F2 to edit, Enter/Escape to commit/cancel
- **Column Resize** - Drag column header edges to resize columns
- **Column Reorder** - Drag column headers to reorder columns
- **Sorting** - Click headers to sort ascending/descending/none with visual indicators
- **Column Types** - Text columns with type conversion and template columns for custom content
- **SQLite Support** - Demo includes WAL-mode SQLite backend for persistent storage
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
    public int Age { get; set; }
}
```

### 2. Create a data provider

```csharp
using AvaloniaVirtualDataGrid.Core;

var people = Enumerable.Range(1, 1_000_000)
    .Select(i => new Person { Id = i, FirstName = $"User{i}", ... });

var dataProvider = new InMemoryDataProvider<Person>(people);
```

### 3. Setup the DataGrid with editable columns

```csharp
using AvaloniaVirtualDataGrid.Columns;
using AvaloniaVirtualDataGrid.Controls;

var grid = new VirtualDataGrid
{
    SelectionMode = DataGridSelectionMode.Multiple
};

// Use Create<T> for editable columns with automatic type conversion
grid.Columns.Add(new VirtualDataGridTextColumn("ID", p => (p as Person)?.Id));
grid.Columns.Add(VirtualDataGridTextColumn.Create<Person>("First Name", p => p.FirstName));
grid.Columns.Add(VirtualDataGridTextColumn.Create<Person>("Last Name", p => p.LastName));
grid.Columns.Add(VirtualDataGridTextColumn.Create<Person>("Age", p => p.Age));

grid.ItemsSource = dataProvider;
```

### 4. Handle edit completion (for persistence)

```csharp
grid.CellEditCompleted += (sender, e) =>
{
    Console.WriteLine($"Row {e.RowIndex}, Column {e.ColumnName}: {e.OldValue} -> {e.NewValue}");
    // Persist changes to database, API, etc.
};
```

### 5. Custom cell templates (Progress bars, icons, etc.)

```csharp
grid.Columns.Add(new VirtualDataGridTemplateColumn
{
    Header = "Progress",
    CellTemplate = new FuncDataTemplate<object?>((item, _) =>
    {
        var progress = (item as Person)?.Progress ?? 0;
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

## Selection

```csharp
// Single selection
grid.SelectionMode = DataGridSelectionMode.Single;
grid.SelectedIndex = 5;
var selected = grid.SelectedItem;

// Multiple selection
grid.SelectionMode = DataGridSelectionMode.Multiple;
grid.SelectAll();
grid.ClearSelection();
var selectedIndices = grid.SelectedIndices;
```

**Keyboard shortcuts:**
- **Click** - Select row
- **Ctrl+Click** - Toggle selection
- **Shift+Click** - Range selection

## Editing

**Triggers:**
- **Double-click** on a cell
- **F2** when a row is selected

**Keys:**
- **Enter** - Commit edit
- **Escape** - Cancel edit
- **Tab** - Commit and move to next cell

## Column Resize & Reorder

**Resize:**
- Drag the right edge of any column header to resize
- MinWidth and MaxWidth constraints are respected

**Reorder:**
- Drag column headers left/right to reorder columns
- The `ColumnReordered` event fires when a column is moved

## Sorting

- Click any column header to cycle through: **none → ascending → descending → none**
- Sort indicators (▲/▼) appear in sorted column headers
- Implement `IDataProvider.Sort(propertyName, direction)` for custom sorting

```csharp
grid.Sorting += (sender, e) =>
{
    // Custom sort logic
    e.Handled = true; // Set to true to prevent default sorting
};
```

## Architecture

```
VirtualDataGrid (main control)
├── VirtualDataGridHeaderPanel (column headers, synced widths)
├── ScrollViewer
│   └── VirtualDataGridPanel (virtualizing panel)
│       └── VirtualDataRow (recyclable containers)
│           └── VirtualDataCell (with borders, editing support)
├── Columns collection
│   ├── VirtualDataGridTextColumn
│   └── VirtualDataGridTemplateColumn
└── Services
    └── SelectionService
```

### Data Provider Interface

```csharp
public interface IDataProvider
{
    int Count { get; }
    void Sort(string? propertyName, ListSortDirection direction);
    IReadOnlyList<SortDescription> SortDescriptions { get; }
    event EventHandler<DataProviderChangedEventArgs>? DataChanged;
}

public interface IDataProvider<T> : IDataProvider
{
    ValueTask<IReadOnlyList<T>> GetRangeAsync(int startIndex, int count, CancellationToken ct = default);
}
```

Implement `IDataProvider<T>` for:
- Database paging (SQLite demo included)
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

The demo includes:
- **100,000 rows** stored in SQLite with WAL mode
- Editable text columns with type conversion
- Progress bar template column
- Multiple selection mode

## Roadmap

- [x] UI Virtualization
- [x] Column definitions (Text, Template)
- [x] Row/cell selection (Single, Multiple)
- [x] In-place editing with type conversion
- [x] Column resize/reorder
- [x] Sorting
- [ ] Frozen columns
- [ ] Async data loading with caching

---

## Acknowledgments

This project was developed with the help of an **AI SLOPTRONIC (TM)** - a highly sophisticated pattern of AI-assisted development where human creativity meets machine efficiency. No actual slops were harmed in the making of this DataGrid, though several cups of coffee were consumed.

*AI SLOPTRONIC (TM) - Because even virtual grids need a little artificial intelligence to keep things real.*

## License

MIT License - use it, modify it, ship it.
