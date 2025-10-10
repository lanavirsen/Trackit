using Trackit.Core.Domain;

namespace Trackit.Core.Ports
{
    // IUserRepository defines what operations exist on User entities, while classes in Trackit.Data define how those operations are executed.

    // A port is an interface that defines how the application core communicates with the outside world.
    public interface IUserRepository
    {
        // normalizedUsername MUST be lowercased/trimmed by caller.
        Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken ct = default);
        Task<bool> ExistsAsync(string normalizedUsername, CancellationToken ct = default);

        // Returns generated Id. Throws InvalidOperationException on duplicate username.
        Task<int> AddAsync(User user, CancellationToken ct = default);
    }
}
