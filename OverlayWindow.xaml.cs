using System.Windows;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace LNXcounter;

public partial class OverlayWindow : Window
{
    public OverlayWindow()
    {
        InitializeComponent();
        var s = App.Model.Settings;
        if (double.IsNaN(s.OverlayLeft) || double.IsNaN(s.OverlayTop))
        {
            Left = SystemParameters.WorkArea.Right - Width - 24;
            Top = SystemParameters.WorkArea.Top + 24;
        }
        else { Left = s.OverlayLeft; Top = s.OverlayTop; }
        EnsureOnScreen();
        ApplyCollapsed(s.OverlayCollapsed);
        LocationChanged += (_, _) => { s.OverlayLeft = Left; s.OverlayTop = Top; };
        Closing += (_, _) => App.Model.Save();
        SourceInitialized += (_, _) =>
        {
            ApplyClickThrough();
            App.Model.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(AppModel.InMatch)) Dispatcher.BeginInvoke(ApplyClickThrough); };
        };
    }

    const int GWL_EXSTYLE = -20, WS_EX_TRANSPARENT = 0x20;
    [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr h, int i);
    [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr h, int i, int v);

    /// <summary>During a match the overlay lets every click through to the game, so you never hit it by accident.</summary>
    public void ApplyClickThrough()
    {
        var h = new WindowInteropHelper(this).Handle; if (h == IntPtr.Zero) return;
        int ex = GetWindowLong(h, GWL_EXSTYLE);
        bool through = App.Model.Settings.OverlayIgnoreClicksInMatch && App.Model.InMatch;
        SetWindowLong(h, GWL_EXSTYLE, through ? ex | WS_EX_TRANSPARENT : ex & ~WS_EX_TRANSPARENT);
    }

    void EnsureOnScreen()
    {
        var vl = SystemParameters.VirtualScreenLeft; var vt = SystemParameters.VirtualScreenTop;
        var vr = vl + SystemParameters.VirtualScreenWidth; var vb = vt + SystemParameters.VirtualScreenHeight;
        if (Left < vl - Width + 40 || Left > vr - 40 || Top < vt || Top > vb - 40) { Left = vr - Width - 24; Top = vt + 24; }
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
