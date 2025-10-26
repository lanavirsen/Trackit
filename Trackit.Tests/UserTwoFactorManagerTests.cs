using System.Threading.Tasks;
using Trackit.Core.Domain;
using Trackit.Core.Services;
using Trackit.Tests.TestDoubles;
using Xunit;

public class UserTwoFactorManagerTests
{
    [Fact]
    public async Task EnableAndDisable_ShouldPersistChanges()
    {
        var repo = new InMemoryUserRepository();
        var totp = new TotpService();
        var manager = new UserTwoFactorManager(repo);

        // Create a user manually
        var user = new User
        {
            Username = "lana",
            Email = "lana@example.com",
            PasswordHash = new byte[1],
            PasswordSalt = new byte[1],
            CreatedAtUtc = System.DateTimeOffset.UtcNow
        };
        var id = await repo.AddAsync(user);

        // Enable TOTP
        var secret = totp.GenerateSecret();
        await manager.EnableAsync(id, secret);
        var updated = await repo.GetByIdAsync(id);

        Assert.NotNull(updated);
        Assert.True(updated!.TwoFactorEnabled);
        Assert.Equal(secret, updated.TotpSecret);

        // Disable TOTP
        await manager.DisableAsync(id);
        var disabled = await repo.GetByIdAsync(id);

        Assert.False(disabled!.TwoFactorEnabled);
        Assert.Null(disabled.TotpSecret);
    }
}
