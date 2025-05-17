using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace SoapyVNACommon.Extentions;

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

    public enum Keys
    {
        /// <summary>
        ///  The bit mask to extract a key code from a key value.
        /// </summary>
        KeyCode = 0x0000FFFF,

        /// <summary>
        ///  The bit mask to extract modifiers from a key value.
        /// </summary>
        Modifiers = unchecked((int)0xFFFF0000),

        /// <summary>
        ///  No key pressed.
        /// </summary>
        None = 0x00,

        /// <summary>
        ///  The left mouse button.
        /// </summary>
        LButton = 0x01,

        /// <summary>
        ///  The right mouse button.
        /// </summary>
        RButton = 0x02,

        /// <summary>
        ///  The CANCEL key.
        /// </summary>
        Cancel = 0x03,

        /// <summary>
        ///  The middle mouse button (three-button mouse).
        /// </summary>
        MButton = 0x04,

        /// <summary>
        ///  The first x mouse button (five-button mouse).
        /// </summary>
        XButton1 = 0x05,

        /// <summary>
        ///  The second x mouse button (five-button mouse).
        /// </summary>
        XButton2 = 0x06,

        /// <summary>
        ///  The BACKSPACE key.
        /// </summary>
        Back = 0x08,

        /// <summary>
        ///  The TAB key.
        /// </summary>
        Tab = 0x09,

        /// <summary>
        ///  The CLEAR key.
        /// </summary>
        LineFeed = 0x0A,

        /// <summary>
        ///  The CLEAR key.
        /// </summary>
        Clear = 0x0C,

        /// <summary>
        ///  The RETURN key.
        /// </summary>
        Return = 0x0D,

        /// <summary>
        ///  The ENTER key.
        /// </summary>
        Enter = Return,

        /// <summary>
        ///  The SHIFT key.
        /// </summary>
        ShiftKey = 0x10,

        /// <summary>
        ///  The CTRL key.
        /// </summary>
        ControlKey = 0x11,

        /// <summary>
        ///  The ALT key.
        /// </summary>
        Menu = 0x12,

        /// <summary>
        ///  The PAUSE key.
        /// </summary>
        Pause = 0x13,

        /// <summary>
        ///  The CAPS LOCK key.
        /// </summary>
        Capital = 0x14,

        /// <summary>
        ///  The CAPS LOCK key.
        /// </summary>
        CapsLock = 0x14,

        /// <summary>
        ///  The IME Kana mode key.
        /// </summary>
        KanaMode = 0x15,

        /// <summary>
        ///  The IME Hanguel mode key.
        /// </summary>
        HanguelMode = 0x15,

        /// <summary>
        ///  The IME Hangul mode key.
        /// </summary>
        HangulMode = 0x15,

        /// <summary>
        ///  The IME Junja mode key.
        /// </summary>
        JunjaMode = 0x17,

        /// <summary>
        ///  The IME Final mode key.
        /// </summary>
        FinalMode = 0x18,

        /// <summary>
        ///  The IME Hanja mode key.
        /// </summary>
        HanjaMode = 0x19,

        /// <summary>
        ///  The IME Kanji mode key.
        /// </summary>
        KanjiMode = 0x19,

        /// <summary>
        ///  The ESC key.
        /// </summary>
        Escape = 0x1B,

        /// <summary>
        ///  The IME Convert key.
        /// </summary>
        IMEConvert = 0x1C,

        /// <summary>
        ///  The IME NonConvert key.
        /// </summary>
        IMENonconvert = 0x1D,

        /// <summary>
        ///  The IME Accept key.
        /// </summary>
        IMEAccept = 0x1E,

        /// <summary>
        ///  The IME Accept key.
        /// </summary>
        IMEAceept = IMEAccept,

        /// <summary>
        ///  The IME Mode change request.
        /// </summary>
        IMEModeChange = 0x1F,

        /// <summary>
        ///  The SPACEBAR key.
        /// </summary>
        Space = 0x20,

        /// <summary>
        ///  The PAGE UP key.
        /// </summary>
        Prior = 0x21,

        /// <summary>
        ///  The PAGE UP key.
        /// </summary>
        PageUp = Prior,

        /// <summary>
        ///  The PAGE DOWN key.
        /// </summary>
        Next = 0x22,

        /// <summary>
        ///  The PAGE DOWN key.
        /// </summary>
        PageDown = Next,

        /// <summary>
        ///  The END key.
        /// </summary>
        End = 0x23,

        /// <summary>
        ///  The HOME key.
        /// </summary>
        Home = 0x24,

        /// <summary>
        ///  The LEFT ARROW key.
        /// </summary>
        Left = 0x25,

        /// <summary>
        ///  The UP ARROW key.
        /// </summary>
        Up = 0x26,

        /// <summary>
        ///  The RIGHT ARROW key.
        /// </summary>
        Right = 0x27,

        /// <summary>
        ///  The DOWN ARROW key.
        /// </summary>
        Down = 0x28,

        /// <summary>
        ///  The SELECT key.
        /// </summary>
        Select = 0x29,

        /// <summary>
        ///  The PRINT key.
        /// </summary>
        Print = 0x2A,

        /// <summary>
        ///  The EXECUTE key.
        /// </summary>
        Execute = 0x2B,

        /// <summary>
        ///  The PRINT SCREEN key.
        /// </summary>
        Snapshot = 0x2C,

        /// <summary>
        ///  The PRINT SCREEN key.
        /// </summary>
        PrintScreen = Snapshot,

        /// <summary>
        ///  The INS key.
        /// </summary>
        Insert = 0x2D,

        /// <summary>
        ///  The DEL key.
        /// </summary>
        Delete = 0x2E,

        /// <summary>
        ///  The HELP key.
        /// </summary>
        Help = 0x2F,

        /// <summary>
        ///  The 0 key.
        /// </summary>
        D0 = 0x30, // 0

        /// <summary>
        ///  The 1 key.
        /// </summary>
        D1 = 0x31, // 1

        /// <summary>
        ///  The 2 key.
        /// </summary>
        D2 = 0x32, // 2

        /// <summary>
        ///  The 3 key.
        /// </summary>
        D3 = 0x33, // 3

        /// <summary>
        ///  The 4 key.
        /// </summary>
        D4 = 0x34, // 4

        /// <summary>
        ///  The 5 key.
        /// </summary>
        D5 = 0x35, // 5

        /// <summary>
        ///  The 6 key.
        /// </summary>
        D6 = 0x36, // 6

        /// <summary>
        ///  The 7 key.
        /// </summary>
        D7 = 0x37, // 7

        /// <summary>
        ///  The 8 key.
        /// </summary>
        D8 = 0x38, // 8

        /// <summary>
        ///  The 9 key.
        /// </summary>
        D9 = 0x39, // 9

        /// <summary>
        ///  The A key.
        /// </summary>
        A = 0x41,

        /// <summary>
        ///  The B key.
        /// </summary>
        B = 0x42,

        /// <summary>
        ///  The C key.
        /// </summary>
        C = 0x43,

        /// <summary>
        ///  The D key.
        /// </summary>
        D = 0x44,

        /// <summary>
        ///  The E key.
        /// </summary>
        E = 0x45,

        /// <summary>
        ///  The F key.
        /// </summary>
        F = 0x46,

        /// <summary>
        ///  The G key.
        /// </summary>
        G = 0x47,

        /// <summary>
        ///  The H key.
        /// </summary>
        H = 0x48,

        /// <summary>
        ///  The I key.
        /// </summary>
        I = 0x49,

        /// <summary>
        ///  The J key.
        /// </summary>
        J = 0x4A,

        /// <summary>
        ///  The K key.
        /// </summary>
        K = 0x4B,

        /// <summary>
        ///  The L key.
        /// </summary>
        L = 0x4C,

        /// <summary>
        ///  The M key.
        /// </summary>
        M = 0x4D,

        /// <summary>
        ///  The N key.
        /// </summary>
        N = 0x4E,

        /// <summary>
        ///  The O key.
        /// </summary>
        O = 0x4F,

        /// <summary>
        ///  The P key.
        /// </summary>
        P = 0x50,

        /// <summary>
        ///  The Q key.
        /// </summary>
        Q = 0x51,

        /// <summary>
        ///  The R key.
        /// </summary>
        R = 0x52,

        /// <summary>
        ///  The S key.
        /// </summary>
        S = 0x53,

        /// <summary>
        ///  The T key.
        /// </summary>
        T = 0x54,

        /// <summary>
        ///  The U key.
        /// </summary>
        U = 0x55,

        /// <summary>
        ///  The V key.
        /// </summary>
        V = 0x56,

        /// <summary>
        ///  The W key.
        /// </summary>
        W = 0x57,

        /// <summary>
        ///  The X key.
        /// </summary>
        X = 0x58,

        /// <summary>
        ///  The Y key.
        /// </summary>
        Y = 0x59,

        /// <summary>
        ///  The Z key.
        /// </summary>
        Z = 0x5A,

        /// <summary>
        ///  The left Windows logo key (Microsoft Natural Keyboard).
        /// </summary>
        LWin = 0x5B,

        /// <summary>
        ///  The right Windows logo key (Microsoft Natural Keyboard).
        /// </summary>
        RWin = 0x5C,

        /// <summary>
        ///  The Application key (Microsoft Natural Keyboard).
        /// </summary>
        Apps = 0x5D,

        /// <summary>
        ///  The Computer Sleep key.
        /// </summary>
        Sleep = 0x5F,

        /// <summary>
        ///  The 0 key on the numeric keypad.
        /// </summary>
        NumPad0 = 0x60,

        /// <summary>
        ///  The 1 key on the numeric keypad.
        /// </summary>
        NumPad1 = 0x61,

        /// <summary>
        ///  The 2 key on the numeric keypad.
        /// </summary>
        NumPad2 = 0x62,

        /// <summary>
        ///  The 3 key on the numeric keypad.
        /// </summary>
        NumPad3 = 0x63,

        /// <summary>
        ///  The 4 key on the numeric keypad.
        /// </summary>
        NumPad4 = 0x64,

        /// <summary>
        ///  The 5 key on the numeric keypad.
        /// </summary>
        NumPad5 = 0x65,

        /// <summary>
        ///  The 6 key on the numeric keypad.
        /// </summary>
        NumPad6 = 0x66,

        /// <summary>
        ///  The 7 key on the numeric keypad.
        /// </summary>
        NumPad7 = 0x67,

        /// <summary>
        ///  The 8 key on the numeric keypad.
        /// </summary>
        NumPad8 = 0x68,

        /// <summary>
        ///  The 9 key on the numeric keypad.
        /// </summary>
        NumPad9 = 0x69,

        /// <summary>
        ///  The Multiply key.
        /// </summary>
        Multiply = 0x6A,

        /// <summary>
        ///  The Add key.
        /// </summary>
        Add = 0x6B,

        /// <summary>
        ///  The Separator key.
        /// </summary>
        Separator = 0x6C,

        /// <summary>
        ///  The Subtract key.
        /// </summary>
        Subtract = 0x6D,

        /// <summary>
        ///  The Decimal key.
        /// </summary>
        Decimal = 0x6E,

        /// <summary>
        ///  The Divide key.
        /// </summary>
        Divide = 0x6F,

        /// <summary>
        ///  The F1 key.
        /// </summary>
        F1 = 0x70,

        /// <summary>
        ///  The F2 key.
        /// </summary>
        F2 = 0x71,

        /// <summary>
        ///  The F3 key.
        /// </summary>
        F3 = 0x72,

        /// <summary>
        ///  The F4 key.
        /// </summary>
        F4 = 0x73,

        /// <summary>
        ///  The F5 key.
        /// </summary>
        F5 = 0x74,

        /// <summary>
        ///  The F6 key.
        /// </summary>
        F6 = 0x75,

        /// <summary>
        ///  The F7 key.
        /// </summary>
        F7 = 0x76,

        /// <summary>
        ///  The F8 key.
        /// </summary>
        F8 = 0x77,

        /// <summary>
        ///  The F9 key.
        /// </summary>
        F9 = 0x78,

        /// <summary>
        ///  The F10 key.
        /// </summary>
        F10 = 0x79,

        /// <summary>
        ///  The F11 key.
        /// </summary>
        F11 = 0x7A,

        /// <summary>
        ///  The F12 key.
        /// </summary>
        F12 = 0x7B,

        /// <summary>
        ///  The F13 key.
        /// </summary>
        F13 = 0x7C,

        /// <summary>
        ///  The F14 key.
        /// </summary>
        F14 = 0x7D,

        /// <summary>
        ///  The F15 key.
        /// </summary>
        F15 = 0x7E,

        /// <summary>
        ///  The F16 key.
        /// </summary>
        F16 = 0x7F,

        /// <summary>
        ///  The F17 key.
        /// </summary>
        F17 = 0x80,

        /// <summary>
        ///  The F18 key.
        /// </summary>
        F18 = 0x81,

        /// <summary>
        ///  The F19 key.
        /// </summary>
        F19 = 0x82,

        /// <summary>
        ///  The F20 key.
        /// </summary>
        F20 = 0x83,

        /// <summary>
        ///  The F21 key.
        /// </summary>
        F21 = 0x84,

        /// <summary>
        ///  The F22 key.
        /// </summary>
        F22 = 0x85,

        /// <summary>
        ///  The F23 key.
        /// </summary>
        F23 = 0x86,

        /// <summary>
        ///  The F24 key.
        /// </summary>
        F24 = 0x87,

        /// <summary>
        ///  The NUM LOCK key.
        /// </summary>
        NumLock = 0x90,

        /// <summary>
        ///  The SCROLL LOCK key.
        /// </summary>
        Scroll = 0x91,

        /// <summary>
        ///  The left SHIFT key.
        /// </summary>
        LShiftKey = 0xA0,

        /// <summary>
        ///  The right SHIFT key.
        /// </summary>
        RShiftKey = 0xA1,

        /// <summary>
        ///  The left CTRL key.
        /// </summary>
        LControlKey = 0xA2,

        /// <summary>
        ///  The right CTRL key.
        /// </summary>
        RControlKey = 0xA3,

        /// <summary>
        ///  The left ALT key.
        /// </summary>
        LMenu = 0xA4,

        /// <summary>
        ///  The right ALT key.
        /// </summary>
        RMenu = 0xA5,

        /// <summary>
        ///  The Browser Back key.
        /// </summary>
        BrowserBack = 0xA6,

        /// <summary>
        ///  The Browser Forward key.
        /// </summary>
        BrowserForward = 0xA7,

        /// <summary>
        ///  The Browser Refresh key.
        /// </summary>
        BrowserRefresh = 0xA8,

        /// <summary>
        ///  The Browser Stop key.
        /// </summary>
        BrowserStop = 0xA9,

        /// <summary>
        ///  The Browser Search key.
        /// </summary>
        BrowserSearch = 0xAA,

        /// <summary>
        ///  The Browser Favorites key.
        /// </summary>
        BrowserFavorites = 0xAB,

        /// <summary>
        ///  The Browser Home key.
        /// </summary>
        BrowserHome = 0xAC,

        /// <summary>
        ///  The Volume Mute key.
        /// </summary>
        VolumeMute = 0xAD,

        /// <summary>
        ///  The Volume Down key.
        /// </summary>
        VolumeDown = 0xAE,

        /// <summary>
        ///  The Volume Up key.
        /// </summary>
        VolumeUp = 0xAF,

        /// <summary>
        ///  The Media Next Track key.
        /// </summary>
        MediaNextTrack = 0xB0,

        /// <summary>
        ///  The Media Previous Track key.
        /// </summary>
        MediaPreviousTrack = 0xB1,

        /// <summary>
        ///  The Media Stop key.
        /// </summary>
        MediaStop = 0xB2,

        /// <summary>
        ///  The Media Play Pause key.
        /// </summary>
        MediaPlayPause = 0xB3,

        /// <summary>
        ///  The Launch Mail key.
        /// </summary>
        LaunchMail = 0xB4,

        /// <summary>
        ///  The Select Media key.
        /// </summary>
        SelectMedia = 0xB5,

        /// <summary>
        ///  The Launch Application1 key.
        /// </summary>
        LaunchApplication1 = 0xB6,

        /// <summary>
        ///  The Launch Application2 key.
        /// </summary>
        LaunchApplication2 = 0xB7,

        /// <summary>
        ///  The Oem Semicolon key.
        /// </summary>
        OemSemicolon = 0xBA,

        /// <summary>
        ///  The Oem 1 key.
        /// </summary>
        Oem1 = OemSemicolon,

        /// <summary>
        ///  The Oem plus key.
        /// </summary>
        Oemplus = 0xBB,

        /// <summary>
        ///  The Oem comma key.
        /// </summary>
        Oemcomma = 0xBC,

        /// <summary>
        ///  The Oem Minus key.
        /// </summary>
        OemMinus = 0xBD,

        /// <summary>
        ///  The Oem Period key.
        /// </summary>
        OemPeriod = 0xBE,

        /// <summary>
        ///  The Oem Question key.
        /// </summary>
        OemQuestion = 0xBF,

        /// <summary>
        ///  The Oem 2 key.
        /// </summary>
        Oem2 = OemQuestion,

        /// <summary>
        ///  The Oem tilde key.
        /// </summary>
        Oemtilde = 0xC0,

        /// <summary>
        ///  The Oem 3 key.
        /// </summary>
        Oem3 = Oemtilde,

        /// <summary>
        ///  The Oem Open Brackets key.
        /// </summary>
        OemOpenBrackets = 0xDB,

        /// <summary>
        ///  The Oem 4 key.
        /// </summary>
        Oem4 = OemOpenBrackets,

        /// <summary>
        ///  The Oem Pipe key.
        /// </summary>
        OemPipe = 0xDC,

        /// <summary>
        ///  The Oem 5 key.
        /// </summary>
        Oem5 = OemPipe,

        /// <summary>
        ///  The Oem Close Brackets key.
        /// </summary>
        OemCloseBrackets = 0xDD,

        /// <summary>
        ///  The Oem 6 key.
        /// </summary>
        Oem6 = OemCloseBrackets,

        /// <summary>
        ///  The Oem Quotes key.
        /// </summary>
        OemQuotes = 0xDE,

        /// <summary>
        ///  The Oem 7 key.
        /// </summary>
        Oem7 = OemQuotes,

        /// <summary>
        ///  The Oem8 key.
        /// </summary>
        Oem8 = 0xDF,

        /// <summary>
        ///  The Oem Backslash key.
        /// </summary>
        OemBackslash = 0xE2,

        /// <summary>
        ///  The Oem 102 key.
        /// </summary>
        Oem102 = OemBackslash,

        /// <summary>
        ///  The PROCESS KEY key.
        /// </summary>
        ProcessKey = 0xE5,

        /// <summary>
        ///  The Packet KEY key.
        /// </summary>
        Packet = 0xE7,

        /// <summary>
        ///  The ATTN key.
        /// </summary>
        Attn = 0xF6,

        /// <summary>
        ///  The CRSEL key.
        /// </summary>
        Crsel = 0xF7,

        /// <summary>
        ///  The EXSEL key.
        /// </summary>
        Exsel = 0xF8,

        /// <summary>
        ///  The ERASE EOF key.
        /// </summary>
        EraseEof = 0xF9,

        /// <summary>
        ///  The PLAY key.
        /// </summary>
        Play = 0xFA,

        /// <summary>
        ///  The ZOOM key.
        /// </summary>
        Zoom = 0xFB,

        /// <summary>
        ///  A constant reserved for future use.
        /// </summary>
        NoName = 0xFC,

        /// <summary>
        ///  The PA1 key.
        /// </summary>
        Pa1 = 0xFD,

        /// <summary>
        ///  The CLEAR key.
        /// </summary>
        OemClear = 0xFE,

        /// <summary>
        ///  The SHIFT modifier key.
        /// </summary>
        Shift = 0x00010000,

        /// <summary>
        ///  The  CTRL modifier key.
        /// </summary>
        Control = 0x00020000,

        /// <summary>
        ///  The ALT modifier key.
        /// </summary>
        Alt = 0x00040000,
    }

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