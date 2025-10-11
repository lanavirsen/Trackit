using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Trackit.Core.Domain;
using Trackit.Data.Repositories;
using Trackit.Data.Sqlite;
using Xunit;

namespace Trackit.Tests
{
    // Tests for SqliteUserRepository using a temporary SQLite database file.
    public class SqliteUserRepositoryTests
    {
        // Create a new SqliteUserRepository with a fresh database for each test.
        private static async Task<SqliteUserRepository> NewRepoAsync(string dbFile)
        {
            if (File.Exists(dbFile)) File.Delete(dbFile);
            var factory = new DapperConnectionFactory($"Data Source={dbFile};Cache=Shared;");
            await DbBootstrap.EnsureCreatedAsync(factory);
            return new SqliteUserRepository(factory);
        }

        // roundtrip means verifying that data you write to a storage layer can be read back unchanged and interpreted correctly.

        [Fact]
        public async Task Add_and_get_roundtrip_sqlite()
        {
            // Use a temporary file for the SQLite database.
            var repo = await NewRepoAsync(Path.GetTempFileName());
            var id = await repo.AddAsync(new User
            {
                Username = "lana",
                Email = "lana@example.com",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                CreatedAtUtc = System.DateTimeOffset.UtcNow
            });
            id.Should().BeGreaterThan(0);

            var fetched = await repo.GetByUsernameAsync("lana");
            fetched.Should().NotBeNull();
            fetched!.Id.Should().Be(id);
            fetched.Email.Should().Be("lana@example.com");
        }

        [Fact]
        public async Task Duplicate_username_throws_sqlite()
        {
            var repo = await NewRepoAsync(Path.GetTempFileName());
            var u = new User
            {
                Username = "lana",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                CreatedAtUtc = System.DateTimeOffset.UtcNow
            };
            await repo.AddAsync(u);
            var act = async () => await repo.AddAsync(u);
            await act.Should().ThrowAsync<System.InvalidOperationException>();
        }
    }
}
