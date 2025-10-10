using System;
using FluentAssertions;
using Trackit.Core.Services;
using Xunit;

namespace Trackit.Tests
{
    public class NormalizationTests
    {
        // In xUnit, [Fact] marks a test with one fixed scenario,
        // while [Theory] marks a test that should run the same logic against multiple sets of data.

        [Theory]
        [InlineData("  Lana  ", "lana")]
        [InlineData("LANA", "lana")]
        [InlineData("La Na", "la na")]
        public void NormalizeUsername_works(string input, string expected)
            => Normalization.NormalizeUsername(input).Should().Be(expected);

        [Fact]

        // Verify that Normalization.NormalizeUsername correctly throws an exception when it’s called with a null argument.
        public void NormalizeUsername_throws_on_null()
            => Assert.Throws<ArgumentNullException>(() => Normalization.NormalizeUsername(null!));
    }
}
