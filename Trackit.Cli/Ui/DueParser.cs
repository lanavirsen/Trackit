using System.Globalization;
using System.Text.RegularExpressions;

namespace Trackit.Cli.Ui
{
    public static class DueParser
    {
        // Try to parse relaxed user input into UTC
        public static bool TryParseToUtc(string input, out DateTimeOffset dueUtc)
        {
            input = (input ?? "").Trim().ToLowerInvariant();
            var nowLocal = DateTimeOffset.Now;
            var today = nowLocal.Date;

            // 1) Keywords
            if (input == "now")
            {
                dueUtc = nowLocal.ToUniversalTime();
                return true;
            }

            // 2) Relative: +2h, +3d, in 90m
            var rel = Regex.Match(input, @"^(?:\+|in\s+)(?<n>\d+)\s*(?<unit>m|min|mins|minute|minutes|h|hr|hrs|hour|hours|d|day|days)$");
            if (rel.Success)
            {
                var n = int.Parse(rel.Groups["n"].Value, CultureInfo.InvariantCulture);
                var unit = rel.Groups["unit"].Value;
                var dt = unit switch
                {
                    "m" or "min" or "mins" or "minute" or "minutes" => nowLocal.AddMinutes(n),
                    "h" or "hr" or "hrs" or "hour" or "hours" => nowLocal.AddHours(n),
                    "d" or "day" or "days" => nowLocal.AddDays(n),
                    _ => nowLocal
                };
                dueUtc = dt.ToUniversalTime();
                return true;
            }

            // 3) today/tomorrow HH:mm
            var dayTime = Regex.Match(input, @"^(today|tomorrow)\s+(?<hm>\d{1,2}:\d{2})$");
            if (dayTime.Success)
            {
                var baseDay = dayTime.Groups[1].Value == "today" ? today : today.AddDays(1);
                if (TimeSpan.TryParse(dayTime.Groups["hm"].Value, CultureInfo.InvariantCulture, out var hm))
                {
                    var local = new DateTimeOffset(baseDay.Add(hm), nowLocal.Offset);
                    dueUtc = local.ToUniversalTime();
                    return true;
                }
            }

            // 4) HH:mm (today, or tomorrow if already passed)
            if (Regex.IsMatch(input, @"^\d{1,2}:\d{2}$") &&
                TimeSpan.TryParse(input, CultureInfo.InvariantCulture, out var onlyHm))
            {
                var local = new DateTimeOffset(today.Add(onlyHm), nowLocal.Offset);
                if (local <= nowLocal) local = local.AddDays(1); // next occurrence
                dueUtc = local.ToUniversalTime();
                return true;
            }

            // 5) YYYY-MM-DD HH:mm (no seconds)
            if (DateTimeOffset.TryParseExact(
                    input,
                    new[] { "yyyy-MM-dd HH:mm", "yyyy-MM-dd H:mm" },
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeLocal,
                    out var localDateTime))
            {
                dueUtc = localDateTime.ToUniversalTime();
                return true;
            }

            // 6) YYYY-MM-DD (assume 23:59 local)
            if (DateTime.TryParseExact(
                    input,
                    "yyyy-MM-dd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var dateOnly))
            {
                var local = new DateTimeOffset(dateOnly.Year, dateOnly.Month, dateOnly.Day, 23, 59, 0, nowLocal.Offset);
                dueUtc = local.ToUniversalTime();
                return true;
            }

            // 7) Fallback: try general parse (local)
            if (DateTimeOffset.TryParse(input, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var any))
            {
                dueUtc = any.ToUniversalTime();
                return true;
            }

            dueUtc = default;
            return false;
        }

        public static string Hint =>
            "Examples: '2025-10-12 18:00', '14:30', 'today 19:00', 'tomorrow 09:00', '+2h', 'in 90m', 'now'.";
    }
}
