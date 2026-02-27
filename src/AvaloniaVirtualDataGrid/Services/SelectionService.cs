using System.Collections;
using System.Collections.Specialized;

namespace AvaloniaVirtualDataGrid.Services;

public enum DataGridSelectionMode
{
    None,
    Single,
    Multiple
}

public class SelectionService : INotifyCollectionChanged
{
    private readonly HashSet<int> _selectedIndices = [];
    private int _selectedIndex = -1;
    private object? _selectedItem;
    private DataGridSelectionMode _selectionMode = DataGridSelectionMode.Single;
    private int _anchorIndex = -1;
    private Func<int, object?>? _getItemAt;

    public event EventHandler<DataGridSelectionChangedEventArgs>? SelectionChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public DataGridSelectionMode SelectionMode
    {
        get => _selectionMode;
        set
        {
            if (_selectionMode != value)
            {
                _selectionMode = value;
                if (value == DataGridSelectionMode.None)
                {
                    ClearSelection();
                }
                else if (value == DataGridSelectionMode.Single && _selectedIndices.Count > 1)
                {
                    var first = _selectedIndices.First();
                    ClearSelection();
                    SelectIndex(first);
                }
            }
        }
    }

    public int SelectedIndex
    {
        get => _selectedIndex;
        set
        {
            if (_selectedIndex != value)
            {
                var oldIndex = _selectedIndex;
                var oldItem = _selectedItem;

                ClearSelection();
                if (value >= 0)
                {
                    SelectIndex(value);
                }

                OnSelectionChanged(
                    _selectedItem != null ? new[] { _selectedItem } : Array.Empty<object>(),
                    oldItem != null ? new[] { oldItem } : Array.Empty<object>()
                );
            }
        }
    }

    public object? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (!Equals(_selectedItem, value))
            {
                var oldItem = _selectedItem;

                ClearSelection();
                if (value != null && _getItemAt != null)
                {
                    var index = FindIndexForItem(value);
                    if (index >= 0)
                    {
                        SelectIndex(index);
                    }
                }

                OnSelectionChanged(
                    _selectedItem != null ? new[] { _selectedItem } : Array.Empty<object>(),
                    oldItem != null ? new[] { oldItem } : Array.Empty<object>()
                );
            }
        }
    }

    public IReadOnlySet<int> SelectedIndices => _selectedIndices;

    public IEnumerable<object> SelectedItems => _selectedIndices
        .Where(i => _getItemAt != null)
        .Select(i => _getItemAt!(i))
        .Where(item => item != null)!;

    public void SetGetItemFunc(Func<int, object?> getItemAt)
    {
        _getItemAt = getItemAt;
    }

    public bool IsSelected(int index)
    {
        return _selectedIndices.Contains(index);
    }

    public void SelectIndex(int index)
    {
        if (index < 0) return;

        if (_selectionMode == DataGridSelectionMode.None) return;

        if (_selectionMode == DataGridSelectionMode.Single && _selectedIndices.Count > 0)
        {
            _selectedIndices.Clear();
        }

        if (_selectedIndices.Add(index))
        {
            _selectedIndex = index;
            _selectedItem = _getItemAt?.Invoke(index);
            _anchorIndex = index;
            RaiseCollectionChanged();
        }
    }

    public void DeselectIndex(int index)
    {
        if (_selectedIndices.Remove(index))
        {
            if (_selectedIndex == index)
            {
                _selectedIndex = _selectedIndices.Count > 0 ? _selectedIndices.First() : -1;
                _selectedItem = _selectedIndex >= 0 ? _getItemAt?.Invoke(_selectedIndex) : null;
            }
            RaiseCollectionChanged();
        }
    }

    public void ToggleIndex(int index)
    {
        if (_selectedIndices.Contains(index))
        {
            DeselectIndex(index);
        }
        else
        {
            SelectIndex(index);
        }
    }

    public void SelectRange(int fromIndex, int toIndex)
    {
        if (_selectionMode != DataGridSelectionMode.Multiple) return;

        var start = Math.Min(fromIndex, toIndex);
        var end = Math.Max(fromIndex, toIndex);

        for (int i = start; i <= end; i++)
        {
            _selectedIndices.Add(i);
        }

        _selectedIndex = start;
        _selectedItem = _getItemAt?.Invoke(start);
        RaiseCollectionChanged();
    }

    public void SelectTo(int index)
    {
        if (_anchorIndex < 0)
        {
            SelectIndex(index);
            return;
        }

        ClearSelection();
        SelectRange(_anchorIndex, index);
    }

    public void ClearSelection()
    {
        var hadSelection = _selectedIndices.Count > 0;
        _selectedIndices.Clear();
        _selectedIndex = -1;
        _selectedItem = null;

        if (hadSelection)
        {
            RaiseCollectionChanged();
        }
    }

    public void SelectAll(int totalCount)
    {
        if (_selectionMode != DataGridSelectionMode.Multiple) return;

        _selectedIndices.Clear();
        for (int i = 0; i < totalCount; i++)
        {
            _selectedIndices.Add(i);
        }

        if (_selectedIndices.Count > 0)
        {
            _selectedIndex = 0;
            _selectedItem = _getItemAt?.Invoke(0);
        }

        RaiseCollectionChanged();
    }

    public void HandleClick(int index, bool ctrlPressed, bool shiftPressed)
    {
        if (_selectionMode == DataGridSelectionMode.None) return;

        if (_selectionMode == DataGridSelectionMode.Single)
        {
            if (!IsSelected(index) || ctrlPressed || shiftPressed)
            {
                SelectedIndex = index;
            }
        }
        else if (_selectionMode == DataGridSelectionMode.Multiple)
        {
            if (ctrlPressed && !shiftPressed)
            {
                ToggleIndex(index);
            }
            else if (shiftPressed)
            {
                SelectTo(index);
            }
            else
            {
                ClearSelection();
                SelectIndex(index);
            }
        }
    }

    private int FindIndexForItem(object item)
    {
        return -1;
    }

    private void RaiseCollectionChanged()
    {
        CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }

    protected virtual void OnSelectionChanged(IList addedItems, IList removedItems)
    {
        SelectionChanged?.Invoke(this, new DataGridSelectionChangedEventArgs(addedItems, removedItems));
    }
}

public class DataGridSelectionChangedEventArgs : EventArgs
{
    public IList AddedItems { get; }
    public IList RemovedItems { get; }

    public DataGridSelectionChangedEventArgs(IList addedItems, IList removedItems)
    {
        AddedItems = addedItems;
        RemovedItems = removedItems;
    }
}
