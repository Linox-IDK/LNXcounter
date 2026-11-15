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
    public const string Version = "1.25.0";
    public static AppModel Model { get; private set; } = null!;
    public static OverlayWindow Overlay { get; private set; } = null!;
    public static bool Quitting { get; private set; }
    /// <summary>Low-resource mode (Settings): read once when the app starts.</summary>
    public static bool Low { get; private set; }
    static bool autoWeak;

    /// <summary>A weak PC: 4 logical processors or fewer, or less than 7 GB of memory (an 8 GB PC reports about 7.4 to 7.9).</summary>
    static bool IsWeakPc()
    {
        try
        {
            double gb = GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / 1073741824.0;
            return Environment.ProcessorCount <= 4 || (gb > 0 && gb < 7);
        }
        catch { return false; }
    }

    Mutex? mutex;
    EventWaitHandle? showEvent;
    TrayIcon? tray;
    Window? helper;
    bool trayTipShown;

    [DllImport("user32.dll")] static extern bool SetForegroundWindow(IntPtr hWnd);

    protected override void OnStartup(StartupEventArgs e)
    {
        // the first time (and only the first time) a weak PC is detected, low-resource mode and the small overlay are switched on; the choice is saved below
        try { var set = Store.Load().Settings; autoWeak = !set.LowChecked && IsWeakPc(); Low = set.LowResource || autoWeak; } catch { }
        // every animation (the pulses, the floating token) is capped at 24 frames per second instead of 60: far less CPU, no visible difference.
        // Low-resource mode: 6, which is as good as standing still
        System.Windows.Media.Animation.Timeline.DesiredFrameRateProperty.OverrideMetadata(typeof(System.Windows.Media.Animation.Timeline), new FrameworkPropertyMetadata(Low ? 6 : 24));
        base.OnStartup(e);
        Loc.Install();   // texts follow the language of Windows

        // "LNXcounter.exe -up" in a terminal: update and exit, no window
        if (e.Args.Contains("-up") || e.Args.Contains("--up")) { Environment.ExitCode = Updater.RunCli(); Shutdown(); return; }

        // started by a restart (update / restore): wait for the previous copy to be gone
        foreach (var a in e.Args)
            if (a.StartsWith("--after=") && int.TryParse(a[8..], out var pid))
                try { System.Diagnostics.Process.GetProcessById(pid).WaitForExit(15000); } catch { }

        Efficiency.Enable();   // Windows "Efficiency mode" from the very first moment

        // one copy only: starting the exe again just brings the running one to the front
        mutex = new Mutex(true, "LNXcounter.SingleInstance.v1" + (Environment.GetEnvironmentVariable("LNX_DATA") is { Length: > 0 } ? ".test" : ""), out bool first);   // a test run (LNX_DATA) can sit next to the real app
        if (!first)
        {
            try { EventWaitHandle.OpenExisting("LNXcounter.ShowMain").Set(); } catch { }
            Shutdown();
            return;
        }
        Updater.Cleanup();
        showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, "LNXcounter.ShowMain");
        new Thread(() => { while (showEvent.WaitOne()) Dispatcher.BeginInvoke(ShowMain); }) { IsBackground = true }.Start();

        SessionEnding += (_, _) => { try { Log.Info("Windows is ending the session"); Model?.Save(); } catch { } };   // Windows is shutting down or logging off: save first
        // anything that goes wrong without being caught is written to the log first (see Settings > Data > Logs)
        DispatcherUnhandledException += (_, ev) => Log.Error("Unhandled error", ev.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, ev) => Log.Error("Fatal error" + (ev.IsTerminating ? " (the app closes)" : ""), ev.ExceptionObject as Exception);
        TaskScheduler.UnobservedTaskException += (_, ev) => Log.Warn("Background task error: " + ev.Exception.GetBaseException().Message);
        Log.Info($"LNXcounter {Version} started" + (e.Args.Contains("--startup") ? " (with Windows)" : "") + (Low ? ", low-resource mode" : ""));
        Model = new AppModel();
        Model.Start();
        if (!Model.Settings.LowChecked)
        {
            Model.Settings.LowChecked = true;
            if (autoWeak) { Model.Settings.LowResource = true; Model.Settings.MinimalOverlay = true; }
            Model.Save();
        }
        MainWindow = new MainWindow { DataContext = Model };
        Overlay = new OverlayWindow { DataContext = Model };

        bool startup = e.Args.Contains("--startup");
        if (!startup) ShowMain();
        if (Model.Settings.OverlayVisible) Overlay.Show();

        CreateTray();
        Hotkey.Pressed += () => Dispatcher.BeginInvoke(() => SetOverlayVisible(!Model.Settings.OverlayVisible));
        Hotkey.Apply(Model.Settings.HotkeyOn, Model.Settings.HotkeyMods, Model.Settings.HotkeyKey);
        KeepMemoryLow();

        if (e.Args.Contains("--selftest-menu")) { var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2) }; t.Tick += (_, _) => { t.Stop(); ShowTrayMenu(); }; t.Start(); }
    }

    /// <summary>Gives unused memory back to Windows shortly after start, whenever the window is hidden or shown, and every 30 seconds while hidden.</summary>
    void KeepMemoryLow()
    {
        MemoryTrim.Schedule(5000);
        MainWindow.IsVisibleChanged += (_, _) => { MemoryTrim.Schedule(MainWindow.IsVisible ? 6000 : 2000); Model.Wake(); };
        MainWindow.StateChanged += (_, _) => { MemoryTrim.Schedule(MainWindow.WindowState == WindowState.Minimized ? 2000 : 6000); Model.Wake(); };
        var t = new DispatcherTimer { Interval = TimeSpan.FromSeconds(Low ? 15 : 30) };
        t.Tick += (_, _) => { if (!MainWindow.IsVisible || MainWindow.WindowState == WindowState.Minimized) MemoryTrim.Now(); };
        t.Start();
    }

    void CreateTray()
    {
        tray = new TrayIcon(Environment.ProcessPath!, "LNXcounter");
        tray.Clicked += right => Dispatcher.BeginInvoke(() => { if (right) ShowTrayMenu(); else ShowMain(); });
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
        app.tray.ShowBalloon("LNXcounter is still running", "Left-click the tray icon to reopen it, right-click for options.");
    }

    void ShowTrayMenu()
    {
        Model.Wake();
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
        Model.Wake();
        Model.Save();
    }

    /// <summary>Closes this copy and starts a fresh one (after an update or a restore).</summary>
    public static void Restart(bool save = true)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Environment.ProcessPath!, $"--after={Environment.ProcessId}") { UseShellExecute = false }); } catch { }
        Quit(save);
    }

    public static void Quit(bool save = true)
    {
        Quitting = true;
        Log.Info("LNXcounter closed");
        var app = (App)Current;
        if (save) Model.Save();
        app.tray?.Dispose();
        Hotkey.Dispose();
        Overlay.Close();
        Current.MainWindow?.Close();
        Current.Shutdown();
    }
}
