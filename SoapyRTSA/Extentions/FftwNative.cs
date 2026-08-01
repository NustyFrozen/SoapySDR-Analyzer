using System.Runtime.InteropServices;
using NLog;
using Logger = NLog.Logger;

namespace SoapyRTSA.Extentions;

/// <summary>
///     Binds whichever FFTW happens to be installed, by hand rather than through FFTW.NET: we want the
///     <b>guru split</b> interface, which transforms separate real and imaginary arrays. That is the layout
///     the rest of the sweep already uses, so no interleaving or deinterleaving is needed on either side of
///     the transform, and the dB conversion stays vectorised over contiguous runs.
///     <para>
///         Single precision is preferred - half the memory traffic and twice the lanes - and the double
///         precision library is accepted as a second choice because that is the one the FFTW.NET package
///         ships on Windows. Neither present is not an error: <see cref="FftTransform" /> falls back to the
///         managed transform.
///     </para>
/// </summary>
internal static unsafe class FftwNative
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();

    public enum Precision
    {
        None,
        Single,
        Double
    }

    /// <summary>FFTW_MEASURE: time a few candidate plans instead of guessing. Worth it, plans are cached per size.</summary>
    public const uint Measure = 0;

    /// <summary>Dimensions of one transform, matching fftw_iodim.</summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct IoDim
    {
        public int N;
        public int InputStride;
        public int OutputStride;
    }

    public static Precision Loaded { get; private set; } = Precision.None;
    public static string LibraryName { get; private set; } = string.Empty;

    #region single precision entry points

    private static delegate* unmanaged[Cdecl]<nuint, void*> _mallocSingle;
    private static delegate* unmanaged[Cdecl]<void*, void> _freeSingle;
    private static delegate* unmanaged[Cdecl]<int, IoDim*, int, IoDim*, float*, float*, float*, float*, uint, IntPtr> _planSingle;
    private static delegate* unmanaged[Cdecl]<IntPtr, float*, float*, float*, float*, void> _executeSingle;
    private static delegate* unmanaged[Cdecl]<IntPtr, void> _destroySingle;

    #endregion single precision entry points

    #region double precision entry points

    private static delegate* unmanaged[Cdecl]<nuint, void*> _mallocDouble;
    private static delegate* unmanaged[Cdecl]<void*, void> _freeDouble;
    private static delegate* unmanaged[Cdecl]<int, IoDim*, int, IoDim*, double*, double*, double*, double*, uint, IntPtr> _planDouble;
    private static delegate* unmanaged[Cdecl]<IntPtr, double*, double*, double*, double*, void> _executeDouble;
    private static delegate* unmanaged[Cdecl]<IntPtr, void> _destroyDouble;

    #endregion double precision entry points

    /// <summary>Planning is the one part of FFTW that is not thread safe, so every plan goes through here.</summary>
    public static readonly object PlanLock = new();

    private static readonly string[] SingleCandidates =
    {
        "libfftw3f.so.3", "libfftw3f.so", "libfftw3f-3.dll", "fftw3f.dll", "libfftw3f.3.dylib", "fftw3f"
    };

    private static readonly string[] DoubleCandidates =
    {
        "libfftw3.so.3", "libfftw3.so", "libfftw3-3.dll", "libfftw3-3-x64.dll", "fftw3.dll",
        "libfftw3.3.dylib", "fftw3"
    };

    static FftwNative()
    {
        //SOAPYRTSA_FFT=single|double|managed forces a backend, which is how the double precision path
        //that windows takes can be exercised on a machine that has the single precision library
        var forced = Environment.GetEnvironmentVariable("SOAPYRTSA_FFT")?.Trim().ToLowerInvariant();

        if (forced == "managed")
        {
            Logger.Info("SOAPYRTSA_FFT=managed, using the managed transform");
            return;
        }

        if (forced != "double" && TryBindSingle())
            return;

        if (forced != "single" && TryBindDouble())
            return;

        if (forced is "single" or "double")
        {
            Logger.Warn($"SOAPYRTSA_FFT={forced} but that FFTW precision could not be loaded");
            return;
        }

        Logger.Warn(
            "FFTW not found, falling back to the managed transform. Install libfftw3f "
                + "(apt install libfftw3-single3) or drop libfftw3f-3.dll beside the executable."
        );
    }

    private static bool TryBindSingle()
    {
        foreach (var candidate in SingleCandidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var handle))
                continue;

            if (!Export(handle, "fftwf_malloc", out var malloc)
                || !Export(handle, "fftwf_free", out var free)
                || !Export(handle, "fftwf_plan_guru_split_dft", out var plan)
                || !Export(handle, "fftwf_execute_split_dft", out var execute)
                || !Export(handle, "fftwf_destroy_plan", out var destroy))
                continue;

            _mallocSingle = (delegate* unmanaged[Cdecl]<nuint, void*>)malloc;
            _freeSingle = (delegate* unmanaged[Cdecl]<void*, void>)free;
            _planSingle =
                (delegate* unmanaged[Cdecl]<int, IoDim*, int, IoDim*, float*, float*, float*, float*, uint, IntPtr>)plan;
            _executeSingle = (delegate* unmanaged[Cdecl]<IntPtr, float*, float*, float*, float*, void>)execute;
            _destroySingle = (delegate* unmanaged[Cdecl]<IntPtr, void>)destroy;

            Loaded = Precision.Single;
            LibraryName = candidate;
            Logger.Info($"FFTW single precision bound from {candidate}");
            return true;
        }

        return false;
    }

    private static bool TryBindDouble()
    {
        foreach (var candidate in DoubleCandidates)
        {
            if (!NativeLibrary.TryLoad(candidate, out var handle))
                continue;

            if (!Export(handle, "fftw_malloc", out var malloc)
                || !Export(handle, "fftw_free", out var free)
                || !Export(handle, "fftw_plan_guru_split_dft", out var plan)
                || !Export(handle, "fftw_execute_split_dft", out var execute)
                || !Export(handle, "fftw_destroy_plan", out var destroy))
                continue;

            _mallocDouble = (delegate* unmanaged[Cdecl]<nuint, void*>)malloc;
            _freeDouble = (delegate* unmanaged[Cdecl]<void*, void>)free;
            _planDouble =
                (delegate* unmanaged[Cdecl]<int, IoDim*, int, IoDim*, double*, double*, double*, double*, uint, IntPtr>)plan;
            _executeDouble = (delegate* unmanaged[Cdecl]<IntPtr, double*, double*, double*, double*, void>)execute;
            _destroyDouble = (delegate* unmanaged[Cdecl]<IntPtr, void>)destroy;

            Loaded = Precision.Double;
            LibraryName = candidate;
            Logger.Info($"FFTW double precision bound from {candidate}");
            return true;
        }

        return false;
    }

    private static bool Export(IntPtr handle, string name, out IntPtr address) =>
        NativeLibrary.TryGetExport(handle, name, out address);

    #region single precision

    public static float* MallocSingle(int floats) => (float*)_mallocSingle((nuint)((long)floats * sizeof(float)));

    public static void FreeSingle(float* pointer) => _freeSingle(pointer);

    /// <summary>
    ///     Plans a forward 1D split transform. There is no sign argument in the split interface: passing the
    ///     imaginary pointers second is what makes it forward, and swapping them would make it inverse.
    /// </summary>
    public static IntPtr PlanSingle(int n, float* ri, float* ii, float* ro, float* io)
    {
        var dims = new IoDim { N = n, InputStride = 1, OutputStride = 1 };
        lock (PlanLock)
            return _planSingle(1, &dims, 0, null, ri, ii, ro, io, Measure);
    }

    public static void ExecuteSingle(IntPtr plan, float* ri, float* ii, float* ro, float* io) =>
        _executeSingle(plan, ri, ii, ro, io);

    public static void DestroySingle(IntPtr plan)
    {
        lock (PlanLock)
            _destroySingle(plan);
    }

    #endregion single precision

    #region double precision

    public static double* MallocDouble(int doubles) => (double*)_mallocDouble((nuint)((long)doubles * sizeof(double)));

    public static void FreeDouble(double* pointer) => _freeDouble(pointer);

    public static IntPtr PlanDouble(int n, double* ri, double* ii, double* ro, double* io)
    {
        var dims = new IoDim { N = n, InputStride = 1, OutputStride = 1 };
        lock (PlanLock)
            return _planDouble(1, &dims, 0, null, ri, ii, ro, io, Measure);
    }

    public static void ExecuteDouble(IntPtr plan, double* ri, double* ii, double* ro, double* io) =>
        _executeDouble(plan, ri, ii, ro, io);

    public static void DestroyDouble(IntPtr plan)
    {
        lock (PlanLock)
            _destroyDouble(plan);
    }

    #endregion double precision
}
