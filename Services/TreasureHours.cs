using System.Text.Json;

namespace LNXcounter.Services;

/// <summary>One Treasure Hour. The multipliers are how much it boosts the odds of the Gold Outfit, the Pirate Shop token and the vehicle decal.</summary>
public record TreasureWindow(DateTime StartUtc, DateTime EndUtc, bool Confirmed, double Outfit = 7.5, double Token = 1.5, double Vehicle = 10);

/// <summary>Fortnite Treasure Hours (the pirate / fortune hour), as listed on fortnite.bot/treasure. Override the list by creating
/// %APPDATA%\LNXcounter\treasure-hours.json, for example:
/// [ { "StartUtc": "2026-11-14T19:00:00Z", "EndUtc": "2026-11-14T21:00:00Z", "Confirmed": true, "Outfit": 7.5, "Token": 1.5, "Vehicle": 10 } ]</summary>
public static class TreasureHours
{
    public static List<TreasureWindow> Load()
    {
        try
        {
            var f = Path.Combine(Store.Dir, "treasure-hours.json");
            if (File.Exists(f))
            {
                var list = JsonSerializer.Deserialize<List<TreasureWindow>>(File.ReadAllText(f));
                if (list is { Count: > 0 }) return list.OrderBy(w => w.StartUtc).ToList();
            }
        }
        catch { }
        return Defaults();
    }

    static List<TreasureWindow> Defaults()
    {
        // fortnite.bot lists the windows in US Eastern time: Saturdays 2-4 PM and 9-11 PM EST (UTC-5). Stored in UTC, shown in Paris time.
        var l = new List<TreasureWindow>
        {
            new(U(2026, 11, 7, 19), U(2026, 11, 7, 21), true),                              // Sat 7 Nov, 2-4 PM EST
            new(U(2026, 11, 8, 2), U(2026, 11, 8, 4), true, 10, 2, 10),                     // Sat 7 Nov, 9-11 PM EST (the stronger one)
        };
        foreach (var day in new[] { 14, 21, 28 })
        {
            l.Add(new(U(2026, 11, day, 19), U(2026, 11, day, 21), true));
            l.Add(new(U(2026, 11, day + 1, 2), U(2026, 11, day + 1, 4), true));
        }
        return l;
    }

    static DateTime U(int y, int m, int d, int h) => new(y, m, d, h, 0, 0, DateTimeKind.Utc);

    public static TreasureWindow? Next(List<TreasureWindow> all, DateTime nowUtc) => all.FirstOrDefault(w => w.EndUtc > nowUtc);
}
