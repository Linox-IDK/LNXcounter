using System.Text.Json;
using System.Text.Json.Serialization;

namespace LNXcounter.Services;

public class MatchRecord
{
    public string File { get; set; } = "";
    public DateTime Started { get; set; }
    public int Opened { get; set; }
    public int Broken { get; set; }
    public Dictionary<string, int> OpenedByType { get; set; } = new();
    public string? Error { get; set; }
    /// <summary>Version of the analysis that produced this record. Replays still on disk are read again when it is older.</summary>
    public int Analysis { get; set; }
    /// <summary>Treasure Hunters rewards and gizmos seen in this match (golden chest prizes, equipped gizmos).</summary>
    public List<TreasureFind> Finds { get; set; } = new();
    [JsonIgnore] public string FindsText => Finds.Count(f => f.Mine) is var n and > 0 ? Loc.F(n > 1 ? "  ★ {0} treasures" : "  ★ {0} treasure", n) : Finds.Count > 0 ? "  ☆" : "";
    /// <summary>Tooltip of the match row: every treasure seen, yours first.</summary>
    [JsonIgnore] public string? FindsDetail => Finds.Count == 0 ? null
        : string.Join("\n", Finds.OrderByDescending(f => f.Mine).ThenBy(f => f.AtSeconds)
            .Select(f => Loc.F("{0}: {1} ({2} into the match)", Loc.T(f.Mine ? "★ You" : "☆ Nearby player"), f.Name, $"{(int)(f.AtSeconds / 60)}:{(int)(f.AtSeconds % 60):00}")));
    /// <summary>Replay file size when it was read. If the file later grows, it is read again.</summary>
    public long Size { get; set; }
    [JsonIgnore] public string DateText => Paris.FromLocal(Started).ToString("ddd d MMM · HH:mm");
}

/// <summary>One treasure seen in a replay. Kind: "prize" (golden chest reward) or "gizmo". Mine = it was for the recording player.</summary>
public class TreasureFind
{
    public string Kind { get; set; } = "";
    public string Name { get; set; } = "";
    /// <summary>Drop key it ticks on the Drops page ("token", "outfit", "pickaxe", "vehicle", "jam", "loading", week number in Week), or empty.</summary>
    public string DropId { get; set; } = "";
    public int Week { get; set; }
    public bool Mine { get; set; }
    public double AtSeconds { get; set; }
}

public class Settings
{
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public bool OverlayVisible { get; set; } = true;
    /// <summary>Fortnite name read from the replays (shown next to the app name).</summary>
    public string? PlayerName { get; set; }
    public bool OverlayCollapsed { get; set; }
    public string? ReplayFolder { get; set; }
    public int Volume { get; set; } = 60;
    /// <summary>Pop-up with a sound in the top right corner when a Treasure Hour starts.</summary>
    public bool HourAlert { get; set; } = true;
    /// <summary>Warnings 30 minutes (silent) and 10 minutes (with sound) before a Treasure Hour.</summary>
    public bool HourWarn { get; set; } = true;
    /// <summary>Look for a new LNXcounter version on GitHub when the app starts (and every few hours).</summary>
    public bool AutoUpdateCheck { get; set; } = true;
    /// <summary>Chests added by hand (played on another device, or before the app was installed). Counted as normal chests.</summary>
    public int ManualChests { get; set; }
}

public class StateFile
{
    public int Version { get; set; } = 1;
    public Settings Settings { get; set; } = new();
    public List<MatchRecord> Matches { get; set; } = new();
    /// <summary>Drops the user ticked as found, keyed like "w2:token".</summary>
    public Dictionary<string, bool> Found { get; set; } = new();
}

/// <summary>JSON save file in %APPDATA%\LNXcounter. Written atomically, with a .bak of the previous save.</summary>
public static class Store
{
    // LNX_DATA is only meant for testing: it points the app at another folder so a test run never touches the real save
    public static readonly string Dir = Environment.GetEnvironmentVariable("LNX_DATA") is { Length: > 0 } custom
        ? custom
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "LNXcounter");
    static readonly string FilePath = Path.Combine(Dir, "state.json");
    static readonly JsonSerializerOptions Opt = new() { WriteIndented = true };

    public static StateFile Load()
    {
        foreach (var p in new[] { FilePath, FilePath + ".bak" })
        {
            try { if (File.Exists(p)) return JsonSerializer.Deserialize<StateFile>(File.ReadAllText(p)) ?? new(); } catch { }
        }
        return new();
    }

    public static void Save(StateFile s)
    {
        try
        {
            Directory.CreateDirectory(Dir);
            var tmp = FilePath + ".tmp";
            File.WriteAllText(tmp, JsonSerializer.Serialize(s, Opt));
            if (File.Exists(FilePath)) File.Copy(FilePath, FilePath + ".bak", true);
            File.Move(tmp, FilePath, true);
        }
        catch { }
    }
}
