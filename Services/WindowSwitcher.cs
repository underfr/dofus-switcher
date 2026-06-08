using System.Runtime.InteropServices;
using DofusSwitcher.Models;

namespace DofusSwitcher.Services;

public static class WindowSwitcher
{
    private const int SW_RESTORE = 9;
    private const int SW_SHOW = 5;

    [DllImport("user32.dll")]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [DllImport("user32.dll")]
    private static extern bool BringWindowToTop(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);

    [DllImport("kernel32.dll")]
    private static extern uint GetCurrentThreadId();

    [DllImport("user32.dll")]
    private static extern bool IsIconic(IntPtr hWnd);

    public static IntPtr GetCurrentForeground() => GetForegroundWindow();

    public static void Activate(DofusWindowInfo window)
    {
        var hwnd = window.Handle;

        if (IsIconic(hwnd))
            ShowWindow(hwnd, SW_RESTORE);
        else
            ShowWindow(hwnd, SW_SHOW);

        // Trick AttachThreadInput pour forcer le focus (contourne les restrictions Windows)
        uint currentThread = GetCurrentThreadId();
        uint foregroundThread = GetWindowThreadProcessId(GetForegroundWindow(), out _);
        uint targetThread = GetWindowThreadProcessId(hwnd, out _);

        bool attachedFg = false, attachedTarget = false;

        if (foregroundThread != currentThread)
        {
            AttachThreadInput(foregroundThread, currentThread, true);
            attachedFg = true;
        }
        if (targetThread != currentThread && targetThread != foregroundThread)
        {
            AttachThreadInput(targetThread, currentThread, true);
            attachedTarget = true;
        }

        BringWindowToTop(hwnd);
        SetForegroundWindow(hwnd);

        if (attachedFg) AttachThreadInput(foregroundThread, currentThread, false);
        if (attachedTarget) AttachThreadInput(targetThread, currentThread, false);
    }
}
