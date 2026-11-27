using System.Runtime.InteropServices;

namespace IPTray;

/// <summary>The few Win32 calls shared by the windows and the tray.</summary>
internal static partial class Native
{
    public enum Corners { Round = 2, RoundSmall = 3 }

    /// <summary>Windows 11 rounded corners for a borderless window.</summary>
    public static void SetCorners(nint hwnd, Corners corners)
    {
        var value = (int)corners;
        _ = DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref value, sizeof(int));
    }

    /// <summary>Cursor position in physical pixels.</summary>
    public static (int X, int Y) CursorPosition => GetCursorPos(out var p) ? (p.X, p.Y) : default;

    /// <summary>Width of a small (tray) icon in physical pixels.</summary>
    public static int SmallIconSize => GetSystemMetrics(49 /* SM_CXSMICON */);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    private struct POINT { public int X, Y; }

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out POINT point);

    [LibraryImport("user32.dll")]
    private static partial int GetSystemMetrics(int index);

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
