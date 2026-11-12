using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>Shows where the chests of one match were, on the map picture (Assets/map.jpg).</summary>
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

    public MapWindow(MatchRecord m)
    {
        InitializeComponent();
        TitleText.Text = Loc.F("{0} · {1} on the map", m.DateText, m.Spots.Count);
        try { MapImage.Source = new BitmapImage(new Uri("pack://application:,,,/Assets/map.jpg")); }
        catch { DrawGrid(); }

        foreach (var s in m.Spots)
        {
            var kind = Kinds.FirstOrDefault(k => k.Type == s.Type || (s.Type == "sprite_jewel" && k.Type == "sprite"), Kinds[^1]);
            var p = Project(s.X, s.Y);
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
            int n = m.Spots.Count(s => k.Type == "sprite" ? s.Type is "sprite" or "sprite_jewel" : s.Type == k.Type);
            if (n == 0) continue;
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 18, 0) };
            row.Children.Add(new Image { Width = 20, Height = 20, Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{k.Icon}.png")) });
            row.Children.Add(new TextBlock { Text = $"{Loc.T(k.Name)}  {n}", Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center, Foreground = (Brush)FindResource("TextBrush") });
            Legend.Children.Add(row);
        }
        if (m.Spots.Any(s => !s.Exact))
            Legend.Children.Add(new TextBlock { Text = Loc.T("A chest is drawn where you stood when you opened it."), Foreground = (Brush)FindResource("MutedBrush"), FontSize = 11.5, VerticalAlignment = VerticalAlignment.Center });
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
