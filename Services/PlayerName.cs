using System.Text.RegularExpressions;

namespace LNXcounter.Services;

/// <summary>The Fortnite name of the player on this PC. Replays only hold Epic account ids, not display names, so it is read from the
/// Fortnite log (%LOCALAPPDATA%\\FortniteGame\\Saved\\Logs), which names the account that launched the game. Nothing is sent anywhere.</summary>
public static class PlayerName
{
    static readonly Regex Arg = new(@"-epicusername=""([^""]{3,32})""", RegexOptions.Compiled);
    static readonly Regex Display = new(@"DisplayName=\[([^\[\]]{3,32})\] EpicAccountId=\[MCP:", RegexOptions.Compiled);

    public static string? Find()
    {
        try
        {
            var dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Logs");
            if (!Directory.Exists(dir)) return null;
            foreach (var f in new DirectoryInfo(dir).GetFiles("*.log").OrderByDescending(x => x.LastWriteTimeUtc).Take(4))
            {
                using var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                using var rd = new StreamReader(fs);
                string? line; int n = 0;
                while ((line = rd.ReadLine()) != null && ++n < 400_000)
                {
                    if (line.Contains("-epicusername=") && Arg.Match(line) is { Success: true } m) return m.Groups[1].Value;
                    if (line.Contains("DisplayName=[") && Display.Match(line) is { Success: true } d) return d.Groups[1].Value;
                }
            }
        }
        catch { }
        return null;
    }
}
