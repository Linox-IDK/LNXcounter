using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;

namespace LNXcounter.Services;

/// <summary>
/// A small log to help with bug reports: %APPDATA%\LNXcounter\logs\lnxcounter.log (the previous one is kept as lnxcounter.1.log, each is at most 512 KB).
/// It records what the app does (start, quit, matches read, errors, update checks), nothing personal: your Windows user name is hidden
/// (replaced by %USERPROFILE% or &lt;user&gt;), so a log can be posted publicly. Settings > Data > Logs copies or saves it.
/// </summary>
public static class Log
{
    static readonly object Gate = new();
    const long MaxBytes = 512 * 1024;

    public static string Dir => Path.Combine(Store.Dir, "logs");
    public static string FilePath => Path.Combine(Dir, "lnxcounter.log");
    static string OldPath => Path.Combine(Dir, "lnxcounter.1.log");

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null) => Write("ERROR", ex == null ? message : message + Environment.NewLine + Describe(ex));

    static string Describe(Exception ex)
    {
        var sb = new StringBuilder();
        for (var e = ex; e != null; e = e.InnerException)
        {
            sb.Append(e.GetType().FullName).Append(": ").AppendLine(e.Message);
            if (e.StackTrace != null) sb.AppendLine(e.StackTrace);
            if (e.InnerException != null) sb.AppendLine("--- caused by ---");
        }
        return sb.ToString().TrimEnd();
    }

    /// <summary>Your Windows user name is part of many paths: it is hidden so that a log can be shared.</summary>
    static string Clean(string s)
    {
        try
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (home.Length > 3) s = s.Replace(home, "%USERPROFILE%", StringComparison.OrdinalIgnoreCase);
            var user = Environment.UserName;
            if (user.Length > 2) s = s.Replace(user, "<user>", StringComparison.OrdinalIgnoreCase);
        }
        catch { }
        return s;
    }

    static void Write(string level, string message)
    {
        try
        {
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {Clean(message)}{Environment.NewLine}";
            lock (Gate)
            {
                Directory.CreateDirectory(Dir);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes) File.Move(FilePath, OldPath, true);
                File.AppendAllText(FilePath, line, new UTF8Encoding(false));
            }
        }
        catch { }   // logging must never be the thing that breaks the app
    }

    /// <summary>What is useful to know about this PC and this copy of the app, without anything personal.</summary>
    public static string Header()
    {
        var sb = new StringBuilder();
        sb.AppendLine("LNXtracker log");
        sb.AppendLine($"Version:      {App.Version}");
        sb.AppendLine($"Windows:      {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        sb.AppendLine($".NET:         {RuntimeInformation.FrameworkDescription}");
        sb.AppendLine($"Processors:   {Environment.ProcessorCount}");
        sb.AppendLine($"Language:     {CultureInfo.CurrentUICulture.Name}");
        sb.AppendLine($"Started:      {StartedAt:yyyy-MM-dd HH:mm:ss}");
        try
        {
            var m = App.Model;
            if (m != null)
            {
                sb.AppendLine($"Low-resource: {App.Low}");
                sb.AppendLine($"Overlay:      {(m.Settings.OverlayVisible ? "on" : "off")}, {(m.Settings.MinimalOverlay ? "minimal" : "full")}");
                sb.AppendLine($"Replay folder found: {m.FolderOk}");
                sb.AppendLine($"Matches read: {m.MatchCount}");
                sb.AppendLine($"Fortnite running: {m.FortniteRunning}");
            }
        }
        catch { }
        return sb.ToString();
    }

    public static readonly DateTime StartedAt = DateTime.Now;

    /// <summary>The header and the end of the log (the last ~600 lines), ready to paste into a bug report.</summary>
    public static string Report()
    {
        var sb = new StringBuilder(Header());
        sb.AppendLine("------------------------------------------------------------");
        try
        {
            lock (Gate)
            {
                var lines = new List<string>();
                if (File.Exists(OldPath)) lines.AddRange(File.ReadAllLines(OldPath));
                if (File.Exists(FilePath)) lines.AddRange(File.ReadAllLines(FilePath));
                if (lines.Count == 0) sb.AppendLine("(the log is empty)");
                foreach (var l in lines.Skip(Math.Max(0, lines.Count - 600))) sb.AppendLine(l);
            }
        }
        catch (Exception e) { sb.AppendLine("(could not read the log: " + Clean(e.Message) + ")"); }
        return sb.ToString();
    }
}
