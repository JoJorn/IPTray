using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace IPTray;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, @"Local\IPTray", out var isFirst);
        if (!isFirst) { Shutdown(); return; }

        base.OnStartup(e);

        var flyout = new MainWindow();
        var owner = new WindowInteropHelper(flyout).EnsureHandle();
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;

        // Native Windows menu: instant, and Windows 11 gives it the modern look.
        SetPreferredAppMode(1); // follow system light/dark (undocumented, used by many apps)
        FlushMenuThemes();
        var menu = CreatePopupMenu();
        AppendMenu(menu, MF_STRING, 1, "Open");
        AppendMenu(menu, MF_SEPARATOR, 0, null);
        AppendMenu(menu, MF_STRING, 2, "Exit");

        // Own hover window instead of the tray tooltip, which shows only after a delay.
        var hover = new HoverWindow();
        var menuOpen = false;

        _tray = new Forms.NotifyIcon
        {
            Icon = new Drawing.Icon(iconStream, Forms.SystemInformation.SmallIconSize),
            Text = "", // no native tooltip
            Visible = true,
        };
        _tray.MouseMove += (_, _) =>
        {
            if (!menuOpen && !flyout.IsVisible) hover.OnIconHover();
        };
        _tray.MouseUp += (_, args) =>
        {
            hover.Hide();
            if (args.Button == Forms.MouseButtons.Left) { flyout.Toggle(); return; }
            if (args.Button != Forms.MouseButtons.Right) return;

            var pos = Forms.Cursor.Position;
            SetForegroundWindow(owner); // so the menu closes on an outside click
            menuOpen = true;
            var cmd = TrackPopupMenuEx(menu, TPM_RETURNCMD | TPM_RIGHTBUTTON | TPM_BOTTOMALIGN, pos.X, pos.Y, owner, 0);
            menuOpen = false;
            if (cmd == 1) flyout.ShowFlyout();
            else if (cmd == 2) Shutdown();
        };

        flyout.CurrentChanged += hover.SetAdapter;
        flyout.Refresh();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }

    // ── Win32 ──────────────────────────────────────────────────────

    private const uint MF_STRING = 0x0, MF_SEPARATOR = 0x800;
    private const uint TPM_RIGHTBUTTON = 0x2, TPM_BOTTOMALIGN = 0x20, TPM_RETURNCMD = 0x100;

    [LibraryImport("user32.dll")]
    private static partial nint CreatePopupMenu();

    [LibraryImport("user32.dll", EntryPoint = "AppendMenuW", StringMarshalling = StringMarshalling.Utf16)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool AppendMenu(nint menu, uint flags, nuint id, string? text);

    [LibraryImport("user32.dll")]
    private static partial int TrackPopupMenuEx(nint menu, uint flags, int x, int y, nint hwnd, nint tpm);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetForegroundWindow(nint hwnd);

    [LibraryImport("uxtheme.dll", EntryPoint = "#135")]
    private static partial int SetPreferredAppMode(int mode);

    [LibraryImport("uxtheme.dll", EntryPoint = "#136")]
    private static partial void FlushMenuThemes();
}
