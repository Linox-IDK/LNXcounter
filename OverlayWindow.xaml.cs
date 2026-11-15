using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace LNXcounter;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        var s = App.Model.Settings;
        ApplyStyle();
        ApplyCollapsed(s.OverlayCollapsed);
        LocationChanged += (_, _) => { if (!App.Model.Settings.MinimalOverlay) { s.OverlayLeft = Left; s.OverlayTop = Top; } };   // the small style is placed by the app, it never moves the saved spot
        Closing += (_, _) => App.Model.Save();
        SourceInitialized += (_, _) =>
        {
            ApplyClickThrough();
            App.Model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppModel.InMatch)) Dispatcher.BeginInvoke(ApplyClickThrough); };
        };
    }

    const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20, WS_EX_NOACTIVATE = 0x08000000;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);
    [DllImport("user32.dll")] static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    static readonly IntPtr HWND_TOPMOST = new(-1);
    const uint SWP_NOSIZE = 1, SWP_NOMOVE = 2, SWP_NOACTIVATE = 0x10;
    [DllImport("user32.dll")] static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
    [StructLayout(LayoutKind.Sequential)] struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] struct POINT { public int X, Y; }

    /// <summary>The small style never takes a click (and never takes focus); the full one lets every click through to the game only during a match.</summary>
    public void ApplyClickThrough()
    {
        var h = new WindowInteropHelper(this).Handle; if (h == IntPtr.Zero) return;
        int ex = GetWindowLong(h, GWL_EXSTYLE);
        bool through = App.Model.Settings.MinimalOverlay || (App.Model.Settings.OverlayIgnoreClicksInMatch && App.Model.InMatch);
        SetWindowLong(h, GWL_EXSTYLE, through ? ex | WS_EX_TRANSPARENT | WS_EX_NOACTIVATE : ex & ~(WS_EX_TRANSPARENT | WS_EX_NOACTIVATE));
    }

    void EnsureOnScreen()
    {
        var vl = SystemParameters.VirtualScreenLeft; var vt = SystemParameters.VirtualScreenTop;
        var vr = vl + SystemParameters.VirtualScreenWidth; var vb = vt + SystemParameters.VirtualScreenHeight;
        if (Left < vl - Width + 40 || Left > vr - 40 || Top < vt || Top > vb - 40) { Left = vr - Width - 24; Top = vt + 24; }
    }

    // ----- the two styles -----
    DispatcherTimer? follow;
    double appliedScale;

    /// <summary>Full overlay (movable), or the small one: only the chest icon and the count, fixed under the minimap of the game.</summary>
    public void ApplyStyle()
    {
        var s = App.Model.Settings;
        bool mini = s.MinimalOverlay;
        FullBox.Visibility = mini ? Visibility.Collapsed : Visibility.Visible;
        MiniBox.Visibility = mini ? Visibility.Visible : Visibility.Collapsed;
        // one light timer for both styles: it keeps the overlay above the game (see KeepOnTop) and, for the small style, follows the game window
        follow ??= new DispatcherTimer();
        follow.Interval = TimeSpan.FromSeconds(App.Low ? 2 : 1);
        follow.Tick -= FollowTick; follow.Tick += FollowTick;
        follow.Start();
        if (mini)
        {
            SizeToContent = SizeToContent.WidthAndHeight;
            Place();
        }
        else
        {
            Opacity = 1;
            double sc = Math.Clamp(s.OverlayScale, 0.6, 1.6);
            FullBox.LayoutTransform = sc == 1 ? Transform.Identity : new ScaleTransform(sc, sc);   // only the framed style has a size setting
            Width = 280 * sc; SizeToContent = SizeToContent.Height;
            if (double.IsNaN(s.OverlayLeft) || double.IsNaN(s.OverlayTop))
            {
                Left = SystemParameters.WorkArea.Right - Width - 24;
                Top = SystemParameters.WorkArea.Top + 24;
            }
            else { Left = s.OverlayLeft; Top = s.OverlayTop; }
            EnsureOnScreen();
        }
        ApplyClickThrough();
    }

    void FollowTick(object? sender, EventArgs e)
    {
        if (!IsVisible) return;
        if (App.Model.Settings.MinimalOverlay) Place();
        KeepOnTop();
    }

    /// <summary>A game in Fullscreen (or one that was just clicked) can take the top of the stack from a Topmost window; asking for it again, without
    /// activating anything, puts the overlay back in front. It is the same in Windowed, Windowed Fullscreen and Fullscreen.</summary>
    void KeepOnTop()
    {
        var h = new WindowInteropHelper(this).Handle; if (h == IntPtr.Zero) return;
        SetWindowPos(h, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE);
    }

    // Where the minimap of Fortnite is, measured on a real 1080p recording of the HUD: it is a square about 202 wide whose right edge is 28 from the
    // right of the screen, 27 from the top, so it ends at y = 229. Everything of the HUD grows with the height of the screen (1440p = 1.333, 2160p = 2).
    const double MapWidth = 202, MapRightGap = 28, UnderMap = 236, Pad = 6;

    void Place()
    {
        double dpi = VisualTreeHelper.GetDpi(this).DpiScaleX;
        double left, top, w, h; bool inGame = false;
        var hwnd = AppModel.FindWindowOf(AppModel.FortnitePid);   // looked up again: the game may have made a new window when its display mode changed
        if (hwnd == IntPtr.Zero) hwnd = AppModel.FortniteHwnd;
        var pt = new POINT();
        if (hwnd != IntPtr.Zero && GetClientRect(hwnd, out var r) && ClientToScreen(hwnd, ref pt) && r.Right > 300 && r.Bottom > 300)
        { left = pt.X; top = pt.Y; w = r.Right; h = r.Bottom; inGame = true; }
        else { left = 0; top = 0; w = SystemParameters.PrimaryScreenWidth * dpi; h = SystemParameters.PrimaryScreenHeight * dpi; }

        double s = h / 1080.0;                                   // device pixels per "1080p pixel"
        double k = s / dpi;                                      // the same, in WPF units
        if (Math.Abs(k - appliedScale) > 0.001) { MiniContent.LayoutTransform = new ScaleTransform(k, k); appliedScale = k; }
        var set = App.Model.Settings;
        double x = left + w - (MapRightGap + MapWidth) * s + set.OverlayNudgeX * s - Pad * s;
        double y = top + UnderMap * s + set.OverlayNudgeY * s - Pad * s;
        Left = x / dpi; Top = y / dpi;

        // shown for as long as Fortnite is open with a visible window (not minimized), whatever is in front of it or whether the main window is open,
        // hidden or closed to the tray; with no game it only shows while this app is in front, so that the position can be checked and fine-tuned in Settings
        GetWindowThreadProcessId(GetForegroundWindow(), out uint fg);
        Opacity = inGame || fg == (uint)Environment.ProcessId ? 1 : 0;
    }

    void ApplyCollapsed(bool collapsed)
    {
        Details.Visibility = collapsed ? Visibility.Collapsed : Visibility.Visible;
        Chevron.Content = collapsed ? "⌃" : "⌄";
    }

    void Chevron_Click(object sender, RoutedEventArgs e) => SetCollapsed(!App.Model.Settings.OverlayCollapsed);

    public void SetCollapsed(bool collapsed)
    {
        App.Model.Settings.OverlayCollapsed = collapsed;
        ApplyCollapsed(collapsed);
        App.Model.Save();
    }

    /// <summary>"tl", "tr", "bl" or "br".</summary>
    public void MoveToCorner(string corner)
    {
        if (App.Model.Settings.MinimalOverlay) return;   // the small style stays under the minimap
        var wa = SystemParameters.WorkArea; const double m = 16;
        UpdateLayout();
        Left = corner.EndsWith('l') ? wa.Left + m : wa.Right - ActualWidth - m;
        Top = corner.StartsWith('t') ? wa.Top + m : wa.Bottom - ActualHeight - m;
        App.Model.Save();
    }

    void Right_Up(object sender, MouseButtonEventArgs e) { AppMenu.ShowAt(this); e.Handled = true; }

    void Drag_Down(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed) { DragMove(); App.Model.Save(); }
    }

}
