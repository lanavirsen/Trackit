
using Microsoft.Data.Sqlite;
using System.Reflection;

namespace Trackit.Data.Sqlite
{
    // This class is responsible for setting up the SQLite database schema by executing an SQL script.
    // Bootstrap means initialize the system so it can run.
    public static class DbBootstrap
    {
        // Task means “this method runs asynchronously and may take time (e.g., I/O)”.
        public static async Task EnsureCreatedAsync(DapperConnectionFactory factory)
        {
            // Create and open a new SQLite connection using the provided factory.
            // Factory is a design pattern that provides a way to create objects without specifying the exact class of object that will be created.
            await using var conn = (SqliteConnection)factory.Create();
            await conn.OpenAsync();

            foreach (var resourceName in new[]
            {
                "Trackit.Data.Migrations.001_init.sql",
                "Trackit.Data.Migrations.002_workorders.sql"
            })
            {
                // Load the embedded SQL script from the assembly's resources.
                await using var stream = Assembly.GetExecutingAssembly()
                .GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded {resourceName} not found.");

                // Read the entire SQL script into a string, so I can later execute it against SQLite.
                using var reader = new StreamReader(stream);
                var sql = await reader.ReadToEndAsync();

                // Create a command to execute the SQL script and run it asynchronously.
                using var cmd = new SqliteCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }
        }
    }
}
