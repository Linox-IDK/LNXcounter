using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>Bar graph of the chests you opened: per day (last 30), per week (last 12) or per month (last 12), from the replays read so far.</summary>
public partial class GraphWindow : Window
{
    readonly Dictionary<DateTime, int> perDay;
    int mode;   // 0 days, 1 weeks, 2 months

    public GraphWindow(AppModel model)
    {
        InitializeComponent();
        perDay = model.ChestsPerDay();
        Loaded += (_, _) => Draw();
    }

    record Bucket(string Label, string Tip, int Value, bool Current);

    static string Chests(int v) => Loc.F(v == 1 ? "{0} chest" : "{0} chests", v.ToString("N0"));

    List<Bucket> Build()
    {
        var today = Paris.FromUtc(DateTime.UtcNow).Date;
        var list = new List<Bucket>();
        var ci = CultureInfo.CurrentCulture;
        if (mode == 0)
            for (int i = 29; i >= 0; i--)
            {
                var d = today.AddDays(-i);
                int v = perDay.GetValueOrDefault(d);
                list.Add(new Bucket(d.ToString("d/M", ci), Paris.Day(d) + ": " + Chests(v), v, i == 0));
            }
        else if (mode == 1)
        {
            var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));   // weeks run Monday to Sunday, like the goal
            for (int i = 11; i >= 0; i--)
            {
                var s = monday.AddDays(-7 * i); var e = s.AddDays(7);
                int v = perDay.Where(kv => kv.Key >= s && kv.Key < e).Sum(kv => kv.Value);
                list.Add(new Bucket(s.ToString("d/M", ci), Loc.F("Week of {0}", Paris.Day(s)) + ": " + Chests(v), v, i == 0));
            }
        }
        else
        {
            var first = new DateTime(today.Year, today.Month, 1);
            for (int i = 11; i >= 0; i--)
            {
                var s = first.AddMonths(-i); var e = s.AddMonths(1);
                int v = perDay.Where(kv => kv.Key >= s && kv.Key < e).Sum(kv => kv.Value);
                list.Add(new Bucket(s.ToString("MMM", ci), s.ToString("MMMM yyyy", ci) + ": " + Chests(v), v, i == 0));
            }
        }
        return list;
    }

    static int NiceStep(int max)
    {
        if (max <= 4) return 1;
        double raw = max / 4.0, mag = Math.Pow(10, Math.Floor(Math.Log10(raw))), f = raw / mag;
        double n = f <= 1 ? 1 : f <= 2 ? 2 : f <= 5 ? 5 : 10;
        return (int)Math.Max(1, n * mag);
    }

    void Draw()
    {
        if (Plot == null || Plot.ActualWidth < 50 || Plot.ActualHeight < 50) return;
        Plot.Children.Clear();
        var data = Build();
        double W = Plot.ActualWidth, H = Plot.ActualHeight, L = 52, R = 14, T = 20, B = 30;
        double pw = W - L - R, ph = H - T - B;
        int max = Math.Max(1, data.Max(d => d.Value));
        int step = NiceStep(max), top = (int)Math.Ceiling(max / (double)step) * step;
        var muted = (Brush)FindResource("MutedBrush"); var line = (Brush)FindResource("LineBrush"); var gold = (Brush)FindResource("GoldBrush"); var text = (Brush)FindResource("TextBrush");

        for (int y = 0; y <= top; y += step)
        {
            double py = T + ph - ph * y / top;
            Plot.Children.Add(new Line { X1 = L, X2 = W - R, Y1 = py, Y2 = py, Stroke = line, StrokeThickness = 1 });
            var t = new TextBlock { Text = y.ToString("N0"), Foreground = muted, FontSize = 11, Width = L - 8, TextAlignment = TextAlignment.Right };
            Canvas.SetLeft(t, 0); Canvas.SetTop(t, py - 8); Plot.Children.Add(t);
        }

        double slot = pw / data.Count, bw = Math.Max(3, slot * 0.68);
        int every = (int)Math.Ceiling(34 / slot);   // keep the labels from touching
        var bar = new LinearGradientBrush(Color.FromRgb(0xF2, 0xD3, 0x5B), Color.FromRgb(0xA9, 0x85, 0x1C), 90);
        var barNow = new LinearGradientBrush(Color.FromRgb(0xFF, 0xF0, 0x9A), Color.FromRgb(0xE2, 0xC9, 0x43), 90);
        for (int i = 0; i < data.Count; i++)
        {
            var d = data[i]; double x = L + slot * i + (slot - bw) / 2, h = ph * d.Value / top;
            var r = new Rectangle { Width = bw, Height = Math.Max(d.Value > 0 ? 2 : 0, h), RadiusX = 3, RadiusY = 3, Fill = d.Current ? barNow : bar, ToolTip = d.Tip, Cursor = Cursors.Hand };
            Canvas.SetLeft(r, x); Canvas.SetTop(r, T + ph - r.Height); Plot.Children.Add(r);
            if ((data.Count - 1 - i) % every == 0)
            {
                var t = new TextBlock { Text = d.Label, Foreground = d.Current ? gold : muted, FontSize = 11, Width = slot * every, TextAlignment = TextAlignment.Center };
                Canvas.SetLeft(t, x + bw / 2 - slot * every / 2); Canvas.SetTop(t, T + ph + 7); Plot.Children.Add(t);
            }
            if (d.Value > 0 && slot >= 26)
            {
                var t = new TextBlock { Text = d.Value.ToString("N0"), Foreground = text, FontSize = 10.5, Width = slot, TextAlignment = TextAlignment.Center };
                Canvas.SetLeft(t, x + bw / 2 - slot / 2); Canvas.SetTop(t, T + ph - r.Height - 16); Plot.Children.Add(t);
            }
        }

        int total = data.Sum(d => d.Value); var best = data.MaxBy(d => d.Value)!;
        string unit = Loc.T(mode == 0 ? "day" : mode == 1 ? "week" : "month");
        Summary.Text = total == 0 ? Loc.T("No chests opened in this period yet.")
            : Loc.F("Total {0}  ·  Best {1}: {2}  ·  Average per {1}: {3}", total.ToString("N0"), unit, best.Value.ToString("N0") + " (" + best.Label + ")", (total / (double)data.Count).ToString("0.#", CultureInfo.CurrentCulture))
              + "   " + Loc.T("Only chests counted from replays (chests added by hand have no date).");
    }

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (TabDay == null || TabWeek == null || TabMonth == null) return;
        mode = TabMonth.IsChecked == true ? 2 : TabWeek.IsChecked == true ? 1 : 0;
        Draw();
    }
    void Plot_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    void Title_Down(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Close_Click(object sender, RoutedEventArgs e) => Close();
    void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
