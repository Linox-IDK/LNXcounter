using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Animation;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>Silent pop-up in the bottom right corner after a new match was read: "+12 chests". Never shown for a rescan or a re-read.</summary>
public partial class MatchToast : Window
{
    const double Seconds = 6;
    static MatchToast? current;
    bool leaving;

    public static void Show(int opened, int matches, bool treasure, int session, int total)
    {
        current?.Leave();
        current = new MatchToast(opened, matches, treasure, session, total);
        current.Show();
    }

    MatchToast(int opened, int matches, bool treasure, int session, int total)
    {
        InitializeComponent();
        Big.Text = opened == 0 ? Loc.T("No chest this match") : opened == 1 ? Loc.T("+1 chest") : Loc.F("+{0} chests", opened);
        if (matches > 1) Chip.Text = Loc.F("{0} MATCHES COUNTED", matches);
        Line.Text = Loc.F("This session: {0}  ·  Total: {1}", session, total);
        if (treasure) { Extra.Text = "★ " + Loc.T("Treasure found"); Extra.Visibility = Visibility.Visible; }
        Loaded += (_, _) => Enter();
        Closed += (_, _) => { if (ReferenceEquals(current, this)) current = null; };
    }

    double shownLeft;
    void Enter()
    {
        var wa = SystemParameters.WorkArea;
        shownLeft = wa.Right - ActualWidth - 4;
        Top = wa.Bottom - ActualHeight - 6;
        Left = wa.Right + 20; Opacity = 0;
        BeginAnimation(LeftProperty, new DoubleAnimation(wa.Right + 20, shownLeft, TimeSpan.FromMilliseconds(500)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300)));
        var bar = new DoubleAnimation(320, 0, TimeSpan.FromSeconds(Seconds));
        bar.Completed += (_, _) => Leave();
        Bar.BeginAnimation(WidthProperty, bar);
    }

    void Leave()
    {
        if (leaving) return;
        leaving = true;
        var wa = SystemParameters.WorkArea;
        var slide = new DoubleAnimation(Left, wa.Right + 20, TimeSpan.FromMilliseconds(400)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        slide.Completed += (_, _) => Close();
        BeginAnimation(LeftProperty, slide);
        BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(400)));
    }

    void Card_Down(object sender, MouseButtonEventArgs e) => Leave();
}
