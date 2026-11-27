using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Drawing = System.Drawing;
using Forms = System.Windows.Forms;

namespace IPTray;

/// <summary>Small info window shown instantly while hovering the tray icon.</summary>
public class HoverWindow : Window
{
    private readonly StackPanel _info = new();
    private readonly DispatcherTimer _leaveCheck = new() { Interval = TimeSpan.FromMilliseconds(100) };
    private Drawing.Point _lastOverIcon;
    private bool _showing;

    public HoverWindow()
    {
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        ShowActivated = false;
        SizeToContent = SizeToContent.WidthAndHeight;
        Left = Top = -10000;

        var border = new Border
        {
            Padding = new Thickness(12, 8, 12, 8),
            BorderBrush = new SolidColorBrush(Color.FromArgb(0x40, 0x80, 0x80, 0x80)),
            BorderThickness = new Thickness(1),
            Child = _info,
        };
        border.SetResourceReference(Border.BackgroundProperty, "ApplicationBackgroundBrush");
        Content = border;

        // The tray icon has no "mouse leave" event, so hide once the cursor
        // has moved more than an icon's width away from where it last was over it.
        _leaveCheck.Tick += (_, _) =>
        {
            var pos = Forms.Cursor.Position;
            var size = Forms.SystemInformation.SmallIconSize.Width;
            if (Math.Abs(pos.X - _lastOverIcon.X) > size || Math.Abs(pos.Y - _lastOverIcon.Y) > size) Hide();
        };
        IsVisibleChanged += (_, _) => { if (!IsVisible) _leaveCheck.Stop(); };

        // Create the window now, so showing it on hover is quick and does no setup.
        new WindowInteropHelper(this).EnsureHandle();
    }

    /// <summary>Show the details of this adapter (null = none found).</summary>
    public void SetAdapter(Adapter? a)
    {
        _info.Children.Clear();
        _info.Children.Add(new TextBlock
        {
            Text = a?.Name ?? "IPTray",
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 0, 4),
        });

        if (a is null) { AddLine("No network adapter found"); return; }
        if (!a.IsUp) { AddLine("Disconnected"); return; }

        var rows = new Grid();
        rows.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        rows.ColumnDefinitions.Add(new ColumnDefinition());
        AddRow(rows, "Mode", a.Dhcp ? "Automatic (DHCP)" : "Manual");
        AddRow(rows, "IP address", a.Ip is "" ? "none" : a.Ip);
        AddRow(rows, "Subnet mask", a.Mask);
        AddRow(rows, "Gateway", a.Gateway);
        AddRow(rows, "DNS", a.Dns);
        _info.Children.Add(rows);
    }

    private void AddLine(string text) => _info.Children.Add(new TextBlock { Text = text, Opacity = 0.7 });

    private static void AddRow(Grid grid, string label, string value)
    {
        if (value is "") return; // skip empty fields, e.g. no gateway
        var row = grid.RowDefinitions.Count;
        grid.RowDefinitions.Add(new RowDefinition());

        var name = new TextBlock { Text = label, Opacity = 0.7, Margin = new Thickness(0, 1, 16, 1) };
        var text = new TextBlock { Text = value, Margin = new Thickness(0, 1, 0, 1) };
        Grid.SetRow(name, row);
        Grid.SetRow(text, row);
        Grid.SetColumn(text, 1);
        grid.Children.Add(name);
        grid.Children.Add(text);
    }

    /// <summary>Call on every mouse move over the tray icon.</summary>
    public void OnIconHover()
    {
        _lastOverIcon = Forms.Cursor.Position;
        // Show() can process messages, so another hover may arrive while it runs.
        if (IsVisible || _showing) return;

        _showing = true;
        try
        {
            Show();
            UpdateLayout();
            var dpi = VisualTreeHelper.GetDpi(this);
            var area = SystemParameters.WorkArea;
            var x = _lastOverIcon.X / dpi.DpiScaleX - ActualWidth / 2;
            Left = Math.Clamp(x, area.Left + 8, Math.Max(area.Left + 8, area.Right - ActualWidth - 8));
            Top = area.Bottom - ActualHeight - 8;
            _leaveCheck.Start();
        }
        finally
        {
            _showing = false;
        }
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        int round = 3; // DWMWCP_ROUNDSMALL, like a tooltip
        _ = MainWindow.DwmSetWindowAttribute(new WindowInteropHelper(this).Handle, 33, ref round, sizeof(int));
    }
}
