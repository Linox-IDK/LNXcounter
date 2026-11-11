using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows.Threading;
using LNXcounter.Services;

namespace LNXcounter;

public abstract class Notify : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;    protected void Raise([CallerMemberName] string? n = null) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));
    protected bool Set<T>(ref T f, T v, [CallerMemberName] string? n = null)
    {
        if (EqualityComparer<T>.Default.Equals(f, v)) return false;
        f = v; Raise(n); return true;
    }
}

public record TypeRow(string Name, int Count, string Icon);

/// <summary>Everything both windows show. Totals are always recomputed from the saved per-match records.</summary>
public class AppModel : Notify
{
    static readonly Dictionary<string, string> TypeNames = new()
    {
        ["normal"] = "Normal chests", ["rare"] = "Rare chests", ["sprite"] = "Sprite chests", ["sprite_jewel"] = "Jewel sprite chests",
        ["buried"] = "Buried chests", ["wanted"] = "Wanted posters", ["relic"] = "Relic chests", ["other"] = "Other chests",
    };

    // chest type -> icon file in Assets (swap the PNGs to change them)
    static readonly Dictionary<string, string> TypeIcons = new()
    {
        ["normal"] = "chest", ["rare"] = "rare", ["buried"] = "buried",
        ["sprite"] = "special", ["sprite_jewel"] = "special", ["relic"] = "special",
        ["other"] = "plus", ["wanted"] = "plus",
    };

    readonly StateFile state = Store.Load();
    public DropsVM Drops { get; private set; } = null!;
    readonly HashSet<string> known = new();
    readonly List<TreasureWindow> treasure = TreasureHours.Load();
    readonly DispatcherTimer scanTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    readonly DispatcherTimer clockTimer = new() { Interval = TimeSpan.FromSeconds(1) };
    bool scanning, firstScanDone;
    int failed;

    public Settings Settings => state.Settings;
    public ObservableCollection<MatchRecord> Matches { get; } = new();
    public ObservableCollection<TypeRow> Types { get; } = new();

    int totalOpened, totalBroken, matchCount, sessionOpened, lastMatchOpened = -1;
    string status = "Starting…", folderText = "", pirateShort = "", pirateCountdown = "", pirateBadge = "", avgText = "0";
    bool folderOk, pirateLive;

    public int TotalOpened { get => totalOpened; set => Set(ref totalOpened, value); }
    public int TotalBroken { get => totalBroken; set => Set(ref totalBroken, value); }
    public int MatchCount { get => matchCount; set => Set(ref matchCount, value); }
    public int SessionOpened { get => sessionOpened; set { if (Set(ref sessionOpened, value)) Raise(nameof(OverlayStatus)); } }
    public int LastMatchOpened { get => lastMatchOpened; set { if (Set(ref lastMatchOpened, value)) Raise(nameof(OverlayStatus)); } }
    public string AvgText { get => avgText; set => Set(ref avgText, value); }
    public string Status { get => status; set => Set(ref status, value); }
    public string FolderText { get => folderText; set => Set(ref folderText, value); }
    public bool FolderOk { get => folderOk; set => Set(ref folderOk, value); }

    public string PirateShort { get => pirateShort; set => Set(ref pirateShort, value); }

    // ----- Treasure Hours frame (Overview) -----
    readonly List<HourRow> hourRows = new();
    public ObservableCollection<HourRow> Hours { get; } = new();
    string headBadge = "", headLabel = "", headClock = "", headWhen = "";
    System.Windows.Media.Brush headBadgeFg = System.Windows.Media.Brushes.White, headBadgeBg = System.Windows.Media.Brushes.Transparent;
    public string HeadBadge { get => headBadge; set => Set(ref headBadge, value); }
    public string HeadLabel { get => headLabel; set => Set(ref headLabel, value); }
    public string HeadClock { get => headClock; set => Set(ref headClock, value); }
    public string HeadWhen { get => headWhen; set => Set(ref headWhen, value); }
    public System.Windows.Media.Brush HeadBadgeFg { get => headBadgeFg; set => Set(ref headBadgeFg, value); }
    public System.Windows.Media.Brush HeadBadgeBg { get => headBadgeBg; set => Set(ref headBadgeBg, value); }
    public string PirateCountdown { get => pirateCountdown; set => Set(ref pirateCountdown, value); }
    public string PirateBadge { get => pirateBadge; set => Set(ref pirateBadge, value); }
    public bool PirateLive { get => pirateLive; set => Set(ref pirateLive, value); }

    public string OverlayStatus => LastMatchOpened >= 0
        ? $"This session: {SessionOpened}  ·  Last match: +{LastMatchOpened}"
        : $"This session: {SessionOpened}";

    public string ReplayFolder => Settings.ReplayFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");

    public void Start()
    {
        foreach (var m in state.Matches) known.Add(m.File);
        Drops = new DropsVM(state, Save, () => TotalOpened);
        foreach (var w in treasure) hourRows.Add(new HourRow(w));
        Rebuild();
        UpdateFolder();
        Tick();
        clockTimer.Tick += (_, _) => Tick();
        clockTimer.Start();
        scanTimer.Tick += async (_, _) => await ScanAsync();
        scanTimer.Start();
        _ = ScanAsync();
    }

    void UpdateFolder()
    {
        FolderOk = Directory.Exists(ReplayFolder);
        FolderText = ReplayFolder;
    }

    public void SetFolder(string folder) { Settings.ReplayFolder = folder; UpdateFolder(); Save(); _ = ScanAsync(); }
    public void Save() => Store.Save(state);

    // ---- replay detection ----
    // A replay is read once its file has stopped changing: at least 8 s old and no longer open in the game (or 20 s old).
    // If it turns out to have grown afterwards, it is read again, so a match is never counted half-way.
    readonly Dictionary<string, int> attempts = new();
    readonly Dictionary<string, DateTime> retryAt = new();

    static bool CanOpenExclusive(FileInfo f)
    {
        try { using var fs = new FileStream(f.FullName, FileMode.Open, FileAccess.Read, FileShare.None); return true; }
        catch { return false; }
    }

    bool IsFinished(FileInfo f, DateTime now)
    {
        double age = (now - f.LastWriteTimeUtc).TotalSeconds;
        return age >= 8 && (age >= 20 || CanOpenExclusive(f));
    }

    public async Task ScanAsync()
    {
        if (scanning) return;
        scanning = true;
        try
        {
            UpdateFolder();
            if (!FolderOk) { Status = "Replay folder not found. Click Folder… to choose it."; return; }
            var now = DateTime.UtcNow;
            var all = new DirectoryInfo(ReplayFolder).GetFiles("*.replay").Where(f => f.Length > 1_000_000).ToList();

            var recByFile = new Dictionary<string, MatchRecord>();
            foreach (var m in state.Matches) recByFile[m.File] = m;

            var todo = new List<FileInfo>();
            bool inProgress = false;
            foreach (var f in all)
            {
                recByFile.TryGetValue(f.Name, out var rec);
                bool isNew = !known.Contains(f.Name);
                bool grown = rec != null && rec.Size > 0 && f.Length > rec.Size + 50_000;
                if (!isNew && !grown) continue;
                if (retryAt.TryGetValue(f.Name, out var t) && now < t) continue;
                if (IsFinished(f, now)) todo.Add(f);
                else if (isNew) inProgress = true;
            }

            int i = 0;
            foreach (var f in todo.OrderBy(f => f.LastWriteTimeUtc))
            {
                Status = $"Reading match {++i}/{todo.Count}…";
                long size = f.Length;
                var rec = await Task.Run(() => ChestAnalyzer.Analyze(f.FullName));
                rec.Size = size;

                // a failed read of a replay that was still being written: try again in a few seconds instead of keeping the error
                if (rec.Error != null && (DateTime.UtcNow - f.LastWriteTimeUtc).TotalMinutes < 3 && attempts.GetValueOrDefault(f.Name) < 3)
                {
                    attempts[f.Name] = attempts.GetValueOrDefault(f.Name) + 1;
                    retryAt[f.Name] = DateTime.UtcNow.AddSeconds(8);
                    continue;
                }

                var old = state.Matches.FirstOrDefault(m => m.File == rec.File);
                int delta = rec.Opened - (old?.Opened ?? 0);
                if (old != null) state.Matches.Remove(old);
                known.Add(rec.File);
                state.Matches.Add(rec);
                if (rec.Error != null) failed++;
                else if (firstScanDone) { SessionOpened += delta; LastMatchOpened = rec.Opened; }
                Rebuild();
                Save();
            }
            if (todo.Count > 0) MemoryTrim.Schedule(3000);   // reading a replay allocates a lot: give it back
            firstScanDone = true;
            Status = failed > 0 ? $"Watching for new matches  ·  {failed} replay(s) could not be read (game update?)"
                : inProgress ? "Match in progress, it will be counted as soon as it ends" : "Watching for new matches";
        }
        catch (Exception e) { Status = "Scan error: " + e.Message; }
        finally { scanning = false; }
    }

    void Rebuild()
    {
        var ok = state.Matches.Where(m => m.Error == null).OrderByDescending(m => m.Started).ToList();
        TotalOpened = ok.Sum(m => m.Opened);
        TotalBroken = ok.Sum(m => m.Broken);
        MatchCount = ok.Count;
        AvgText = ok.Count == 0 ? "0" : (TotalOpened / (double)ok.Count).ToString("0.#", CultureInfo.CurrentCulture);
        Matches.Clear();
        foreach (var m in ok.Take(60)) Matches.Add(m);
        var byType = new Dictionary<string, int>();
        foreach (var m in ok) foreach (var kv in m.OpenedByType) byType[kv.Key] = byType.GetValueOrDefault(kv.Key) + kv.Value;
        Types.Clear();
        foreach (var kv in byType.OrderByDescending(k => k.Value))
            Types.Add(new TypeRow(TypeNames.GetValueOrDefault(kv.Key, kv.Key), kv.Value, $"/Assets/{TypeIcons.GetValueOrDefault(kv.Key, "plus")}.png"));
        Drops?.Refresh();
    }

    public void ResetAll()
    {
        // keeps the replay files as "already counted" so old matches are not re-imported
        foreach (var m in state.Matches) m.Opened = m.Broken = 0;
        foreach (var m in state.Matches) m.OpenedByType.Clear();
        SessionOpened = 0; LastMatchOpened = -1;
        Rebuild(); Save();
    }

    public async Task RescanAllAsync()
    {
        state.Matches.Clear(); known.Clear(); failed = 0; firstScanDone = false;
        SessionOpened = 0; LastMatchOpened = -1;
        Rebuild(); Save();
        await ScanAsync();
    }

    void Tick()
    {
        var now = DateTime.UtcNow;
        Drops?.Tick();
        var w = TreasureHours.Next(treasure, now);

        // the list: live / next first, then the upcoming ones, then the ended ones (latest first)
        foreach (var r in hourRows) r.Update(now, ReferenceEquals(r.W, w));
        var order = hourRows.OrderBy(r => r.Group).ThenBy(r => r.Group == 2 ? -r.W.StartUtc.Ticks : r.W.StartUtc.Ticks).ToList();
        if (!order.SequenceEqual(Hours)) { Hours.Clear(); foreach (var r in order) Hours.Add(r); }

        var head = hourRows.FirstOrDefault(r => ReferenceEquals(r.W, w));
        if (w == null || head == null)
        {
            HeadBadge = "NO UPCOMING HOUR"; HeadLabel = ""; HeadClock = "--"; HeadWhen = "Nothing announced yet";
            PirateShort = "none scheduled"; PirateCountdown = ""; PirateBadge = ""; PirateLive = false;
            return;
        }
        HeadBadge = head.Badge; HeadLabel = head.Label; HeadClock = head.Clock; HeadWhen = head.When;
        HeadBadgeFg = head.BadgeFg; HeadBadgeBg = head.BadgeBg;

        // overlay + tray menu (Paris time too)
        PirateLive = w.StartUtc <= now;
        PirateBadge = PirateLive ? "LIVE NOW" : w.Confirmed ? "CONFIRMED" : "ESTIMATED";
        var left = PirateLive ? w.EndUtc - now : w.StartUtc - now;
        string cd = left.TotalDays >= 1 ? $"{(int)left.TotalDays}d {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}" : $"{left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
        PirateCountdown = PirateLive ? $"ends in {cd}" : $"starts in {cd}";
        PirateShort = PirateLive ? $"LIVE · ends {Paris.Clock(head.EndParis)}" : $"{Paris.Day(head.StartParis)} · {Paris.Clock(head.StartParis)}";
    }
}
