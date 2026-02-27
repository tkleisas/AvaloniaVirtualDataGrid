using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using Microsoft.Data.Sqlite;
using AvaloniaVirtualDataGrid.Core;

namespace Demo.Data;

public class SqliteDataProvider : IDataProvider, IList
{
    private readonly SqliteConnection _connection;
    private int _count = -1;
    private readonly Dictionary<int, PersonRecord> _cache = [];
    private readonly HashSet<int> _loadedRanges = [];

    public event EventHandler<DataProviderChangedEventArgs>? DataChanged;
    public event NotifyCollectionChangedEventHandler? CollectionChanged;

    public SqliteDataProvider(string dbPath)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate
        };

        _connection = new SqliteConnection(builder.ConnectionString);
        _connection.Open();

        EnableWalMode();
        CreateTable();
    }

    private void EnableWalMode()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = "PRAGMA journal_mode=WAL;";
        cmd.ExecuteNonQuery();
        
        cmd.CommandText = "PRAGMA synchronous=NORMAL;";
        cmd.ExecuteNonQuery();
    }

    private void CreateTable()
    {
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            CREATE TABLE IF NOT EXISTS People (
                Id INTEGER PRIMARY KEY,
                FirstName TEXT NOT NULL,
                LastName TEXT NOT NULL,
                Email TEXT NOT NULL,
                Age INTEGER NOT NULL,
                City TEXT NOT NULL,
                Progress REAL NOT NULL
            )";
        cmd.ExecuteNonQuery();
    }

    public void PopulateData(int count)
    {
        var firstNames = new[] { "James", "Mary", "John", "Patricia", "Robert", "Jennifer", "Michael", "Linda", "William", "Elizabeth", "David", "Barbara", "Richard", "Susan", "Joseph", "Jessica", "Thomas", "Sarah", "Charles", "Karen" };
        var lastNames = new[] { "Smith", "Johnson", "Williams", "Brown", "Jones", "Garcia", "Miller", "Davis", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez", "Wilson", "Anderson", "Thomas", "Taylor", "Moore", "Jackson", "Martin" };
        var cities = new[] { "New York", "Los Angeles", "Chicago", "Houston", "Phoenix", "Philadelphia", "San Antonio", "San Diego", "Dallas", "San Jose" };

        var random = new Random(42);

        using var transaction = _connection.BeginTransaction();
        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO People (Id, FirstName, LastName, Email, Age, City, Progress)
            VALUES ($id, $firstName, $lastName, $email, $age, $city, $progress)";

        var pId = cmd.Parameters.Add("$id", SqliteType.Integer);
        var pFirstName = cmd.Parameters.Add("$firstName", SqliteType.Text);
        var pLastName = cmd.Parameters.Add("$lastName", SqliteType.Text);
        var pEmail = cmd.Parameters.Add("$email", SqliteType.Text);
        var pAge = cmd.Parameters.Add("$age", SqliteType.Integer);
        var pCity = cmd.Parameters.Add("$city", SqliteType.Text);
        var pProgress = cmd.Parameters.Add("$progress", SqliteType.Real);

        for (int i = 1; i <= count; i++)
        {
            pId.Value = i;
            pFirstName.Value = firstNames[random.Next(firstNames.Length)];
            pLastName.Value = lastNames[random.Next(lastNames.Length)];
            pEmail.Value = $"user{i}@example.com";
            pAge.Value = random.Next(18, 80);
            pCity.Value = cities[random.Next(cities.Length)];
            pProgress.Value = random.NextDouble();

            cmd.ExecuteNonQuery();
        }

        transaction.Commit();
        _count = -1;
        _cache.Clear();
        _loadedRanges.Clear();
        
        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.Reset));
    }

    public int Count
    {
        get
        {
            if (_count < 0)
            {
                using var cmd = _connection.CreateCommand();
                cmd.CommandText = "SELECT COUNT(*) FROM People";
                _count = Convert.ToInt32(cmd.ExecuteScalar());
            }
            return _count;
        }
    }

    public int RowCount => Count;

    public ValueTask<IReadOnlyList<PersonRecord>> GetRangeAsync(int startIndex, int count, CancellationToken cancellationToken = default)
    {
        var result = new List<PersonRecord>(count);

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, FirstName, LastName, Email, Age, City, Progress
            FROM People
            WHERE Id >= $start AND Id < $end
            ORDER BY Id";
        
        cmd.Parameters.AddWithValue("$start", startIndex + 1);
        cmd.Parameters.AddWithValue("$end", startIndex + count + 1);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            var record = new PersonRecord
            {
                Id = reader.GetInt32(0),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                Email = reader.GetString(3),
                Age = reader.GetInt32(4),
                City = reader.GetString(5),
                Progress = reader.GetDouble(6)
            };
            result.Add(record);
            _cache[record.Id - 1] = record;
        }

        return new ValueTask<IReadOnlyList<PersonRecord>>(result);
    }

    public PersonRecord? GetAt(int index)
    {
        if (_cache.TryGetValue(index, out var cached))
            return cached;

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = @"
            SELECT Id, FirstName, LastName, Email, Age, City, Progress
            FROM People
            WHERE Id = $id";
        
        cmd.Parameters.AddWithValue("$id", index + 1);

        using var reader = cmd.ExecuteReader();
        if (reader.Read())
        {
            var record = new PersonRecord
            {
                Id = reader.GetInt32(0),
                FirstName = reader.GetString(1),
                LastName = reader.GetString(2),
                Email = reader.GetString(3),
                Age = reader.GetInt32(4),
                City = reader.GetString(5),
                Progress = reader.GetDouble(6)
            };
            _cache[index] = record;
            return record;
        }

        return null;
    }

    public void Update(int index, string column, object value)
    {
        var columnMap = new Dictionary<string, string>
        {
            { nameof(PersonRecord.FirstName), "FirstName" },
            { nameof(PersonRecord.LastName), "LastName" },
            { nameof(PersonRecord.Email), "Email" },
            { nameof(PersonRecord.Age), "Age" },
            { nameof(PersonRecord.City), "City" },
            { nameof(PersonRecord.Progress), "Progress" }
        };

        if (!columnMap.TryGetValue(column, out var dbColumn))
            throw new ArgumentException($"Unknown column: {column}");

        using var cmd = _connection.CreateCommand();
        cmd.CommandText = $"UPDATE People SET {dbColumn} = $value WHERE Id = $id";
        cmd.Parameters.AddWithValue("$value", value);
        cmd.Parameters.AddWithValue("$id", index + 1);
        cmd.ExecuteNonQuery();

        // Update cache
        if (_cache.TryGetValue(index, out var record))
        {
            var prop = typeof(PersonRecord).GetProperty(column);
            prop?.SetValue(record, value);
        }

        OnDataChanged(new DataProviderChangedEventArgs(DataProviderChangeType.ItemsReplaced, index, 1));
    }

    public void Close()
    {
        _connection.Close();
    }

    protected virtual void OnDataChanged(DataProviderChangedEventArgs e)
    {
        DataChanged?.Invoke(this, e);
    }

    // IList implementation
    public object? this[int index]
    {
        get => GetAt(index);
        set
        {
            if (value is PersonRecord record)
            {
                Update(index, nameof(PersonRecord.FirstName), record.FirstName);
                Update(index, nameof(PersonRecord.LastName), record.LastName);
                Update(index, nameof(PersonRecord.Email), record.Email);
                Update(index, nameof(PersonRecord.Age), record.Age);
                Update(index, nameof(PersonRecord.City), record.City);
            }
        }
    }

    public bool IsFixedSize => true;
    public bool IsReadOnly => false;
    public bool IsSynchronized => false;
    public object SyncRoot => this;

    public int Add(object? value) => throw new NotSupportedException();
    public void Clear() => throw new NotSupportedException();
    public bool Contains(object? value) => value is PersonRecord r && GetAt(r.Id - 1) != null;
    public int IndexOf(object? value) => value is PersonRecord r ? r.Id - 1 : -1;
    public void Insert(int index, object? value) => throw new NotSupportedException();
    public void Remove(object? value) => throw new NotSupportedException();
    public void RemoveAt(int index) => throw new NotSupportedException();
    public void CopyTo(Array array, int index) => throw new NotSupportedException();
    public IEnumerator GetEnumerator()
    {
        for (int i = 0; i < Count; i++)
        {
            yield return GetAt(i)!;
        }
    }
}
