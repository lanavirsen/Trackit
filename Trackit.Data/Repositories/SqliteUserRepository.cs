using Dapper;
using Trackit.Core.Domain;
using Trackit.Core.Ports;
using Trackit.Data.Sqlite;

namespace Trackit.Data.Repositories
{
    // Implements IUserRepository using SQLite and Dapper for data access.
    public sealed class SqliteUserRepository : IUserRepository
    {
        // DapperConnectionFactory is a factory pattern that creates and configures database connections.
        private readonly DapperConnectionFactory _factory;

        // Constructor that takes a DapperConnectionFactory to create database connections.
        public SqliteUserRepository(DapperConnectionFactory factory) => _factory = factory;

        // Asynchronously retrieves a User by their normalized username.
        public async Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken ct = default)
        {
            const string sql = @"SELECT Id, Username, Email, PasswordHash, PasswordSalt, CreatedAtUtc,
                                TotpSecret, TwoFactorEnabled
                                FROM Users WHERE Username = @u LIMIT 1;";
            using var conn = _factory.Create();
            var row = await conn.QuerySingleOrDefaultAsync<UserRow>(sql, new { u = normalizedUsername });

            /*
            UserRow is an internal data-transfer object matching the SQL column names.
            .ToDomain() maps that raw row into my domain User instance.
            */

            // If row is null, return null; otherwise, convert UserRow to User domain object.
            return row?.ToDomain();
        }

        // Asynchronously checks if a user with the given normalized username exists.
        public async Task<bool> ExistsAsync(string normalizedUsername, CancellationToken ct = default)
        {
            const string sql = @"SELECT 1 FROM Users WHERE Username = @u LIMIT 1;";
            using var conn = _factory.Create();
            var one = await conn.ExecuteScalarAsync<int?>(sql, new { u = normalizedUsername });
            return one.HasValue;
        }

        // Asynchronously adds a new User to the database and returns the generated Id.
        public async Task<int> AddAsync(User user, CancellationToken ct = default)
        {
            const string sql = @"INSERT INTO Users(Username, Email, PasswordHash, PasswordSalt, CreatedAtUtc, TotpSecret, TwoFactorEnabled)
                             VALUES(@Username, @Email, @PasswordHash, @PasswordSalt, @CreatedAtUtc, @TotpSecret, @TwoFactorEnabled);
                             SELECT last_insert_rowid();";
            using var conn = _factory.Create();

            // Attempt to insert the new user and retrieve the generated Id.
            try
            {
                var id = await conn.ExecuteScalarAsync<long>(sql, new
                {
                    user.Username,
                    user.Email,
                    user.PasswordHash,
                    user.PasswordSalt,
                    CreatedAtUtc = user.CreatedAtUtc.UtcDateTime.ToString("O"),
                    TotpSecret = user.TotpSecret,
                    TwoFactorEnabled = user.TwoFactorEnabled ? 1 : 0
                });
                return checked((int)id);
            }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 19) // UNIQUE constraint
            {
                throw new InvalidOperationException("Username already exists", ex);
            }
        }

        // Asynchronously updates an existing User in the database.
        public async Task UpdateAsync(User user, CancellationToken ct = default)
        {
            const string sql = @"
                               UPDATE Users SET
                                   Email = @Email,
                                   PasswordHash = @PasswordHash,
                                   PasswordSalt = @PasswordSalt,
                                   TotpSecret = @TotpSecret,
                                   TwoFactorEnabled = @TwoFactorEnabled
                               WHERE Id = @Id;";
            using var conn = _factory.Create();
            await conn.ExecuteAsync(sql, new
            {
                user.Id,
                user.Email,
                user.PasswordHash,
                user.PasswordSalt,
                TotpSecret = user.TotpSecret,
                TwoFactorEnabled = user.TwoFactorEnabled ? 1 : 0
            });
        }

        // Internal class representing a row from the Users table.
        private sealed class UserRow
        {
            public long Id { get; init; }
            public string Username { get; init; } = null!;
            public string? Email { get; init; }
            public byte[] PasswordHash { get; init; } = Array.Empty<byte>();
            public byte[] PasswordSalt { get; init; } = Array.Empty<byte>();
            public string CreatedAtUtc { get; init; } = null!;
            public string? TotpSecret { get; init; }
            public int TwoFactorEnabled { get; init; }
            public User ToDomain() => new()
            {
                Id = checked((int)Id),
                Username = Username,
                Email = Email,
                PasswordHash = PasswordHash,
                PasswordSalt = PasswordSalt,
                CreatedAtUtc = DateTimeOffset.Parse(CreatedAtUtc, null, System.Globalization.DateTimeStyles.RoundtripKind),
                TotpSecret = TotpSecret,
                TwoFactorEnabled = TwoFactorEnabled != 0
            };
        }

        // Asynchronously retrieves a User by their unique identifier.
        public async Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
        {
            const string sql = @"SELECT Id, Username, Email, PasswordHash, PasswordSalt, CreatedAtUtc,
                                    TotpSecret, TwoFactorEnabled
                                 FROM Users
                                 WHERE Id = @id
                                 LIMIT 1;";

            using var conn = _factory.Create();
            var row = await conn.QuerySingleOrDefaultAsync<UserRow>(sql, new { id });
            return row?.ToDomain();
        }

    }
}
