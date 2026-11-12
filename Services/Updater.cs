using System.Net.Http;
using System.Text.Json;

namespace LNXcounter.Services;

public record UpdateInfo(string Tag, Version Version, string Url, long Size);

/// <summary>Looks for a newer release on the project's GitHub page and swaps the exe for it. A running exe cannot be overwritten but it can be
/// renamed, so the new file takes its place and the old one is deleted at the next start.</summary>
public static class Updater
{
    const string Repo = "Linox-IDK/LNXcounter";
    static readonly HttpClient Http = Make();

    static HttpClient Make()
    {
        var c = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        c.DefaultRequestHeaders.UserAgent.ParseAdd("LNXcounter-updater");
        c.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
        return c;
    }

    /// <summary>The version that is running. LNX_FAKEVER is only for testing the update path.</summary>
    public static Version Current =>
        Version.TryParse(Environment.GetEnvironmentVariable("LNX_FAKEVER"), out var f) ? f : Version.Parse(App.Version);

    static string ExePath => Environment.ProcessPath ?? "";

    /// <summary>Null when this is the newest version (or GitHub could not be reached: see error).</summary>
    public static async Task<(UpdateInfo? Info, string? Error)> CheckAsync()
    {
        try
        {
            using var doc = JsonDocument.Parse(await Http.GetStringAsync($"https://api.github.com/repos/{Repo}/releases/latest"));
            var root = doc.RootElement;
            var tag = root.GetProperty("tag_name").GetString() ?? "";
            if (!Version.TryParse(tag.TrimStart('v', 'V'), out var v)) return (null, "Could not read the latest version.");
            if (v <= Current) return (null, null);
            foreach (var a in root.GetProperty("assets").EnumerateArray())
            {
                if (!string.Equals(a.GetProperty("name").GetString(), "LNXcounter.exe", StringComparison.OrdinalIgnoreCase)) continue;
                var url = a.GetProperty("browser_download_url").GetString() ?? "";
                return (new UpdateInfo(tag, v, url, a.GetProperty("size").GetInt64()), null);
            }
            return (null, "The new release has no LNXcounter.exe file yet.");
        }
        catch (Exception ex) { return (null, "Could not check for updates: " + ex.Message); }
    }

    /// <summary>Downloads the new exe next to the current one and swaps them. Returns an error text, or null when it worked.</summary>
    public static async Task<string?> InstallAsync(UpdateInfo info, IProgress<int>? progress = null)
    {
        string tmp = ExePath + ".new";
        try
        {
            if (!Uri.TryCreate(info.Url, UriKind.Absolute, out var u) || u.Scheme != Uri.UriSchemeHttps ||
                !(u.Host == "github.com" || u.Host.EndsWith(".github.com") || u.Host.EndsWith(".githubusercontent.com")))
                return "The download address is not a GitHub address, so it was refused.";

            using (var r = await Http.GetAsync(u, HttpCompletionOption.ResponseHeadersRead))
            {
                r.EnsureSuccessStatusCode();
                long total = r.Content.Headers.ContentLength ?? info.Size;
                await using var src = await r.Content.ReadAsStreamAsync();
                await using var dst = File.Create(tmp);
                var buf = new byte[81920]; long got = 0; int n, last = -1;
                while ((n = await src.ReadAsync(buf)) > 0)
                {
                    await dst.WriteAsync(buf.AsMemory(0, n));
                    got += n;
                    int pct = total > 0 ? (int)(got * 100 / total) : 0;
                    if (pct != last) { last = pct; progress?.Report(pct); }
                }
            }
            var fi = new FileInfo(tmp);
            using (var fs = fi.OpenRead())
                if (fi.Length < 1_000_000 || fs.ReadByte() != 'M' || fs.ReadByte() != 'Z') { File.Delete(tmp); return "The downloaded file is not a valid program, nothing was changed."; }

            string old = ExePath + ".old";
            if (File.Exists(old)) File.Delete(old);
            File.Move(ExePath, old);
            try { File.Move(tmp, ExePath); }
            catch { File.Move(old, ExePath); throw; }
            return null;
        }
        catch (Exception ex)
        {
            try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            return "Update failed: " + ex.Message;
        }
    }

    /// <summary>Removes what an earlier update left behind.</summary>
    public static void Cleanup()
    {
        foreach (var f in new[] { ExePath + ".old", ExePath + ".new" })
            try { if (File.Exists(f)) File.Delete(f); } catch { }
    }

    /// <summary>"LNXcounter.exe -up" in a terminal: check, download, swap, report in the terminal.</summary>
    public static int RunCli()
    {
        AttachConsole(-1);
        var o = new StreamWriter(Console.OpenStandardOutput()) { AutoFlush = true };
        o.WriteLine();
        o.WriteLine($"LNXcounter {Current} - looking for updates...");
        var (info, err) = Task.Run(CheckAsync).GetAwaiter().GetResult();   // off the UI thread, or the await would wait for it forever
        if (err != null) { o.WriteLine(err); return 1; }
        if (info == null) { o.WriteLine("You already have the newest version."); return 0; }
        o.WriteLine($"New update available {info.Tag}. Downloading ({info.Size / 1_048_576} MB)...");
        int shown = -10;
        var res = Task.Run(() => InstallAsync(info, new Progress<int>(p => { if (p >= shown + 10) { shown = p; o.Write($" {p}%"); } }))).GetAwaiter().GetResult();
        o.WriteLine();
        if (res != null) { o.WriteLine(res); return 1; }
        o.WriteLine($"Updated to {info.Tag}. Start LNXcounter again (if it is running, quit it from the tray first).");
        return 0;
    }

    [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool AttachConsole(int pid);
}
