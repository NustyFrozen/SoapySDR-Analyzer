using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SoapySA.Extentions;

public class Imports
{
    public const int SW_HIDE = 0;
    public const int SW_SHOW = 5;

    internal const int CTRL_C_EVENT = 0;

    public static string RandomString(int length)
    {
        const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
        return new string(Enumerable.Repeat(chars, length)
            .Select(s => s[new Random().Next(s.Length)]).ToArray());
    }

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("kernel32.dll")]
    public static extern IntPtr GetConsoleWindow();

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool MoveWindow(IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    [DllImport("user32.dll")]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll")]
    public static extern bool SetLayeredWindowAttributes(IntPtr hwnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool AllocConsole();

    // Token: 0x06000058 RID: 88
    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    [DllImport("User32.dll")]
    public static extern IntPtr GetDC(IntPtr hwnd);

    [DllImport("User32.dll")]
    public static extern void ReleaseDC(IntPtr hwnd, IntPtr dc);

    // Token: 0x06000011 RID: 17
    [DllImport("gdi32.dll")]
    private static extern bool BitBlt(IntPtr hdcDest, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc,
        int nXSrc, int nYSrc, int dwRop);

    [DllImport("user32.dll")]
    internal static extern IntPtr GetDCEx(IntPtr hWnd, IntPtr hrgnClip, DeviceContextValues flags);

    public static double Scale(double value, double oldMin, double oldMax, double newMin, double newMax)
    {
        return newMin + (value - oldMin) * (newMax - newMin) / (oldMax - oldMin);
    }

    [DllImport("kernel32.dll")]
    internal static extern bool GenerateConsoleCtrlEvent(uint dwCtrlEvent, uint dwProcessGroupId);

    [DllImport("kernel32.dll", SetLastError = true)]
    internal static extern bool AttachConsole(uint dwProcessId);

    [DllImport("kernel32.dll", SetLastError = true, ExactSpelling = true)]
    internal static extern bool FreeConsole();

    [DllImport("kernel32.dll")]
    private static extern bool SetConsoleCtrlHandler(ConsoleCtrlDelegate HandlerRoutine, bool Add);

    [DllImport("user32.dll")]
    public static extern bool GetCursorPos(ref Point lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool GetWindowRect(IntPtr hwnd, out RECT lpRect);

    [DllImport("gdi32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool BitBlt(IntPtr hdc, int nXDest, int nYDest, int nWidth, int nHeight, IntPtr hdcSrc,
        int nXSrc, int nYSrc, TernaryRasterOperations dwRop);

    [DllImport("gdi32.dll", SetLastError = true)]
    internal static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr CreateCompatibleBitmap(IntPtr hdc, int nWidth, int nHeight);

    [DllImport("gdi32.dll")]
    internal static extern IntPtr SelectObject(IntPtr hdc, IntPtr hgdiobj);

    [DllImport("gdi32.dll")]
    internal static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool DeleteObject(IntPtr hObject);

    public static string GetActiveWindowTitle()
    {
        const int nChars = 256;
        var Buff = new StringBuilder(nChars);
        var handle = GetForegroundWindow();
        if (GetWindowText(handle, Buff, nChars) > 0) return Buff.ToString();
        return "NULL";
    }

    [DllImport("user32.dll")]
    public static extern bool GetAsyncKeyState(Keys vKey);

    [DllImport("user32.dll")]
    public static extern int SetForegroundWindow(IntPtr hwnd);

    public static void BringToFront(Process pTemp)
    {
        SetForegroundWindow(pTemp.MainWindowHandle);
    }

    [DllImport("User32")]
    public static extern void SetWindowDisplayAffinity(IntPtr handle, int affinity);

    [DllImport("user32.dll")]
    public static extern int SetWindowText(IntPtr hWnd, string text);

    public static void RemoveScrubs(Form theForm)
    {
        SetWindowDisplayAffinity(theForm.Handle, 1);
    }

    internal enum DeviceContextValues : uint
    {
        /// <summary>
        ///     DCX_WINDOW: Returns a DC that corresponds to the window rectangle rather
        ///     than the client rectangle.
        /// </summary>
        Window = 0x00000001,

        /// <summary>
        ///     DCX_CACHE: Returns a DC from the cache, rather than the OWNDC or CLASSDC
        ///     window. Essentially overrides CS_OWNDC and CS_CLASSDC.
        /// </summary>
        Cache = 0x00000002,

        /// <summary>
        ///     DCX_NORESETATTRS: Does not reset the attributes of this DC to the
        ///     default attributes when this DC is released.
        /// </summary>
        NoResetAttrs = 0x00000004,

        /// <summary>
        ///     DCX_CLIPCHILDREN: Excludes the visible regions of all child windows
        ///     below the window identified by hWnd.
        /// </summary>
        ClipChildren = 0x00000008,

        /// <summary>
        ///     DCX_CLIPSIBLINGS: Excludes the visible regions of all sibling windows
        ///     above the window identified by hWnd.
        /// </summary>
        ClipSiblings = 0x00000010,

        /// <summary>
        ///     DCX_PARENTCLIP: Uses the visible region of the parent window. The
        ///     parent's WS_CLIPCHILDREN and CS_PARENTDC style bits are ignored. The origin is
        ///     set to the upper-left corner of the window identified by hWnd.
        /// </summary>
        ParentClip = 0x00000020,

        /// <summary>
        ///     DCX_EXCLUDERGN: The clipping region identified by hrgnClip is excluded
        ///     from the visible region of the returned DC.
        /// </summary>
        ExcludeRgn = 0x00000040,

        /// <summary>
        ///     DCX_INTERSECTRGN: The clipping region identified by hrgnClip is
        ///     intersected with the visible region of the returned DC.
        /// </summary>
        IntersectRgn = 0x00000080,

        /// <summary>DCX_EXCLUDEUPDATE: Unknown...Undocumented</summary>
        ExcludeUpdate = 0x00000100,

        /// <summary>DCX_INTERSECTUPDATE: Unknown...Undocumented</summary>
        IntersectUpdate = 0x00000200,

        /// <summary>
        ///     DCX_LOCKWINDOWUPDATE: Allows drawing even if there is a LockWindowUpdate
        ///     call in effect that would otherwise exclude this window. Used for drawing during
        ///     tracking.
        /// </summary>
        LockWindowUpdate = 0x00000400,

        /// <summary>
        ///     DCX_VALIDATE When specified with DCX_INTERSECTUPDATE, causes the DC to
        ///     be completely validated. Using this function with both DCX_INTERSECTUPDATE and
        ///     DCX_VALIDATE is identical to using the BeginPaint function.
        /// </summary>
        Validate = 0x00200000
    }

    internal enum TernaryRasterOperations : uint
    {
        /// <summary>dest = source</summary>
        SRCCOPY = 0x00CC0020,

        /// <summary>dest = source OR dest</summary>
        SRCPAINT = 0x00EE0086,

        /// <summary>dest = source AND dest</summary>
        SRCAND = 0x008800C6,

        /// <summary>dest = source XOR dest</summary>
        SRCINVERT = 0x00660046,

        /// <summary>dest = source AND (NOT dest)</summary>
        SRCERASE = 0x00440328,

        /// <summary>dest = (NOT source)</summary>
        NOTSRCCOPY = 0x00330008,

        /// <summary>dest = (NOT src) AND (NOT dest)</summary>
        NOTSRCERASE = 0x001100A6,

        /// <summary>dest = (source AND pattern)</summary>
        MERGECOPY = 0x00C000CA,

        /// <summary>dest = (NOT source) OR dest</summary>
        MERGEPAINT = 0x00BB0226,

        /// <summary>dest = pattern</summary>
        PATCOPY = 0x00F00021,

        /// <summary>dest = DPSnoo</summary>
        PATPAINT = 0x00FB0A09,

        /// <summary>dest = pattern XOR dest</summary>
        PATINVERT = 0x005A0049,

        /// <summary>dest = (NOT dest)</summary>
        DSTINVERT = 0x00550009,

        /// <summary>dest = BLACK</summary>
        BLACKNESS = 0x00000042,

        /// <summary>dest = WHITE</summary>
        WHITENESS = 0x00FF0062,

        /// <summary>
        ///     Capture window as seen on screen.  This includes layered windows
        ///     such as WPF windows with AllowsTransparency="true"
        /// </summary>
        CAPTUREBLT = 0x40000000
    }

    // Delegate type to be used as the Handler Routine for SCCH
    private delegate bool ConsoleCtrlDelegate(uint CtrlType);

    public struct RECT
    {
        public int Left; // x position of upper-left corner
        public int Top; // y position of upper-left corner
        public int Right; // x position of lower-right corner
        public int Bottom; // y position of lower-right corner
    }
}