namespace Trackit.Core.Services
{
    public static class Normalization
    {
        public static string NormalizeUsername(string s)
        {
            if (s is null) throw new ArgumentNullException(nameof(s));
            return s.Trim().ToLowerInvariant();
        }
    }
}
