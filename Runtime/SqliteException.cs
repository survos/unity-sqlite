using System;

namespace Survos.Sqlite
{
    public sealed class SqliteException : Exception
    {
        public int ResultCode { get; }
        public int ExtendedResultCode { get; }
        public string Sql { get; }
        internal SqliteException(int code, int extendedCode, string message, string sql = null) : base(message)
        { ResultCode = code & 255; ExtendedResultCode = extendedCode; Sql = sql; }
    }
    public enum SqliteOpenMode { ReadOnly, ReadWrite, ReadWriteCreate }
}
