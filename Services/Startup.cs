using Microsoft.Win32;

namespace LNXcounter.Services;

/// <summary>"Start with Windows" for the current user only (HKCU Run key, no admin rights needed).</summary>
public static class Startup
{
    const string Key = @"Software\Microsoft\Windows\CurrentVersion\Run";
    const string ValueName = "LNXcounter";

    public static bool IsEnabled
    {
        get
        {
            try { using var k = Registry.CurrentUser.OpenSubKey(Key); return k?.GetValue(ValueName) is string; }
            catch { return false; }
        }
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var k = Registry.CurrentUser.OpenSubKey(Key, true) ?? Registry.CurrentUser.CreateSubKey(Key);
            if (enabled) k.SetValue(ValueName, $"\"{Environment.ProcessPath}\" --startup");
            else k.DeleteValue(ValueName, false);
        }
        catch { }
    }
}
