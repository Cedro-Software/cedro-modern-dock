namespace CedroModernDock.Infrastructure.Windows.Native;

using System.Diagnostics;
using System.Runtime.InteropServices;

/// <summary>A passive mouse-button observer, installed only while a dock menu is open.</summary>
public sealed class DesktopMouseClickObserver : IDisposable
{
    private readonly HookProc _callback;
    private readonly Action<int, int> _onClick;
    private IntPtr _hook;

    private DesktopMouseClickObserver(Action<int, int> onClick)
    {
        _onClick = onClick;
        _callback = OnMouseMessage;
        _hook = SetWindowsHookEx(14 /* WH_MOUSE_LL */, _callback, Kernel32.GetModuleHandle(null), 0);
    }

    public static DesktopMouseClickObserver? TryCreate(Action<int, int> onClick)
    {
        var observer = new DesktopMouseClickObserver(onClick);
        if (observer._hook != IntPtr.Zero) return observer;
        Debug.WriteLine($"Could not observe desktop clicks: {Marshal.GetLastWin32Error()}");
        return null;
    }

    private IntPtr OnMouseMessage(int code, IntPtr message, IntPtr data)
    {
        if (code >= 0 && message.ToInt64() is 0x0201 or 0x0204 or 0x0207 or 0x020B)
        {
            // MSLLHOOKSTRUCT starts with POINT. The callback only queues work;
            // hit testing and menu closing run later on the UI dispatcher.
            var point = Marshal.PtrToStructure<POINT>(data);
            try { _onClick(point.X, point.Y); }
            catch (Exception e) { Debug.WriteLine($"Desktop click notification failed: {e.Message}"); }
        }
        return CallNextHookEx(IntPtr.Zero, code, message, data);
    }

    public void Dispose()
    {
        if (_hook == IntPtr.Zero) return;
        UnhookWindowsHookEx(_hook);
        _hook = IntPtr.Zero;
        GC.KeepAlive(_callback);
    }

    private delegate IntPtr HookProc(int code, IntPtr message, IntPtr data);

    [DllImport("user32.dll", EntryPoint = "SetWindowsHookExW", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int hookType, HookProc callback, IntPtr module, uint threadId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool UnhookWindowsHookEx(IntPtr hook);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hook, int code, IntPtr message, IntPtr data);
}
