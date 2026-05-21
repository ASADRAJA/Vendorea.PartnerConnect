using System.Data;

namespace Vendorea.PartnerConnect.WorkerProcesses.Services;

/// <summary>
/// IDataReader implementation for reading SPR CSV files for SqlBulkCopy.
/// Handles quoted fields, proper delimiter parsing, and type conversion.
/// </summary>
public class SprCsvDataReader : IDataReader
{
    private readonly StreamReader _reader;
    private readonly int _fieldCount;
    private readonly char _delimiter;
    private string[]? _currentRow;
    private bool _disposed;

    public long RecordsRead { get; private set; }

    public SprCsvDataReader(string filePath, int fieldCount, char delimiter = ',')
    {
        _reader = new StreamReader(filePath);
        _fieldCount = fieldCount;
        _delimiter = delimiter;
    }

    public int FieldCount => _fieldCount;
    public int Depth => 0;
    public bool IsClosed => _disposed;
    public int RecordsAffected => -1;

    public object this[int i] => GetValue(i);
    public object this[string name] => throw new NotSupportedException();

    public bool Read()
    {
        if (_disposed || _reader.EndOfStream)
            return false;

        var line = _reader.ReadLine();
        if (line == null)
            return false;

        _currentRow = ParseLine(line);
        RecordsRead++;
        return true;
    }

    public object GetValue(int i)
    {
        if (_currentRow == null || i >= _currentRow.Length)
            return DBNull.Value;

        var value = _currentRow[i];
        if (string.IsNullOrEmpty(value))
            return DBNull.Value;

        return value;
    }

    public bool IsDBNull(int i)
    {
        if (_currentRow == null || i >= _currentRow.Length)
            return true;

        return string.IsNullOrEmpty(_currentRow[i]);
    }

    public int GetValues(object[] values)
    {
        var count = Math.Min(values.Length, _fieldCount);
        for (int i = 0; i < count; i++)
        {
            values[i] = GetValue(i);
        }
        return count;
    }

    private string[] ParseLine(string line)
    {
        var fields = new List<string>();
        var currentField = new System.Text.StringBuilder();
        bool inQuotes = false;
        int i = 0;

        while (i < line.Length)
        {
            char c = line[i];

            if (inQuotes)
            {
                if (c == '"')
                {
                    // Check for escaped quote
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        currentField.Append('"');
                        i += 2;
                    }
                    else
                    {
                        // End of quoted field
                        inQuotes = false;
                        i++;
                    }
                }
                else
                {
                    currentField.Append(c);
                    i++;
                }
            }
            else
            {
                if (c == '"')
                {
                    inQuotes = true;
                    i++;
                }
                else if (c == _delimiter)
                {
                    fields.Add(currentField.ToString().Trim());
                    currentField.Clear();
                    i++;
                }
                else
                {
                    currentField.Append(c);
                    i++;
                }
            }
        }

        // Add the last field
        fields.Add(currentField.ToString().Trim());

        // Pad with empty strings if needed
        while (fields.Count < _fieldCount)
        {
            fields.Add(string.Empty);
        }

        return fields.ToArray();
    }

    // Required IDataReader members with basic implementations

    public string GetName(int i) => $"Column{i}";
    public string GetDataTypeName(int i) => "String";
    public Type GetFieldType(int i) => typeof(string);
    public int GetOrdinal(string name) => throw new NotSupportedException();

    public bool GetBoolean(int i) => bool.Parse(GetString(i));
    public byte GetByte(int i) => byte.Parse(GetString(i));
    public long GetBytes(int i, long fieldOffset, byte[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public char GetChar(int i) => GetString(i)[0];
    public long GetChars(int i, long fieldoffset, char[]? buffer, int bufferoffset, int length) => throw new NotSupportedException();
    public IDataReader GetData(int i) => throw new NotSupportedException();
    public DateTime GetDateTime(int i) => DateTime.Parse(GetString(i));
    public decimal GetDecimal(int i) => decimal.Parse(GetString(i));
    public double GetDouble(int i) => double.Parse(GetString(i));
    public float GetFloat(int i) => float.Parse(GetString(i));
    public Guid GetGuid(int i) => Guid.Parse(GetString(i));
    public short GetInt16(int i) => short.Parse(GetString(i));
    public int GetInt32(int i) => int.Parse(GetString(i));
    public long GetInt64(int i) => long.Parse(GetString(i));

    public string GetString(int i)
    {
        var value = GetValue(i);
        return value == DBNull.Value ? string.Empty : (string)value;
    }

    public DataTable GetSchemaTable() => throw new NotSupportedException();
    public bool NextResult() => false;

    public void Close()
    {
        Dispose();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _reader.Dispose();
            _disposed = true;
        }
    }
}
