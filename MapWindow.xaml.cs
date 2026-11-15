using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>Shows where the chests of one match (or of every match) were, on the map picture (Assets/map.jpg): as the spots themselves or as a heat map.</summary>
public partial class MapWindow : Window
{
    // The map picture covers a square of 197,000 Fortnite units (1 unit = 1 cm) centred on (23,000 ; 22,000).
    // Calibrated on the game's own POI positions: world X runs left to right, world Y runs top to bottom.
    public const double CenterX = 23000, CenterY = 22000, Span = 197000;

    public static Point Project(double x, double y) =>
        new(((x - CenterX) / Span + 0.5) * 1000, ((y - CenterY) / Span + 0.5) * 1000);

    static readonly (string Type, string Name, string Icon)[] Kinds =
    {
        ("normal", "Normal chests", "chest"), ("rare", "Rare chests", "rare"), ("buried", "Buried chests", "buried"),
        ("relic", "Relic chests", "special"), ("sprite", "Sprite chests", "special"), ("other", "Other chests", "plus"),
    };

    readonly MatchRecord match;
    List<ChestSpot>? all;   // every chest of every match, loaded the first time "all matches" is picked

    public MapWindow(MatchRecord m)
    {
        InitializeComponent();
        match = m;
        try { MapImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/map.jpg")); }
        catch { DrawGrid(); }
        Render();
    }

    void Opt_Checked(object sender, RoutedEventArgs e)
    {
        if (ModeSpots == null || ModeHeat == null || ScopeMatch == null || ScopeAll == null || Layer == null) return;   // fires during InitializeComponent
        Render();
    }

    void Render()
    {
        bool heat = ModeHeat.IsChecked == true, everything = ScopeAll.IsChecked == true;
        List<ChestSpot> spots;
        if (everything) { all ??= App.Model.AllSpots(); spots = all; }
        else spots = match.Spots;
        TitleText.Text = everything ? Loc.F("All matches · {0} on the map", spots.Count.ToString("N0")) : Loc.F("{0} · {1} on the map", match.DateText, spots.Count);

        Layer.Children.Clear(); Legend.Children.Clear();
        HeatImage.Visibility = heat ? Visibility.Visible : Visibility.Collapsed;
        if (heat) { HeatImage.Source = BuildHeat(spots); LegendNote(Loc.T("Brighter and redder = more chests opened there.")); return; }

        foreach (var s in spots)
        {
            var kind = Kinds.FirstOrDefault(k => k.Type == s.Type || (s.Type == "sprite_jewel" && k.Type == "sprite"), Kinds[^1]);
            var p = Project(s.X, s.Y);
            if (everything)
            {
                // thousands of chests: small dots, not pictures
                var dot = new Ellipse { Width = 9, Height = 9, Fill = new SolidColorBrush(Color.FromArgb(200, 0xF2, 0xD3, 0x5B)), Stroke = Brushes.Black, StrokeThickness = 1, IsHitTestVisible = false };
                Canvas.SetLeft(dot, p.X - 4.5); Canvas.SetTop(dot, p.Y - 4.5); Layer.Children.Add(dot);
                continue;
            }
            var img = new Image
            {
                Width = 26, Height = 26, Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{kind.Icon}.png")),
                Opacity = s.Broken ? 0.45 : 1, Cursor = Cursors.Hand,
                Effect = new System.Windows.Media.Effects.DropShadowEffect { Color = Colors.Black, BlurRadius = 6, ShadowDepth = 0, Opacity = 0.9 },
                ToolTip = Loc.F(s.Broken ? "{0} (broken) · {1} into the match" : "{0} · {1} into the match", Loc.T(kind.Name), $"{s.AtSeconds / 60}:{s.AtSeconds % 60:00}"),
            };
            RenderOptions.SetBitmapScalingMode(img, BitmapScalingMode.HighQuality);
            Canvas.SetLeft(img, p.X - 13); Canvas.SetTop(img, p.Y - 13);
            Layer.Children.Add(img);
        }

        foreach (var k in Kinds)
        {
            int n = spots.Count(s => k.Type == "sprite" ? s.Type is "sprite" or "sprite_jewel" : s.Type == k.Type);
            if (n == 0) continue;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
            row.Children.Add(new Image { Width = 20, Height = 20, Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{k.Icon}.png")) });
            row.Children.Add(new TextBlock { Text = $"{Loc.T(k.Name)}  {n:N0}", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("TextBrush") });
            Legend.Children.Add(row);
        }
        if (spots.Any(s => !s.Exact)) LegendNote(Loc.T("A chest is drawn where you stood when you opened it."));
    }

    void LegendNote(string text) =>
        Legend.Children.Add(new TextBlock { Text = text, Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });

    /// <summary>A transparent picture the size of the map: every chest adds a soft bump, the busiest places glow red.</summary>
    static BitmapSource BuildHeat(List<ChestSpot> spots)
    {
        const int N = 250;                    // 4 map units of the 1000 per pixel
        double sigma = spots.Count > 120 ? 4.5 : 7;   // a single match is blurrier so a few chests still make a visible blob
        var grid = new double[N * N];
        int reach = (int)(sigma * 3);
        foreach (var s in spots)
        {
            var p = Project(s.X, s.Y);
            int cx = (int)(p.X / 1000 * N), cy = (int)(p.Y / 1000 * N);
            for (int dy = -reach; dy <= reach; dy++)
                for (int dx = -reach; dx <= reach; dx++)
                {
                    int x = cx + dx, y = cy + dy;
                    if (x < 0 || y < 0 || x >= N || y >= N) continue;
                    grid[y * N + x] += Math.Exp(-(dx * dx + dy * dy) / (2 * sigma * sigma));
                }
        }
        // scale on the busy places, not on the single busiest cell, so one hot spot does not wash out the rest
        var busy = grid.Where(g => g > 0.05).OrderBy(g => g).ToArray();
        double max = busy.Length == 0 ? 1 : busy[(int)(busy.Length * 0.97)];
        if (max <= 0) max = 1;
        var px = new byte[N * N * 4];
        for (int i = 0; i < grid.Length; i++)
        {
            double v = Math.Min(1, grid[i] / max);
            if (v < 0.08) continue;
            // blue -> green -> yellow -> red
            double r, g, b;
            if (v < 0.33) { double t = v / 0.33; r = 0; g = 160 * t; b = 255 - 100 * t; }
            else if (v < 0.66) { double t = (v - 0.33) / 0.33; r = 255 * t; g = 160 + 95 * t; b = 155 - 155 * t; }
            else { double t = (v - 0.66) / 0.34; r = 255; g = 255 - 200 * t; b = 0; }
            double a = Math.Min(0.88, 0.25 + 0.75 * Math.Sqrt(v));
            px[i * 4] = (byte)(b * a); px[i * 4 + 1] = (byte)(g * a); px[i * 4 + 2] = (byte)(r * a); px[i * 4 + 3] = (byte)(255 * a);   // premultiplied
        }
        var bmp = BitmapSource.Create(N, N, 96, 96, PixelFormats.Pbgra32, null, px, N * 4);
        bmp.Freeze();
        return bmp;
    }

    // no map picture yet: a plain grid so the markers still make sense
    void DrawGrid()
    {
        var line = new SolidColorBrush(Color.FromArgb(40, 0xD4, 0xAF, 0x37));
        for (int i = 1; i < 10; i++)
        {
            Layer.Children.Add(new Line { X1 = i * 100, X2 = i * 100, Y1 = 0, Y2 = 1000, Stroke = line, StrokeThickness = 1 });
            Layer.Children.Add(new Line { Y1 = i * 100, Y2 = i * 100, X1 = 0, X2 = 1000, Stroke = line, StrokeThickness = 1 });
        }
    }

    void Title_Down(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Close_Click(object sender, RoutedEventArgs e) => Close();
    void Window_PreviewKeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Escape) Close(); }
}
