using System.Net.NetworkInformation;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace IPTray;

public partial class MainWindow : Window
{
    private DateTime _hiddenAt;
    private bool _loading;
    private readonly Settings _settings = Settings.Load();

    public event Action<Adapter?>? CurrentChanged;

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
        var selected = Current?.Name ?? _settings.LastAdapter;
        var adapters = NetConfig.GetAdapters();

        _loading = true;
        AdapterBox.ItemsSource = adapters;
        AdapterBox.SelectedItem = adapters.FirstOrDefault(a => a.Name == selected) ?? adapters.FirstOrDefault();
        _loading = false;

        if (fillFields) Fill(Current);
        CurrentChanged?.Invoke(Current);
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
        if (_loading || Current is not { } a) return;
        Fill(a);
        _settings.LastAdapter = a.Name; // remember it between runs
        _settings.Save();
        CurrentChanged?.Invoke(a);
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
        Native.SetCorners(new WindowInteropHelper(this).Handle, Native.Corners.Round);
    }
}
