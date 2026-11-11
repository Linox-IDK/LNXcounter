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
    /// <summary>Replay file size when it was read. If the file later grows, it is read again.</summary>
    public long Size { get; set; }
    [JsonIgnore] public string DateText => Paris.FromLocal(Started).ToString("ddd d MMM · HH:mm");
}

public class Settings
{
    public double OverlayLeft { get; set; } = double.NaN;
    public double OverlayTop { get; set; } = double.NaN;
    public bool OverlayVisible { get; set; } = true;
    public bool OverlayCollapsed { get; set; }
    public string? ReplayFolder { get; set; }
    public int Volume { get; set; } = 60;
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
