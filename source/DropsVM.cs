using System.Collections.ObjectModel;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using LNXcounter.Services;

namespace LNXcounter;

public class WeekTab : Notify
{
    public int Number { get; init; }
    public string Label { get; init; } = "";
    public bool Available { get; init; }
    public string Tip => Available ? "" : "Not announced yet";
    bool selected, ended, live;
    public bool IsSelected { get => selected; set => Set(ref selected, value); }
    public bool Ended { get => ended; set => Set(ref ended, value); }
    public bool Live { get => live; set => Set(ref live, value); }
}

public class TileVM : Notify
{
    readonly DropsVM owner;
    readonly string key;
    public TileVM(DropsVM owner, string key) { this.owner = owner; this.key = key; }

    public string Category { get; init; } = "";
    public string Title { get; init; } = "";
    public string Icon { get; init; } = "";
    public double IconSize { get; init; } = double.NaN;           // NaN = fill the frame width
    public VerticalAlignment IconAlign { get; init; } = VerticalAlignment.Center;
    public Thickness IconMargin { get; init; }
    public Stretch IconStretch { get; init; } = Stretch.Uniform;
    public int Row { get; init; }
    public int Col { get; init; }
    public int RowSpan { get; init; } = 1;
    public int ColSpan { get; init; } = 1;
    public Brush Background { get; init; } = Brushes.Transparent;
    public Visibility CheckVisibility { get; init; } = Visibility.Collapsed;
    public bool Floats { get; init; }
    public int OneIn { get; init; }
    public string Kind { get; init; } = "";
    public string Tier { get; init; } = "";
    public bool Unknown { get; init; }
    public bool IsLoading { get; init; }
    public bool IsGizmo { get; init; }
    public bool IsToken { get; init; }
    public JamPlayer Audio => owner.Audio;
    public string? Video { get; init; }
    public string? Song { get; init; }
    public Visibility AudioVisibility => Video != null ? Visibility.Visible : Visibility.Collapsed;
    public Visibility UnknownVisibility => Unknown ? Visibility.Visible : Visibility.Collapsed;
    public Visibility KnownVisibility => Unknown ? Visibility.Collapsed : Visibility.Visible;
    public Visibility LoadingVisibility => IsLoading && !Unknown ? Visibility.Visible : Visibility.Collapsed;
    public Visibility SharedNoteVisibility => IsToken ? Visibility.Visible : Visibility.Collapsed;
    public Stretch ModalStretch => IsLoading && !Unknown ? Stretch.UniformToFill : Stretch.Uniform;
    public Thickness ModalMargin => IsLoading && !Unknown ? new Thickness(0) : new Thickness(18);
    public string ModalTitle => Unknown ? "UNKNOWN" : Title;

    public string TierText => Tier switch { "supreme" => "SUPREME PRIZE", "major" => "MAJOR PRIZE", _ => "STANDARD PRIZE" };
    public Brush TierFg => Tier switch { "supreme" => Br("#F5D76E"), "major" => Br("#C58BFF"), _ => Br("#5CC4F7") };
    public Brush TierBg => Tier switch { "supreme" => Br("#3A3015"), "major" => Br("#3A2A52"), _ => Br("#163448") };
    static Brush Br(string hex) { var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); b.Freeze(); return b; }

    // filled in when the popup is opened
    Brush? modalBg; string info = "";
    public Brush? ModalBg { get => modalBg; set => Set(ref modalBg, value); }
    public string Info { get => info; set => Set(ref info, value); }

    bool dim;
    /// <summary>True for tiles of a week that has ended: they are shown darker.</summary>
    public bool Dim { get => dim; set => Set(ref dim, value); }

    bool found;
    public bool Found { get => found; set { if (Set(ref found, value)) owner.OnFoundChanged(key, value); } }
    public void SetFoundSilently(bool v) { found = v; Raise(nameof(Found)); }
}

/// <summary>One row of the odds list under the grid: icon, name, "1 in N" and (percent).</summary>
public class OddsRow
{
    public string Name { get; init; } = "";
    public string OneInText { get; init; } = "";
    public string PctText { get; init; } = "";
    public string Icon { get; init; } = "";
    public Brush IconBg { get; init; } = Brushes.Transparent;
}

/// <summary>Drops page: week tabs, the tile grid, "found" ticks (saved) and the countdown.</summary>
public class DropsVM : Notify
{
    readonly StateFile state;
    readonly Action save;
    readonly Func<int> chests;
    WeekDef week = DropsData.Weeks[0];

    public ObservableCollection<WeekTab> Weeks { get; } = new();
    public ObservableCollection<TileVM> Tiles { get; } = new();
    public ObservableCollection<OddsRow> Odds { get; } = new();

    string weekLabel = "", title = "", foundText = "", endLabel = "", countdown = "", expected = "", hero = "";
    Brush accent = Brushes.Gold;
    public string HeroIcon { get => hero; set => Set(ref hero, value); }
    static readonly Brush InactiveTitle = Frozen(new SolidColorBrush(Color.FromRgb(0x8A, 0x8A, 0x8A)));
    Brush titleBrush = Brushes.White; double labelOpacity = 1;
    public Brush TitleBrush { get => titleBrush; set => Set(ref titleBrush, value); }
    public double LabelOpacity { get => labelOpacity; set => Set(ref labelOpacity, value); }
    bool hasFound;
    public bool HasFound { get => hasFound; set => Set(ref hasFound, value); }

    // ----- jam track audio -----
    public JamPlayer Audio { get; }
    DispatcherTimer? volumeSaveTimer;
    void VolumeChanged(int v)
    {
        state.Settings.Volume = v;
        volumeSaveTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        volumeSaveTimer.Tick -= VolumeSave; volumeSaveTimer.Tick += VolumeSave;
        volumeSaveTimer.Stop(); volumeSaveTimer.Start();
    }
    void VolumeSave(object? s, EventArgs e) { volumeSaveTimer?.Stop(); save(); }

    public async void ToggleJam() { if (Open?.Video is string id) await Audio.ToggleAsync(id); }

    bool follow;    // true while the selected week is the live one: the page then moves on by itself when the next week starts

    // ----- reward popup + loading screen preview -----
    TileVM? open; bool previewing; string connecting = "CONNECTING.", tip = "";
    public TileVM? Open { get => open; private set { if (Set(ref open, value)) Raise(nameof(IsModalOpen)); } }
    public bool IsModalOpen => open != null;
    public bool Previewing { get => previewing; private set => Set(ref previewing, value); }
    public string Connecting { get => connecting; private set => Set(ref connecting, value); }
    public string Tip { get => tip; private set => Set(ref tip, value); }

    DispatcherTimer? dotsTimer, tipTimer;
    int dotStep;
    readonly Random rng = new();

    public void OpenTile(TileVM t)
    {
        int n = chests();
        if (Open != t) Audio.Stop();
        t.ModalBg = Flow(t.Kind);
        if (t.Unknown) t.Info = "Info Encrypted";
        else
        {
            double chance = (1 - Math.Pow(1 - 1.0 / t.OneIn, n)) * 100;
            string rate = $"1 in {t.OneIn:N0} per chest ({DropsData.Percent(t.OneIn)})";
            string extra = t.IsGizmo ? " for any gizmo" : "";
            if (t.Song != null) rate = "Song: " + t.Song + "\n" + rate;
            t.Info = n == 0 ? rate + extra
                : $"{rate}{extra}\nWith your {n:N0} chests there is a {(chance >= 99.5 ? ">99" : Fmt(chance))}% chance you would have found one by now.";
        }
        Open = t;
    }

    public void CloseModal() { StopPreview(); Audio.Stop(); Open = null; }

    public void StartPreview()
    {
        if (Open == null) return;
        dotStep = 0; Connecting = "CONNECTING.";
        NextTip();
        Previewing = true;
        dotsTimer ??= new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(520) };
        dotsTimer.Tick -= DotsTick; dotsTimer.Tick += DotsTick; dotsTimer.Start();
        tipTimer ??= new DispatcherTimer { Interval = TimeSpan.FromSeconds(7) };
        tipTimer.Tick -= TipTick; tipTimer.Tick += TipTick; tipTimer.Start();
    }

    public void StopPreview()
    {
        Previewing = false;
        dotsTimer?.Stop(); tipTimer?.Stop();
    }

    // dots loop: 1, 2, 3, 0, 1, 2, 3, 0 ...
    void DotsTick(object? s, EventArgs e)
    {
        dotStep = (dotStep + 1) % 4;
        int dots = (dotStep + 1) % 4;
        Connecting = "CONNECTING" + new string('.', dots);
    }

    void TipTick(object? s, EventArgs e) => NextTip();

    void NextTip()
    {
        var tips = DropsData.Tips; string next;
        do next = tips[rng.Next(tips.Length)]; while (next == tip && tips.Length > 1);
        Tip = next;
    }
    public string WeekLabel { get => weekLabel; set => Set(ref weekLabel, value); }
    public string Title { get => title; set => Set(ref title, value); }
    public string FoundText { get => foundText; set => Set(ref foundText, value); }
    public string EndLabel { get => endLabel; set => Set(ref endLabel, value); }
    public string Countdown { get => countdown; set => Set(ref countdown, value); }
    public string ExpectedText { get => expected; set => Set(ref expected, value); }
    public Brush AccentBrush { get => accent; set => Set(ref accent, value); }

    public DropsVM(StateFile state, Action save, Func<int> chests)
    {
        this.state = state; this.save = save; this.chests = chests;
        Audio = new JamPlayer(state.Settings.Volume, VolumeChanged);
        foreach (var w in DropsData.Weeks)
            Weeks.Add(new WeekTab { Number = w.Number, Label = $"WEEK {w.Number}", Available = w.Available });
        Select(CurrentWeekNumber());
    }

    static int CurrentWeekNumber()
    {
        var now = DateTime.UtcNow;
        var cur = DropsData.Weeks.FirstOrDefault(w => w.Available && w.StartUtc <= now && now < w.EndUtc);
        if (cur != null) return cur.Number;
        var started = DropsData.Weeks.Where(w => w.Available && w.StartUtc <= now).LastOrDefault();
        return started?.Number ?? 1;
    }

    public void Select(int number)
    {
        var def = DropsData.Weeks.FirstOrDefault(w => w.Number == number && w.Available);
        if (def == null) return;
        week = def;
        follow = number == CurrentWeekNumber();
        CloseModal();
        foreach (var t in Weeks) t.IsSelected = t.Number == number;
        AccentBrush = Frozen(new SolidColorBrush((Color)ColorConverter.ConvertFromString(def.AccentHex)));
        WeekLabel = $"WEEK {def.Number} · {def.Range}";
        Title = def.Title;
        HeroIcon = $"/Assets/{def.HeroIcon}.png";

        Tiles.Clear();
        Odds.Clear();
        foreach (var d in DropsData.Drops)
        {
            // the token is the same item every week, so it has one shared "found" flag
            var key = d.Id == "token" ? "token" : $"w{number}:{d.Id}";
            var bg = Flow(d.Kind);
            var item = def.Items![d.Id];
            bool outfit = d.Look == "outfit", fill = d.Look == "fill";
            var tile = new TileVM(this, key)
            {
                Category = item.Category, Title = item.Title, Icon = $"/Assets/{item.Icon}.png",
                IconSize = item.Unknown ? 64 : outfit || fill ? double.NaN : d.IconWidth,
                IconStretch = fill && !item.Unknown ? Stretch.UniformToFill : Stretch.Uniform,
                IconAlign = outfit && !item.Unknown ? VerticalAlignment.Bottom : VerticalAlignment.Center,
                Row = d.Row, Col = d.Col, RowSpan = d.RowSpan, ColSpan = d.ColSpan, Background = bg,
                CheckVisibility = d.Trackable && !item.Unknown ? Visibility.Visible : Visibility.Collapsed, Floats = d.Floats, OneIn = d.OneIn,
                Kind = d.Kind, Tier = d.Tier, Unknown = item.Unknown, Video = item.Video, Song = item.Song, IsLoading = d.Id == "loading", IsGizmo = d.Id.StartsWith("gizmo"), IsToken = d.Id == "token",
            };
            tile.SetFoundSilently(IsFound(key));
            Tiles.Add(tile);

            if (d.Listed)
                Odds.Add(new OddsRow { Name = d.ListName, OneInText = DropsData.OneInText(d.OneIn), PctText = $"({DropsData.Percent(d.OneIn)})",
                                       Icon = $"/Assets/{item.Icon}.png", IconBg = Palette(d.Kind) });
        }
        UpdateFoundText();
        Refresh();
        Tick();
    }

    // older saves stored the token per week ("w1:token"): any of those counts as the shared token
    bool IsFound(string key) => key == "token"
        ? state.Found.Keys.Any(k => k == "token" || k.EndsWith(":token"))
        : state.Found.GetValueOrDefault(key);

    public void OnFoundChanged(string key, bool value)
    {
        if (key == "token")
            foreach (var k in state.Found.Keys.Where(k => k == "token" || k.EndsWith(":token")).ToList()) state.Found.Remove(k);
        if (value) state.Found[key] = true; else state.Found.Remove(key);
        UpdateFoundText();
        save();
    }

    void UpdateFoundText()
    {
        int total = DropsData.Drops.Count(d => d.Trackable);
        int found = Tiles.Count(t => t.CheckVisibility == Visibility.Visible && t.Found);
        FoundText = $"{found}/{total} FOUND";
        HasFound = found > 0;
    }

    /// <summary>Called every second: week countdown.</summary>
    public void Tick()
    {
        var now = DateTime.UtcNow;
        if (follow && CurrentWeekNumber() != week.Number) { Select(CurrentWeekNumber()); return; }   // next week just started
        foreach (var w in Weeks)
        {
            var d = DropsData.Weeks.First(x => x.Number == w.Number);
            w.Ended = now >= d.EndUtc; w.Live = d.StartUtc <= now && now < d.EndUtc;
        }
        bool ended = now >= week.EndUtc;
        bool inactive = ended || now < week.StartUtc;                       // over, or not started yet
        foreach (var t in Tiles) t.Dim = inactive;
        HeroIcon = $"/Assets/{week.HeroIcon}{(inactive ? "_bw" : "")}.png";   // black and white and shadowed unless the week is live
        TitleBrush = inactive ? InactiveTitle : Brushes.White;
        LabelOpacity = inactive ? 0.6 : 1.0;
        if (ended) { EndLabel = "ENDED"; Countdown = ""; return; }
        var left = week.EndUtc - now;
        EndLabel = now < week.StartUtc ? "STARTS IN" : "ENDS IN";
        if (now < week.StartUtc) left = week.StartUtc - now;
        Countdown = $"{(int)left.TotalDays}:{left.Hours:00}:{left.Minutes:00}:{left.Seconds:00}";
    }

    /// <summary>Called when the chest total changes: odds tooltips and the expected-drops line.</summary>
    public void Refresh()
    {
        int n = chests();
        var parts = DropsData.Drops.Where(d => d.Listed)
            .OrderBy(d => d.OneIn)
            .Select(d => $"{d.ListName} {Fmt((double)n / d.OneIn)}");
        ExpectedText = n == 0 ? "Open some chests and your expected drops will show up here."
            : $"At {n:N0} chests you'd expect about:  " + string.Join("   ·   ", parts);
    }

    static string Fmt(double v) => v >= 10 ? v.ToString("0", CultureInfo.CurrentCulture)
        : v >= 1 ? v.ToString("0.#", CultureInfo.CurrentCulture)
        : v >= 0.1 ? v.ToString("0.##", CultureInfo.CurrentCulture)
        : v >= 0.01 ? v.ToString("0.###", CultureInfo.CurrentCulture) : v.ToString("0.####", CultureInfo.CurrentCulture);

    static T Frozen<T>(T b) where T : Freezable { b.Freeze(); return b; }

    static (string a, string b) Colors(string kind) => kind switch
    {
        "token" => ("#FFE24F", "#F0B400"),   // bright yellow, like the Pirate Shop token frame
        "solid" => ("#E2C943", "#8A761F"),   // mustard gold of the solid-gold outfits
        "purple" => ("#B764F0", "#8A3DD2"),
        _ => ("#3DB8F5", "#1D86D4"),
    };

    /// <summary>Plain frozen gradient (used where nothing needs to move, like the odds list icons).</summary>
    static Brush Palette(string kind)
    {
        var (a, b) = Colors(kind);
        var g = new LinearGradientBrush((Color)ColorConverter.ConvertFromString(a), (Color)ColorConverter.ConvertFromString(b), 90);
        g.Freeze(); return g;
    }

    /// <summary>A gradient that slowly flows diagonally. The pattern repeats exactly once per cycle, so the loop has no jump.</summary>
    static Brush Flow(string kind)
    {
        var (a, b) = Colors(kind);
        Color ca = (Color)ColorConverter.ConvertFromString(a), cb = (Color)ColorConverter.ConvertFromString(b);
        var move = new TranslateTransform();
        var g = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0), EndPoint = new Point(1, 1), SpreadMethod = GradientSpreadMethod.Repeat,
            RelativeTransform = move,
        };
        g.GradientStops.Add(new GradientStop(ca, 0));
        g.GradientStops.Add(new GradientStop(cb, 0.5));
        g.GradientStops.Add(new GradientStop(ca, 1));
        foreach (var prop in new[] { TranslateTransform.XProperty, TranslateTransform.YProperty })
        {
            var anim = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(7)) { RepeatBehavior = RepeatBehavior.Forever };
            Timeline.SetDesiredFrameRate(anim, 30);
            move.BeginAnimation(prop, anim);
        }
        return g;
    }
}
