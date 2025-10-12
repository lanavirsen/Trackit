using System.ComponentModel;
using Trackit.Core.Auth;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Core.Services
{
    public sealed class UserService
    {
        /* readonly means the field’s reference can only be assigned:
        1. At declaration, or
        2. Inside the constructor of that same class.
        */

        // The leading underscore is a naming convention for private fields.
        private readonly IUserRepository _repo;
        private readonly PasswordHasher _hasher;

        // Func<DateTimeOffset> means “a parameterless function that returns a DateTimeOffset”.
        // It’s used instead of directly calling DateTimeOffset.UtcNow so that time can be injected and controlled during testing.
        private readonly Func<DateTimeOffset> _nowUtc;

        public UserService(IUserRepository repo, PasswordHasher hasher, Func<DateTimeOffset>? nowUtc = null)
        {
            _repo = repo;
            _hasher = hasher;

            // If caller passed null for nowUtc, use DateTimeOffset.UtcNow.
            // (?? is the null-coalescing operator)
            _nowUtc = nowUtc ?? (() => DateTimeOffset.UtcNow);
        }

        // CancellationToken is a .NET mechanism that allows cooperative cancellation of asynchronous operations.

        public async Task<int> RegisterAsync(string username, string? email, string password, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username)) throw new ArgumentException("Username required", nameof(username));
            if (string.IsNullOrWhiteSpace(password)) throw new ArgumentException("Password required", nameof(password));

            var norm = Normalization.NormalizeUsername(username);
            if (await _repo.ExistsAsync(norm, ct))
                throw new InvalidOperationException("Username already exists");

            var (hash, salt) = _hasher.Hash(password);
            var user = new User
            {
                Username = norm,
                Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
                PasswordHash = hash,
                PasswordSalt = salt,
                CreatedAtUtc = _nowUtc()
            };

            // Wait until AddAsync completes and give its returned int.
            // While waiting, the runtime frees the current thread to do other work.
            return await _repo.AddAsync(user, ct);
        }

        public async Task<LoginResult> LoginAsync(string username, string password, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
                return LoginResult.Fail("Missing credentials");

            var norm = Normalization.NormalizeUsername(username);
            var user = await _repo.GetByUsernameAsync(norm, ct);
            if (user is null) return LoginResult.Fail("Invalid username or password");

            var ok = _hasher.Verify(password, user.PasswordHash, user.PasswordSalt);
            return ok ? LoginResult.Ok(user) : LoginResult.Fail("Invalid username or password");
        }
    }

    /*
    In C#, a record is a special kind of reference type designed for data-centric types —
    classes meant to hold data, not to manage complex behavior.
    */

    // Declare a record type with a primary constructor — a shorthand that defines both a constructor and three init-only properties.

    public sealed record LoginResult(bool IsSuccess, User? User, string? Error)
    {
        // Factory methods to create instances of LoginResult for success or failure cases.
        public static LoginResult Ok(User u) => new(true, u, null);
        public static LoginResult Fail(string e) => new(false, null, e);
    }

    /*
    How it’s used

    return LoginResult.Ok(user);
    => expands to new LoginResult(true, user, null)

    return LoginResult.Fail("Invalid password");
    => expands to new LoginResult(false, null, "Invalid password")
    */
}
