using System.Collections.Concurrent;
using System.Threading;
using Trackit.Core.Domain;
using Trackit.Core.Ports;

namespace Trackit.Data.Repositories
{
    public sealed class InMemoryUserRepository : IUserRepository
    {
        /*
        Declare and initialize a thread-safe dictionary that maps usernames to User objects.
            - readonly - once assigned (here, on construction), the field reference itself can’t point to a new dictionary.
            - ConcurrentDictionary<TKey,TValue> – a dictionary implementation designed for safe concurrent access by multiple threads
              without needing explicit lock statements.
            - <string, User> – the key type is string (normalized username), and the value type is User.
            - = new(); – shorthand for = new ConcurrentDictionary<string, User>();

        The dictionary is thread-safe because ConcurrentDictionary internally manages synchronization
        so that multiple threads can read, write, or update it at the same time without corrupting its state or throwing exceptions.

        A thread is the smallest unit of execution inside a process — essentially a single sequence of instructions that the CPU runs.

        Key points
            - Every running program (process) starts with one thread: the main thread.
            - You can create additional threads to perform work concurrently.
            - Each thread runs independently but shares the same memory space with other threads in the same process.

        A reference
        In C#, most types (like Dictionary, List, etc.) are reference types.
        A reference is not the object itself — it’s like a pointer or an address that tells the runtime where that object lives in memory.

        readonly means:
            - After the constructor finishes, you can’t reassign _byUsername to point to a different dictionary object.
            - You can still change the contents of the dictionary (add/remove items).
        */

        private readonly ConcurrentDictionary<string, User> _byUsername = new();

        private int _nextId = 0;

        // Look up a user in the dictionary by username and return it immediately.
        // Key is normalized username. Caller must pass normalized.
        // CancellationToken is unused here but included for API consistency with truly async implementation.

        public Task<User?> GetByUsernameAsync(string normalizedUsername, CancellationToken ct = default)
        {
            /*
            - Checks if the dictionary _byUsername contains a key equal to normalizedUsername.
            - If it exists, user is assigned that User value.
            - If it doesn’t, user becomes null.
            - The method returns a boolean (true/false), but that result is ignored;
            we only care about the user variable populated by the out parameter.
            */

            _byUsername.TryGetValue(normalizedUsername, out var user);

            return Task.FromResult(user);
        }

        /*
        Conceptually
        - Synchronous: The method runs from start to finish before control returns to the caller. The thread is blocked until it’s done.
        - Asynchronous: The method may start work (like a database call, file I/O, or web request), yield control to the caller,
          and later resume when the result is ready — freeing the thread to do other things in the meantime.

        I/O stands for Input/Output — any operation where your program communicates with something outside its own memory.

        In .NET, a Task object represents a unit of work that may complete in the future — possibly asynchronously.
        Task ≈ a promise of a result.
        */

        // Check if a user with this username exists in the in-memory dictionary, and return the boolean result as a completed asynchronous task.
        public Task<bool> ExistsAsync(string normalizedUsername, CancellationToken ct = default)
            => Task.FromResult(_byUsername.ContainsKey(normalizedUsername));

        // Add a new user to the in-memory dictionary, generating a unique Id, and return that Id as a completed asynchronous task.
        public Task<int> AddAsync(User user, CancellationToken ct = default)
        {
            // Simple guardrails
            if (string.IsNullOrWhiteSpace(user.Username))
                throw new ArgumentException("Username required", nameof(user));
            if (user.PasswordHash is null || user.PasswordSalt is null)
                throw new ArgumentException("Password not hashed", nameof(user));

            /* Generate Id
            
            Interlocked.Increment(...) is a thread-safe way to increment an integer.

            Why not just _nextId++?
            - _nextId++ is not atomic — it compiles into separate read, add, and write steps.
              If two threads run _nextId++ concurrently, both might read the same old value
              before either writes back, resulting in duplicate IDs.
            - Interlocked.Increment guarantees each thread gets a unique, sequential value even under concurrency.
            */

            var id = Interlocked.Increment(ref _nextId);

            var stored = new User
            {
                Id = id,
                Username = user.Username,           // should already be normalized
                Email = user.Email,
                PasswordHash = user.PasswordHash,
                PasswordSalt = user.PasswordSalt,
                CreatedAtUtc = user.CreatedAtUtc
            };

            /*
            The code attempts to insert the new User into the dictionary keyed by username.
            If another user with the same username is already there, the addition fails atomically,
            and the repository explicitly throws an error to indicate the username must be unique.
            */

            if (!_byUsername.TryAdd(stored.Username, stored))
                throw new InvalidOperationException("Username already exists");

            // Return the generated Id as a completed Task.
            return Task.FromResult(id);
        }
    }
}
