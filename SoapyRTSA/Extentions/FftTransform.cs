namespace SoapyRTSA.Extentions;

/// <summary>
///     One transform plus the buffers it works on, so the sweep never has to care which engine is behind
///     it: fill <see cref="InputReal" />/<see cref="InputImaginary" />, call <see cref="Forward" />, read
///     <see cref="OutputReal" />/<see cref="OutputImaginary" />.
///     <para>
///         FFTW is used when it is installed, single precision for choice. Everything is allocated once, in
///         the constructor, and the buffers handed to the planner are the same ones executed against every
///         frame so FFTW can rely on their alignment.
///     </para>
/// </summary>
public sealed unsafe class FftTransform : IDisposable
{
    public int Size { get; }

    /// <summary>What this instance ended up using, for the log and the display tab.</summary>
    public string EngineName { get; }

    private readonly IntPtr _plan;

    //single precision: the transform reads and writes these directly
    private float* _singleInputReal;
    private float* _singleInputImaginary;
    private float* _singleOutputReal;
    private float* _singleOutputImaginary;

    //double precision: staged through managed float arrays so callers see the same api
    private double* _doubleInputReal;
    private double* _doubleInputImaginary;
    private double* _doubleOutputReal;
    private double* _doubleOutputImaginary;

    private readonly float[]? _stageInputReal;
    private readonly float[]? _stageInputImaginary;
    private readonly float[]? _stageOutputReal;
    private readonly float[]? _stageOutputImaginary;

    private readonly Fft? _managed;
    private readonly FftwNative.Precision _precision;
    private bool _disposed;

    public FftTransform(int size)
    {
        Size = size;
        _precision = FftwNative.Loaded;

        switch (_precision)
        {
            case FftwNative.Precision.Single:
                _singleInputReal = FftwNative.MallocSingle(size);
                _singleInputImaginary = FftwNative.MallocSingle(size);
                _singleOutputReal = FftwNative.MallocSingle(size);
                _singleOutputImaginary = FftwNative.MallocSingle(size);

                Clear(_singleInputReal, size);
                Clear(_singleInputImaginary, size);

                _plan = FftwNative.PlanSingle(size, _singleInputReal, _singleInputImaginary,
                    _singleOutputReal, _singleOutputImaginary);
                break;

            case FftwNative.Precision.Double:
                _doubleInputReal = FftwNative.MallocDouble(size);
                _doubleInputImaginary = FftwNative.MallocDouble(size);
                _doubleOutputReal = FftwNative.MallocDouble(size);
                _doubleOutputImaginary = FftwNative.MallocDouble(size);

                Clear(_doubleInputReal, size);
                Clear(_doubleInputImaginary, size);

                _stageInputReal = new float[size];
                _stageInputImaginary = new float[size];
                _stageOutputReal = new float[size];
                _stageOutputImaginary = new float[size];

                _plan = FftwNative.PlanDouble(size, _doubleInputReal, _doubleInputImaginary,
                    _doubleOutputReal, _doubleOutputImaginary);
                break;
        }

        if (_precision != FftwNative.Precision.None && _plan == IntPtr.Zero)
        {
            //planner refused: fall through to the managed transform rather than crash the sweep
            Release();
            _precision = FftwNative.Precision.None;
        }

        if (_precision == FftwNative.Precision.None)
        {
            _managed = new Fft(size);
            _stageInputReal = new float[size];
            _stageInputImaginary = new float[size];
        }

        EngineName = _precision switch
        {
            FftwNative.Precision.Single => $"FFTW single ({FftwNative.LibraryName})",
            FftwNative.Precision.Double => $"FFTW double ({FftwNative.LibraryName})",
            _ => "managed radix-2"
        };
    }

    public Span<float> InputReal => _precision == FftwNative.Precision.Single
        ? new Span<float>(_singleInputReal, Size)
        : _stageInputReal!;

    public Span<float> InputImaginary => _precision == FftwNative.Precision.Single
        ? new Span<float>(_singleInputImaginary, Size)
        : _stageInputImaginary!;

    public Span<float> OutputReal => _precision switch
    {
        FftwNative.Precision.Single => new Span<float>(_singleOutputReal, Size),
        FftwNative.Precision.Double => _stageOutputReal!,
        //the managed transform works in place
        _ => _stageInputReal!
    };

    public Span<float> OutputImaginary => _precision switch
    {
        FftwNative.Precision.Single => new Span<float>(_singleOutputImaginary, Size),
        FftwNative.Precision.Double => _stageOutputImaginary!,
        _ => _stageInputImaginary!
    };

    public void Forward()
    {
        switch (_precision)
        {
            case FftwNative.Precision.Single:
                FftwNative.ExecuteSingle(_plan, _singleInputReal, _singleInputImaginary,
                    _singleOutputReal, _singleOutputImaginary);
                break;

            case FftwNative.Precision.Double:
                SimdOps.Widen(_stageInputReal!, new Span<double>(_doubleInputReal, Size));
                SimdOps.Widen(_stageInputImaginary!, new Span<double>(_doubleInputImaginary, Size));

                FftwNative.ExecuteDouble(_plan, _doubleInputReal, _doubleInputImaginary,
                    _doubleOutputReal, _doubleOutputImaginary);

                SimdOps.Narrow(new Span<double>(_doubleOutputReal, Size), _stageOutputReal!);
                SimdOps.Narrow(new Span<double>(_doubleOutputImaginary, Size), _stageOutputImaginary!);
                break;

            default:
                _managed!.Forward(_stageInputReal!, _stageInputImaginary!);
                break;
        }
    }

    private static void Clear(float* pointer, int count) => new Span<float>(pointer, count).Clear();

    private static void Clear(double* pointer, int count) => new Span<double>(pointer, count).Clear();

    private void Release()
    {
        if (_singleInputReal != null)
        {
            FftwNative.FreeSingle(_singleInputReal);
            FftwNative.FreeSingle(_singleInputImaginary);
            FftwNative.FreeSingle(_singleOutputReal);
            FftwNative.FreeSingle(_singleOutputImaginary);
            _singleInputReal = _singleInputImaginary = _singleOutputReal = _singleOutputImaginary = null;
        }

        if (_doubleInputReal != null)
        {
            FftwNative.FreeDouble(_doubleInputReal);
            FftwNative.FreeDouble(_doubleInputImaginary);
            FftwNative.FreeDouble(_doubleOutputReal);
            FftwNative.FreeDouble(_doubleOutputImaginary);
            _doubleInputReal = _doubleInputImaginary = _doubleOutputReal = _doubleOutputImaginary = null;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;
        _disposed = true;

        if (_plan != IntPtr.Zero)
            switch (_precision)
            {
                case FftwNative.Precision.Single:
                    FftwNative.DestroySingle(_plan);
                    break;
                case FftwNative.Precision.Double:
                    FftwNative.DestroyDouble(_plan);
                    break;
            }

        Release();
    }
}
