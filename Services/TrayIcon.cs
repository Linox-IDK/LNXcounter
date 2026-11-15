using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace LNXcounter.Services;

/// <summary>The notification-area (tray) icon, done with Win32 directly so the app does not have to ship Windows Forms (about 25 MB).</summary>
public sealed class TrayIcon : IDisposable
{
    const int CallbackMsg = 0x8001;                       // WM_APP + 1
    const uint NIM_ADD = 0, NIM_MODIFY = 1, NIM_DELETE = 2;
    const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4, NIF_INFO = 0x10;
    const int WM_LBUTTONUP = 0x0202, WM_RBUTTONUP = 0x0205;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    struct NOTIFYICONDATA
    {
        public int cbSize; public IntPtr hWnd; public uint uID, uFlags, uCallbackMessage; public IntPtr hIcon;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)] public string szTip;
        public uint dwState, dwStateMask;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)] public string szInfo;
        public uint uTimeout;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 64)] public string szInfoTitle;
        public uint dwInfoFlags; public Guid guidItem; public IntPtr hBalloonIcon;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)] static extern bool Shell_NotifyIconW(uint msg, ref NOTIFYICONDATA data);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint PrivateExtractIconsW(string file, int index, int cx, int cy, IntPtr[] icons, IntPtr ids, uint count, uint flags);
    [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr icon);
    [DllImport("user32.dll")] static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern uint RegisterWindowMessageW(string name);

    readonly HwndSource source;
    readonly uint taskbarCreated = RegisterWindowMessageW("TaskbarCreated");
    IntPtr icon;
    string text = "";
    bool added;

    /// <summary>Raised on the UI thread; true when the right mouse button was used.</summary>
    public event Action<bool>? Clicked;

    public TrayIcon(string exePath, string tip)
    {
        // a hidden top-level window (not a message-only one) so that it also gets the broadcast sent when Explorer restarts
        source = new HwndSource(new HwndSourceParameters("LNXcounterTray") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) });
        source.AddHook(WndProc);
        var icons = new IntPtr[1];
        try { if (PrivateExtractIconsW(exePath, 0, GetSystemMetrics(49), GetSystemMetrics(50), icons, IntPtr.Zero, 1, 0) > 0) icon = icons[0]; } catch { }
        text = tip;
        Add();
    }

    NOTIFYICONDATA Data(uint flags) => new()
    {
        cbSize = Marshal.SizeOf<NOTIFYICONDATA>(), hWnd = source.Handle, uID = 1, uFlags = flags,
        uCallbackMessage = CallbackMsg, hIcon = icon, szTip = Trim(text, 127), szInfo = "", szInfoTitle = "",
    };

    static string Trim(string s, int max) => s.Length <= max ? s : s[..max];

    void Add()
    {
        var d = Data(NIF_MESSAGE | NIF_ICON | NIF_TIP);
        added = Shell_NotifyIconW(NIM_ADD, ref d);
    }

    /// <summary>The tooltip shown when hovering the icon.</summary>
    public string Text
    {
        get => text;
        set { if (value == text) return; text = value; var d = Data(NIF_TIP); Shell_NotifyIconW(NIM_MODIFY, ref d); }
    }

    public void ShowBalloon(string title, string message)
    {
        var d = Data(NIF_INFO);
        d.szInfoTitle = Trim(title, 63); d.szInfo = Trim(message, 255); d.dwInfoFlags = 1;   // NIIF_INFO
        Shell_NotifyIconW(NIM_MODIFY, ref d);
    }

    IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == CallbackMsg)
        {
            int m = (int)(lParam.ToInt64() & 0xFFFF);
            if (m == WM_LBUTTONUP) Clicked?.Invoke(false);
            else if (m == WM_RBUTTONUP) Clicked?.Invoke(true);
        }
        else if (msg == taskbarCreated && taskbarCreated != 0) Add();   // Explorer was restarted: put the icon back
        return IntPtr.Zero;
    }

    public void Dispose()
    {
        if (added) { var d = Data(0); Shell_NotifyIconW(NIM_DELETE, ref d); added = false; }
        if (icon != IntPtr.Zero) { DestroyIcon(icon); icon = IntPtr.Zero; }
        source.Dispose();
    }
}
