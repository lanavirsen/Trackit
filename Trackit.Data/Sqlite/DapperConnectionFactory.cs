using Microsoft.Data.Sqlite;
using System.Data;

namespace Trackit.Data.Sqlite
{
    // Factory class for creating SQLite database connections using Dapper.
    // Factory means that this class is responsible for creating and configuring instances of database connections.
    public sealed class DapperConnectionFactory
    {
        // Connection string used to connect to the SQLite database.
        private readonly string _connectionString;

        // Constructor that initializes the connection string.
        public DapperConnectionFactory(string connectionString) => _connectionString = connectionString;

        // Method to create and return a new SQLite database connection.
        public IDbConnection Create() => new SqliteConnection(_connectionString);
    }
}
