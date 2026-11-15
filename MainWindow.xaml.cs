using LNXcounter.Services;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Microsoft.Win32;

namespace LNXcounter;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        // a bit taller than the default when the screen allows it, so more of the Drops page is visible
        Height = Math.Min(840, SystemParameters.WorkArea.Height - 20);
        // test hooks: --tab=drops  --week=1
        foreach (var a in Environment.GetCommandLineArgs())
        {
            if (a == "--tab=drops") DropsTab.IsChecked = true;
            else if (a == "--tab=settings") SettingsTab.IsChecked = true;
            else if (a.StartsWith("--addchests=") && int.TryParse(a[12..], out var ac)) App.Model.AddManual(ac);
            else if (a.StartsWith("--toast")) { int tm = a.Length > 8 && int.TryParse(a[8..], out var tv) ? tv : 0; System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(() => App.Model.TestHourAlert(tm)); }
            else if (a.StartsWith("--goal=")) { var gp = a[7..].Split(','); if (gp.Length == 2 && int.TryParse(gp[0], out var gk) && int.TryParse(gp[1], out var gt)) App.Model.SetGoal(gk, gt); }
            else if (a == "--graph") { var gt = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(6) }; gt.Tick += (_, _) => { gt.Stop(); try { new GraphWindow(App.Model) { Owner = this }.Show(); } catch (Exception ex) { Log.Error("graph test", ex); } }; gt.Start(); }
            else if (a == "--map") { var mt = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) }; mt.Tick += (_, _) => { mt.Stop(); if (App.Model.Matches.FirstOrDefault(x => x.Spots.Count > 0) is { } mm) new MapWindow(mm) { Owner = this }.Show(); }; mt.Start(); }
            else if (a == "--updatenow") { var ut = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(22) }; ut.Tick += async (_, _) => { ut.Stop(); await App.Model.InstallUpdateAsync(); }; ut.Start(); }
            else if (a == "--scrollend") Loaded += (_, _) => Dispatcher.BeginInvoke(() => SettingsScroll.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.ContextIdle);
            else if (a == "--fakehour") App.Model.Drops.ForceHour(new LNXcounter.Services.TreasureWindow(DateTime.UtcNow.AddHours(-1), DateTime.UtcNow.AddHours(1), true));
            else if (a == "--selftest-found") { foreach (var t in App.Model.Drops.Tiles.Where(t => t.Title.Contains("TOKEN") || t.Category == "OUTFIT")) t.SetFoundSilently(true); }
            else if (a.StartsWith("--open=")) { var t = App.Model.Drops.Tiles.FirstOrDefault(x => x.Title.Contains(a[7..].ToUpperInvariant()) || x.Category == a[7..].ToUpperInvariant()); if (t != null) App.Model.Drops.OpenTile(t); }
            else if (a == "--play") App.Model.Drops.ToggleJam();
            else if (a.StartsWith("--vol=") && int.TryParse(a[6..], out var vv)) App.Model.Drops.Audio.Volume = vv;
            else if (a == "--preview") App.Model.Drops.StartPreview();
            else if (a.StartsWith("--closeafter=") && int.TryParse(a[13..], out var ca))
            {
                var ct = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(ca) };
                ct.Tick += (_, _) => { ct.Stop(); App.Model.Drops.CloseModal(); };
                ct.Start();
            }
            else if (a.StartsWith("--week=") && int.TryParse(a[7..], out var wk)) App.Model.Drops.Select(wk);
        }
    }

    AppModel M => App.Model;

    // ----- tab change: the old page slides out, the new one slides in from the side it is on -----
    int curPanel;
    int slideGen;
    FrameworkElement[] Panels => new FrameworkElement[] { OverviewPanel, DropsPanel, SettingsPanel };

    void SwitchPanel(int to)
    {
        var panels = Panels;
        foreach (var p in panels) if (p.RenderTransform is not TranslateTransform) p.RenderTransform = new TranslateTransform();
        if (!IsLoaded || to == curPanel || App.Low)   // low-resource mode: tabs switch at once, no sliding
        {
            for (int i = 0; i < panels.Length; i++) panels[i].Visibility = i == to ? Visibility.Visible : Visibility.Collapsed;
            curPanel = to; return;
        }
        int gen = ++slideGen, dir = to > curPanel ? 1 : -1;
        for (int i = 0; i < panels.Length; i++)   // a quick second click: put everything else away at once
        {
            if (i == to || i == curPanel) continue;
            panels[i].BeginAnimation(OpacityProperty, null); panels[i].Visibility = Visibility.Collapsed;
        }
        var from = panels[curPanel]; var next = panels[to]; curPanel = to;
        var outT = (TranslateTransform)from.RenderTransform; var inT = (TranslateTransform)next.RenderTransform;
        var ease = new System.Windows.Media.Animation.CubicEase { EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut };
        System.Windows.Media.Animation.DoubleAnimation A(double f, double t, int ms) => new(f, t, TimeSpan.FromMilliseconds(ms)) { EasingFunction = ease };

        outT.BeginAnimation(TranslateTransform.XProperty, A(outT.X, -dir * 70, 180));
        var fade = A(from.Opacity, 0, 160);
        fade.Completed += (_, _) => { if (gen == slideGen) { from.Visibility = Visibility.Collapsed; from.BeginAnimation(OpacityProperty, null); from.Opacity = 1; outT.BeginAnimation(TranslateTransform.XProperty, null); outT.X = 0; } };
        from.BeginAnimation(OpacityProperty, fade);

        next.Opacity = 0; next.Visibility = Visibility.Visible;
        inT.BeginAnimation(TranslateTransform.XProperty, A(dir * 90, 0, 340));
        var show = A(0, 1, 300);
        show.Completed += (_, _) => { if (gen == slideGen) { next.BeginAnimation(OpacityProperty, null); next.Opacity = 1; inT.BeginAnimation(TranslateTransform.XProperty, null); inT.X = 0; } };
        next.BeginAnimation(OpacityProperty, show);
    }

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (OverviewPanel == null || DropsPanel == null || SettingsPanel == null) return; // fires once during InitializeComponent
        SwitchPanel(OverviewTab.IsChecked == true ? 0 : DropsTab.IsChecked == true ? 1 : 2);
        if (SettingsTab.IsChecked == true) { StartupBox.IsChecked = LNXcounter.Services.Startup.IsEnabled; AlertBox.IsChecked = M.Settings.HourAlert; WarnBox.IsChecked = M.Settings.HourWarn; AutoUpdateBox.IsChecked = M.Settings.AutoUpdateCheck; LowBox.IsChecked = M.Settings.LowResource; CleanBox.IsChecked = M.Settings.DeleteReadReplays; ToastBox.IsChecked = M.Settings.MatchToast; SyncHotkey(); SyncSize(); ShowOverlayChoice(); GoalOff.IsChecked = M.GoalKind == 0; GoalDay.IsChecked = M.GoalKind == 1; GoalWeek.IsChecked = M.GoalKind == 2; GoalBox.Text = M.GoalTarget.ToString(); }   // it can also be switched from the tray menu
    }

    /// <summary>Settings: shows the section picked in the list on the left.</summary>
    void SetPage_Checked(object sender, RoutedEventArgs e)
    {
        if (SetData == null || SettingsScroll == null) return;   // fires while the window is being built
        var pages = new (System.Windows.Controls.RadioButton nav, UIElement page)[] { (NavGeneral, SetGeneral), (NavOverlay, SetOverlay), (NavNotify, SetNotify), (NavCount, SetCount), (NavData, SetData) };
        foreach (var (nav, page) in pages) page.Visibility = nav.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsScroll.ScrollToTop();
    }

    void Pause_Click(object sender, RoutedEventArgs e) => M.TogglePause();

    void OvStyle_Click(object sender, MouseButtonEventArgs e)
    {
        M.Settings.MinimalOverlay = (sender as FrameworkElement)?.Tag as string == "mini";
        M.Save();
        App.Overlay.ApplyStyle();
        ShowOverlayChoice();
        SyncSize();
    }

    // ----- size of the full overlay -----
    bool syncing;
    System.Windows.Threading.DispatcherTimer? sizeSave;

    void SyncSize()
    {
        syncing = true;
        SizeSlider.Value = Math.Clamp(M.Settings.OverlayScale, 0.6, 1.6);
        SizeText.Text = $"{Math.Round(SizeSlider.Value * 100)}%";
        syncing = false;
        Fps24.IsChecked = M.Settings.AnimFps is not (60 or 120); Fps60.IsChecked = M.Settings.AnimFps == 60; Fps120.IsChecked = M.Settings.AnimFps == 120;
        TfAuto.IsChecked = M.Settings.TimeFormat == 0; Tf12.IsChecked = M.Settings.TimeFormat == 1; Tf24.IsChecked = M.Settings.TimeFormat == 2;
        OvNumAll.IsChecked = M.Settings.OverlayNumberKind == 0; OvNumToday.IsChecked = M.Settings.OverlayNumberKind == 1; OvNumSession.IsChecked = M.Settings.OverlayNumberKind == 2;
        SizeRow.IsEnabled = !M.Settings.MinimalOverlay; SizeRow.Opacity = M.Settings.MinimalOverlay ? 0.4 : 1;
    }

    void Tf_Click(object sender, RoutedEventArgs e)
    {
        if (!IsLoaded) return;
        int k = Tf24.IsChecked == true ? 2 : Tf12.IsChecked == true ? 1 : 0;
        if (k != M.Settings.TimeFormat) M.SetTimeFormat(k);
    }
    void OvNum_Click(object sender, RoutedEventArgs e) => M.SetOverlayNumber(OvNumSession.IsChecked == true ? 2 : OvNumToday.IsChecked == true ? 1 : 0);

    void Size_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (syncing || SizeText == null) return;
        M.Settings.OverlayScale = Math.Round(e.NewValue, 2);
        SizeText.Text = $"{Math.Round(e.NewValue * 100)}%";
        App.Overlay.ApplyStyle();
        sizeSave ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(700) };
        sizeSave.Tick -= SizeSave_Tick; sizeSave.Tick += SizeSave_Tick;
        sizeSave.Stop(); sizeSave.Start();                      // saved when the slider has stopped moving, not on every step
    }

    void SizeSave_Tick(object? sender, EventArgs e) { sizeSave?.Stop(); M.Save(); }

    // ----- hotkey -----
    bool capturing;

    void SyncHotkey()
    {
        HotkeyBox.IsChecked = M.Settings.HotkeyOn;
        HotkeyBtn.Content = Hotkey.Describe(M.Settings.HotkeyMods, M.Settings.HotkeyKey);
        capturing = false;
    }

    void Hotkey_Toggle(object sender, RoutedEventArgs e)
    {
        M.Settings.HotkeyOn = HotkeyBox.IsChecked == true;
        bool ok = Hotkey.Apply(M.Settings.HotkeyOn, M.Settings.HotkeyMods, M.Settings.HotkeyKey);
        HotkeyMsg.Text = ok ? "" : Loc.T("Another program already uses these keys.");
        M.Save();
    }

    void HotkeyBtn_Click(object sender, RoutedEventArgs e) { capturing = true; HotkeyMsg.Text = ""; HotkeyBtn.Content = Loc.T("Press the keys…"); }

    void HotkeyBtn_Lost(object sender, RoutedEventArgs e) { if (capturing) SyncHotkey(); }

    void HotkeyBtn_Key(object sender, KeyEventArgs e)
    {
        if (!capturing) return;
        e.Handled = true;
        var key = e.Key == Key.System ? e.SystemKey : e.Key;
        if (key == Key.Escape) { SyncHotkey(); return; }
        if (key is Key.LeftCtrl or Key.RightCtrl or Key.LeftAlt or Key.RightAlt or Key.LeftShift or Key.RightShift or Key.LWin or Key.RWin or Key.None) return;   // wait for the real key
        uint mods = (uint)Keyboard.Modifiers;                   // Alt 1, Ctrl 2, Shift 4, Win 8: the same bits as the Windows API
        if (mods == 0) { HotkeyMsg.Text = Loc.T("Add Ctrl, Alt, Shift or Win to the key."); return; }
        uint vk = (uint)KeyInterop.VirtualKeyFromKey(key);
        uint oldMods = M.Settings.HotkeyMods, oldKey = M.Settings.HotkeyKey;
        if (Hotkey.Apply(true, mods, vk)) { M.Settings.HotkeyMods = mods; M.Settings.HotkeyKey = vk; M.Settings.HotkeyOn = true; HotkeyMsg.Text = ""; M.Save(); }
        else { Hotkey.Apply(M.Settings.HotkeyOn, oldMods, oldKey); HotkeyMsg.Text = Loc.T("Another program already uses these keys. The old ones are kept."); }
        SyncHotkey();
    }

    // ----- match pop-up -----
    void MatchToast_Click(object sender, RoutedEventArgs e) { M.Settings.MatchToast = ToastBox.IsChecked == true; M.Save(); }
    void MatchToastTest_Click(object sender, RoutedEventArgs e) => MatchToast.Show(12, 1, true, M.SessionOpened + 12, M.TotalOpened);

    void Nudge_Click(object sender, RoutedEventArgs e)
    {
        var tag = (sender as FrameworkElement)?.Tag as string ?? "";
        if (tag == "reset") { M.Settings.OverlayNudgeX = 0; M.Settings.OverlayNudgeY = 0; }
        else
        {
            var p = tag.Split(',');
            if (p.Length == 2 && double.TryParse(p[0], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dx)
                && double.TryParse(p[1], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var dy))
            { M.Settings.OverlayNudgeX += dx; M.Settings.OverlayNudgeY += dy; }
        }
        M.Save();
        App.Overlay.ApplyStyle();
    }

    /// <summary>A gold frame around the overlay style in use.</summary>
    void ShowOverlayChoice()
    {
        bool mini = M.Settings.MinimalOverlay;
        OvFullTile.BorderBrush = mini ? System.Windows.Media.Brushes.Transparent : (System.Windows.Media.Brush)FindResource("GoldBrush");
        OvMiniTile.BorderBrush = mini ? (System.Windows.Media.Brush)FindResource("GoldBrush") : System.Windows.Media.Brushes.Transparent;
    }

    void Clean_Click(object sender, RoutedEventArgs e)
    {
        if (CleanBox.IsChecked == true && MessageBox.Show(this, Loc.T("Delete your Fortnite replays for good once LNXcounter has counted them? Only replays that are more than 1 hour old are deleted, they do not go to the Recycle Bin, and they cannot be read again. Your totals are kept."),
                "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
        {
            CleanBox.IsChecked = false; return;
        }
        M.Settings.DeleteReadReplays = CleanBox.IsChecked == true;
        M.Save();
        M.CleanSoon();
    }

    void Fps_Click(object sender, RoutedEventArgs e)
    {
        int f = Fps120.IsChecked == true ? 120 : Fps60.IsChecked == true ? 60 : 24;
        if (f == M.Settings.AnimFps) return;
        M.Settings.AnimFps = f; M.Save();
        App.Restart();   // the frame rate is decided when the app starts
    }

    void Low_Click(object sender, RoutedEventArgs e)
    {
        M.Settings.LowResource = LowBox.IsChecked == true;
        M.Save();
        App.Restart();   // the frame rate and the song player are decided when the app starts
    }

    // ----- Settings: start with Windows, CSV export -----
    void Startup_Click(object sender, RoutedEventArgs e)
    {
        LNXcounter.Services.Startup.Set(StartupBox.IsChecked == true);
        StartupBox.IsChecked = LNXcounter.Services.Startup.IsEnabled;   // shows what Windows really has
    }

    async void UpdateNow_Click(object sender, RoutedEventArgs e) => await M.InstallUpdateAsync();
    async void CheckUpdate_Click(object sender, RoutedEventArgs e) => await M.CheckUpdateAsync(true);
    void AutoUpdate_Click(object sender, RoutedEventArgs e)
    {
        M.Settings.AutoUpdateCheck = AutoUpdateBox.IsChecked == true; M.Save();
        if (M.Settings.AutoUpdateCheck) _ = M.CheckUpdateAsync(false);
    }

    void Backup_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Title = Loc.T("Save a backup of your stats"), Filter = "LNXcounter backup (*.json)|*.json", DefaultExt = ".json", AddExtension = true, FileName = $"LNXcounter-backup-{DateTime.Now:yyyy-MM-dd}.json" };
        if (dlg.ShowDialog(this) != true) return;
        try { M.BackupTo(dlg.FileName); M.BackupMessage = Loc.F("Backup saved: {0}.", System.IO.Path.GetFileName(dlg.FileName)); }
        catch (Exception ex) { M.BackupMessage = Loc.T("Could not save the backup: ") + ex.Message; }
    }

    void Restore_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Title = Loc.T("Choose a LNXcounter backup"), Filter = "LNXcounter backup (*.json)|*.json|All files (*.*)|*.*" };
        if (dlg.ShowDialog(this) != true) return;
        if (MessageBox.Show(this, Loc.T("Replace your current stats with this backup? The current stats are kept as state.json.bak. The app restarts afterwards."), "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        var err = M.RestoreFrom(dlg.FileName);
        if (err != null) { M.BackupMessage = err; return; }
        App.Restart(save: false);
    }

    // ----- logs -----
    void LogCopy_Click(object sender, RoutedEventArgs e)
    {
        try { System.Windows.Clipboard.SetText(LNXcounter.Services.Log.Report()); LogMsg.Text = Loc.T("Log copied. Paste it in your bug report."); }
        catch (Exception ex) { LogMsg.Text = Loc.T("Could not copy the log: ") + ex.Message; }
    }

    void LogSave_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SaveFileDialog { Title = Loc.T("Save the log"), Filter = "Text file (*.txt)|*.txt", DefaultExt = ".txt", AddExtension = true, FileName = $"LNXcounter-log-{DateTime.Now:yyyy-MM-dd}.txt" };
        if (dlg.ShowDialog(this) != true) return;
        try { System.IO.File.WriteAllText(dlg.FileName, LNXcounter.Services.Log.Report(), new System.Text.UTF8Encoding(true)); LogMsg.Text = Loc.F("Saved {0}.", System.IO.Path.GetFileName(dlg.FileName)); }
        catch (Exception ex) { LogMsg.Text = Loc.T("Could not save the file: ") + ex.Message; }
    }

    void LogOpen_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            System.IO.Directory.CreateDirectory(LNXcounter.Services.Log.Dir);
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LNXcounter.Services.Log.Dir) { UseShellExecute = true });
        }
        catch (Exception ex) { LogMsg.Text = ex.Message; }
    }

    // ----- uninstall -----
    void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Loc.T("Uninstall LNXcounter completely? Your stats, settings, logs and the program itself are deleted for good, with nothing left behind. Your Fortnite replays and the files you saved yourself are not touched. This cannot be undone."),
                "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) != MessageBoxResult.Yes) return;
        var err = LNXcounter.Services.Uninstaller.Start();
        if (err != null) { LogMsg.Text = Loc.T("Uninstall failed: ") + err; return; }
        App.Quit(save: false);   // the hidden script deletes everything as soon as the app is closed
    }

    void Kofi_Click(object sender, RoutedEventArgs e)
    {
        try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("https://ko-fi.com/lnxidk") { UseShellExecute = true }); } catch { }
    }

    void Alert_Click(object sender, RoutedEventArgs e) { M.Settings.HourAlert = AlertBox.IsChecked == true; M.Save(); }
    void Warn_Click(object sender, RoutedEventArgs e) { M.Settings.HourWarn = WarnBox.IsChecked == true; M.Save(); }
    void AlertTest_Click(object sender, RoutedEventArgs e) => M.TestHourAlert();
    void WarnTest_Click(object sender, RoutedEventArgs e) => M.TestHourAlert(10);

    void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button b || !Enum.TryParse<LNXcounter.Services.CsvExport.Period>(b.Tag as string, out var period)) return;
        bool pdf = FormatPdf.IsChecked == true;
        var dlg = new SaveFileDialog
        {
            Title = Loc.T(pdf ? "Save your chests as PDF" : "Save your chests as CSV"),
            Filter = pdf ? "PDF file (*.pdf)|*.pdf" : "CSV file (*.csv)|*.csv", DefaultExt = pdf ? ".pdf" : ".csv", AddExtension = true,
            FileName = $"LNXcounter-{period.ToString().ToLowerInvariant()}-{DateTime.Now:yyyy-MM-dd}" + (pdf ? ".pdf" : ".csv"),
        };
        if (dlg.ShowDialog(this) != true) return;
        try
        {
            if (pdf) LNXcounter.Services.PdfExport.Save(dlg.FileName, M.BuildPdf(period)); else LNXcounter.Services.CsvExport.Save(dlg.FileName, M.BuildCsv(period));
            M.ExportMessage = Loc.F("Saved {0}.", System.IO.Path.GetFileName(dlg.FileName));
        }
        catch (Exception ex) { M.ExportMessage = Loc.T("Could not save the file: ") + ex.Message; }
    }

    // click a reward tile -> details popup (the checkbox inside the tile handles its own click)
    void Tile_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement fe && fe.DataContext is TileVM t) M.Drops.OpenTile(t);
    }

    void Backdrop_Down(object sender, MouseButtonEventArgs e) => M.Drops.CloseModal();
    void ModalClose_Click(object sender, RoutedEventArgs e) => M.Drops.CloseModal();
    void Preview_Click(object sender, RoutedEventArgs e) => M.Drops.StartPreview();
    void PreviewClose_Click(object sender, RoutedEventArgs e) => M.Drops.StopPreview();

    void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Escape) return;
        if (M.Drops.Previewing) { M.Drops.StopPreview(); e.Handled = true; }
        else if (M.Drops.IsModalOpen) { M.Drops.CloseModal(); e.Handled = true; }
    }

    void Jam_Click(object sender, RoutedEventArgs e) => M.Drops.ToggleJam();

    void Progress_Down(object sender, MouseButtonEventArgs e) => M.Drops.Audio.Scrubbing = true;

    void Progress_Up(object sender, MouseButtonEventArgs e)
    {
        if (sender is System.Windows.Controls.Slider s) M.Drops.Audio.Seek(s.Value);
        M.Drops.Audio.Scrubbing = false;
    }

    void Youtube_Click(object sender, RoutedEventArgs e)
    {
        if (M.Drops.Open?.Video is string id)
            try { System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(LNXcounter.Services.JamPlayer.WatchUrl(id)) { UseShellExecute = true }); } catch { }
    }

    void Week_Click(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button b && b.Tag is int n) M.Drops.Select(n);
    }

    // closing the big window only hides it: the app keeps counting from the tray (use the tray menu > Quit to exit)
    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (!App.Quitting)
        {
            e.Cancel = true;
            M.Save();
            Hide();
            App.NotifyHiddenToTray();
            return;
        }
        base.OnClosing(e);
    }

    void TitleBar_Down(object sender, MouseButtonEventArgs e) { if (e.ButtonState == MouseButtonState.Pressed) DragMove(); }
    void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    void Close_Click(object sender, RoutedEventArgs e) => Close();

    void Overlay_Click(object sender, RoutedEventArgs e) => App.SetOverlayVisible(!App.Overlay.IsVisible);

    void Folder_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = Loc.T("Choose the Fortnite replays folder (Demos)"), InitialDirectory = M.ReplayFolder };
        if (dlg.ShowDialog(this) == true) M.SetFolder(dlg.FolderName);
    }

    async void Refresh_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Loc.T("Re-read the replays still on your PC with the latest detection? Matches whose replay is gone are kept as they are."), "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await M.RefreshExistingAsync();
    }

    async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Loc.T("Re-read every replay in the folder and rebuild your totals from scratch?"), "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await M.RescanAllAsync();
    }

    // ----- Settings: chests added by hand -----
    void Number_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    bool TryNumber(System.Windows.Controls.TextBox box, out int n)
    {
        var txt = box.Text.Replace(" ", "").Replace(",", "").Replace(".", "");
        if (int.TryParse(txt, out n) && n > 0 && n <= AppModel.MaxManual) return true;
        M.SettingsMessage = Loc.T("Type a whole number above 0.");
        return false;
    }

    void AddChests_Click(object sender, RoutedEventArgs e) { if (TryNumber(AddBox, out var n)) { M.AddManual(n); AddBox.Clear(); } }
    void RemoveChests_Click(object sender, RoutedEventArgs e) { if (TryNumber(AddBox, out var n)) { M.AddManual(-n); AddBox.Clear(); } }
    void SetTotal_Click(object sender, RoutedEventArgs e) { if (TryNumber(TotalBox, out var n)) { M.SetTotal(n); TotalBox.Clear(); } }
    void AddBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddChests_Click(sender, e); }
    void TotalBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SetTotal_Click(sender, e); }

    void Graph_Click(object sender, RoutedEventArgs e) => new GraphWindow(M) { Owner = this }.Show();

    void Eye_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { DataContext: MatchRecord m } && m.Spots.Count > 0) new MapWindow(m) { Owner = this }.Show();
    }

    void Goal_Click(object sender, RoutedEventArgs e)
    {
        int kind = GoalWeek.IsChecked == true ? 2 : GoalDay.IsChecked == true ? 1 : 0;
        int.TryParse(GoalBox.Text.Replace(" ", ""), out var target);
        M.SetGoal(kind, target);
        GoalBox.Text = M.GoalTarget.ToString();
    }
    void GoalBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) Goal_Click(sender, e); }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, Loc.T("Set all counters to zero, including chests you added by hand? Existing replays will not be counted again, only new matches."), "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            M.ResetAll();
    }
}
