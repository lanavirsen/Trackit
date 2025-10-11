
using Microsoft.Data.Sqlite;

namespace Trackit.Data.Sqlite
{
    // This class is responsible for setting up the SQLite database schema by executing an SQL script.
    // Bootstrap means initialize the system so it can run.
    public static class DbBootstrap
    {
        // Task means “this method runs asynchronously and may take time (e.g., I/O)”.
        public static async Task EnsureCreatedAsync(DapperConnectionFactory factory)
        {
            // Read the SQL script from the file system.
            var sqlPath = Path.Combine(AppContext.BaseDirectory, "001_init.sql");
            var sql = await File.ReadAllTextAsync(sqlPath);

            // Create and open a new SQLite connection using the provided factory.
            // Factory is a design pattern that provides a way to create objects without specifying the exact class of object that will be created.
            using var conn = (SqliteConnection)factory.Create();
            await conn.OpenAsync();

            // Create a command to execute the SQL script and run it asynchronously.
            using var cmd = new SqliteCommand(sql, conn);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
