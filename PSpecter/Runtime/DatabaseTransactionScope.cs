using System;
using Microsoft.Data.Sqlite;

namespace PSpecter.Runtime
{
    /// <summary>
    /// A using-friendly transaction wrapper. Commit must be called explicitly;
    /// if Dispose runs without a prior Commit, the transaction is rolled back.
    /// </summary>
    public sealed class DatabaseTransactionScope : IDisposable
    {
        private readonly SqliteTransaction _transaction;
        private bool _committed;

        internal DatabaseTransactionScope(SqliteConnection connection)
        {
            _transaction = connection.BeginTransaction();
        }

        internal SqliteTransaction Transaction => _transaction;

        public void Commit()
        {
            _transaction.Commit();
            _committed = true;
        }

        public void Dispose()
        {
            if (!_committed)
            {
                try { _transaction.Rollback(); } catch { }
            }
            _transaction.Dispose();
        }
    }
}
