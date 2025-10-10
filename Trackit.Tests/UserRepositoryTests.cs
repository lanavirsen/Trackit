using System;
using System.Threading.Tasks;
using FluentAssertions;
using Trackit.Core.Domain;
using Trackit.Data.Repositories;
using Xunit;

namespace Trackit.Tests
{
    public class UserRepositoryTests
    {
        [Fact]
        public async Task Add_and_get_by_username_roundtrip()
        {
            var repo = new InMemoryUserRepository(); // each test method needs its own isolated instance of the repository.
            var now = DateTimeOffset.UtcNow; // the current moment in UTC.

            var user = new User
            {
                Username = "lana", // normalized
                Email = "lana@example.com",
                PasswordHash = new byte[] { 1, 2, 3 },
                PasswordSalt = new byte[] { 4, 5, 6 },
                CreatedAtUtc = now
            };

            var id = await repo.AddAsync(user); // If you omitted await, you’d get a Task<int> object, not the integer value.
            id.Should().BeGreaterThan(0);

            var fetched = await repo.GetByUsernameAsync("lana");
            fetched.Should().NotBeNull();
            fetched!.Id.Should().Be(id);
            fetched.Email.Should().Be("lana@example.com");
        }

        /*
        Difference between await repo.AddAsync(new User { … }) and var user = new User { … }

        Functionally, both create a User and add it to the repository.
        The only difference is how you handle that User object:
        - Inline version → fire-and-forget setup.
        - Variable version → you’ll reuse it or assert on its properties later in the test.
        */

        [Fact]
        public async Task Exists_returns_true_after_add()
        {
            var repo = new InMemoryUserRepository();
            await repo.AddAsync(new User
            {
                Username = "lana",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                CreatedAtUtc = DateTimeOffset.UtcNow
            });

            (await repo.ExistsAsync("lana")).Should().BeTrue();
            (await repo.ExistsAsync("other")).Should().BeFalse();
        }

        [Fact]
        public async Task Duplicate_username_throws()
        {
            var repo = new InMemoryUserRepository();
            var u = new User
            {
                Username = "lana",
                PasswordHash = new byte[] { 1 },
                PasswordSalt = new byte[] { 2 },
                CreatedAtUtc = DateTimeOffset.UtcNow
            };
            await repo.AddAsync(u); // inserts the key "lana" successfully.

            var act = async () => await repo.AddAsync(u); // tries to insert "lana" again.
            await act.Should().ThrowAsync<InvalidOperationException>();
        }
    }
}
