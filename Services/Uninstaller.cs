using System.Diagnostics;
using System.Text;

namespace LNXcounter.Services;

/// <summary>
/// Removes LNXcounter completely: the "Start with Windows" entry, the save folder (%APPDATA%\LNXcounter: stats, settings, backups of the save, logs, the song
/// player's data) and the program itself with every copy an update left next to it. A program cannot delete itself while it runs, so a small hidden
/// script (in the temp folder, it deletes itself last) waits for the app to close and then does the deleting, trying again for up to 40 seconds.
/// The folder of the exe is removed only when nothing else is left in it. Fortnite's replays and files you saved yourself (exports, backups) are never touched.
/// </summary>
public static class Uninstaller
{
    /// <summary>Starts the removal. Returns an error text, or null when the script was started (the caller then quits the app right away).</summary>
    public static string? Start()
    {
        try
        {
            var exe = Environment.ProcessPath ?? "";
            var data = Path.GetFullPath(Store.Dir);
            if (exe.Length == 0 || !File.Exists(exe)) return "The program file could not be found.";
            // a safety net: never aim the deletion at a drive root or at a big folder that is not ours
            var profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (data.Length < 12 || Path.GetPathRoot(data) == data || SamePath(data, profile) || SamePath(data, appData))
                return "The save folder looks wrong, nothing was deleted: " + data;

            if (Environment.GetEnvironmentVariable("LNX_DATA") is not { Length: > 0 }) Startup.Set(false);   // a test run (LNX_DATA) must not touch the real Windows entry
            Log.Info("Uninstall started");

            var dir = Path.GetDirectoryName(exe)!;
            var name = Path.GetFileName(exe);
            var stem = Path.GetFileNameWithoutExtension(exe);
            var sb = new StringBuilder();
            sb.AppendLine("@echo off");
            sb.AppendLine("rem LNXcounter uninstall: waits for the app to close, then removes everything");
            // the program and what updates leave next to it (name.exe.old, name.exe.new, name.old*)
            sb.AppendLine($"for /l %%i in (1,1,40) do if exist {Q(exe)} (del /f /q {Q(exe)} >nul 2>&1 & ping -n 2 127.0.0.1 >nul)");
            sb.AppendLine($"del /f /q {Q(exe + ".old")} {Q(exe + ".new")} {Q(Path.Combine(dir, stem + ".old*"))} >nul 2>&1");
            // the save folder
            sb.AppendLine($"for /l %%i in (1,1,40) do if exist {Q(data)} (rd /s /q {Q(data)} >nul 2>&1 & ping -n 2 127.0.0.1 >nul)");
            // the folder of the program, only when it is empty now (never a folder like Downloads)
            sb.AppendLine($"rd {Q(dir)} >nul 2>&1");
            sb.AppendLine("(goto) 2>nul & del \"%~f0\"");

            var script = Path.Combine(Path.GetTempPath(), "lnxcounter-uninstall-" + Guid.NewGuid().ToString("N")[..8] + ".cmd");
            File.WriteAllText(script, sb.ToString(), new UTF8Encoding(false));
            Process.Start(new ProcessStartInfo("cmd.exe", "/c " + Q(script)) { UseShellExecute = false, CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
            return null;
        }
        catch (Exception ex)
        {
            Log.Error("Uninstall could not start", ex);
            return "Could not start the uninstall: " + ex.Message;
        }
    }

    static bool SamePath(string a, string b) =>
        b.Length > 0 && string.Equals(a.TrimEnd('\\'), Path.GetFullPath(b).TrimEnd('\\'), StringComparison.OrdinalIgnoreCase);

    /// <summary>A path for a batch file: in quotes, and a percent sign doubled.</summary>
    static string Q(string p) => "\"" + p.Replace("%", "%%") + "\"";
}
