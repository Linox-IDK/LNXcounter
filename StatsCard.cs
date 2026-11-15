using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>The shareable picture of your stats (Settings > Data > Stats card, or the Card button): built off-screen and drawn at 3x, so it is 1080 pixels wide.</summary>
public static class StatsCard
{
    const double W = 360, Scale = 3;
    static Brush B(string hex) { var b = (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!; b.Freeze(); return b; }

    static TextBlock T(string text, double size, Brush fg, FontFamily? font = null, TextAlignment align = TextAlignment.Left) =>
        new() { Text = text, FontSize = size, Foreground = fg, FontFamily = font ?? new FontFamily("Segoe UI"), TextAlignment = align };

    public static BitmapSource Render(AppModel m)
    {
        var pirate = (FontFamily)Application.Current.FindResource("PirateFont");
        Brush gold = B("#FFF2D35B"), goldDim = B("#FF6E5A1A"), muted = B("#FF8A8A8A"), text = B("#FFEDEDED"), tile = B("#FF1A1A1A"), tileLine = B("#FF3A3010");

        var root = new Border { Width = W, Background = B("#FF0F0F0F"), BorderBrush = goldDim, BorderThickness = new Thickness(1.5), CornerRadius = new CornerRadius(16), Padding = new Thickness(20) };
        var col = new StackPanel();
        root.Child = col;

        // header: app name and your Fortnite name
        var head = new Grid();
        head.ColumnDefinitions.Add(new ColumnDefinition()); head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.Children.Add(T("LNXcounter", 22, gold, pirate));
        if (!string.IsNullOrWhiteSpace(m.PlayerName))
        {
            var pill = new Border { BorderBrush = goldDim, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 2, 9, 2), VerticalAlignment = VerticalAlignment.Center, Child = T(m.PlayerName!, 12, B("#FFB8A24A")) };
            Grid.SetColumn(pill, 1); head.Children.Add(pill);
        }
        col.Children.Add(head);

        // the big number, with the chest next to it
        var big = new Grid { Margin = new Thickness(0, 14, 0, 0) };
        big.ColumnDefinitions.Add(new ColumnDefinition()); big.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var left = new StackPanel();
        left.Children.Add(T(Loc.T("CHESTS OPENED"), 15, muted, pirate));
        var number = T(m.TotalOpened.ToString("N0", CultureInfo.CurrentCulture), 72, B("#FFE8C547"), pirate);
        number.Margin = new Thickness(0, -6, 0, -4);
        left.Children.Add(number);
        if (!string.IsNullOrEmpty(m.TreasureNote)) left.Children.Add(T(m.TreasureNote, 12, muted));
        big.Children.Add(left);
        try
        {
            var chest = new Image { Width = 78, Height = 78, VerticalAlignment = VerticalAlignment.Center, Source = new BitmapImage(new Uri("pack://application:,,,/Assets/chest.png")) };
            RenderOptions.SetBitmapScalingMode(chest, BitmapScalingMode.HighQuality);
            Grid.SetColumn(chest, 1); big.Children.Add(chest);
        }
        catch { }
        col.Children.Add(big);

        // four tiles
        var perDay = m.ChestsPerDay();
        int bestDay = perDay.Count == 0 ? 0 : perDay.Values.Max();
        var grid = new UniformGrid { Columns = 2, Margin = new Thickness(0, 16, 0, 0) };
        void Tile(string label, string value)
        {
            var st = new StackPanel();
            st.Children.Add(T(label, 13, muted, pirate));
            st.Children.Add(T(value, 24, gold, pirate));
            grid.Children.Add(new Border { Background = tile, BorderBrush = tileLine, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10), Padding = new Thickness(10, 6, 10, 6), Margin = new Thickness(0, 0, 8, 8), Child = st });
        }
        Tile(Loc.T("TEAM CHESTS"), m.TeamChestsText);
        Tile(Loc.T("TREASURES"), m.TotalTreasures.ToString("N0", CultureInfo.CurrentCulture));
        Tile(Loc.T("MATCHES"), m.MatchCount.ToString("N0", CultureInfo.CurrentCulture));
        Tile(Loc.T("BEST DAY"), bestDay.ToString("N0", CultureInfo.CurrentCulture));
        col.Children.Add(grid);

        // milestones
        var chips = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 2, 0, 0) };
        foreach (var c in m.Milestones)
            chips.Children.Add(new Border { Background = c.Bg, CornerRadius = new CornerRadius(10), Padding = new Thickness(9, 1, 9, 1), Margin = new Thickness(0, 0, 6, 0), Child = T(c.Label, 15, c.Fg, pirate) });
        col.Children.Add(chips);

        // last 7 days
        var today = Paris.FromUtc(DateTime.UtcNow).Date;
        var days = Enumerable.Range(0, 7).Select(i => today.AddDays(i - 6)).ToList();
        int max = Math.Max(1, days.Max(d => perDay.GetValueOrDefault(d)));
        var bars = new Grid { Height = 64, Margin = new Thickness(-3, 16, -3, 0) };
        for (int i = 0; i < days.Count; i++)
        {
            bars.ColumnDefinitions.Add(new ColumnDefinition());
            int v = perDay.GetValueOrDefault(days[i]);
            var bar = new Border
            {
                Background = i == days.Count - 1 ? gold : B("#FFA9851C"), CornerRadius = new CornerRadius(3), Margin = new Thickness(3, 0, 3, 0),
                VerticalAlignment = VerticalAlignment.Bottom, Height = v == 0 ? 2 : Math.Max(3, 64.0 * v / max),
            };
            Grid.SetColumn(bar, i); bars.Children.Add(bar);
        }
        col.Children.Add(bars);
        var foot = new Grid { Margin = new Thickness(0, 4, 0, 0) };
        foot.ColumnDefinitions.Add(new ColumnDefinition()); foot.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        foot.Children.Add(T(Loc.T("last 7 days"), 11, B("#FF7A7A7A")));
        var date = T(Paris.Day(today), 11, B("#FF7A7A7A"));
        Grid.SetColumn(date, 1); foot.Children.Add(date);
        col.Children.Add(foot);

        // draw it off-screen at 3x
        root.Measure(new Size(W, double.PositiveInfinity));
        root.Arrange(new Rect(0, 0, root.DesiredSize.Width, root.DesiredSize.Height));
        root.UpdateLayout();
        var bmp = new RenderTargetBitmap((int)Math.Ceiling(root.DesiredSize.Width * Scale), (int)Math.Ceiling(root.DesiredSize.Height * Scale), 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bmp.Render(root);
        bmp.Freeze();
        return bmp;
    }

    public static void SavePng(BitmapSource img, string path)
    {
        var enc = new PngBitmapEncoder(); enc.Frames.Add(BitmapFrame.Create(img));
        using var fs = File.Create(path); enc.Save(fs);
    }
}
