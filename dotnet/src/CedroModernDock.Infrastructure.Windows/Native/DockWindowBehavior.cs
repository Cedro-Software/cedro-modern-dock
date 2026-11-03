namespace CedroModernDock.Infrastructure.Windows.Native;

using System;
using System.Text;

/// <summary>
/// Applies dock-specific Win32 window behavior that Avalonia alone cannot provide:
/// <list type="bullet">
/// <item><b>WS_EX_NOACTIVATE | WS_EX_TOOLWINDOW</b> — the dock never steals focus
/// and never appears in the taskbar or Alt-Tab cycle.</item>
/// <item><b>Desktop attachment</b> — the dock becomes an owned window of the
/// desktop (the WorkerW hosting the desktop icons), so it sticks to the
/// desktop: Win+D "Show Desktop" keeps it visible, maximized/normal windows
/// cover it, and it is never part of the minimize-all cycle.</item>
/// <item><b>Subclass</b> — defense in depth: intercepts minimize commands
/// (SC_MINIMIZE, SIZE_MINIMIZED) in case a shell path minimizes it anyway.</item>
/// </list>
/// </summary>
public sealed class DockWindowBehavior : IDisposable
{
    private IntPtr _hwnd;
    private readonly Action<string>? _onStatus;
    private SubclassProc? _subclassProc; // kept alive to prevent GC of the native callback
    private bool _subclassed;

    public DockWindowBehavior(IntPtr hwnd, Action<string>? onStatus = null)
    {
        _hwnd = hwnd;
        _onStatus = onStatus;
    }

    public void Apply(bool autoHide = false)
    {
        if (_hwnd == IntPtr.Zero)
        {
            _onStatus?.Invoke("ERROR: HWND is zero — window not opened yet?");
            return;
        }

        ApplyExtendedStyles();

        if (autoHide)
            SetAutoHideMode(true);
        else
            AttachToDesktop();

        // Store the delegate in a field so it is not garbage-collected while
        // the native subclass is active (would cause a crash on next message).
        _subclassProc = new SubclassProc(HandleMessage);
        _subclassed = Comctl32.SetWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero, IntPtr.Zero);

        _onStatus?.Invoke($"HWND=0x{_hwnd:X} | Subclass={(_subclassed ? "OK" : "FAIL")}");
    }

    /// <summary>
    /// Switches between Topmost mode (for auto-hide so the dock pops up over windows)
    /// and Desktop attachment mode (sits behind normal windows on the desktop).
    /// </summary>
    public void SetAutoHideMode(bool autoHide)
    {
        if (_hwnd == IntPtr.Zero) return;

        if (autoHide)
        {
            User32.SetParent(_hwnd, IntPtr.Zero);
            User32.SetWindowPos(_hwnd, Win32Constants.HWND_TOPMOST, 0, 0, 0, 0,
                Win32Constants.SWP_NOMOVE | Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOACTIVATE | Win32Constants.SWP_SHOWWINDOW);
            _onStatus?.Invoke("Auto-hide mode: Topmost overlay");
        }
        else
        {
            User32.SetWindowPos(_hwnd, Win32Constants.HWND_NOTOPMOST, 0, 0, 0, 0,
                Win32Constants.SWP_NOMOVE | Win32Constants.SWP_NOSIZE | Win32Constants.SWP_NOACTIVATE);
            AttachToDesktop();
            _onStatus?.Invoke("Normal mode: Attached to desktop");
        }
    }

    private void ApplyExtendedStyles()
    {
        IntPtr exStylePtr = User32.GetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE);
        int exStyle = exStylePtr.ToInt32();

        exStyle |= Win32Constants.WS_EX_NOACTIVATE | Win32Constants.WS_EX_TOOLWINDOW;
        exStyle &= ~Win32Constants.WS_EX_APPWINDOW; // remove taskbar entry

        User32.SetWindowLongPtr(_hwnd, Win32Constants.GWL_EXSTYLE, new IntPtr(exStyle));
    }

    /// <summary>
    /// Parents the dock to the desktop icons window. The dock stays a popup
    /// window (screen coordinates and layered transparency are preserved), but
    /// being owned by the desktop it survives "Show Desktop" and sits behind
    /// every normal window — exactly like the desktop icons.
    /// </summary>
    private void AttachToDesktop()
    {
        IntPtr desktop = FindDesktopWindow();
        if (desktop == IntPtr.Zero)
        {
            _onStatus?.Invoke("AttachToDesktop: desktop window not found");
            return;
        }

        User32.SetParent(_hwnd, desktop);
        _onStatus?.Invoke($"Attached to desktop 0x{desktop:X}");
    }

    /// <summary>
    /// Locates the desktop window that hosts the desktop icons: the WorkerW
    /// containing a SHELLDLL_DefView child (created on demand by Progman).
    /// Falls back to Progman itself when no such WorkerW exists.
    /// </summary>
    private static IntPtr FindDesktopWindow()
    {
        IntPtr progman = User32.FindWindow("Progman", null);
        if (progman == IntPtr.Zero)
            return IntPtr.Zero;

        // Ask Progman to create the icons WorkerW if it does not exist yet.
        User32.SendMessageTimeout(progman, Win32Constants.WM_PROGMAN_CREATE_DESKTOP,
            IntPtr.Zero, IntPtr.Zero, Win32Constants.SMTO_ABORTIFHUNG, 1000, out _);

        IntPtr workerW = FindWorkerWWithIcons();
        return workerW != IntPtr.Zero ? workerW : progman;
    }

    private static IntPtr FindWorkerWWithIcons()
    {
        IntPtr found = IntPtr.Zero;
        User32.EnumWindows((hWnd, _) =>
        {
            var className = new StringBuilder(256);
            User32.GetClassName(hWnd, className, className.Capacity);
            if (className.ToString() == "WorkerW" &&
                User32.FindWindowEx(hWnd, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
            {
                found = hWnd;
                return false;
            }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private IntPtr HandleMessage(
        IntPtr hWnd, uint uMsg, IntPtr wParam, IntPtr lParam,
        UIntPtr uIdSubclass, IntPtr dwRefData)
    {
        // 1. Block explicit minimize (SC_MINIMIZE) — covers normal "Minimize"
        //    clicks and Win+D "Show Desktop" (which sends SC_MINIMIZE).
        if (uMsg == Win32Constants.WM_SYSCOMMAND)
        {
            int command = wParam.ToInt32() & 0xFFF0;
            if (command == Win32Constants.SC_MINIMIZE)
            {
                _onStatus?.Invoke("Blocked SC_MINIMIZE");
                return IntPtr.Zero; // swallow the message; do not minimize
            }
        }

        // 2. Win+D may also minimize windows through other paths. If we receive
        //    WM_SIZE with SIZE_MINIMIZED, undo it so the dock stays on the desktop.
        if (uMsg == Win32Constants.WM_SIZE && wParam.ToInt32() == Win32Constants.SIZE_MINIMIZED)
        {
            User32.ShowWindow(_hwnd, Win32Constants.SW_RESTORE);
            _onStatus?.Invoke("Restored from SIZE_MINIMIZED (Win+D defense)");
        }

        return Comctl32.DefSubclassProc(hWnd, uMsg, wParam, lParam);
    }

    public void Dispose()
    {
        if (_subclassed && _subclassProc != null && _hwnd != IntPtr.Zero)
        {
            Comctl32.RemoveWindowSubclass(_hwnd, _subclassProc, UIntPtr.Zero);
            _subclassed = false;
        }

        _subclassProc = null;
        _hwnd = IntPtr.Zero;
    }
}
