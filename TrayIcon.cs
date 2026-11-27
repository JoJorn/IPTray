using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace IPTray;

/// <summary>
/// Minimal tray icon on Shell_NotifyIcon. Unlike WinForms' NotifyIcon it uses
/// NOTIFYICON_VERSION_4 without NIF_SHOWTIP, so Windows never shows its own tooltip.
/// </summary>
public sealed unsafe partial class TrayIcon : IDisposable
{
    private const int WM_TRAY = 0x8001; // WM_APP + 1
    private const int WM_MOUSEMOVE = 0x200, WM_CONTEXTMENU = 0x7B, NIN_SELECT = 0x400, NIN_KEYSELECT = 0x401;
    private const uint NIM_ADD = 0, NIM_DELETE = 2, NIM_SETVERSION = 4;
    private const uint NIF_MESSAGE = 1, NIF_ICON = 2, NIF_TIP = 4;

    private static readonly int WM_TASKBARCREATED = (int)RegisterWindowMessage("TaskbarCreated");

    private readonly HwndSource _window;
    private readonly nint _icon;
    private readonly string _name;

    public event Action? Hover, LeftClick, RightClick;

    public TrayIcon(string name)
    {
        _name = name;
        
        ExtractIconEx(Environment.ProcessPath!, 0, 0, out _icon, 1);
        
        _window = new HwndSource(new HwndSourceParameters(name) { WindowStyle = 0, Width = 0, Height = 0 });
        _window.AddHook(WndProc);
        // Let the TaskbarCreated broadcast through (UIPI blocks it when running elevated)
        ChangeWindowMessageFilterEx(_window.Handle, (uint)WM_TASKBARCREATED, 1 /* MSGFLT_ALLOW */, 0);
        Add();
    }

    private void Add()
    {
        var data = NewData();
        data.uFlags = NIF_MESSAGE | NIF_ICON | NIF_TIP; // tip is only for screen readers, no NIF_SHOWTIP
        data.uCallbackMessage = WM_TRAY;
        data.hIcon = _icon;
        _name.AsSpan(0, Math.Min(_name.Length, 127)).CopyTo(new Span<char>(data.szTip, 128));
        Shell_NotifyIcon(NIM_ADD, ref data);

        data.uVersion = 4; // NOTIFYICON_VERSION_4
        Shell_NotifyIcon(NIM_SETVERSION, ref data);
    }

    private NOTIFYICONDATAW NewData() => new()
    {
        cbSize = sizeof(NOTIFYICONDATAW),
        hWnd = _window.Handle,
        uID = 1,
    };

    private nint WndProc(nint hwnd, int msg, nint wParam, nint lParam, ref bool handled)
    {
        if (msg == WM_TASKBARCREATED) Add(); // Explorer restarted: put the icon back
        else if (msg == WM_TRAY)
        {
            switch ((int)(lParam & 0xFFFF))
            {
                case WM_MOUSEMOVE: Hover?.Invoke(); break;
                case NIN_SELECT or NIN_KEYSELECT: LeftClick?.Invoke(); break;
                case WM_CONTEXTMENU: RightClick?.Invoke(); break;
            }
            handled = true;
        }
        return 0;
    }

    public void Dispose()
    {
        var data = NewData();
        Shell_NotifyIcon(NIM_DELETE, ref data);
        DestroyIcon(_icon);
        _window.Dispose();
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct NOTIFYICONDATAW
    {
        public int cbSize;
        public nint hWnd;
        public uint uID, uFlags;
        public int uCallbackMessage;
        public nint hIcon;
        public fixed char szTip[128];
        public uint dwState, dwStateMask;
        public fixed char szInfo[256];
        public uint uVersion;
        public fixed char szInfoTitle[64];
        public uint dwInfoFlags;
        public Guid guidItem;
        public nint hBalloonIcon;
    }

    [LibraryImport("shell32.dll", EntryPoint = "Shell_NotifyIconW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool Shell_NotifyIcon(uint message, ref NOTIFYICONDATAW data);

    [LibraryImport("shell32.dll", EntryPoint = "ExtractIconExW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint ExtractIconEx(string file, int index, nint large, out nint small, uint count);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DestroyIcon(nint icon);

    [LibraryImport("user32.dll", EntryPoint = "RegisterWindowMessageW", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint RegisterWindowMessage(string name);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ChangeWindowMessageFilterEx(nint hwnd, uint message, uint action, nint filterInfo);
}
