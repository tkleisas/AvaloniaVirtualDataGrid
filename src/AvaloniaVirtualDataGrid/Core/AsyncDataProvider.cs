using System.Collections;
using System.Collections.Concurrent;

namespace AvaloniaVirtualDataGrid.Core;

public class AsyncDataProvider<T> : IDataProvider, IList
{
    private readonly Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> _fetchFunc;
    private readonly Func<Task<int>> _countFunc;
    private readonly ConcurrentDictionary<int, T> _cache = new();
    private readonly HashSet<int> _loadingRanges = [];
    private readonly object _lock = new();
    private int _count = -1;
    private bool _isLoading;
    private CancellationTokenSource? _currentLoadCts;
    private readonly List<SortDescription> _sortDescriptions = [];
    private string? _sortProperty;
    private ListSortDirection _sortDirection;

    public event EventHandler<DataProviderChangedEventArgs>? DataChanged;
    public event EventHandler<AsyncDataLoadingEventArgs>? LoadingStateChanged;

    public int Count
    {
        get
        {
            if (_count < 0)
            {
                Task.Run(async () => await EnsureCountAsync()).GetAwaiter().GetResult();
            }
            return _count;
        }
    }

    public bool IsLoading => _isLoading;
    public IReadOnlyList<SortDescription> SortDescriptions => _sortDescriptions;

    public AsyncDataProvider(
        Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> fetchFunc,
        Func<Task<int>> countFunc)
    {
        _fetchFunc = fetchFunc;
        _countFunc = countFunc;
    }

    public AsyncDataProvider(
        Func<int, int, CancellationToken, Task<IReadOnlyList<T>>> fetchFunc,
        int knownCount)
    {
        _fetchFunc = fetchFunc;
        _count = knownCount;
        _countFunc = () => Task.FromResult(knownCount);
    }

    public async Task EnsureCountAsync()
    {
        if (_count < 0)
        {
            _count = await _countFunc();
        }
    }

    public async Task<IReadOnlyList<T>> GetRangeAsync(int startIndex, int count, CancellationToken cancellationToken = default)
    {
        var result = new List<T>(count);
        var missingRanges = new List<(int start, int count)>();
        var currentRangeStart = -1;
        var currentRangeCount = 0;

        for (int i = startIndex; i < startIndex + count && i < Count; i++)
        {
            if (_cache.TryGetValue(i, out var item))
            {
                result.Add(item);
                if (currentRangeStart >= 0)
                {
                    missingRanges.Add((currentRangeStart, currentRangeCount));
                    currentRangeStart = -1;
                    currentRangeCount = 0;
                }
            }
            else
            {
                if (currentRangeStart < 0)
                {
                    currentRangeStart = i;
                    currentRangeCount = 1;
                }
                else
                {
                    currentRangeCount++;
                }
                result.Add(default!);
            }
        }

        if (currentRangeStart >= 0)
        {
            missingRanges.Add((currentRangeStart, currentRangeCount));
        }

        if (missingRanges.Count > 0)
        {
            await LoadRangesAsync(missingRanges, startIndex, result, cancellationToken);
        }

        return result;
    }

    private async Task LoadRangesAsync(
        List<(int start, int count)> ranges,
        int resultStartIndex,
        List<T> result,
        CancellationToken cancellationToken)
    {
        SetLoadingState(true);

        try
        {
            _currentLoadCts?.Cancel();
            _currentLoadCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

            foreach (var (start, count) in ranges)
            {
                if (_currentLoadCts.Token.IsCancellationRequested)
                    break;

                lock (_lock)
                {
                    if (_loadingRanges.Contains(start))
                        continue;
                    _loadingRanges.Add(start);
                }

                try
                {
                    LoadingStateChanged?.Invoke(this, new AsyncDataLoadingEventArgs(start, count, true));

                    var items = await _fetchFunc(start, count, _currentLoadCts.Token);

                    var index = start;
                    foreach (var item in items)
                    {
                        if (index >= resultStartIndex && index < resultStartIndex + result.Count)
                        {
                            result[index - resultStartIndex] = item;
                        }
                        _cache[index] = item;
                        index++;
                    }

                    LoadingStateChanged?.Invoke(this, new AsyncDataLoadingEventArgs(start, count, false));
                }
                finally
                {
                    lock (_lock)
                    {
                        _loadingRanges.Remove(start);
                    }
                }
            }
        }
        finally
        {
            SetLoadingState(false);
            _currentLoadCts?.Dispose();
            _currentLoadCts = null;
        }
    }

    public void Prefetch(int startIndex, int count)
    {
        Task.Run(async () =>
        {
            var rangesToFetch = new List<(int start, int count)>();
            var currentStart = -1;
            var currentCount = 0;

            for (int i = startIndex; i < startIndex + count && i < Count; i++)
            {
                if (!_cache.ContainsKey(i))
                {
                    if (currentStart < 0)
                    {
                        currentStart = i;
                        currentCount = 1;
                    }
                    else
                    {
                        currentCount++;
                    }
                }
                else if (currentStart >= 0)
                {
                    rangesToFetch.Add((currentStart, currentCount));
                    currentStart = -1;
                    currentCount = 0;
                }
            }

            if (currentStart >= 0)
            {
                rangesToFetch.Add((currentStart, currentCount));
            }

            if (rangesToFetch.Count > 0)
            {
                await GetRangeAsync(rangesToFetch[0].start, rangesToFetch[0].count);
            }
        });
    }

    private void SetLoadingState(bool loading)
    {
        _isLoading = loading;
    }

    public void Sort(string? propertyName, ListSortDirection direction)
    {
        _sortProperty = propertyName;
        _sortDirection = direction;
        _sortDescriptions.Clear();

        if (!string.IsNullOrEmpty(propertyName))
        {
            _sortDescriptions.Add(new SortDescription(propertyName, direction));
        }

        _cache.Clear();
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Sorted));
    }

    public void ClearCache()
    {
        _cache.Clear();
    }

    public void InvalidateCount()
    {
        _count = -1;
    }

    public void Refresh()
    {
        _cache.Clear();
        _count = -1;
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Reset));
    }

    protected virtual void OnDataChanged(DataProviderChangedEventArgs e)
    {
        DataChanged?.Invoke(this, e);
    }

    public T? GetCached(int index)
    {
        return _cache.TryGetValue(index, out var item) ? item : default;
    }

    public bool IsCached(int index)
    {
        return _cache.ContainsKey(index);
    }

    object? IList.this[int index]
    {
        get => GetCached(index);
        set => throw new NotSupportedException();
    }

    bool IList.IsFixedSize => true;
    bool IList.IsReadOnly => true;
    bool ICollection.IsSynchronized => false;
    object ICollection.SyncRoot => _lock;

    int IList.Add(object? value) => throw new NotSupportedException();
    void IList.Clear() => throw new NotSupportedException();
    bool IList.Contains(object? value) => throw new NotSupportedException();
    int IList.IndexOf(object? value) => throw new NotSupportedException();
    void IList.Insert(int index, object? value) => throw new NotSupportedException();
    void IList.Remove(object? value) => throw new NotSupportedException();
    void IList.RemoveAt(int index) => throw new NotSupportedException();
    void ICollection.CopyTo(Array array, int index) => throw new NotSupportedException();

    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return _cache.TryGetValue(i, out var item) ? item : default!;
        }
    }
}

public class AsyncDataLoadingEventArgs : EventArgs
{
    public int StartIndex { get; }
    public int Count { get; }
    public bool IsLoading { get; }

    public AsyncDataLoadingEventArgs(int startIndex, int count, bool isLoading)
    {
        StartIndex = startIndex;
        Count = count;
        IsLoading = isLoading;
    }
}
