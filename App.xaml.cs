using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using LNXcounter.Services;

namespace LNXcounter;

public partial class App : Application
{
    public const string Version = "1.8.0";
    public static AppModel Model { get; private set; } = null!;
    public static OverlayWindow Overlay { get; private set; } = null!;
    public static bool Quitting { get; private set; }

    Mutex? mutex;
    EventWaitHandle? showEvent;
    System.Windows.Forms.NotifyIcon? tray;
    Window? helper;
    bool trayTipShown;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        Efficiency.Enable();   // Windows "Efficiency mode" from the very first moment

        // one copy only: starting the exe again just brings the running one to the front
        mutex = new Mutex(true, "LNXcounter.SingleInstance.v1", out bool first);
        if (!first)
        {
            try { EventWaitHandle.OpenExisting("LNXcounter.ShowMain").Set(); } catch { }
            Shutdown();
            return;
        }
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "LNXcounter.ShowMain");
        new Thread(() => { while (showEvent.WaitOne()) Dispatcher.BeginInvoke(ShowMain); }) { IsBackground = true }.Start();

        Model = new AppModel();
        Model.Start();
        MainWindow = new MainWindow { DataContext = Model };
        Overlay = new OverlayWindow { DataContext = Model };

        bool startup = e.Args.Contains("--startup");
        if (!startup) ShowMain();
        if (Model.Settings.OverlayVisible) Overlay.Show();

        CreateTray();
        KeepMemoryLow();

        if (e.Args.Contains("--selftest-menu")) { var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; t.Tick += (_, _) => { t.Stop(); ShowTrayMenu(); }; t.Start(); }
    }

    /// <summary>Gives unused memory back to Windows shortly after start, whenever the window is hidden or shown, and every 30 seconds while hidden.</summary>
    void KeepMemoryLow()
    {
        MemoryTrim.Schedule(5000);
        MainWindow.IsVisibleChanged += (_, _) => MemoryTrim.Schedule(MainWindow.IsVisible ? 6000 : 2000);
        MainWindow.StateChanged += (_, _) => MemoryTrim.Schedule(MainWindow.WindowState == WindowState.Minimized ? 2000 : 6000);
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(30) };
        t.Tick += (_, _) => { if (!MainWindow.IsVisible || MainWindow.WindowState == WindowState.Minimized) MemoryTrim.Now(); };
        t.Start();
    }

    void CreateTray()
    {
        tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath!),
            Text = "LNXcounter",
            Visible = true,
        };
        tray.MouseUp += (_, ev) => Dispatcher.BeginInvoke(() =>
        {
            if (ev.Button == System.Windows.Forms.MouseButtons.Left) ShowMain();
            else if (ev.Button == System.Windows.Forms.MouseButtons.Right) ShowTrayMenu();
        });
        void UpdateTip() => tray.Text = $"LNXcounter - {Model.TotalOpened} chests";
        UpdateTip();
        Model.PropertyChanged += (_, ev) => { if (ev.PropertyName == nameof(AppModel.TotalOpened)) UpdateTip(); };
    }

    public static void ShowMain()
    {
        var w = Current.MainWindow;
        if (w == null) return;
        if (!w.IsVisible) w.Show();
        if (w.WindowState == WindowState.Minimized) w.WindowState = WindowState.Normal;
        w.Activate(); w.Topmost = true; w.Topmost = false; w.Focus();
    }

    /// <summary>Called when the big window is closed: it only hides, the app keeps running in the tray.</summary>
    public static void NotifyHiddenToTray()
    {
        var app = (App)Current;
        if (app.trayTipShown || app.tray == null) return;
        app.trayTipShown = true;
        app.tray.ShowBalloonTip(3000, "LNXcounter is still running",
            "Left-click the tray icon to reopen it, right-click for options.", System.Windows.Forms.ToolTipIcon.Info);
    }

    void ShowTrayMenu()
    {
        // WPF menus need a focused window to close properly when you click elsewhere
        helper ??= new Window
        {
            Width = 1, Height = 1, WindowStyle = WindowStyle.None, ShowInTaskbar = false, AllowsTransparency = true,
            Background = Brushes.Transparent, Left = -20000, Top = -20000, Topmost = true,
        };
        helper.Show();
        SetForegroundWindow(new WindowInteropHelper(helper).Handle);
        var menu = AppMenu.Build();
        menu.PlacementTarget = helper;
        menu.Closed += (_, _) => helper.Hide();
        menu.IsOpen = true;
        if (Environment.GetCommandLineArgs().Contains("--selftest-menu"))
        {
            // test hook: open the Overlay submenu without any mouse input
            var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            t.Tick += (_, _) =>
            {
                t.Stop();
                foreach (var it in menu.Items)
                    if (it is System.Windows.Controls.MenuItem mi && mi.Header as string == "Overlay") mi.IsSubmenuOpen = true;
            };
            t.Start();
        }
    }

    public static void SetOverlayVisible(bool visible)
    {
        Model.Settings.OverlayVisible = visible;
        if (visible) Overlay.Show(); else Overlay.Hide();
        Model.Save();
    }

    public static void Quit()
    {
        Quitting = true;
        var app = (App)Current;
        Model.Save();
        if (app.tray != null) { app.tray.Visible = false; app.tray.Dispose(); }
        Overlay.Close();
        Current.MainWindow?.Close();
        Current.Shutdown();
    }
}
