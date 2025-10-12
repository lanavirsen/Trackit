
using Microsoft.Data.Sqlite;
using System.Data;
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
            using var conn = (SqliteConnection)factory.Create();
            await conn.OpenAsync();

            // Local function to execute an embedded SQL script by filename.
            async Task ExecEmbedded(string filename)
            {
                var sql = ReadEmbeddedSql(filename);
                using var cmd = new SqliteCommand(sql, conn);
                await cmd.ExecuteNonQueryAsync();
            }

            // Execute the initial migration scripts to set up the database schema.
            await ExecEmbedded("001_init.sql");
            await ExecEmbedded("002_workorders.sql");

            // Check if the "Stage" column exists in the "WorkOrders" table; if not, apply the migration to add it.
            if (!await ColumnExistsAsync(conn, "WorkOrders", "Stage"))
                await ExecEmbedded("003_stage.sql");
        }

        // Reads an embedded SQL file from the assembly's resources.
        private static string ReadEmbeddedSql(string filename)
        {
            // Get the assembly where this class is defined.
            // Assembly is a compiled code library used for deployment, versioning, and security in .NET.
            var asm = typeof(DbBootstrap).Assembly;
            var resourceName = $"Trackit.Data.Migrations.{filename}";
            using var stream = asm.GetManifestResourceStream(resourceName)
                ?? throw new FileNotFoundException($"Embedded resource not found: {resourceName}");
            using var reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        // Checks if a specific column exists in a given table within the SQLite database.
        private static async Task<bool> ColumnExistsAsync(SqliteConnection conn, string table, string column)
        {
            using var cmd = new SqliteCommand($"PRAGMA table_info('{table}')", conn);
            using var reader = await cmd.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                var name = reader.GetString(1); // column "name"
                if (string.Equals(name, column, StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
