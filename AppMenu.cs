using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using LNXcounter.Services;

namespace LNXcounter;

/// <summary>The right-click menu shared by the tray icon and the overlay. Rebuilt on every open so the info lines are always fresh.</summary>
public static class AppMenu
{
    public static ContextMenu Build()
    {
        var m = App.Model;
        var s = m.Settings;
        var menu = new ContextMenu { Placement = PlacementMode.MousePoint, StaysOpen = false };

        menu.Items.Add(Info($"LNXcounter · v{App.Version}", bold: true));
        menu.Items.Add(Info(Loc.F("Total: {0} chests opened", m.TotalOpened)));
        menu.Items.Add(Info(Trim(m.Status, 52)));
        menu.Items.Add(new Separator());

        menu.Items.Add(Item("Open LNXcounter", App.ShowMain, bold: true));
        var next = string.IsNullOrEmpty(m.PirateBadge) ? m.PirateShort : $"{m.PirateShort}  ({m.PirateBadge.ToLowerInvariant()})";
        menu.Items.Add(Info(Loc.F("Treasure Hours: {0}", next)));
        menu.Items.Add(new Separator());

        var overlay = new MenuItem { Header = Loc.T("Overlay") };
        overlay.Items.Add(Check("Show overlay", s.OverlayVisible, v => App.SetOverlayVisible(v)));
        overlay.Items.Add(Check("Ignore clicks during a match", s.OverlayIgnoreClicksInMatch, v => { s.OverlayIgnoreClicksInMatch = v; m.Save(); App.Overlay.ApplyClickThrough(); }));
        overlay.Items.Add(Check("Collapse details", s.OverlayCollapsed, v => App.Overlay.SetCollapsed(v)));
        var corner = new MenuItem { Header = Loc.T("Move to corner") };
        corner.Items.Add(Item("Top left", () => App.Overlay.MoveToCorner("tl")));
        corner.Items.Add(Item("Top right", () => App.Overlay.MoveToCorner("tr")));
        corner.Items.Add(Item("Bottom left", () => App.Overlay.MoveToCorner("bl")));
        corner.Items.Add(Item("Bottom right", () => App.Overlay.MoveToCorner("br")));
        overlay.Items.Add(corner);
        menu.Items.Add(overlay);

        menu.Items.Add(Check("Start with Windows", Startup.IsEnabled, v => Startup.Set(v)));
        menu.Items.Add(new Separator());

        menu.Items.Add(Item("Scan for new matches now", () => _ = m.ScanAsync()));
        menu.Items.Add(Item("Rebuild from all replays…", async () =>
        {
            if (MessageBox.Show(Loc.T("Re-read every replay in the folder and rebuild your totals from scratch?"), "LNXcounter",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
                await m.RescanAllAsync();
        }));
        menu.Items.Add(Item("Open replays folder", () => OpenFolder(m.ReplayFolder)));
        menu.Items.Add(Item("Open save folder", () => { Directory.CreateDirectory(Store.Dir); OpenFolder(Store.Dir); }));
        menu.Items.Add(new Separator());
        menu.Items.Add(Item("Quit", () => App.Quit()));
        return menu;
    }

    public static void ShowAt(FrameworkElement target)
    {
        var menu = Build();
        menu.PlacementTarget = target;
        menu.IsOpen = true;
    }

    static string Trim(string s, int n) => s.Length <= n ? s : s[..(n - 1)] + "…";

    static void OpenFolder(string path)
    {
        try { if (Directory.Exists(path)) Process.Start(new ProcessStartInfo("explorer.exe", $"\"{path}\"") { UseShellExecute = true }); }
        catch { }
    }

    static MenuItem Info(string text, bool bold = false) =>
        new() { Header = text, IsEnabled = false, FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };

    static MenuItem Item(string text, Action onClick, bool bold = false)
    {
        var mi = new MenuItem { Header = Loc.T(text), FontWeight = bold ? FontWeights.Bold : FontWeights.Normal };
        mi.Click += (_, _) => onClick();
        return mi;
    }

    static MenuItem Check(string text, bool isChecked, Action<bool> onChange)
    {
        var mi = new MenuItem { Header = Loc.T(text), IsCheckable = true, IsChecked = isChecked, StaysOpenOnClick = false };
        mi.Click += (_, _) => onChange(mi.IsChecked);
        return mi;
    }
}
