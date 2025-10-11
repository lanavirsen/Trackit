using Microsoft.Data.Sqlite;
using System.Data;

namespace Trackit.Data.Sqlite
{
    public sealed class DapperConnectionFactory
    {
        private readonly string _connectionString;
        public DapperConnectionFactory(string connectionString) => _connectionString = connectionString;
        public IDbConnection Create() => new SqliteConnection(_connectionString);
    }
}
