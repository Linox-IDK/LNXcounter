using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;

namespace LNXcounter.Services;

/// <summary>One global hotkey (works while the game is in front) that shows / hides the overlay. Win32 RegisterHotKey: no keyboard hook, no CPU.</summary>
public static class Hotkey
{
    const int WM_HOTKEY = 0x0312, Id = 0x4C4E;
    const uint MOD_NOREPEAT = 0x4000;
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr h, int id, uint mods, uint vk);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h, int id);

    static HwndSource? source;
    static bool registered;
    public static event Action? Pressed;

    /// <summary>Registers (or, with on = false, removes) the hotkey. False when another program already uses that combination.</summary>
    public static bool Apply(bool on, uint mods, uint key)
    {
        if (source == null)
        {
            source = new HwndSource(new HwndSourceParameters("LNXcounterHotkey") { Width = 0, Height = 0, WindowStyle = unchecked((int)0x80000000) });
            source.AddHook((IntPtr h, int msg, IntPtr w, IntPtr l, ref bool handled) =>
            {
                if (msg == WM_HOTKEY && w.ToInt32() == Id) { handled = true; Pressed?.Invoke(); }
                return IntPtr.Zero;
            });
        }
        if (registered) { UnregisterHotKey(source.Handle, Id); registered = false; }
        if (!on || mods == 0 || key == 0) return true;
        registered = RegisterHotKey(source.Handle, Id, mods | MOD_NOREPEAT, key);
        return registered;
    }

    public static void Dispose()
    {
        if (source == null) return;
        if (registered) UnregisterHotKey(source.Handle, Id);
        registered = false; source.Dispose(); source = null;
    }

    /// <summary>"Ctrl + Alt + O"</summary>
    public static string Describe(uint mods, uint key)
    {
        var parts = new List<string>();
        if ((mods & 2) != 0) parts.Add("Ctrl");
        if ((mods & 1) != 0) parts.Add("Alt");
        if ((mods & 4) != 0) parts.Add("Shift");
        if ((mods & 8) != 0) parts.Add("Win");
        var k = KeyInterop.KeyFromVirtualKey((int)key);
        string name = k switch
        {
            >= Key.D0 and <= Key.D9 => ((int)k - (int)Key.D0).ToString(),
            >= Key.NumPad0 and <= Key.NumPad9 => "Num" + ((int)k - (int)Key.NumPad0),
            _ => k.ToString(),
        };
        parts.Add(name);
        return string.Join(" + ", parts);
    }
}
