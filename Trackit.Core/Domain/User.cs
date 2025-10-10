using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Trackit.Core.Domain
{
    public sealed class User // sealed → cannot be inherited.
    {
        /*
        get; set; → can change value any time (mutable).
        get; init; → can set only once, when creating the object (immutable afterwards).
        */

        public int Id { get; init; }
        public string Username { get; init; } = null!; // tell the compiler to skip the nullability check.
        public string? Email { get; init; }
        public byte[] PasswordHash { get; init; } = Array.Empty<byte>();
        public byte[] PasswordSalt { get; init; } = Array.Empty<byte>();
        public DateTimeOffset CreatedAtUtc { get; init; }   // record when the user account was created, in universal time (UTC).
    }
}


/*
PBKDF2 = Password-Based Key Derivation Function 2

It is a standard algorithm for converting a plain-text password into a derived key (a fixed-length sequence of bytes).
PBKDF2 deliberately makes hashing slow and unique per password so attackers can’t brute-force millions of guesses quickly.


How it works (simplified)

1. Input:
    - Password (user text).
    - Salt - a random 32-byte value unique per user.
    - Iteration count - e.g., 100,000 (how many times to re-hash).
    - Hash algorithm - e.g., SHA-256.

2. Process:
    The function repeatedly applies HMAC-SHA256 to the password + salt for the given number of iterations.
    The output is a fixed-length byte array (e.g., 32 bytes).

3. Output:
    - PasswordHash → result of the PBKDF2 computation.
    - PasswordSalt → the random salt used so identical passwords produce different hashes.


byte[] — array of bytes;
Array.Empty<byte>() — returns a single shared, zero-length byte[].
It avoids allocating a new array and guarantees the property isn’t null.

Why initialize to Array.Empty<byte>() instead of null:
- Prevents NullReferenceException if something inspects the property before a value is assigned.
- Makes it explicit that “no data yet” is represented by an empty array, not null.
*/