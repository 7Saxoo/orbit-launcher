using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Orbit.App.Infrastructure;

/// <summary>
/// Trims Orbit's footprint while it sits hidden in the tray: drops the process
/// priority, collects managed memory and releases the working set back to the
/// OS. Everything is best-effort and reversible.
/// </summary>
public static class PowerManager
{
    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetProcessWorkingSetSize(IntPtr handle, IntPtr min, IntPtr max);

    public static void EnterLowPower()
    {
        TrySetPriority(ProcessPriorityClass.Idle);

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        try
        {
            // (-1, -1) tells Windows to trim the working set to the minimum.
            SetProcessWorkingSetSize(GetCurrentProcess(), -1, -1);
        }
        catch (Exception ex) when (ex is EntryPointNotFoundException or DllNotFoundException)
        {
        }
    }

    public static void ExitLowPower() => TrySetPriority(ProcessPriorityClass.Normal);

    private static void TrySetPriority(ProcessPriorityClass priority)
    {
        try
        {
            using var process = Process.GetCurrentProcess();
            process.PriorityClass = priority;
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
        }
    }
}
