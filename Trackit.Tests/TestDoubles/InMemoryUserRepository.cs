using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Tests.TestDoubles;

/// <summary>
/// Lightweight in-memory implementation of <see cref="IUserRepository"/> for unit tests.
/// </summary>
public sealed class InMemoryUserRepository : IUserRepository
{
    private readonly ConcurrentDictionary<string, User> _byUsername = new();
    private readonly ConcurrentDictionary<int, User> _byId = new();
    private readonly object _gate = new();
    private int _nextId;

    public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken ct = default)
    {
        _byUsername.TryGetValue(normalizedUsername, out var user);
        return Task.FromResult(user);
    }

    public Task<bool> ExistsAsync(string normalizedUsername, CancellationToken ct = default)
        => Task.FromResult(_byUsername.ContainsKey(normalizedUsername));

    public Task<int> AddAsync(User user, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(user.Username))
            throw new ArgumentException("Username required", nameof(user));
        if (user.PasswordHash is null || user.PasswordSalt is null)
            throw new ArgumentException("Password not hashed", nameof(user));

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

    public Task<User?> GetByIdAsync(int id, CancellationToken ct = default)
    {
        _byId.TryGetValue(id, out var user);
        return Task.FromResult(user);
    }

    public Task UpdateAsync(User user, CancellationToken ct = default)
    {
        if (user.Id <= 0) throw new ArgumentException("Valid Id required", nameof(user));

        lock (_gate)
        {
            if (!_byId.TryGetValue(user.Id, out var existing))
                throw new InvalidOperationException("User not found");

            if (!string.Equals(existing.Username, user.Username, StringComparison.Ordinal))
            {
                if (_byUsername.TryGetValue(user.Username, out var other) && other.Id != user.Id)
                    throw new InvalidOperationException("Username already exists");

                _byUsername.TryRemove(existing.Username, out _);
            }

            var stored = Clone(user, existing.Id, existing.CreatedAtUtc);

            _byId[stored.Id] = stored;
            _byUsername[stored.Username] = stored;
        }

        return Task.CompletedTask;
    }
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
