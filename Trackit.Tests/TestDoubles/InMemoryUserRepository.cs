using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Tests.TestDoubles;

// In-memory user repository for testing purposes.
// Each test sets up its own repository and populates it manually before calling the code being tested.

// When class is sealed, it means no other class can inherit from it.
public sealed class InMemoryUserRepository : IUserRepository
{
    // ConcurrentDictionary is a thread-safe collection for storing users by username and by ID.
    // Thread-safe means it can be safely accessed by multiple threads at the same time.
    private readonly ConcurrentDictionary<string, User> _byUsername = new();
    private readonly ConcurrentDictionary<int, User> _byId = new();

    // _gate is used to synchronize access to critical sections of code.
    // It ensures that only one thread can access the code inside the lock at a time.
    // It's just a private field — a plain object.
    // It isn’t an instance of a special class; it’s literally a new, empty object used only as a lock handle.
    private readonly object _gate = new();

    private int _nextId;


    // Task is a representation of an asynchronous operation.
    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken ct = default)
    {
        _byUsername.TryGetValue(normalizedUsername, out var user);
        return Task.FromResult(user);
    }

    // ExistsAsync checks if a user with the given username exists in the repository.
    public Task<bool> ExistsAsync(string normalizedUsername, CancellationToken ct = default)
        => Task.FromResult(_byUsername.ContainsKey(normalizedUsername));

    // AddAsync adds a new user to the repository and returns the assigned user ID.
    public Task<int> AddAsync(User user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new ArgumentException("Username required", nameof(user));
        if (user.PasswordHash is null || user.PasswordSalt is null)
            throw new ArgumentException("Password not hashed", nameof(user));

        // Locking ensures that the code inside the block is executed by only one thread at a time.

        /*
        If multiple threads tried to add users to _byUsername or _byId at the same time, we could get:
          - race conditions (two threads using the same ID)
          - inconsistent state (half-written data)
          - exceptions like “key already exists” or corrupted structures.
        */
        lock (_gate)
        {
            if (_byUsername.ContainsKey(user.Username))
                throw new InvalidOperationException("Username already exists");

            var id = Interlocked.Increment(ref _nextId);
            var stored = Clone(user, id, user.CreatedAtUtc);

            _byUsername[stored.Username] = stored;
            _byId[id] = stored;
            return Task.FromResult(id);
        }
    }

    // GetByIdAsync retrieves a user by their ID.
    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _byId.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    // UpdateAsync updates an existing user in the repository.
    // It just signals completion — nothing is awaited, and no value is returned.
    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        if (user.Id <= 0) throw new ArgumentException("Valid Id required", nameof(user));

        // Locking ensures that the code inside the block is executed by only one thread at a time.
        lock (_gate)
        {
            // Check if the user exists.
            if (!_byId.TryGetValue(user.Id, out var existing))
                throw new InvalidOperationException("User not found");

            // If the username is being changed, ensure the new username is not already taken.
            if (!string.Equals(existing.Username, user.Username, StringComparison.Ordinal))
            {
                if (_byUsername.TryGetValue(user.Username, out var other) && other.Id != user.Id)
                    throw new InvalidOperationException("Username already exists");

                _byUsername.TryRemove(existing.Username, out _);
            }

            // Create a clone of the user to store, preserving the original ID and creation timestamp.
            var stored = Clone(user, existing.Id, existing.CreatedAtUtc);

            // Update the dictionaries with the new user data.
            _byId[stored.Id] = stored;
            _byUsername[stored.Username] = stored;
        }

        return Task.CompletedTask;
    }

    // Clone creates a copy of the user with a new ID and creation timestamp.
    private static User Clone(User user, int id, DateTimeOffset createdAt)
        => new()
        {
            Id = id,
            Username = user.Username,
            Email = user.Email,
            PasswordHash = user.PasswordHash,
            PasswordSalt = user.PasswordSalt,
            CreatedAtUtc = createdAt,
            TotpSecret = user.TotpSecret,
            TwoFactorEnabled = user.TwoFactorEnabled
        };
}
