using LNXcounter.Services;
using System.Windows;
using System.Windows.Input;
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
            else if (a == "--map") { var mt = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(10) }; mt.Tick += (_, _) => { mt.Stop(); if (App.Model.Matches.FirstOrDefault(x => x.Spots.Count > 0) is { } mm) new MapWindow(mm) { Owner = this }.Show(); }; mt.Start(); }
            else if (a == "--updatenow") { var ut = new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromSeconds(22) }; ut.Tick += async (_, _) => { ut.Stop(); await App.Model.InstallUpdateAsync(); }; ut.Start(); }
            else if (a == "--scrollend") Loaded += (_, _) => Dispatcher.BeginInvoke(() => SettingsPanel.ScrollToEnd(), System.Windows.Threading.DispatcherPriority.ContextIdle);
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

    void Tab_Checked(object sender, RoutedEventArgs e)
    {
        if (OverviewPanel == null || DropsPanel == null || SettingsPanel == null) return; // fires once during InitializeComponent
        OverviewPanel.Visibility = OverviewTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        DropsPanel.Visibility = DropsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        SettingsPanel.Visibility = SettingsTab.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
        if (SettingsTab.IsChecked == true) { StartupBox.IsChecked = LNXcounter.Services.Startup.IsEnabled; AlertBox.IsChecked = M.Settings.HourAlert; WarnBox.IsChecked = M.Settings.HourWarn; AutoUpdateBox.IsChecked = M.Settings.AutoUpdateCheck; GoalOff.IsChecked = M.GoalKind == 0; GoalDay.IsChecked = M.GoalKind == 1; GoalWeek.IsChecked = M.GoalKind == 2; GoalBox.Text = M.GoalTarget.ToString(); }   // it can also be switched from the tray menu
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
