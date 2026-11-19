using System.Windows;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace IpTray;

public partial class App : Application
{
    private Mutex? _singleInstance;
    private Forms.NotifyIcon? _tray;

    protected override void OnStartup(StartupEventArgs e)
    {
        _singleInstance = new Mutex(true, @"Local\IpTray", out var isFirst);
        if (!isFirst) { Shutdown(); return; }

        base.OnStartup(e);

        var flyout = new MainWindow();
        using var iconStream = GetResourceStream(new Uri("pack://application:,,,/app.ico")).Stream;

        _tray = new Forms.NotifyIcon
        {
            Icon = new Drawing.Icon(iconStream, Forms.SystemInformation.SmallIconSize),
            Text = "IP Switcher",
            Visible = true,
            ContextMenuStrip = new Forms.ContextMenuStrip(),
        };
        _tray.ContextMenuStrip.Items.Add("Open", null, (_, _) => flyout.ShowFlyout());
        _tray.ContextMenuStrip.Items.Add("Exit", null, (_, _) => Shutdown());
        _tray.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left) flyout.Toggle();
        };

        flyout.TooltipChanged += text => _tray.Text = text.Length > 127 ? text[..127] : text;
        flyout.Refresh();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _singleInstance?.Dispose();
        base.OnExit(e);
    }
}
