using System;
using System.Globalization;

namespace LastFolder.Helpers
{
    internal static class Formatters
    {
        public static string RelativeTime(DateTime time)
        {
            var now = DateTime.Now;
            var diff = now - time;
            CultureInfo culture = Strings.Culture;

            if (diff.TotalSeconds < 60) return Strings.JustNow;
            if (diff.TotalMinutes < 60) return Strings.MinutesAgo((int)diff.TotalMinutes);
            if (time.Date == now.Date) return Strings.HoursAgo((int)diff.TotalHours);
            if (time.Date == now.Date.AddDays(-1)) return Strings.Yesterday(time.ToString("HH:mm", culture));
            if (time.Year == now.Year) return time.ToString("d MMM, HH:mm", culture);
            return time.ToString("d MMM yyyy", culture);
        }

        public static string FileSize(long? bytes)
        {
            if (bytes is null) return "—";

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double value = bytes.Value;
            int unit = 0;
            while (value >= 1024 && unit < units.Length - 1)
            {
                value /= 1024;
                unit++;
            }

            CultureInfo culture = Strings.Culture;
            if (unit == 0) return $"{value:0} B";
            return value < 10
                ? value.ToString("0.#", culture) + " " + units[unit]
                : value.ToString("0", culture) + " " + units[unit];
        }
    }
}
