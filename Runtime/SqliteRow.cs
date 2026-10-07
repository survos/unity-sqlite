using System;

namespace Survos.Sqlite
{
    /// <summary>An owned row snapshot, valid after the next step and after closing the database.</summary>
    public sealed class SqliteRow
    {
        private readonly string[] names;
        private readonly object[] values;
        internal SqliteRow(string[] names, object[] values) { this.names = names; this.values = values; }
        public int Count => values.Length;
        public string GetName(int index) => names[index];
        public object this[int index] => values[index];
        public object this[string name] => values[Ordinal(name)];
        public int Ordinal(string name)
        {
            for (int i = 0; i < names.Length; i++) if (string.Equals(names[i], name, StringComparison.Ordinal)) return i;
            throw new ArgumentException("Unknown column: " + name, nameof(name));
        }
        public bool IsNull(int index) => values[index] == null;
        public bool IsNull(string name) => IsNull(Ordinal(name));
        public long GetInt64(int index) => (long)values[index];
        public long GetInt64(string name) => GetInt64(Ordinal(name));
        public double GetDouble(int index) => values[index] is long n ? n : (double)values[index];
        public double GetDouble(string name) => GetDouble(Ordinal(name));
        public string GetString(int index) => (string)values[index];
        public string GetString(string name) => GetString(Ordinal(name));
        public byte[] GetBlob(int index) => (byte[])values[index];
        public byte[] GetBlob(string name) => GetBlob(Ordinal(name));
    }
}
