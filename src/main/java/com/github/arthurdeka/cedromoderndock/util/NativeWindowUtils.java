package com.github.arthurdeka.cedromoderndock.util;

import com.sun.jna.platform.win32.Kernel32;
import com.sun.jna.platform.win32.User32;
import com.sun.jna.Pointer;
import com.sun.jna.Native;
import com.sun.jna.win32.W32APIOptions;
import com.sun.jna.win32.StdCallLibrary;
import com.sun.jna.platform.win32.WinDef.DWORD;
import com.sun.jna.platform.win32.WinDef.LONG;
import com.sun.jna.platform.win32.WinUser;
import com.sun.jna.platform.win32.WinDef.HWND;
import com.sun.jna.platform.win32.WinNT.HANDLE;
import com.sun.jna.platform.win32.WinNT;
import com.sun.jna.ptr.IntByReference;
import javafx.application.Platform;
import javafx.stage.Stage;

import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.Executors;
import java.util.concurrent.ScheduledExecutorService;
import java.util.concurrent.TimeUnit;

public class NativeWindowUtils {
    private static final int EVENT_SYSTEM_FOREGROUND = 0x0003;
    private static final int EVENT_OBJECT_SHOW = 0x8002;
    private static final int EVENT_OBJECT_HIDE = 0x8003;
    private static final int OBJID_WINDOW = 0;
    private static final int WINEVENT_OUTOFCONTEXT = 0x0000;
    private static final int WINEVENT_SKIPOWNPROCESS = 0x0002;
    private static final int WS_EX_TOOLWINDOW = 0x00000080;
    private static final int WS_EX_APPWINDOW = 0x00040000;
    private static final Pointer HWND_BOTTOM = Pointer.createConstant(1);

    private interface User32Extra extends StdCallLibrary {
        User32Extra INSTANCE = Native.load("user32", User32Extra.class, W32APIOptions.DEFAULT_OPTIONS);

        boolean SetWindowPos(HWND hWnd, Pointer hWndInsertAfter, int X, int Y, int cx, int cy, int uFlags);
    }

    // Minimal info required by the popup to activate and label a window.
    public record WindowInfo(HWND hwnd, String title) {}

    public static void configureDesktopDockWindow(Stage stage) {
        if (stage == null || stage.getTitle() == null || stage.getTitle().isBlank()) {
            return;
        }

        HWND hwnd = findCurrentProcessWindow(stage.getTitle());
        if (hwnd == null) {
            return;
        }

        int extendedStyle = User32.INSTANCE.GetWindowLong(hwnd, WinUser.GWL_EXSTYLE);
        extendedStyle |= WS_EX_TOOLWINDOW;
        extendedStyle &= ~WS_EX_APPWINDOW;
        User32.INSTANCE.SetWindowLong(hwnd, WinUser.GWL_EXSTYLE, extendedStyle);

        int style = User32.INSTANCE.GetWindowLong(hwnd, WinUser.GWL_STYLE);
        style &= ~WinUser.WS_MINIMIZEBOX;
        User32.INSTANCE.SetWindowLong(hwnd, WinUser.GWL_STYLE, style);

        User32.INSTANCE.SetWindowPos(
                hwnd,
                null,
                0,
                0,
                0,
                0,
                WinUser.SWP_NOMOVE
                        | WinUser.SWP_NOSIZE
                        | WinUser.SWP_NOACTIVATE
                        | WinUser.SWP_FRAMECHANGED
                        | WinUser.SWP_NOZORDER
                        | WinUser.SWP_SHOWWINDOW
        );
    }

    public static AutoCloseable keepDockVisibleWithShowDesktop(Stage stage) {
        if (stage == null || stage.getTitle() == null || stage.getTitle().isBlank()) {
            return () -> {};
        }

        String title = stage.getTitle();
        DesktopDockWindowController controller = new DesktopDockWindowController(stage, title);
        controller.install();
        return controller;
    }

    public static void refreshDockZOrderForShowDesktop(Stage stage) {
        if (stage == null || stage.getTitle() == null || stage.getTitle().isBlank()) {
            return;
        }

        updateDockZOrderForDesktopState(stage, stage.getTitle(), false);
    }

    public static void disableShowDesktopProtection(Stage stage) {
        setStageAlwaysOnTop(stage, false);
        if (stage == null || stage.getTitle() == null || stage.getTitle().isBlank()) {
            return;
        }

        HWND hwnd = findCurrentProcessWindow(stage.getTitle());
        if (hwnd == null) {
            return;
        }

        User32Extra.INSTANCE.SetWindowPos(
                hwnd,
                HWND_BOTTOM,
                0,
                0,
                0,
                0,
                WinUser.SWP_NOMOVE
                        | WinUser.SWP_NOSIZE
                        | WinUser.SWP_NOACTIVATE
                        | WinUser.SWP_SHOWWINDOW
        );
    }

    private static class DesktopDockWindowController implements AutoCloseable {
        private final Stage stage;
        private final String dockTitle;
        private final User32.WinEventProc callback;
        private HANDLE foregroundHook;
        private HANDLE objectHook;
        private boolean showingDesktop;
        private long forceShowDesktopUntilNanos;
        private ScheduledExecutorService poller;

        private DesktopDockWindowController(Stage stage, String dockTitle) {
            this.stage = stage;
            this.dockTitle = dockTitle;
            this.callback = this::handleWinEvent;
        }

        private void install() {
            foregroundHook = User32.INSTANCE.SetWinEventHook(
                    EVENT_SYSTEM_FOREGROUND,
                    EVENT_SYSTEM_FOREGROUND,
                    null,
                    callback,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT
            );
            objectHook = User32.INSTANCE.SetWinEventHook(
                    EVENT_OBJECT_SHOW,
                    EVENT_OBJECT_HIDE,
                    null,
                    callback,
                    0,
                    0,
                    WINEVENT_OUTOFCONTEXT | WINEVENT_SKIPOWNPROCESS
            );
            poller = Executors.newSingleThreadScheduledExecutor(r -> {
                Thread thread = new Thread(r, "DesktopDockWindowWatcher");
                thread.setDaemon(true);
                return thread;
            });
            poller.scheduleWithFixedDelay(this::updateDockZOrder, 150, 150, TimeUnit.MILLISECONDS);
            updateDockZOrder();
        }

        private void handleWinEvent(
                HANDLE hWinEventHook,
                DWORD event,
                HWND hwnd,
                LONG idObject,
                LONG idChild,
                DWORD dwEventThread,
                DWORD dwmsEventTime
        ) {
            if (idObject.longValue() != OBJID_WINDOW) {
                return;
            }
            if (event.intValue() == EVENT_OBJECT_SHOW && isExternalAppWindow(hwnd)) {
                setStageAlwaysOnTop(stage, false);
            }
            updateDockZOrder();
        }

        private void updateDockZOrder() {
            boolean dockWasMinimized = restoreDockIfMinimized(dockTitle);
            if (dockWasMinimized) {
                forceShowDesktopUntilNanos = System.nanoTime() + TimeUnit.MILLISECONDS.toNanos(1200);
            }

            boolean forceShowDesktop = System.nanoTime() < forceShowDesktopUntilNanos;
            boolean desktopActive = updateDockZOrderForDesktopState(
                    stage,
                    dockTitle,
                    showingDesktop,
                    forceShowDesktop
            );
            if (desktopActive == showingDesktop) {
                return;
            }

            showingDesktop = desktopActive;
        }

        @Override
        public void close() {
            if (poller != null) {
                poller.shutdownNow();
                poller = null;
            }
            if (foregroundHook != null) {
                User32.INSTANCE.UnhookWinEvent(foregroundHook);
                foregroundHook = null;
            }
            if (objectHook != null) {
                User32.INSTANCE.UnhookWinEvent(objectHook);
                objectHook = null;
            }
            setStageAlwaysOnTop(stage, false);
        }
    }

    private static boolean updateDockZOrderForDesktopState(Stage stage, String dockTitle, boolean keepDesktopStateForDockFocus) {
        return updateDockZOrderForDesktopState(stage, dockTitle, keepDesktopStateForDockFocus, false);
    }

    private static boolean updateDockZOrderForDesktopState(
            Stage stage,
            String dockTitle,
            boolean keepDesktopStateForDockFocus,
            boolean forceShowDesktop
    ) {
        HWND dockHwnd = findCurrentProcessWindow(dockTitle);
        if (dockHwnd == null) {
            return false;
        }

        configureDesktopDockWindow(dockHwnd);
        boolean desktopActive = forceShowDesktop || isDesktopForeground(dockHwnd, keepDesktopStateForDockFocus);
        if (desktopActive) {
            User32.INSTANCE.ShowWindow(dockHwnd, WinUser.SW_RESTORE);
            setStageAlwaysOnTop(stage, true);
        } else {
            setStageAlwaysOnTop(stage, false);
            User32Extra.INSTANCE.SetWindowPos(
                    dockHwnd,
                    HWND_BOTTOM,
                    0,
                    0,
                    0,
                    0,
                    WinUser.SWP_NOMOVE
                            | WinUser.SWP_NOSIZE
                            | WinUser.SWP_NOACTIVATE
                            | WinUser.SWP_SHOWWINDOW
            );
        }
        return desktopActive;
    }

    private static void setStageAlwaysOnTop(Stage stage, boolean alwaysOnTop) {
        if (stage == null) {
            return;
        }

        Runnable action = () -> {
            if (stage.isAlwaysOnTop() == alwaysOnTop) {
                return;
            }
            stage.setAlwaysOnTop(alwaysOnTop);
        };

        if (Platform.isFxApplicationThread()) {
            action.run();
        } else {
            Platform.runLater(action);
        }
    }

    private static boolean restoreDockIfMinimized(String dockTitle) {
        HWND dockHwnd = findCurrentProcessWindow(dockTitle);
        if (dockHwnd == null || !isWindowMinimized(dockHwnd)) {
            return false;
        }

        User32.INSTANCE.ShowWindow(dockHwnd, WinUser.SW_RESTORE);
        return true;
    }

    private static boolean isWindowMinimized(HWND hwnd) {
        WinUser.WINDOWPLACEMENT placement = new WinUser.WINDOWPLACEMENT();
        if (!User32.INSTANCE.GetWindowPlacement(hwnd, placement).booleanValue()) {
            return false;
        }

        return placement.showCmd == WinUser.SW_SHOWMINIMIZED
                || placement.showCmd == WinUser.SW_SHOWMINNOACTIVE
                || placement.showCmd == WinUser.SW_MINIMIZE;
    }

    private static boolean isDesktopForeground(HWND dockHwnd, boolean keepDesktopStateForDockFocus) {
        HWND foreground = User32.INSTANCE.GetForegroundWindow();
        if (foreground == null) {
            return false;
        }
        if (foreground.equals(dockHwnd)) {
            return keepDesktopStateForDockFocus;
        }

        return isDesktopWindow(foreground) || isDesktopWindow(User32.INSTANCE.GetAncestor(foreground, WinUser.GA_ROOT));
    }

    private static void configureDesktopDockWindow(HWND hwnd) {
        int extendedStyle = User32.INSTANCE.GetWindowLong(hwnd, WinUser.GWL_EXSTYLE);
        extendedStyle |= WS_EX_TOOLWINDOW;
        extendedStyle &= ~WS_EX_APPWINDOW;
        User32.INSTANCE.SetWindowLong(hwnd, WinUser.GWL_EXSTYLE, extendedStyle);
        User32.INSTANCE.SetWindowPos(
                hwnd,
                null,
                0,
                0,
                0,
                0,
                WinUser.SWP_NOMOVE
                        | WinUser.SWP_NOSIZE
                        | WinUser.SWP_NOACTIVATE
                        | WinUser.SWP_FRAMECHANGED
                        | WinUser.SWP_NOZORDER
                        | WinUser.SWP_SHOWWINDOW
        );
    }

    private static boolean isDesktopWindow(HWND hwnd) {
        String className = getWindowClassName(hwnd);
        return "Progman".equals(className)
                || "WorkerW".equals(className)
                || "SHELLDLL_DefView".equals(className);
    }

    private static boolean isExternalAppWindow(HWND hwnd) {
        if (hwnd == null || !User32.INSTANCE.IsWindowVisible(hwnd)) {
            return false;
        }

        HWND root = User32.INSTANCE.GetAncestor(hwnd, WinUser.GA_ROOT);
        if (isDesktopWindow(hwnd) || isDesktopWindow(root)) {
            return false;
        }

        IntByReference pidRef = new IntByReference();
        User32.INSTANCE.GetWindowThreadProcessId(root == null ? hwnd : root, pidRef);
        return pidRef.getValue() != Kernel32.INSTANCE.GetCurrentProcessId();
    }

    private static String getWindowClassName(HWND hwnd) {
        if (hwnd == null) {
            return "";
        }
        char[] className = new char[256];
        int length = User32.INSTANCE.GetClassName(hwnd, className, className.length);
        if (length <= 0) {
            return "";
        }
        return new String(className, 0, length);
    }

    private static HWND findCurrentProcessWindow(String title) {
        int currentProcessId = Kernel32.INSTANCE.GetCurrentProcessId();
        HWND[] foundWindow = new HWND[1];

        User32.INSTANCE.EnumWindows((hWnd, arg1) -> {
            if (!User32.INSTANCE.IsWindowVisible(hWnd)) {
                return true;
            }

            IntByReference pidRef = new IntByReference();
            User32.INSTANCE.GetWindowThreadProcessId(hWnd, pidRef);
            if (pidRef.getValue() != currentProcessId) {
                return true;
            }

            char[] buffer = new char[1024];
            User32.INSTANCE.GetWindowText(hWnd, buffer, buffer.length);
            String windowTitle = new String(buffer).trim();
            if (title.equals(windowTitle)) {
                foundWindow[0] = hWnd;
                return false;
            }
            return true;
        }, null);

        return foundWindow[0];
    }

    public static List<WindowInfo> getOpenWindows(String executablePath) {
        List<WindowInfo> windows = new ArrayList<>();
        if (executablePath == null || executablePath.isEmpty()) {
            return windows;
        }

        // Normalize the target executable path so comparisons are stable.
        final Path targetPath = Paths.get(executablePath).toAbsolutePath().normalize();

        User32.INSTANCE.EnumWindows((hWnd, arg1) -> {
            if (User32.INSTANCE.IsWindowVisible(hWnd)) {
                char[] buffer = new char[1024];
                User32.INSTANCE.GetWindowText(hWnd, buffer, 1024);
                String title = new String(buffer).trim();

                // Skip windows without title or hidden ones (some invisible windows report visible but have empty title/rect).
                if (title.isEmpty()) {
                    return true;
                }

                // Keep only windows belonging to the executable path requested.
                if (isWindowFromExecutable(hWnd, targetPath)) {
                    windows.add(new WindowInfo(hWnd, title));
                }
            }
            return true;
        }, null);

        return windows;
    }

    private static boolean isWindowFromExecutable(HWND hWnd, Path targetPath) {
        IntByReference pidRef = new IntByReference();
        User32.INSTANCE.GetWindowThreadProcessId(hWnd, pidRef);
        int pid = pidRef.getValue();

        // Query the process image path to match it against the target executable.
        WinNT.HANDLE process = Kernel32.INSTANCE.OpenProcess(
                WinNT.PROCESS_QUERY_LIMITED_INFORMATION,
                false,
                pid
        );

        if (process != null) {
            try {
                char[] pathBuffer = new char[1024];
                IntByReference size = new IntByReference(pathBuffer.length);
                if (Kernel32.INSTANCE.QueryFullProcessImageName(process, 0, pathBuffer, size)) {
                    String processPathStr = new String(pathBuffer, 0, size.getValue());
                    Path processPath = Paths.get(processPathStr).toAbsolutePath().normalize();
                    // Prefer full path match, but fall back to filename match for edge cases.
                    if (isSameExecutable(processPath, targetPath)) {
                        return true;
                    }
                }
            } finally {
                Kernel32.INSTANCE.CloseHandle(process);
            }
        }
        return false;
    }

    private static boolean isSameExecutable(Path processPath, Path targetPath) {
        if (processPath == null || targetPath == null) {
            return false;
        }

        // Exact path match.
        if (processPath.equals(targetPath)) {
            return true;
        }

        // Case-insensitive path match (Windows path comparisons).
        String processStr = normalizePathString(processPath);
        String targetStr = normalizePathString(targetPath);
        if (processStr.equalsIgnoreCase(targetStr)) {
            return true;
        }

        // Fallback: match only the filename when the full path is not comparable.
        Path processFile = processPath.getFileName();
        Path targetFile = targetPath.getFileName();
        if (processFile != null && targetFile != null) {
            return processFile.toString().equalsIgnoreCase(targetFile.toString());
        }

        return false;
    }

    private static String normalizePathString(Path path) {
        String value = path.toString();
        // Strip Windows extended-length path prefix if present.
        if (value.startsWith("\\\\?\\")) {
            value = value.substring(4);
        }
        return value;
    }

    public static void activateWindow(HWND hwnd) {
        if (hwnd == null) return;

        // Restore window if minimized
        User32.INSTANCE.ShowWindow(hwnd, User32.SW_RESTORE);

        // Bring to front
        User32.INSTANCE.SetForegroundWindow(hwnd);
    }
}
