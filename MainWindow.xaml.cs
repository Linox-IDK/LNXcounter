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
        var dlg = new OpenFolderDialog { Title = "Choose the Fortnite replays folder (Demos)", InitialDirectory = M.ReplayFolder };
        if (dlg.ShowDialog(this) == true) M.SetFolder(dlg.FolderName);
    }

    async void Rescan_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Re-read every replay in the folder and rebuild your totals from scratch?", "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
            await M.RescanAllAsync();
    }

    // ----- Settings: chests added by hand -----
    void Number_PreviewTextInput(object sender, TextCompositionEventArgs e) => e.Handled = !e.Text.All(char.IsDigit);

    bool TryNumber(System.Windows.Controls.TextBox box, out int n)
    {
        var txt = box.Text.Replace(" ", "").Replace(",", "").Replace(".", "");
        if (int.TryParse(txt, out n) && n > 0 && n <= AppModel.MaxManual) return true;
        M.SettingsMessage = "Type a whole number above 0.";
        return false;
    }

    void AddChests_Click(object sender, RoutedEventArgs e) { if (TryNumber(AddBox, out var n)) { M.AddManual(n); AddBox.Clear(); } }
    void RemoveChests_Click(object sender, RoutedEventArgs e) { if (TryNumber(AddBox, out var n)) { M.AddManual(-n); AddBox.Clear(); } }
    void SetTotal_Click(object sender, RoutedEventArgs e) { if (TryNumber(TotalBox, out var n)) { M.SetTotal(n); TotalBox.Clear(); } }
    void AddBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) AddChests_Click(sender, e); }
    void TotalBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key == Key.Enter) SetTotal_Click(sender, e); }

    void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(this, "Set all counters to zero, including chests you added by hand? Existing replays will not be counted again, only new matches.", "LNXcounter", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            M.ResetAll();
    }
}
