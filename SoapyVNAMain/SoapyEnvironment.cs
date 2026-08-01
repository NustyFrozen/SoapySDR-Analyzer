using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using NLog;
using Logger = NLog.Logger;

namespace SoapyVNAMain;

/// <summary>
///     Locates SoapySDR at runtime. The core library and the C# bindings ship with the app, but the
///     driver modules live wherever the machine happens to have put them, so they are searched for
///     instead of being assumed to sit at a fixed path.
/// </summary>
public static class SoapyEnvironment
{
    private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
    private static readonly object LoadLock = new();

    private static bool _coreLoaded;
    private static bool _configured;
    private static string? _modulesAbi;

    /// <summary>
    ///     SoapySDR rejects any module whose ABI string differs from the core library's. The core we
    ///     ship is built from master ("0.8-3") while distributions package the 0.8 release, so on a
    ///     stock Linux box every installed driver is rejected and no device is ever found. When this is
    ///     set, the core is loaded from a patched copy that reports the modules' ABI, which makes the
    ///     check pass. Disable with SOAPYVNA_IGNORE_ABI=0.
    /// </summary>
    public static bool IgnoreAbiMismatch { get; set; } =
        Environment.GetEnvironmentVariable("SOAPYVNA_IGNORE_ABI") is not ("0" or "false" or "False");

    /// <summary>ABI the core library we ship was built with, used when the file name gives nothing away.</summary>
    private const string FallbackCoreAbi = "0.8-3";

    private static string AppRoot => AppContext.BaseDirectory;
    private static string BundledSoapyPath => Path.Combine(AppRoot, "SoapySDR");
    private static string BundledLibsPath => Path.Combine(BundledSoapyPath, "Libs");
    private static string BundledRootPath => Path.Combine(BundledSoapyPath, "root", "SoapySDR");

    /// <summary>A SoapySDR installation: a directory of driver modules plus the prefix it was installed under.</summary>
    private sealed record Installation(
        string ModulesPath,
        string Root,
        string Abi,
        int ModuleCount,
        bool FromEnvironment
    );

    /// <summary>
    ///     Points SoapySDR at whatever installation this machine has, and loads the native libraries.
    /// </summary>
    public static void Setup()
    {
        lock (LoadLock)
        {
            //a second pass would throw: an assembly only takes one DllImport resolver
            if (_configured)
                return;
            _configured = true;
        }

        var coreLibrary = FindCoreLibrary();
        var coreAbi = DetectCoreAbi(coreLibrary);
        Logger.Info(
            $"SoapySDR core library -> {coreLibrary ?? "(default search)"} (ABI {coreAbi})"
        );

        var install = Discover(coreAbi);
        if (install is null)
        {
            Logger.Error(
                "No SoapySDR modules found on this machine. Install SoapySDR "
                    + "(apt install soapysdr-tools soapysdr-module-all) or set SOAPY_SDR_PLUGIN_PATH."
            );
        }
        else
        {
            _modulesAbi = install.Abi;
            Logger.Info(
                $"Found SoapySDR install: {install.ModuleCount} modules, ABI {install.Abi}, root {install.Root}"
            );

            //a plugin path that was already set is somebody's deliberate choice, keep it whole
            if (install.FromEnvironment)
                Logger.Info("Keeping the SOAPY_SDR_PLUGIN_PATH that was already set.");
            else
                SetVariable("SOAPY_SDR_PLUGIN_PATH", install.ModulesPath);

            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SOAPY_SDR_ROOT")))
                SetVariable("SOAPY_SDR_ROOT", install.Root);

            if (install.Abi != coreAbi)
                Logger.Warn(
                    $"SoapySDR ABI mismatch: core library is {coreAbi}, installed modules are {install.Abi}. "
                        + (
                            IgnoreAbiMismatch
                                ? "The ABI check will be bypassed."
                                : "Modules will be rejected; set SOAPYVNA_IGNORE_ABI=1 to bypass the check."
                        )
                );
        }

        AppendToPath(BundledSoapyPath, BundledLibsPath);

        Logger.Info(
            $"SOAPY_SDR_PLUGIN_PATH -> {Environment.GetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH")}"
        );
        Logger.Info($"SOAPY_SDR_ROOT -> {Environment.GetEnvironmentVariable("SOAPY_SDR_ROOT")}");

        RegisterDllResolvers();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            EnsureCoreLoaded();
    }

    #region discovery

    /// <summary>
    ///     Walks every place a SoapySDR installation is plausibly found and keeps the ones that actually
    ///     hold modules for this platform, preferring an ABI that matches the core library.
    /// </summary>
    private static Installation? Discover(string coreAbi)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new List<Installation>();
        var fromEnvironment = EnvironmentModuleDirectories().ToHashSet(StringComparer.Ordinal);

        foreach (var modulesPath in CandidateModuleDirectories())
        {
            var wasSetByUser = fromEnvironment.Contains(modulesPath);
            string full;
            try
            {
                full = Path.TrimEndingDirectorySeparator(Path.GetFullPath(modulesPath));
            }
            catch (Exception)
            {
                continue;
            }

            if (!seen.Add(full) || !Directory.Exists(full))
                continue;

            var moduleCount = CountModules(full);
            if (moduleCount == 0)
                continue;

            candidates.Add(
                new Installation(
                    full,
                    RootOfModuleDirectory(full),
                    AbiOfModuleDirectory(full),
                    moduleCount,
                    wasSetByUser
                )
            );
        }

        //OrderBy is stable, so an ABI match wins and discovery order breaks the tie
        return candidates.OrderByDescending(c => c.Abi == coreAbi).FirstOrDefault();
    }

    /// <summary>Module directories named by SOAPY_SDR_PLUGIN_PATH, which are somebody's explicit choice.</summary>
    private static IEnumerable<string> EnvironmentModuleDirectories()
    {
        var pluginPath = Environment.GetEnvironmentVariable("SOAPY_SDR_PLUGIN_PATH");
        if (string.IsNullOrWhiteSpace(pluginPath))
            yield break;

        foreach (var entry in pluginPath.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
            yield return entry;
    }

    /// <summary>Directories that may hold driver modules, best guess first.</summary>
    private static IEnumerable<string> CandidateModuleDirectories()
    {
        //an explicit override always wins
        foreach (var entry in EnvironmentModuleDirectories())
            yield return entry;

        foreach (var root in CandidateRoots())
        foreach (var modules in ModuleDirectoriesUnder(root))
            yield return modules;
    }

    /// <summary>Installation prefixes to search, best guess first.</summary>
    private static IEnumerable<string> CandidateRoots()
    {
        //the copy shipped next to the executable matches the libraries we load, so it goes first
        yield return BundledRootPath;

        var envRoot = Environment.GetEnvironmentVariable("SOAPY_SDR_ROOT");
        if (!string.IsNullOrWhiteSpace(envRoot))
            yield return envRoot;

        //a SoapySDRUtil on PATH gives away a custom prefix nothing else would find
        foreach (var root in RootsFromPath())
            yield return root;

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            foreach (var programFiles in new[]
                     {
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                         Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                     })
            {
                if (string.IsNullOrEmpty(programFiles))
                    continue;
                yield return Path.Combine(programFiles, "PothosSDR");
                yield return Path.Combine(programFiles, "SoapySDR");
            }

            yield return @"C:\PothosSDR";
        }
        else
        {
            yield return "/usr";
            yield return "/usr/local";
            yield return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".local"
            );
            yield return "/opt/SoapySDR";
            yield return "/opt/local";
        }
    }

    /// <summary>Derives installation prefixes from a SoapySDRUtil found on PATH.</summary>
    private static IEnumerable<string> RootsFromPath()
    {
        var path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            yield break;

        var utility = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "SoapySDRUtil.exe"
            : "SoapySDRUtil";

        foreach (var entry in path.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            string? parent;
            try
            {
                if (!File.Exists(Path.Combine(entry, utility)))
                    continue;
                //<root>/bin/SoapySDRUtil
                parent = Path.GetDirectoryName(Path.TrimEndingDirectorySeparator(Path.GetFullPath(entry)));
            }
            catch (Exception)
            {
                continue;
            }

            if (parent is not null)
                yield return parent;
        }
    }

    /// <summary>Expands the &lt;root&gt;/lib[64]/[triplet]/SoapySDR/modules&lt;abi&gt; layouts.</summary>
    private static IEnumerable<string> ModuleDirectoriesUnder(string root)
    {
        foreach (var libName in new[] { "lib", "lib64", "lib32" })
        {
            var libDirectory = Path.Combine(root, libName);
            if (!Directory.Exists(libDirectory))
                continue;

            foreach (var modules in ModuleDirectoriesIn(Path.Combine(libDirectory, "SoapySDR")))
                yield return modules;

            //debian and friends bury the modules under an architecture triplet
            string[] architectures;
            try
            {
                architectures = Directory.GetDirectories(libDirectory);
            }
            catch (Exception)
            {
                continue;
            }

            foreach (var architecture in architectures)
            foreach (var modules in ModuleDirectoriesIn(Path.Combine(architecture, "SoapySDR")))
                yield return modules;
        }
    }

    private static IEnumerable<string> ModuleDirectoriesIn(string soapyDirectory)
    {
        if (!Directory.Exists(soapyDirectory))
            yield break;

        string[] found;
        try
        {
            found = Directory.GetDirectories(soapyDirectory, "modules*");
        }
        catch (Exception)
        {
            yield break;
        }

        foreach (var modules in found)
            yield return modules;
    }

    /// <summary>Counts loadable modules, which is also how a Windows module set is ruled out on Linux and back.</summary>
    private static int CountModules(string modulesPath)
    {
        var extension = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".dll" : ".so";
        try
        {
            return Directory
                .EnumerateFiles(modulesPath)
                .Count(file =>
                    Path.GetFileName(file).Contains(extension, StringComparison.OrdinalIgnoreCase)
                );
        }
        catch (Exception)
        {
            return 0;
        }
    }

    /// <summary>modules0.8 -> 0.8</summary>
    private static string AbiOfModuleDirectory(string modulesPath)
    {
        var name = Path.GetFileName(Path.TrimEndingDirectorySeparator(modulesPath));
        return name.StartsWith("modules", StringComparison.OrdinalIgnoreCase)
            ? name["modules".Length..]
            : string.Empty;
    }

    /// <summary>&lt;root&gt;/lib/x86_64-linux-gnu/SoapySDR/modules0.8 -> &lt;root&gt;</summary>
    private static string RootOfModuleDirectory(string modulesPath)
    {
        var directory = new DirectoryInfo(modulesPath);
        //climb past modules<abi>, SoapySDR, the optional triplet and lib
        for (var parent = directory.Parent; parent is not null; parent = parent.Parent)
            if (parent.Name is "lib" or "lib64" or "lib32")
                return parent.Parent?.FullName ?? parent.FullName;

        return directory.Parent?.Parent?.FullName ?? directory.FullName;
    }

    #endregion

    #region native library loading

    /// <summary>The core library that pairs with the C# bindings we ship, if it is there.</summary>
    private static string? FindCoreLibrary()
    {
        if (!Directory.Exists(BundledLibsPath))
            return null;

        var pattern = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? "SoapySDR.dll"
            : "libSoapySDR.so*";

        try
        {
            return Directory.EnumerateFiles(BundledLibsPath, pattern).OrderBy(f => f).FirstOrDefault();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>libSoapySDR.so.0.8-3 -> 0.8-3, falling back to the modules we ship next to it.</summary>
    private static string DetectCoreAbi(string? coreLibrary)
    {
        if (coreLibrary is not null)
        {
            const string marker = "libSoapySDR.so.";
            var name = Path.GetFileName(coreLibrary);
            if (name.StartsWith(marker, StringComparison.Ordinal) && name.Length > marker.Length)
                return name[marker.Length..];
        }

        //windows core libraries carry no version in the name, but the bundled modules do
        var bundled = ModuleDirectoriesUnder(BundledRootPath)
            .Select(AbiOfModuleDirectory)
            .FirstOrDefault(abi => abi.Length > 0);

        return bundled ?? FallbackCoreAbi;
    }

    /// <summary>
    ///     Loads the core library into the global symbol scope before anything else pulls it in, so that
    ///     driver modules bind to it rather than to whatever copy their own DT_NEEDED drags in.
    /// </summary>
    private static void EnsureCoreLoaded()
    {
        lock (LoadLock)
        {
            if (_coreLoaded)
                return;

            var coreLibrary = FindCoreLibrary();
            if (coreLibrary is null)
                return;

            var abi = DetectCoreAbi(coreLibrary);
            if (IgnoreAbiMismatch && _modulesAbi is { Length: > 0 } modulesAbi && modulesAbi != abi)
            {
                var shim = CreateAbiShim(coreLibrary, abi, modulesAbi);
                if (shim is not null)
                {
                    Logger.Warn($"Loading ABI-relaxed core library {shim} (reports ABI {modulesAbi})");
                    coreLibrary = shim;
                }
            }

            _coreLoaded = LoadGlobal(coreLibrary);
        }
    }

    /// <summary>
    ///     dlopen with RTLD_GLOBAL. NativeLibrary.Load uses RTLD_LOCAL, which leaves the core's symbols
    ///     invisible to modules whose DT_NEEDED names a differently versioned soname.
    /// </summary>
    private static bool LoadGlobal(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            foreach (var open in new Func<string, int, IntPtr>[] { Libc.Open, Libdl.Open })
            {
                try
                {
                    if (open(path, Libc.RtldLazy | Libc.RtldGlobal) != IntPtr.Zero)
                        return true;
                }
                catch (Exception exception) when (exception is DllNotFoundException or EntryPointNotFoundException)
                {
                    //try the next libc layout
                }
            }

        try
        {
            NativeLibrary.Load(path);
            return true;
        }
        catch (Exception exception)
        {
            Logger.Error($"Failed to load {path}: {exception.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Teaches the runtime where the native bindings live, and which of the two Pothosware assemblies
    ///     applies on Linux.
    /// </summary>
    private static void RegisterDllResolvers()
    {
        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return;

        NativeLibrary.SetDllImportResolver(
            Assembly.GetExecutingAssembly(),
            (libraryName, assembly, searchPath) =>
                libraryName.Equals("Pothosware.SoapySDR.dll", StringComparison.OrdinalIgnoreCase)
                    //force the linux binding so the runtime never goes looking for the windows .dll
                    ? NativeLibrary.Load("Pothosware.SoapySDRLinux.dll", assembly, searchPath)
                    : IntPtr.Zero
        );

        NativeLibrary.SetDllImportResolver(
            typeof(Pothosware.SoapySDR.Device).Assembly,
            (libraryName, assembly, searchPath) =>
            {
                if (libraryName != "SoapySDRCSharpSWIG")
                    return IntPtr.Zero;

                EnsureCoreLoaded();

                var swigLibrary = Path.Combine(BundledLibsPath, "libSoapySDRCSharpSWIG.so");
                return File.Exists(swigLibrary) ? NativeLibrary.Load(swigLibrary) : IntPtr.Zero;
            }
        );
    }

    #endregion

    #region abi shim

    /// <summary>
    ///     Writes a copy of the core library whose compiled-in ABI string reads as the modules' ABI, so
    ///     SoapySDR::Registry accepts them. Only the standalone string is touched: the soname and the
    ///     error message that quote the same digits are left alone.
    /// </summary>
    private static string? CreateAbiShim(string coreLibrary, string fromAbi, string toAbi)
    {
        if (toAbi.Length > fromAbi.Length)
        {
            //the replacement has to fit in the original bytes; growing it would move every offset
            Logger.Warn($"Cannot relax ABI {fromAbi} to the longer {toAbi}, leaving the check in place.");
            return null;
        }

        try
        {
            var shimPath = Path.Combine(ShimDirectory(), toAbi, Path.GetFileName(coreLibrary));
            if (IsShimCurrent(shimPath, coreLibrary, fromAbi, toAbi))
                return shimPath;

            var image = File.ReadAllBytes(coreLibrary);
            var offsets = FindStandaloneString(image, fromAbi);
            if (offsets.Count == 0)
            {
                Logger.Warn($"No ABI string found in {coreLibrary}, leaving the check in place.");
                return null;
            }

            var replacement = Encoding.ASCII.GetBytes(toAbi);
            foreach (var offset in offsets)
            {
                replacement.CopyTo(image, offset);
                for (var i = offset + replacement.Length; i < offset + fromAbi.Length; i++)
                    image[i] = 0;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(shimPath)!);
            //write beside the target and swap it in, so a second instance keeps its mapped copy
            var staging = shimPath + "." + Environment.ProcessId + ".tmp";
            File.WriteAllBytes(staging, image);
            File.Move(staging, shimPath, true);
            return shimPath;
        }
        catch (Exception exception)
        {
            Logger.Error($"Could not build the ABI shim: {exception.Message}");
            return null;
        }
    }

    private static bool IsShimCurrent(string shimPath, string coreLibrary, string fromAbi, string toAbi)
    {
        try
        {
            if (!File.Exists(shimPath))
                return false;

            var shim = new FileInfo(shimPath);
            var core = new FileInfo(coreLibrary);
            if (shim.Length != core.Length || shim.LastWriteTimeUtc < core.LastWriteTimeUtc)
                return false;

            var image = File.ReadAllBytes(shimPath);
            return FindStandaloneString(image, fromAbi).Count == 0
                && FindStandaloneString(image, toAbi).Count > 0;
        }
        catch (Exception)
        {
            return false;
        }
    }

    /// <summary>Offsets of <paramref name="value" /> where it is a whole NUL terminated string, not part of a longer one.</summary>
    private static List<int> FindStandaloneString(byte[] image, string value)
    {
        var needle = Encoding.ASCII.GetBytes(value);
        var offsets = new List<int>();

        for (var i = 1; i + needle.Length < image.Length; i++)
        {
            if (image[i] != needle[0] || image[i - 1] != 0 || image[i + needle.Length] != 0)
                continue;

            var matched = true;
            for (var j = 1; j < needle.Length && matched; j++)
                matched = image[i + j] == needle[j];

            if (matched)
                offsets.Add(i);
        }

        return offsets;
    }

    private static string ShimDirectory()
    {
        try
        {
            var cache = Environment.GetEnvironmentVariable("XDG_CACHE_HOME");
            if (string.IsNullOrWhiteSpace(cache))
                cache = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".cache"
                );

            return Path.Combine(cache, "SoapySDR-Analyzer", "abi");
        }
        catch (Exception)
        {
            return Path.Combine(Path.GetTempPath(), "SoapySDR-Analyzer", "abi");
        }
    }

    #endregion

    #region environment

    /// <summary>
    ///     Sets a variable for both the managed and the native side. On unix
    ///     Environment.SetEnvironmentVariable only touches the runtime's own copy, so getenv() inside
    ///     libSoapySDR would never see it.
    /// </summary>
    private static void SetVariable(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                Ucrt.PutEnv(name, value);
            else
                Libc.SetEnv(name, value, 1);
        }
        catch (Exception exception)
        {
            Logger.Warn($"Could not publish {name} to native code: {exception.Message}");
        }
    }

    private static void AppendToPath(params string[] directories)
    {
        var existing = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var entries = existing
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .ToList();

        var additions = directories
            .Where(Directory.Exists)
            .Where(directory => !entries.Contains(directory, StringComparer.Ordinal))
            .ToArray();

        if (additions.Length == 0)
            return;

        entries.AddRange(additions);
        SetVariable("PATH", string.Join(Path.PathSeparator, entries));
    }

    #endregion

    private static class Libc
    {
        public const int RtldLazy = 0x0001;
        public const int RtldGlobal = 0x0100;

        [DllImport("libc", EntryPoint = "setenv")]
        public static extern int SetEnv(
            [MarshalAs(UnmanagedType.LPUTF8Str)] string name,
            [MarshalAs(UnmanagedType.LPUTF8Str)] string value,
            int overwrite
        );

        //glibc 2.34 and newer fold libdl into libc
        [DllImport("libc", EntryPoint = "dlopen")]
        public static extern IntPtr Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    }

    private static class Libdl
    {
        [DllImport("libdl.so.2", EntryPoint = "dlopen")]
        public static extern IntPtr Open([MarshalAs(UnmanagedType.LPUTF8Str)] string path, int flags);
    }

    private static class Ucrt
    {
        //SetEnvironmentVariable does not reach the CRT table that getenv() reads
        [DllImport("ucrtbase.dll", EntryPoint = "_wputenv_s", CharSet = CharSet.Unicode)]
        public static extern int PutEnv(string name, string value);
    }
}
