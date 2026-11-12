using System.Globalization;

namespace LNXcounter.Services;

/// <summary>Every clock time shown in the app is Paris time (CET in winter, CEST in summer), whatever the PC's own time zone is.</summary>
public static class Paris
{
    public static readonly TimeZoneInfo Zone = Find();
    static readonly CultureInfo En = CultureInfo.CurrentCulture;   // Windows format: 8:00 PM or 20:00, Sat or sam.
    static readonly bool Use12h = En.DateTimeFormat.ShortTimePattern.Contains('t');

    static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Romance Standard Time", "Europe/Paris" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { }
        return TimeZoneInfo.Local;
    }

    public static DateTime FromUtc(DateTime utc) =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);

    /// <summary>A time read from a replay file name (written in the PC's local time) shown in Paris time.</summary>
    public static DateTime FromLocal(DateTime local)
    {
        try { return TimeZoneInfo.ConvertTime(DateTime.SpecifyKind(local, DateTimeKind.Unspecified), TimeZoneInfo.Local, Zone); }
        catch { return local; }
    }

    public static string Day(DateTime t) => t.ToString(En.TwoLetterISOLanguageName == "en" ? "ddd, MMM d" : "ddd d MMM", En);   // Sat, Nov 14 / sam. 14 nov.
    public static string Clock(DateTime t) => t.ToString(Use12h ? "h:mm tt" : "HH:mm", En);   // 8:00 PM / 20:00

    /// <summary>"8:00 – 10:00 PM", or "11:00 PM – 1:00 AM" when the window crosses noon or midnight.</summary>
    public static string Range(DateTime s, DateTime e) =>
        Use12h && s.ToString("tt", En) == e.ToString("tt", En) ? $"{s.ToString("h:mm", En)} – {Clock(e)}" : $"{Clock(s)} – {Clock(e)}";

    /// <summary>"3:00:52:32" (days:hours:minutes:seconds), like the countdown on fortnite.bot.</summary>
    public static string Countdown(TimeSpan t)
    {
        if (t < TimeSpan.Zero) t = TimeSpan.Zero;
        return $"{(int)t.TotalDays}:{t.Hours:00}:{t.Minutes:00}:{t.Seconds:00}";
    }
}
