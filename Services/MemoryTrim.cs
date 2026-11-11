using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace LNXcounter.Services;

/// <summary>
/// Keeps the app's RAM small: after the app starts, when its window goes to the tray, after a match has been read, and now and then while it is
/// hidden, it runs a full garbage collection and hands every unused page back to Windows. The pages that are still needed are simply loaded
/// again the next time they are touched, so this is invisible to the user.
/// </summary>
public static class MemoryTrim
{
    [DllImport("psapi.dll")] static extern bool EmptyWorkingSet(IntPtr process);

    static DispatcherTimer? timer;

    /// <summary>Trims in <paramref name="delayMs"/> milliseconds. Calling it again before then just pushes it back, so bursts cause one trim.</summary>
    public static void Schedule(int delayMs = 3000)
    {
        var d = System.Windows.Application.Current?.Dispatcher;
        if (d == null) return;
        d.BeginInvoke(() =>
        {
            timer ??= new DispatcherTimer(DispatcherPriority.Background);
            timer.Stop();
            timer.Interval = TimeSpan.FromMilliseconds(delayMs);
            timer.Tick -= OnTick; timer.Tick += OnTick;
            timer.Start();
        });
    }

    static void OnTick(object? s, EventArgs e)
    {
        timer?.Stop();
        Now();
    }

    public static void Now()
    {
        try
        {
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect(2, GCCollectionMode.Aggressive, blocking: true, compacting: true);
            GC.WaitForPendingFinalizers();
            GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
            using var me = Process.GetCurrentProcess();
            EmptyWorkingSet(me.Handle);
        }
        catch { }
    }
}
