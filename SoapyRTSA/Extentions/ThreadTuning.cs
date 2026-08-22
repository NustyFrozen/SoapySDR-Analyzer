using System.Runtime.InteropServices;
using NLog;
using Logger = NLog.Logger;

namespace SoapyRTSA.Extentions;

/// <summary>
///     Nails the sampler and fft threads to a core of their own and asks the kernel to leave them alone.
///     A real time display is judged on jitter, and the default scheduler will happily migrate a busy
///     thread across the machine mid sweep. Every call must be made from inside the thread it applies to.
/// </summary>
public static class ThreadTuning
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    private const int SchedFifo = 1;
    private const int PrioProcess = 0;

    /// <summary>glibc's cpu_set_t is a fixed 1024 bit mask.</summary>
    private const int CpuSetLongs = 16;

    private const int WindowsThreadPriorityTimeCritical = 15;

    [StructLayout(LayoutKind.Sequential)]
    private struct SchedParam
    {
        public int Priority;
    }

    //pid 0 means the calling thread for both of these
    [DllImport("libc", EntryPoint = "sched_setaffinity", SetLastError = true)]
    private static extern int SchedSetAffinity(int pid, nuint setSize, ulong[] mask);

    [DllImport("libc", EntryPoint = "sched_setscheduler", SetLastError = true)]
    private static extern int SchedSetScheduler(int pid, int policy, ref SchedParam param);

    [DllImport("libc", EntryPoint = "setpriority", SetLastError = true)]
    private static extern int SetPriority(int which, uint who, int priority);

    [DllImport("kernel32.dll")]
    private static extern IntPtr GetCurrentThread();

    [DllImport("kernel32.dll")]
    private static extern UIntPtr SetThreadAffinityMask(IntPtr thread, UIntPtr mask);

    [DllImport("kernel32.dll")]
    private static extern bool SetThreadPriority(IntPtr thread, int priority);

    /// <summary>
    ///     Cores are handed out from the top down, because core 0 is where the kernel puts interrupt work.
    /// </summary>
    public static int CoreFromTop(int offset)
    {
        var cores = Environment.ProcessorCount;
        if (cores <= 2)
            return cores - 1;

        var core = cores - 1 - offset;
        return core < 1 ? 1 : core;
    }

    /// <summary>Confines the calling thread to a single core. Returns false when the platform said no.</summary>
    public static bool Pin(string name, int core)
    {
        if (core < 0 || core >= Environment.ProcessorCount)
            return false;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var applied = SetThreadAffinityMask(GetCurrentThread(), (UIntPtr)(1UL << core));
                if (applied == UIntPtr.Zero)
                    return Report(name, $"affinity to core {core} refused");

                Logger.Info($"{name} pinned to core {core}");
                return true;
            }

            var mask = new ulong[CpuSetLongs];
            mask[core >> 6] = 1UL << (core & 63);

            if (SchedSetAffinity(0, (nuint)(CpuSetLongs * sizeof(ulong)), mask) != 0)
                return Report(name, $"sched_setaffinity to core {core} failed (errno {Marshal.GetLastWin32Error()})");

            Logger.Info($"{name} pinned to core {core}");
            return true;
        }
        catch (Exception exception)
        {
            return Report(name, $"pinning is unavailable -> {exception.Message}");
        }
    }

    /// <summary>
    ///     Asks for real time scheduling, then for a better nice value, then settles for what the runtime
    ///     can give. SCHED_FIFO needs CAP_SYS_NICE, so on a plain desktop the fallback is the normal case.
    /// </summary>
    public static void Prioritise(string name)
    {
        try
        {
            Thread.CurrentThread.Priority = ThreadPriority.Highest;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (!SetThreadPriority(GetCurrentThread(), WindowsThreadPriorityTimeCritical))
                    Report(name, "time critical priority refused");
                return;
            }

            var param = new SchedParam { Priority = 10 };
            if (SchedSetScheduler(0, SchedFifo, ref param) == 0)
            {
                Logger.Info($"{name} running SCHED_FIFO at priority {param.Priority}");
                return;
            }

            if (SetPriority(PrioProcess, 0, -10) == 0)
            {
                Logger.Info($"{name} running at nice -10");
                return;
            }

            Report(name, "no permission for realtime scheduling, staying on the default policy "
                + "(grant it with: setcap cap_sys_nice+ep on the executable)");
        }
        catch (Exception exception)
        {
            Report(name, $"priority is unavailable -> {exception.Message}");
        }
    }

    private static bool Report(string name, string message)
    {
        Logger.Warn($"{name}: {message}");
        return false;
    }
}
