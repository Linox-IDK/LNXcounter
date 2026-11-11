using System.Diagnostics;
using System.Runtime.InteropServices;

namespace LNXcounter.Services;

/// <summary>
/// Puts the app in Windows' "Efficiency mode" (the leaf icon in Task Manager) as soon as it starts: the process runs at lower priority and on the
/// power-efficient cores, and its memory is the first to be reclaimed when the PC needs it. LNXcounter only reads a file every few seconds,
/// so this has no visible effect on it, and it never competes with the game for the CPU.
/// Needs Windows 10 version 1709 or newer; on older versions the calls fail and are ignored.
/// </summary>
public static class Efficiency
{
    [StructLayout(LayoutKind.Sequential)]
    struct PowerThrottlingState { public uint Version, ControlMask, StateMask; }

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessInformation(IntPtr process, int infoClass, ref PowerThrottlingState info, int size);

    [DllImport("kernel32.dll", SetLastError = true)]
    static extern bool SetProcessInformation(IntPtr process, int infoClass, ref uint info, int size);

    const int ProcessMemoryPriority = 0, ProcessPowerThrottling = 4;
    const uint ThrottlingVersion = 1, ExecutionSpeed = 1;   // PROCESS_POWER_THROTTLING_EXECUTION_SPEED = EcoQoS

    public static void Enable()
    {
        try
        {
            using var me = Process.GetCurrentProcess();
            var state = new PowerThrottlingState { Version = ThrottlingVersion, ControlMask = ExecutionSpeed, StateMask = ExecutionSpeed };
            SetProcessInformation(me.Handle, ProcessPowerThrottling, ref state, Marshal.SizeOf<PowerThrottlingState>());
            uint low = 2;                                   // MEMORY_PRIORITY_LOW
            SetProcessInformation(me.Handle, ProcessMemoryPriority, ref low, sizeof(uint));
            me.PriorityClass = ProcessPriorityClass.BelowNormal;
        }
        catch { }
    }
}
