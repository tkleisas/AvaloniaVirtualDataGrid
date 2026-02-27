using System.Collections;
using System.Linq.Expressions;

namespace AvaloniaVirtualDataGrid.Core;

public class InMemoryDataProvider<T> : IDataProvider<T>, IList, IList<T>
{
    private readonly List<T> _items;
    private readonly List<T> _originalItems;
    private readonly List<SortDescription> _sortDescriptions = [];

    public InMemoryDataProvider(IEnumerable<T> items)
    {
        _items = new List<T>(items);
        _originalItems = new List<T>(items);
    }

    public InMemoryDataProvider() : this([])
    {
    }

    public int Count => _items.Count;

    public event EventHandler<DataProviderChangedEventArgs>? DataChanged;

    public IReadOnlyList<SortDescription> SortDescriptions => _sortDescriptions;

    public ValueTask<IReadOnlyList<T>> GetRangeAsync(int startIndex, int count, CancellationToken cancellationToken = default)
    {
        if (startIndex < 0 || startIndex >= _items.Count)
            return new ValueTask<IReadOnlyList<T>>(Array.Empty<T>());

        var actualCount = Math.Min(count, _items.Count - startIndex);
        var result = new T[actualCount];
        _items.CopyTo(startIndex, result, 0, actualCount);
        return new ValueTask<IReadOnlyList<T>>(result);
    }

    public void Sort(string? propertyName, ListSortDirection direction)
    {
        if (string.IsNullOrEmpty(propertyName))
        {
            _sortDescriptions.Clear();
            _items.Clear();
            _items.AddRange(_originalItems);
        }
        else
        {
            var existingIndex = _sortDescriptions.FindIndex(sd => sd.PropertyName == propertyName);
            if (existingIndex >= 0)
            {
                _sortDescriptions.RemoveAt(existingIndex);
            }
            _sortDescriptions.Insert(0, new SortDescription(propertyName, direction));

            ApplySorting();
        }

        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Sorted));
    }

    private void ApplySorting()
    {
        if (_sortDescriptions.Count == 0)
        {
            _items.Clear();
            _items.AddRange(_originalItems);
            return;
        }

        var sorted = _originalItems.AsEnumerable();

        foreach (var sortDesc in _sortDescriptions)
        {
            var propertyName = sortDesc.PropertyName;
            var prop = typeof(T).GetProperty(propertyName!);
            if (prop == null) continue;

            var param = Expression.Parameter(typeof(T), "x");
            var access = Expression.Property(param, prop);
            var lambda = Expression.Lambda<Func<T, object?>>(Expression.Convert(access, typeof(object)), param);
            var getter = lambda.Compile();

            var currentSorted = sorted;
            sorted = sortDesc.Direction == ListSortDirection.Ascending
                ? currentSorted.OrderBy(getter)
                : currentSorted.OrderByDescending(getter);
        }

        _items.Clear();
        _items.AddRange(sorted);
    }

    public void Sort(Comparison<T?> comparison)
    {
        _items.Sort(comparison);
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Sorted));
    }

    public void Sort(IComparer<T?> comparer)
    {
        _items.Sort(comparer);
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Sorted));
    }

    public void Reset(IEnumerable<T> newItems)
    {
        _items.Clear();
        _items.AddRange(newItems);
        _originalItems.Clear();
        _originalItems.AddRange(newItems);
        _sortDescriptions.Clear();
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Reset));
    }

    public void Add(T item)
    {
        _items.Add(item);
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.ItemsAdded, _items.Count - 1, 1));
    }

    public void Insert(int index, T item)
    {
        _items.Insert(index, item);
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.ItemsAdded, index, 1));
    }

    public bool Remove(T item)
    {
        var index = _items.IndexOf(item);
        if (index >= 0)
        {
            _items.RemoveAt(index);
            OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.ItemsRemoved, index, 1));
            return true;
        }
        return false;
    }

    public void RemoveAt(int index)
    {
        _items.RemoveAt(index);
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.ItemsRemoved, index, 1));
    }

    public void Clear()
    {
        _items.Clear();
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Reset));
    }

    public T this[int index]
    {
        get => _items[index];
        set
        {
            _items[index] = value;
            OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.ItemsReplaced, index, 1));
        }
    }

    public int IndexOf(T item) => _items.IndexOf(item);
    public bool Contains(T item) => _items.Contains(item);
    public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
    public bool IsReadOnly => false;
    public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

    protected virtual void OnDataChanged(DataProviderChangedEventArgs e)
    {
        DataChanged?.Invoke(this, e);
    }

    // IList (non-generic) implementation
    object? IList.this[int index]
    {
        get => _items[index];
        set
        {
            if (value is T t)
                this[index] = t;
            else
                throw new ArgumentException($"Value must be of type {typeof(T).Name}", nameof(value));
        }
    }

    bool IList.IsFixedSize => false;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => _items;

    int IList.Add(object? value)
    {
        if (value is T t)
        {
            Add(t);
            return _items.Count - 1;
        }
        throw new ArgumentException($"Value must be of type {typeof(T).Name}", nameof(value));
    }

    bool IList.Contains(object? value) => value is T t && Contains(t);
    int IList.IndexOf(object? value) => value is T t ? IndexOf(t) : -1;
    void IList.Insert(int index, object? value)
    {
        if (value is T t)
            Insert(index, t);
        else
            throw new ArgumentException($"Value must be of type {typeof(T).Name}", nameof(value));
    }
    void IList.Remove(object? value)
    {
        if (value is T t)
            Remove(t);
    }
    void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
}
