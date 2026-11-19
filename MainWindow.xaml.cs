using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace IpTray;

public partial class MainWindow : Window
{
    private DateTime _hiddenAt;
    private bool _loading;

    public event Action<string>? TooltipChanged;

    private Adapter? Current => AdapterBox.SelectedItem as Adapter;

    public MainWindow()
    {
        InitializeComponent();
        // Keep the list (and tray tooltip) fresh when Windows changes addresses.
        NetworkChange.NetworkAddressChanged += (_, _) =>
            Dispatcher.InvokeAsync(() => Refresh(fillFields: !IsVisible));
    }

    // ── Show / hide like a tray flyout ─────────────────────────────

    public void Toggle()
    {
        if (IsVisible) Hide();
        // a tray click first deactivates (hides) us — don't instantly reopen
        else if ((DateTime.UtcNow - _hiddenAt).TotalMilliseconds > 300) ShowFlyout();
    }

    public void ShowFlyout()
    {
        Refresh();
        Show();
        UpdateLayout();
        var area = SystemParameters.WorkArea;
        Left = area.Right - ActualWidth - 12;
        Top = area.Bottom - ActualHeight - 12;
        Activate();
    }

    private void OnDeactivated(object? sender, EventArgs e)
    {
        Hide();
        _hiddenAt = DateTime.UtcNow;
    }

    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Hide();
    }

    // ── Data ───────────────────────────────────────────────────────

    public void Refresh(bool fillFields = true)
    {
        var selected = Current?.Index;
        var adapters = NetConfig.GetAdapters();

        _loading = true;
        AdapterBox.ItemsSource = adapters;
        AdapterBox.SelectedItem = adapters.FirstOrDefault(a => a.Index == selected) ?? adapters.FirstOrDefault();
        _loading = false;

        if (fillFields) Fill(Current);
        TooltipChanged?.Invoke(Current is { } a ? $"IP Switcher\n{a.Name}: {a.Summary}" : "IP Switcher");
    }

    private void Fill(Adapter? a)
    {
        if (a is null) return;
        (a.Dhcp ? DhcpRadio : StaticRadio).IsChecked = true;
        IpBox.Text = a.Ip;
        MaskBox.Text = a.Mask;
        GatewayBox.Text = a.Gateway;
        DnsBox.Text = a.Dns;
        SetStatus("");
    }

    private void OnAdapterChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_loading) Fill(Current);
    }

    private void OnRefresh(object sender, RoutedEventArgs e) => Refresh();

    private async void OnApply(object sender, RoutedEventArgs e)
    {
        if (Current is not { } adapter) return;

        ApplyButton.IsEnabled = false;
        SetStatus("Applying…");
        try
        {
            IpSettings settings = DhcpRadio.IsChecked == true
                ? new DhcpSettings()
                : StaticSettings.Parse(IpBox.Text, MaskBox.Text, GatewayBox.Text, DnsBox.Text);

            await NetConfig.ApplyAsync(adapter, settings);
            await Task.Delay(1500); // give Windows a moment to settle
            Refresh();
            SetStatus("✓ Applied", "SystemFillColorSuccessBrush", Brushes.SeaGreen);
        }
        catch (Exception ex)
        {
            SetStatus(ex.Message, "SystemFillColorCriticalBrush", Brushes.IndianRed);
        }
        finally
        {
            ApplyButton.IsEnabled = true;
        }
    }

    private void SetStatus(string text, string? brushKey = null, Brush? fallback = null)
    {
        StatusText.Text = text;
        if (brushKey is null) StatusText.ClearValue(TextBlock.ForegroundProperty);
        else StatusText.Foreground = TryFindResource(brushKey) as Brush ?? fallback;
    }

    // ── Windows 11 rounded corners for the borderless flyout ───────

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        var hwnd = new WindowInteropHelper(this).Handle;
        int round = 2; // DWMWCP_ROUND
        _ = DwmSetWindowAttribute(hwnd, 33 /* DWMWA_WINDOW_CORNER_PREFERENCE */, ref round, sizeof(int));
    }

    [LibraryImport("dwmapi.dll")]
    private static partial int DwmSetWindowAttribute(nint hwnd, int attribute, ref int value, int size);
}
