namespace Trackit.Core.Services
{
    public static class Normalization
    {
        // NormalizeUsername trims whitespace and converts the username to lowercase.
        public static string NormalizeUsername(string s)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));
            return s.Trim().ToLowerInvariant();
        }
    }
}
