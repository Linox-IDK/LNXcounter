using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>"Treasure Hour is live" pop-up in the top right corner of the screen: slides in with a chime, stays a few seconds, slides out.</summary>
public partial class HourToast : Window
{
    const double Seconds = 9;
    static HourToast? current;
    bool leaving;
    double shownLeft;

    readonly int minutes;

    /// <summary>minutes = 0: the hour is live now. 30 / 10: it starts in that many minutes (the 30 minute one is silent).</summary>
    public static void Show(TreasureWindow w, int minutes = 0)
    {
        current?.Leave();
        current = new HourToast(w, minutes);
        current.Show();
    }

    HourToast(TreasureWindow w, int minutes)
    {
        this.minutes = minutes;
        InitializeComponent();
        if (minutes > 0)
        {
            var amber = new SolidColorBrush(Color.FromRgb(0xFF, 0xC8, 0x3D));
            Chip.Text = Loc.F("STARTS IN {0} MINUTES", minutes); Chip.Foreground = amber; Dot.Fill = amber;
        }
        string M(double m) => m.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "×";
        Boosts.Text = $"{Loc.T("Outfit")} {M(w.Outfit)}  ·  {Loc.T("Pirate Shop")} {M(w.Token)}  ·  {Loc.T("Vehicle")} {M(w.Vehicle)}";
        var end = Paris.FromUtc(w.EndUtc);
        Ends.Text = minutes > 0 ? Loc.F("Starts at {0} Paris time  ·  click to close", Paris.Clock(Paris.FromUtc(w.StartUtc))) : Loc.F("Until {0} Paris time  ·  click to close", Paris.Clock(end));

        Loaded += (_, _) => Enter();
        Closed += (_, _) => { if (ReferenceEquals(current, this)) current = null; };
    }

    void Enter()
    {
        var wa = SystemParameters.WorkArea;
        shownLeft = wa.Right - ActualWidth - 6;
        Top = wa.Top + 8;
        Left = wa.Right + 20;                                   // starts just outside the screen
        Opacity = 0;

        var ease = new BackEase { EasingMode = EasingMode.EaseOut, Amplitude = 0.35 };
        BeginAnimation(LeftProperty, new DoubleAnimation(wa.Right + 20, shownLeft, TimeSpan.FromMilliseconds(650)) { EasingFunction = ease });
        BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(350)));

        // a little life: the logo wobbles once, the glow and the green dot pulse, the bar runs down
        Wobble.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimationUsingKeyFrames
        {
            KeyFrames = { new EasingDoubleKeyFrame(0, KeyTime.FromPercent(0)), new EasingDoubleKeyFrame(-14, KeyTime.FromPercent(0.2)),
                          new EasingDoubleKeyFrame(12, KeyTime.FromPercent(0.45)), new EasingDoubleKeyFrame(-6, KeyTime.FromPercent(0.7)), new EasingDoubleKeyFrame(0, KeyTime.FromPercent(1)) },
            Duration = TimeSpan.FromMilliseconds(900), BeginTime = TimeSpan.FromMilliseconds(500),
        });
        Glow.BeginAnimation(System.Windows.Media.Effects.DropShadowEffect.OpacityProperty,
            new DoubleAnimation(0.25, 0.85, TimeSpan.FromMilliseconds(900)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        Dot.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0.25, TimeSpan.FromMilliseconds(600)) { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever });
        var bar = new DoubleAnimation(356, 0, TimeSpan.FromSeconds(Seconds));
        bar.Completed += (_, _) => Leave();
        Bar.BeginAnimation(WidthProperty, bar);

        if (minutes != 30) Chime.Play();   // the 30 minute warning is silent
    }

    void Leave()
    {
        if (leaving) return;
        leaving = true;
        var wa = SystemParameters.WorkArea;
        var slide = new DoubleAnimation(Left, wa.Right + 20, TimeSpan.FromMilliseconds(450)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseIn } };
        slide.Completed += (_, _) => Close();
        BeginAnimation(LeftProperty, slide);
        BeginAnimation(OpacityProperty, new DoubleAnimation(Opacity, 0, TimeSpan.FromMilliseconds(450)));
    }

    void Card_Down(object sender, MouseButtonEventArgs e) => Leave();
}
