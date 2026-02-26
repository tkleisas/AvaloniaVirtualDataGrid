namespace AvaloniaVirtualDataGrid.Core;

public interface IDataProvider
{
    int Count { get; }
    event EventHandler<DataProviderChangedEventArgs>? DataChanged;
}

public interface IDataProvider<T> : IDataProvider
{
    ValueTask<IReadOnlyList<T>> GetRangeAsync(int startIndex, int count, CancellationToken cancellationToken = default);
}

public class DataProviderChangedEventArgs : EventArgs
{
    public DataProviderChangeType ChangeType { get; }
    public int StartIndex { get; }
    public int Count { get; }

    public DataProviderChangedEventArgs(DataProviderChangeType changeType, int startIndex = -1, int count = 0)
    {
        ChangeType = changeType;
        StartIndex = startIndex;
        Count = count;
    }
}

public enum DataProviderChangeType
{
    Reset,
    ItemsAdded,
    ItemsRemoved,
    ItemsReplaced,
    Sorted
}
