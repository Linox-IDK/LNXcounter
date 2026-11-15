using System.Globalization;
using System.Windows.Media;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>One milestone on the Overview (500, 1K, 5K, 10K, 20K chests): gold once reached, with a progress hint while it is the next one.</summary>
public class MilestoneChip : Notify
{
    static Brush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }
    static readonly Brush DoneFg = B("#FF1B1400"), DoneBg = B("#FFE2C943"), NextFg = B("#FFE2C943"), NextBg = B("#3B3719"), FarFg = B("#FF7A7A7A"), FarBg = B("#FF262626");

    public int Goal { get; }
    public string Label { get; }
    string tip = ""; Brush fg = FarFg, bg = FarBg; bool reached;
    public string Tip { get => tip; private set => Set(ref tip, value); }
    public Brush Fg { get => fg; private set => Set(ref fg, value); }
    public Brush Bg { get => bg; private set => Set(ref bg, value); }
    public bool Reached { get => reached; private set => Set(ref reached, value); }

    public MilestoneChip(int goal)
    {
        Goal = goal;
        Label = goal >= 1000 ? (goal / 1000).ToString(CultureInfo.InvariantCulture) + "K" : goal.ToString(CultureInfo.InvariantCulture);
    }

    public void Update(int total, bool isNext)
    {
        Reached = total >= Goal;
        Fg = Reached ? DoneFg : isNext ? NextFg : FarFg;
        Bg = Reached ? DoneBg : isNext ? NextBg : FarBg;
        Tip = Reached ? Loc.F("{0} chests: reached", Goal.ToString("N0"))
            : Loc.F("{0} chests: {1} to go", Goal.ToString("N0"), (Goal - total).ToString("N0"));
    }
}
