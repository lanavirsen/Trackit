using System.Security.Cryptography;

namespace Trackit.Core.Auth
{
    public sealed class PasswordHasher
    {
        private const int Iterations = 100_000;
        private const int SaltLen = 32;
        private const int HashLen = 32;

        // Implement secure password hashing and verification using PBKDF2.

        public (byte[] Hash, byte[] Salt) Hash(string password)
        {
            if (password is null) throw new ArgumentNullException(nameof(password)); // validate input.
            byte[] salt = RandomNumberGenerator.GetBytes(SaltLen); // create a unique 32-byte salt for this password.

            // Derive a cryptographic hash.
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);
            byte[] hash = pbkdf2.GetBytes(HashLen); // output a 32-byte hash.

            return (hash, salt);

            // Both hash and salt must be stored with the user record.
            // (Can't verify later without the original salt.)
        }

        public bool Verify(string password, byte[] hash, byte[] salt)
        {
            // Validate inputs.
            if (password is null) throw new ArgumentNullException(nameof(password));
            if (hash is null) throw new ArgumentNullException(nameof(hash));
            if (salt is null) throw new ArgumentNullException(nameof(salt));

            // Recompute the hash from the provided password and stored salt.
            using var pbkdf2 = new Rfc2898DeriveBytes(password, salt, Iterations, HashAlgorithmName.SHA256);

            // Run PBKDF2 to generate a fixed-length cryptographic hash of the password.
            byte[] computed = pbkdf2.GetBytes(HashLen);

            return CryptographicOperations.FixedTimeEquals(computed, hash); // compare hashes in constant time.

            /*
            FixedTimeEquals compares every byte of both arrays in the same amount of time, regardless of where the first mismatch occurs.

            A normal comparison (SequenceEqual, ==, or manual loop) can stop early when a mismatch is found;
            an attacker could measure those timing differences and infer part of the hash.

            FixedTimeEquals prevents that by always taking identical CPU time for equal-length arrays, making timing attacks impractical.
            */
        }
    }
}
