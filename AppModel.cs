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
        ["normal"] = Loc.T("Normal chests"), ["rare"] = Loc.T("Rare chests"), ["sprite"] = Loc.T("Sprite chests"), ["sprite_jewel"] = Loc.T("Jewel sprite chests"),
        ["buried"] = Loc.T("Buried chests"), ["wanted"] = Loc.T("Wanted posters"), ["relic"] = Loc.T("Relic chests"), ["other"] = Loc.T("Other chests"),
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
    string status = Loc.T("Starting…"), folderText = "", pirateShort = "", pirateCountdown = "", pirateBadge = "", avgText = "0";
    bool folderOk, pirateLive;

    public int TotalOpened { get => totalOpened; set => Set(ref totalOpened, value); }
    public int TotalBroken { get => totalBroken; set => Set(ref totalBroken, value); }
    public int MatchCount { get => matchCount; set => Set(ref matchCount, value); }
    public int SessionOpened { get => sessionOpened; set { if (Set(ref sessionOpened, value)) Raise(nameof(OverlayStatus)); } }
    public int LastMatchOpened { get => lastMatchOpened; set { if (Set(ref lastMatchOpened, value)) Raise(nameof(OverlayStatus)); } }
    // ----- chests added by hand (Settings tab) -----
    public int ReplayChests { get; private set; }
    public int ManualChests => state.Settings.ManualChests;
    public string CountNote => ManualChests > 0
        ? Loc.F("counted from your replays + {0} added by hand", ManualChests.ToString("N0"))
        : Loc.T("counted from your replays, only the ones you opened yourself");
    public string ManualInfo => ManualChests > 0
        ? Loc.F("Added by hand so far: {0}   ·   Counted from replays: {1}   ·   Total: {2}", ManualChests.ToString("N0"), ReplayChests.ToString("N0"), TotalOpened.ToString("N0"))
        : Loc.F("Nothing added by hand yet   ·   Counted from replays: {0}", ReplayChests.ToString("N0"));
    // ----- records, streak, goal, Treasure Hour count (Overview) -----
    string bestMatchText = "", recordsLine = "", goalText = "", treasureNote = "";
    double goalFraction;
    bool goalVisible;
    public string BestMatchText { get => bestMatchText; private set => Set(ref bestMatchText, value); }
    public string RecordsLine { get => recordsLine; private set => Set(ref recordsLine, value); }
    public string GoalText { get => goalText; private set => Set(ref goalText, value); }
    public double GoalFraction { get => goalFraction; private set => Set(ref goalFraction, value); }
    public bool GoalVisible { get => goalVisible; private set => Set(ref goalVisible, value); }
    public string TreasureNote { get => treasureNote; private set => Set(ref treasureNote, value); }
    public int GoalKind => state.Settings.GoalKind;
    public int GoalTarget => state.Settings.GoalTarget;
    DateTime recordsDay;

    public void SetGoal(int kind, int target)
    {
        state.Settings.GoalKind = Math.Clamp(kind, 0, 2);
        if (target > 0) state.Settings.GoalTarget = Math.Min(target, 100000);
        Raise(nameof(GoalKind)); Raise(nameof(GoalTarget));
        RebuildRecords(); Save();
    }

    static DateTime ParisDay(DateTime local) => Paris.FromLocal(local).Date;

    void RebuildRecords()
    {
        var ok = state.Matches.Where(m => m.Error == null && m.Opened > 0).ToList();
        var today = Paris.FromUtc(DateTime.UtcNow).Date;
        recordsDay = today;
        var best = ok.OrderByDescending(m => m.Opened).ThenByDescending(m => m.Started).FirstOrDefault();
        BestMatchText = best == null ? "" : Loc.F(best.Opened == 1 ? "★ BEST MATCH · {0} chest · {1}" : "★ BEST MATCH · {0} chests · {1}", best.Opened, Paris.Day(Paris.FromLocal(best.Started)));

        var perDay = ok.GroupBy(m => ParisDay(m.Started)).ToDictionary(g => g.Key, g => g.Sum(m => m.Opened));
        int bestDay = perDay.Count == 0 ? 0 : perDay.Values.Max();
        var days = perDay.Keys.OrderBy(d => d).ToList();
        int bestStreak = 0, run = 0; DateTime? prev = null;
        foreach (var d in days) { run = prev != null && (d - prev.Value).Days == 1 ? run + 1 : 1; bestStreak = Math.Max(bestStreak, run); prev = d; }
        int streak = 0;
        var cur = perDay.ContainsKey(today) ? today : today.AddDays(-1);
        while (perDay.ContainsKey(cur)) { streak++; cur = cur.AddDays(-1); }
        var parts = new List<string>();
        if (bestDay > 0) parts.Add(Loc.F("Best day {0}", bestDay));
        if (bestStreak > 0) parts.Add(Loc.F(streak == 1 ? "Streak {0} day" : "Streak {0} days", streak) + (bestStreak > streak ? Loc.F(" (best {0})", bestStreak) : ""));
        RecordsLine = string.Join("  ·  ", parts);

        // goal
        int kind = state.Settings.GoalKind, target = Math.Max(1, state.Settings.GoalTarget);
        GoalVisible = kind != 0;
        if (kind != 0)
        {
            var from = kind == 1 ? today : today.AddDays(-(((int)today.DayOfWeek + 6) % 7));
            int done = perDay.Where(kv => kv.Key >= from).Sum(kv => kv.Value);
            GoalFraction = Math.Min(1, done / (double)target);
            GoalText = done >= target
                ? Loc.F(kind == 1 ? "GOAL REACHED · {0}/{1} today" : "GOAL REACHED · {0}/{1} this week", done, target)
                : Loc.F(kind == 1 ? "GOAL TODAY · {0}/{1}" : "GOAL THIS WEEK · {0}/{1}", done, target);
        }

        // chests opened while a Treasure Hour was running: the match start (file name) plus the moment of each chest
        int inHour = 0;
        foreach (var m in ok)
        {
            var start = DateTime.SpecifyKind(m.Started, DateTimeKind.Local).ToUniversalTime();
            foreach (var s in m.Spots)
            {
                if (s.Broken) continue;
                var t = start.AddSeconds(s.AtSeconds);
                if (treasure.Any(w => w.StartUtc <= t && t < w.EndUtc)) inHour++;
            }
        }
        TreasureNote = Loc.F("({0} during Treasure Hours)", inHour);
    }

    string settingsMessage = "";
    public string SettingsMessage { get => settingsMessage; set => Set(ref settingsMessage, value); }

    string exportMessage = "";
    public string ExportMessage { get => exportMessage; set => Set(ref exportMessage, value); }

    /// <summary>The CSV text for a period (the window asks the user where to save it).</summary>
    public string BuildCsv(CsvExport.Period period) => CsvExport.Build(state.Matches, ManualChests, period);

    public byte[] BuildPdf(CsvExport.Period period) => PdfExport.Build(state.Matches, ManualChests, period);

    DateTime alertedStart;
    readonly HashSet<(DateTime, int)> warned = new();
    /// <summary>Pop-up + chime when a Treasure Hour has just started (not when the app is opened in the middle of one).</summary>
    void CheckHourAlert(DateTime now)
    {
        if (state.Settings.HourWarn && treasure.Where(w => w.StartUtc > now).MinBy(w => w.StartUtc) is { } next)
        {
            var left = next.StartUtc - now;
            foreach (var m in new[] { 30, 10 })   // only when the moment is fresh, so opening the app at 12 minutes left does not show the 30 minute one
                if (left <= TimeSpan.FromMinutes(m) && left > TimeSpan.FromMinutes(m - 5) && warned.Add((next.StartUtc, m))) { HourToast.Show(next, m); break; }
        }
        var live = treasure.FirstOrDefault(w => w.StartUtc <= now && now < w.EndUtc);
        if (live == null || live.StartUtc == alertedStart) return;
        alertedStart = live.StartUtc;
        if (state.Settings.HourAlert && now - live.StartUtc < TimeSpan.FromMinutes(10)) HourToast.Show(live);
    }

    public void TestHourAlert(int minutes = 0)
    {
        var now = DateTime.UtcNow;
        var w = treasure.FirstOrDefault(x => x.StartUtc <= now && now < x.EndUtc) ?? treasure.Where(x => x.StartUtc > now).MinBy(x => x.StartUtc) ?? new TreasureWindow(now, now.AddHours(2), true);
        HourToast.Show(w, minutes);
    }

    // ----- backup and restore (Settings tab) -----
    string backupMessage = "";
    public string BackupMessage { get => backupMessage; set => Set(ref backupMessage, value); }

    public void BackupTo(string path)
    {
        Save();
        File.WriteAllText(path, System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>Loads a backup file as this app's data. Returns an error text, or null when it worked (the app then restarts).</summary>
    public string? RestoreFrom(string path)
    {
        StateFile? s;
        try { s = System.Text.Json.JsonSerializer.Deserialize<StateFile>(File.ReadAllText(path)); }
        catch { return Loc.T("That file is not a LNXcounter backup."); }
        if (s == null || s.Matches == null || s.Settings == null || (s.Matches.Count == 0 && s.Settings.ManualChests == 0 && s.Found.Count == 0))
            return Loc.T("That file is not a LNXcounter backup (or it is empty).");
        // the folder and the overlay position belong to this PC, not to the backup
        var mine = state.Settings;
        s.Settings.ReplayFolder = mine.ReplayFolder; s.Settings.OverlayLeft = mine.OverlayLeft; s.Settings.OverlayTop = mine.OverlayTop;
        s.Settings.OverlayVisible = mine.OverlayVisible; s.Settings.OverlayCollapsed = mine.OverlayCollapsed;
        Store.Save(s);   // keeps the previous save as state.json.bak
        return null;
    }

    // ----- the player's Fortnite name -----
    public string PlayerName => state.Settings.PlayerName ?? "";
    void RefreshPlayerName()
    {
        _ = Task.Run(() =>
        {
            var n = Services.PlayerName.Find();
            if (n != null && n != state.Settings.PlayerName)
                System.Windows.Application.Current.Dispatcher.Invoke(() => { state.Settings.PlayerName = n; Raise(nameof(PlayerName)); Save(); });
        });
    }

    // ----- updates -----
    UpdateInfo? update;
    string updateHeadline = "";
    string updateNote = "";
    bool updateAvailable, updateBusy;
    public bool UpdateAvailable { get => updateAvailable; private set => Set(ref updateAvailable, value); }
    public bool UpdateBusy { get => updateBusy; private set => Set(ref updateBusy, value); }
    public string UpdateHeadline { get => updateHeadline; private set => Set(ref updateHeadline, value); }
    public string UpdateNote { get => updateNote; private set => Set(ref updateNote, value); }

    public async Task CheckUpdateAsync(bool manual)
    {
        if (UpdateBusy) return;
        if (manual) UpdateNote = Loc.T("Checking...");
        var (info, err) = await Updater.CheckAsync();
        if (info != null)
        {
            update = info; UpdateAvailable = true;
            UpdateHeadline = Loc.F("New update available {0}", info.Tag);
            UpdateNote = Loc.F("You have v{0}. Press Update to download it ({1} MB) and restart.", Updater.Current.ToString(3), info.Size / 1_048_576);
        }
        else if (err != null) { if (manual) UpdateNote = err; }
        else
        {
            update = null; UpdateAvailable = false;
            UpdateHeadline = $"LNXcounter v{Updater.Current.ToString(3)}";
            UpdateNote = manual ? Loc.T("You have the newest version.") : UpdateNote;
        }
    }

    public async Task InstallUpdateAsync()
    {
        if (update == null || UpdateBusy) return;
        UpdateBusy = true;
        var res = await Updater.InstallAsync(update, new Progress<int>(p => UpdateNote = Loc.F("Downloading {0}... {1}%", update.Tag, p)));
        UpdateBusy = false;
        if (res != null) { UpdateNote = res; return; }
        UpdateNote = Loc.T("Updated. Restarting...");
        await Task.Delay(600);
        App.Restart();
    }

    readonly DispatcherTimer updateTimer = new() { Interval = TimeSpan.FromHours(6) };
    void StartUpdateChecks()
    {
        UpdateHeadline = $"LNXcounter v{Updater.Current.ToString(3)}";
        updateTimer.Tick += async (_, _) => { if (state.Settings.AutoUpdateCheck) await CheckUpdateAsync(false); };
        updateTimer.Start();
        var first = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        first.Tick += async (_, _) => { first.Stop(); if (state.Settings.AutoUpdateCheck) await CheckUpdateAsync(false); };
        first.Start();
    }

    public const int MaxManual = 10_000_000;

    /// <summary>Adds (or, with a negative number, removes) chests by hand. They count as normal chests.</summary>
    public void AddManual(int n)
    {
        int before = ManualChests;
        state.Settings.ManualChests = Math.Clamp(before + n, 0, MaxManual);
        int delta = ManualChests - before;
        Rebuild(); Save();
        SettingsMessage = delta == 0 ? (n < 0 ? Loc.T("There is nothing added by hand to remove.") : Loc.T("Nothing changed."))
            : delta > 0 ? Loc.F("Added {0} chests. Your total is now {1}.", delta.ToString("N0"), TotalOpened.ToString("N0"))
            : Loc.F("Removed {0} chests. Your total is now {1}.", (-delta).ToString("N0"), TotalOpened.ToString("N0"));
    }

    /// <summary>Makes the total equal <paramref name="total"/> by adding whatever the replays did not count.</summary>
    public void SetTotal(int total)
    {
        if (total < ReplayChests)
        {
            SettingsMessage = Loc.F("LNXcounter already counted {0} chests from your replays, so your total can't be lower than that.", ReplayChests.ToString("N0"));
            return;
        }
        state.Settings.ManualChests = Math.Min(total - ReplayChests, MaxManual);
        Rebuild(); Save();
        SettingsMessage = Loc.F("Done. Your total is now {0} ({1} from replays + {2} added by hand).", TotalOpened.ToString("N0"), ReplayChests.ToString("N0"), ManualChests.ToString("N0"));
    }

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
        ? Loc.F("This session: {0}  ·  Last match: +{1}", SessionOpened, LastMatchOpened)
        : Loc.F("This session: {0}", SessionOpened);

    public string ReplayFolder => Settings.ReplayFolder ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FortniteGame", "Saved", "Demos");

    public void Start()
    {
        foreach (var m in state.Matches) known.Add(m.File);
        Drops = new DropsVM(state, Save, () => TotalOpened, now => treasure.FirstOrDefault(w => w.StartUtc <= now && now < w.EndUtc));
        foreach (var w in treasure) hourRows.Add(new HourRow(w));
        Rebuild();
        UpdateFolder();
        Tick();
        clockTimer.Tick += (_, _) => Tick();
        clockTimer.Start();
        scanTimer.Tick += async (_, _) => await ScanAsync();
        scanTimer.Start();
        _ = ScanAsync();
        StartUpdateChecks();
        RefreshPlayerName();
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
            if (!FolderOk) { Status = Loc.T("Replay folder not found. Click Folder… to choose it."); return; }
            var now = DateTime.UtcNow;
            var all = new DirectoryInfo(ReplayFolder).GetFiles("*.replay").Where(f => f.Length > 1_000_000).ToList();

            var recByFile = new Dictionary<string, MatchRecord>();
            foreach (var m in state.Matches) recByFile[m.File] = m;

            var todo = new List<FileInfo>();
            var mapOnly = new HashSet<string>();   // replays read before 1.20: only the chest positions are added, their counts stay
            bool inProgress = false;
            foreach (var f in all)
            {
                recByFile.TryGetValue(f.Name, out var rec);
                bool isNew = !known.Contains(f.Name);
                bool grown = rec != null && rec.Size > 0 && f.Length > rec.Size + 50_000;
                bool needsMap = rec != null && rec.Error == null && rec.Analysis < ChestAnalyzer.Version && rec.Spots.Count == 0;
                if (!isNew && !grown && !needsMap) continue;   // a replay that was already read is never counted again (only "Rescan all" does that)
                if (needsMap && !grown) mapOnly.Add(f.Name);
                if (retryAt.TryGetValue(f.Name, out var t) && now < t) continue;
                if (IsFinished(f, now)) todo.Add(f);
                else if (isNew) inProgress = true;
            }

            int i = 0;
            foreach (var f in todo.OrderBy(f => f.LastWriteTimeUtc))
            {
                Status = Loc.F(mapOnly.Contains(f.Name) ? "Adding map data {0}/{1}…" : "Reading match {0}/{1}…", ++i, todo.Count);
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
                if (mapOnly.Contains(rec.File) && old != null)
                {
                    if (rec.Error == null) old.Spots = rec.Spots;
                    old.Analysis = ChestAnalyzer.Version;
                    Rebuild(); Save();
                    continue;
                }
                int delta = rec.Opened - (old?.Opened ?? 0);
                if (old != null) state.Matches.Remove(old);
                known.Add(rec.File);
                state.Matches.Add(rec);
                foreach (var tf in rec.Finds.Where(x => x.Mine && x.DropId != ""))
                    Drops?.AutoFound(tf.DropId == "token" ? "token" : $"w{tf.Week}:{tf.DropId}");   // a golden chest reward of yours ticks itself
                if (rec.Error != null) failed++;
                else if (firstScanDone) { SessionOpened += delta; LastMatchOpened = rec.Opened; }
                Rebuild();
                Save();
            }
            if (todo.Count > 0) MemoryTrim.Schedule(3000);   // reading a replay allocates a lot: give it back
            firstScanDone = true;
            Status = failed > 0 ? Loc.F("Watching for new matches  ·  {0} replay(s) could not be read (game update?)", failed)
                : inProgress ? Loc.T("Match in progress, it will be counted as soon as it ends") : Loc.T("Watching for new matches");
        }
        catch (Exception e) { Status = Loc.T("Scan error: ") + e.Message; }
        finally { scanning = false; }
    }

    void Rebuild()
    {
        var ok = state.Matches.Where(m => m.Error == null).OrderByDescending(m => m.Started).ToList();
        ReplayChests = ok.Sum(m => m.Opened);
        TotalOpened = ReplayChests + ManualChests;
        TotalBroken = ok.Sum(m => m.Broken);
        MatchCount = ok.Count;
        AvgText = ok.Count == 0 ? "0" : (ReplayChests / (double)ok.Count).ToString("0.#", CultureInfo.CurrentCulture);
        Matches.Clear();
        foreach (var m in ok.Take(60)) Matches.Add(m);
        var byType = new Dictionary<string, int>();
        foreach (var m in ok) foreach (var kv in m.OpenedByType) byType[kv.Key] = byType.GetValueOrDefault(kv.Key) + kv.Value;
        if (ManualChests > 0) byType["normal"] = byType.GetValueOrDefault("normal") + ManualChests;   // added by hand = normal chests
        Types.Clear();
        foreach (var kv in byType.OrderByDescending(k => k.Value))
            Types.Add(new TypeRow(TypeNames.GetValueOrDefault(kv.Key, kv.Key), kv.Value, $"/Assets/{TypeIcons.GetValueOrDefault(kv.Key, "plus")}.png"));
        Raise(nameof(ReplayChests)); Raise(nameof(ManualChests)); Raise(nameof(CountNote)); Raise(nameof(ManualInfo));
        Drops?.Refresh();
        RebuildRecords();
    }

    public void ResetAll()
    {
        // keeps the replay files as "already counted" so old matches are not re-imported
        foreach (var m in state.Matches) m.Opened = m.Broken = 0;
        state.Settings.ManualChests = 0;
        SettingsMessage = "";
        foreach (var m in state.Matches) m.OpenedByType.Clear();
        SessionOpened = 0; LastMatchOpened = -1;
        Rebuild(); Save();
    }

    /// <summary>Re-reads only the replays that are still on disk; matches whose replay is gone are kept as they are.</summary>
    public async Task RefreshExistingAsync()
    {
        if (!FolderOk || scanning) return;
        scanning = true;
        try
        {
            var now = DateTime.UtcNow;
            var files = new DirectoryInfo(ReplayFolder).GetFiles("*.replay")
                .Where(f => f.Length > 1_000_000 && IsFinished(f, now) && state.Matches.Any(m => m.File == f.Name)).OrderBy(f => f.LastWriteTimeUtc).ToList();
            int i = 0, changed = 0;
            foreach (var f in files)
            {
                Status = Loc.F("Re-reading match {0}/{1}…", ++i, files.Count);
                long size = f.Length;
                var rec = await Task.Run(() => ChestAnalyzer.Analyze(f.FullName));
                if (rec.Error != null) continue;   // keep what we had
                rec.Size = size;
                var old = state.Matches.First(m => m.File == rec.File);
                if (old.Opened != rec.Opened || old.Finds.Count != rec.Finds.Count) changed++;
                state.Matches[state.Matches.IndexOf(old)] = rec;
                Rebuild(); Save();
            }
            MemoryTrim.Schedule(3000);
            Status = Loc.F("Re-read {0} matches, {1} changed", files.Count, changed);
        }
        catch (Exception e) { Status = Loc.T("Scan error: ") + e.Message; }
        finally { scanning = false; }
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
        CheckHourAlert(now);
        if (recordsDay != default && Paris.FromUtc(now).Date != recordsDay) RebuildRecords();   // a new day: the goal starts again
        var w = TreasureHours.Next(treasure, now);

        // the list: live / next first, then the upcoming ones, then the ended ones (latest first)
        foreach (var r in hourRows) r.Update(now, ReferenceEquals(r.W, w));
        var order = hourRows.OrderBy(r => r.Group).ThenBy(r => r.Group == 2 ? -r.W.StartUtc.Ticks : r.W.StartUtc.Ticks).ToList();
        if (!order.SequenceEqual(Hours)) { Hours.Clear(); foreach (var r in order) Hours.Add(r); }

        var head = hourRows.FirstOrDefault(r => ReferenceEquals(r.W, w));
        if (w == null || head == null)
        {
            HeadBadge = Loc.T("NO UPCOMING HOUR"); HeadLabel = ""; HeadClock = "--"; HeadWhen = Loc.T("Nothing announced yet");
            PirateShort = Loc.T("none scheduled"); PirateCountdown = ""; PirateBadge = ""; PirateLive = false;
            return;
        }
        HeadBadge = head.Badge; HeadLabel = head.Label; HeadClock = head.Clock; HeadWhen = head.When;
        HeadBadgeFg = head.BadgeFg; HeadBadgeBg = head.BadgeBg;

        // overlay + tray menu (Paris time too)
        PirateLive = w.StartUtc <= now;
        PirateBadge = PirateLive ? Loc.T("LIVE NOW") : w.Confirmed ? Loc.T("CONFIRMED") : Loc.T("ESTIMATED");
        var left = PirateLive ? w.EndUtc - now : w.StartUtc - now;
        string cd = left.TotalDays >= 1 ? $"{(int)left.TotalDays}d {left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}" : $"{left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
        PirateCountdown = PirateLive ? Loc.F("ends in {0}", cd) : Loc.F("starts in {0}", cd);
        PirateShort = PirateLive ? Loc.F("LIVE · ends {0}", Paris.Clock(head.EndParis)) : $"{Paris.Day(head.StartParis)} · {Paris.Clock(head.StartParis)}";
    }
}
