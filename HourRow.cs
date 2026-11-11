using System.Globalization;
using System.Windows;
using System.Windows.Media;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>One line of the Treasure Hours list (and, for the next one, the big countdown above it).</summary>
public class HourRow : Notify
{
    public TreasureWindow W { get; }
    public string When { get; }
    public string Outfit { get; }
    public string Token { get; }
    public string Vehicle { get; }
    public DateTime StartParis { get; }
    public DateTime EndParis { get; }

    public HourRow(TreasureWindow w)
    {
        W = w;
        StartParis = Paris.FromUtc(w.StartUtc); EndParis = Paris.FromUtc(w.EndUtc);
        When = $"{Paris.Day(StartParis)} · {Paris.Range(StartParis, EndParis)}";
        Outfit = Mul(w.Outfit); Token = Mul(w.Token); Vehicle = Mul(w.Vehicle);
    }

    static string Mul(double m) => m.ToString("0.##", CultureInfo.InvariantCulture) + "×";

    static Brush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    static readonly Brush NextFg = B("#FFE24F"), NextBg = B("#3B3719"), LiveFg = B("#4ADE80"), LiveBg = B("#153322"),
                          UpFg = B("#A6A6A6"), UpBg = B("#2C2C2C"), EndFg = B("#7A7A7A"), EndBg = B("#262626");

    string badge = "", countdown = "", label = "", clock = "";
    Brush badgeFg = UpFg, badgeBg = UpBg;
    double opacity = 1;
    int group;

    public string Badge { get => badge; private set => Set(ref badge, value); }
    public Brush BadgeFg { get => badgeFg; private set => Set(ref badgeFg, value); }
    public Brush BadgeBg { get => badgeBg; private set => Set(ref badgeBg, value); }
    public string Countdown { get => countdown; private set { if (Set(ref countdown, value)) Raise(nameof(CountdownVisible)); } }
    public Visibility CountdownVisible => countdown.Length > 0 ? Visibility.Visible : Visibility.Collapsed;
    public double Opacity { get => opacity; private set => Set(ref opacity, value); }
    /// <summary>"STARTS IN" or "ENDS IN" (used by the big countdown).</summary>
    public string Label { get => label; private set => Set(ref label, value); }
    /// <summary>The bare countdown, "3:00:52:32".</summary>
    public string Clock { get => clock; private set => Set(ref clock, value); }
    /// <summary>0 = live or next, 1 = upcoming, 2 = ended: the list order.</summary>
    public int Group => group;

    public void Update(DateTime nowUtc, bool isNext)
    {
        bool ended = nowUtc >= W.EndUtc, live = W.StartUtc <= nowUtc && !ended;
        if (live) { group = 0; Badge = "LIVE NOW"; BadgeFg = LiveFg; BadgeBg = LiveBg; Opacity = 1; Label = "ENDS IN"; Clock = Paris.Countdown(W.EndUtc - nowUtc); Countdown = "Ends in " + Clock; }
        else if (ended) { group = 2; Badge = "ENDED"; BadgeFg = EndFg; BadgeBg = EndBg; Opacity = 0.5; Label = ""; Clock = ""; Countdown = ""; }
        else if (isNext) { group = 0; Badge = "NEXT TREASURE HOUR"; BadgeFg = NextFg; BadgeBg = NextBg; Opacity = 1; Label = "STARTS IN"; Clock = Paris.Countdown(W.StartUtc - nowUtc); Countdown = "In " + Clock; }
        else { group = 1; Badge = "UPCOMING"; BadgeFg = UpFg; BadgeBg = UpBg; Opacity = 1; Label = ""; Clock = ""; Countdown = ""; }
    }
}
