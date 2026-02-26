# AvaloniaVirtualDataGrid Implementation Plan

## Overview

A virtualized DataGrid control for AvaloniaUI supporting 1M+ rows with UI and data virtualization.

## Target
- **.NET**: 10
- **AvaloniaUI**: 11.2.x (latest stable)

## Requirements

| Feature | Status |
|---------|--------|
| UI Virtualization | Phase 1 |
| Data Virtualization | Phase 7 |
| Row/Cell Selection | Phase 3 |
| In-place Editing | Phase 4 |
| Sorting | Phase 6 |
| Column Resize/Reorder | Phase 5 |
| Frozen Columns | Not in v1 |
| Edit Tracking | Not needed |

## Architecture

### Core Interfaces

```
IDataProvider<T>          - Async data virtualization contract
  ├─ Count property
  ├─ GetRangeAsync(start, count)
  ├─ SortAsync(comparer)
  └─ DataChanged event

IColumn<T>                - Column definition contract  
  ├─ Header, Width, IsResizable, etc.
  ├─ CreateCellContent() / CreateEditContent()
  └─ Binding/Value accessor
```

### Main Components

```
VirtualDataGrid           - Main control (templated)
  ├─ Columns collection
  ├─ ItemsSource (IDataProvider<T>)
  ├─ SelectionService
  └─ EditService

VirtualDataGridPanel      - Custom IVirtualizingPanel
  ├─ Viewport management
  ├─ Container recycling pool
  └─ Scroll synchronization

VirtualDataRow            - Row container (recyclable)
VirtualDataCell           - Cell container (recyclable)
```

## Project Structure

```
AvaloniaVirtualDataGrid/
├─ Core/
│  ├─ IDataProvider.cs
│  ├─ InMemoryDataProvider.cs      - Basic IList wrapper
│  └─ SortedDataProvider.cs        - Sorting wrapper
├─ Controls/
│  ├─ VirtualDataGrid.cs
│  ├─ VirtualDataGridPanel.cs
│  ├─ VirtualDataRow.cs
│  └─ VirtualDataCell.cs
├─ Columns/
│  ├─ VirtualDataGridColumn.cs     - Abstract base
│  ├─ VirtualDataGridTextColumn.cs
│  └─ VirtualDataGridTemplateColumn.cs
├─ Services/
│  ├─ SelectionService.cs
│  └─ EditService.cs
└─ Themes/
   └─ Generic.axaml
```

## Implementation Phases

### Phase 1: Core Infrastructure

**Files to create:**
1. `Core/IDataProvider.cs` - Async data provider interface
2. `Core/InMemoryDataProvider.cs` - Simple `IList<T>` wrapper for testing
3. `Controls/VirtualDataGridPanel.cs` - Custom panel implementing `IVirtualizingPanel`
4. `Controls/VirtualDataGrid.cs` - Main control shell

**Key decisions:**
- Row height: Fixed (configurable, default 32px) for simpler v1
- Scroll: Use `IScrollable` pattern with virtual extent

### Phase 2: Columns & Cells

**Files to create:**
1. `Columns/VirtualDataGridColumn.cs` - Abstract base
2. `Columns/VirtualDataGridTextColumn.cs` - Text binding column
3. `Columns/VirtualDataGridTemplateColumn.cs` - Custom template column
4. `Controls/VirtualDataRow.cs` - Row container
5. `Controls/VirtualDataCell.cs` - Cell container

**Key decisions:**
- Columns defined in code: `grid.Columns.Add(new VirtualDataGridTextColumn<Person>("Name", p => p.Name))`
- Width: Auto, Pixel, Star support

### Phase 3: Selection

**Files to create:**
1. `Services/SelectionService.cs`

**Features:**
- SelectionMode: None, Single, Multiple
- SelectionUnit: Cell, Row
- Keyboard: Shift+Click (range), Ctrl+Click (toggle)
- Properties: `SelectedItem`, `SelectedItems`, `SelectedIndex`

### Phase 4: Editing

**Files to create:**
1. `Services/EditService.cs`

**Behavior:**
- Enter/F2/DblClick → edit mode
- Enter/Tab → commit, Escape → cancel
- Direct write to source (no undo stack)
- Validation via `IDataErrorInfo` or callback

### Phase 5: Column Resize/Reorder

**Features:**
- Drag column header edge to resize
- MinWidth/MaxWidth constraints
- Drag column header to reorder
- `ColumnReordered` event

### Phase 6: Sorting

**Files to modify:**
- `Core/IDataProvider.cs` - Add `SortAsync`
- `Core/InMemoryDataProvider.cs` - Implement sorting

**Behavior:**
- Click header → sort asc/desc/none cycle
- Sort arrow indicator in header
- `Sorting` event (cancelable)

### Phase 7: Async Data Loading

**Files to create:**
1. `Core/AsyncDataProvider.cs` - With caching and prefetch

**Features:**
- Load only visible + buffer rows
- Background fetch with cancellation
- Loading placeholder while fetching
- Cache recently accessed ranges

## Testing Strategy

- Unit tests for `IDataProvider` implementations
- Integration tests with 100K+ mock rows
- Performance profiling for scroll smoothness
