using System;
using System.Linq;
using FluentAssertions;
using Trackit.Core.Auth;
using Xunit;

namespace Trackit.Tests
{
    public class PasswordHasherTests
    {
        // [Fact] is an xUnit attribute marking a method as a test case with no parameters.
        // On dotnet test, xUnit looks for methods decorated with [Fact] and executes them automatically.

        [Fact]
        public void Hash_then_verify_succeeds_for_correct_password()
        {
            var hasher = new PasswordHasher();
            var (hash, salt) = hasher.Hash("P@ssw0rd!");

            // Hash the plain password again using the same salt and then compares the two hashes.
            hasher.Verify("P@ssw0rd!", hash, salt).Should().BeTrue(); 
        }

        [Fact]
        public void Verify_fails_for_wrong_password()
        {
            var hasher = new PasswordHasher();
            var (hash, salt) = hasher.Hash("P@ssw0rd!");
            hasher.Verify("wrong", hash, salt).Should().BeFalse();
        }

        [Fact]
        public void Same_password_different_salt_produces_different_hash()
        {
            var hasher = new PasswordHasher();
            var (h1, _) = hasher.Hash("P@ssw0rd!");
            var (h2, _) = hasher.Hash("P@ssw0rd!");
            h1.SequenceEqual(h2).Should().BeFalse();
        }

        [Fact]
        public void Salt_and_hash_have_expected_lengths()
        {
            var hasher = new PasswordHasher();
            var (hash, salt) = hasher.Hash("x");
            hash.Length.Should().Be(32);
            salt.Length.Should().Be(32);
        }
    }
}
