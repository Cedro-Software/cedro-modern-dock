using System.Runtime.InteropServices;
using System.Text;

namespace CedroModernDock.Infrastructure.Windows.Native;

/// <summary>
/// Win32 constants and P/Invoke declarations used by the dock's native interop layer.
/// Direct equivalent of the JNA calls in the original Java project's NativeWindowUtils
/// and WindowsIconHandler, now expressed as idiomatic .NET P/Invoke.
/// </summary>
public static class Win32Constants
{
    // Window style indices
    public const int GWL_EXSTYLE = -20;
    public const int GWLP_USERDATA = -21;

    // Extended window styles
    public const int WS_EX_TOOLWINDOW = 0x00000080;
    public const int WS_EX_NOACTIVATE = 0x08000000;
    public const int WS_EX_TOPMOST = 0x00000008;
    public const int WS_EX_APPWINDOW = 0x00040000;
    public const int WS_EX_LAYERED = 0x00080000;

    // SetLayeredWindowAttributes flags
    public const uint LWA_ALPHA = 0x2;

    // ShowWindow commands
    public const int SW_RESTORE = 9;
    public const int SW_SHOW = 5;
    public const int SW_HIDE = 0;

    // SetWindowPos flags
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOACTIVATE = 0x0010;
    public const uint SWP_SHOWWINDOW = 0x0040;
    public static readonly IntPtr HWND_TOPMOST = new(-1);
    public static readonly IntPtr HWND_NOTOPMOST = new(-2);

    // SendMessageTimeout flags
    public const uint SMTO_ABORTIFHUNG = 0x0002;

    // Undocumented message that makes Progman create the WorkerW hosting the
    // desktop icons (the standard trick used by desktop widgets).
    public const uint WM_PROGMAN_CREATE_DESKTOP = 0x052C;

    // Window messages
    public const int WM_SYSCOMMAND = 0x0112;
    public const int WM_SIZE = 0x0005;
    public const int SC_MINIMIZE = 0xF020;
    public const int SIZE_MINIMIZED = 0;
    public const int WM_GETICON = 0x007F;
    public const int WM_CLOSE = 0x0010;
    public const int WM_LBUTTONUP = 0x0202;
    public const int WM_MOUSEMOVE = 0x0200;
    public const int WM_MOUSELEAVE = 0x02A3;
    public const int WM_PAINT = 0x000F;
    public const int WM_ERASEBKGND = 0x0014;
    public const uint TME_LEAVE = 0x00000002;

    // WM_GETICON icon size selectors
    public const IntPtr ICON_BIG = 1;
    public const IntPtr ICON_SMALL2 = 2;

    // GetClassLongPtr indices
    public const int GCLP_HICON = -14;

    // GetWindow commands
    public const int GW_OWNER = 4;

    // Window styles
    public const uint WS_POPUP = 0x80000000;
    public const uint WS_VISIBLE = 0x10000000;

    // Process access rights
    public const uint PROCESS_QUERY_LIMITED_INFORMATION = 0x1000;

    // Shell hook notification codes
    public const int HSHELL_WINDOWACTIVATED = 4;
    public const int HSHELL_RUDEAPPACTIVATED = 0x8004;
}

/// <summary>Callback for EnumWindows.</summary>
public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

/// <summary>Native POINT structure.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct POINT
{
    public int X;
    public int Y;
}

/// <summary>Native TRACKMOUSEEVENT structure for mouse-leave notifications.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct TRACKMOUSEEVENT
{
    public uint cbSize;
    public uint dwFlags;
    public IntPtr hwndTrack;
    public uint dwHoverTime;
}

/// <summary>Native RECT structure.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct RECT
{
    public int Left;
    public int Top;
    public int Right;
    public int Bottom;
}

/// <summary>Native PAINTSTRUCT used with BeginPaint/EndPaint.</summary>
[StructLayout(LayoutKind.Sequential)]
public struct PAINTSTRUCT
{
    public IntPtr hdc;
    public bool fErase;
    public RECT rcPaint;
    public bool fRestore;
    public bool fIncUpdate;
    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
    public byte[] rgbReserved;
}

/// <summary>Callback for window class WndProc.</summary>
public delegate IntPtr WndProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

/// <summary>Native WNDCLASS structure (Unicode).</summary>
[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
public struct WNDCLASS
{
    public uint style;
    public IntPtr lpfnWndProc;
    public int cbClsExtra;
    public int cbWndExtra;
    public IntPtr hInstance;
    public IntPtr hIcon;
    public IntPtr hCursor;
    public IntPtr hbrBackground;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszMenuName;
    [MarshalAs(UnmanagedType.LPWStr)] public string? lpszClassName;
}

/// <summary>Callback for SetWindowSubclass (comctl32 window subclassing).</summary>
internal delegate IntPtr SubclassProc(
    IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
    UIntPtr uIdSubclass, IntPtr dwRefData);

public static class User32
{
    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumWindows(EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc enumProc, IntPtr lParam);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr FindWindowEx(
        IntPtr hWndParent, IntPtr hWndChildAfter, string? lpszClass, string? lpszWindow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out UIntPtr lpdwResult);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr GetWindow(IntPtr hWnd, int uCmd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool IsWindow(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter,
        int X, int Y, int cx, int cy, uint uFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SendMessage(IntPtr hWnd, int Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool PostMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool TrackMouseEvent(ref TRACKMOUSEEVENT lpEventTrack);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr LoadCursor(IntPtr hInstance, IntPtr lpCursorName);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr SetCursor(IntPtr hCursor);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetCursorPos(out POINT lpPoint);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT lpRect);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern ushort RegisterClass(WNDCLASS lpWndClass);

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr CreateWindowEx(uint dwExStyle, string lpClassName,
        string lpWindowName, uint dwStyle, int x, int y, int nWidth, int nHeight,
        IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern IntPtr DefWindowProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int cx, int cy);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr CreateEllipticRgn(int nLeftRect, int nTopRect, int nRightRect, int nBottomRect);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern int SetWindowRgn(IntPtr hWnd, IntPtr hRgn, bool bRedraw);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool DestroyWindow(IntPtr hWnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool SetLayeredWindowAttributes(
        IntPtr hWnd, uint crKey, byte bAlpha, uint dwFlags);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern bool RegisterShellHookWindow(IntPtr hwnd);

    [DllImport("user32.dll", SetLastError = true)]
    public static extern uint RegisterWindowMessage(string lpString);

    // GetWindowLongPtr / SetWindowLongPtr — handle 32/64-bit transparently
    public static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex)
                            : new IntPtr(GetWindowLong32(hWnd, nIndex));

    public static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        => IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                            : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));

    public static IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex)
        => IntPtr.Size == 8 ? GetClassLongPtr64(hWnd, nIndex)
                            : new IntPtr(GetClassLong32(hWnd, nIndex));

    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetWindowLong", SetLastError = true)]
    private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
    private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
    private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtr", SetLastError = true)]
    private static extern IntPtr GetClassLongPtr64(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", EntryPoint = "GetClassLong", SetLastError = true)]
    private static extern int GetClassLong32(IntPtr hWnd, int nIndex);
}

internal static class Kernel32
{
    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern IntPtr OpenProcess(uint dwDesiredAccess, bool bInheritHandle, uint dwProcessId);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern bool QueryFullProcessImageName(
        IntPtr hProcess, uint dwFlags, StringBuilder lpExeName, ref uint lpdwSize);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool CloseHandle(IntPtr hObject);

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    public static extern IntPtr GetModuleHandle(string? lpModuleName);
}

internal static class Comctl32
{
    [DllImport("comctl32.dll", SetLastError = true)]
    public static extern bool SetWindowSubclass(
        IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass, IntPtr dwRefData);

    [DllImport("comctl32.dll", SetLastError = true)]
    public static extern bool RemoveWindowSubclass(
        IntPtr hWnd, SubclassProc pfnSubclass, UIntPtr uIdSubclass);

    [DllImport("comctl32.dll", SetLastError = true)]
    public static extern IntPtr DefSubclassProc(IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam);
}

internal static class Gdi32
{
    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern IntPtr SelectObject(IntPtr hdc, IntPtr hObject);

    [DllImport("gdi32.dll", SetLastError = true)]
    public static extern bool DeleteObject(IntPtr hObject);
}
