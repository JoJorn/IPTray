using System.Buffers.Binary;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace IPTray;

public sealed record Adapter(
    int Index, string Name, bool IsUp, bool Dhcp,
    string Ip, string Mask, string Gateway, string Dns)
{
    public string Summary => !IsUp ? "disconnected" : Ip is "" ? "no IPv4" : $"{Ip} ({(Dhcp ? "DHCP" : "static")})";
    public string Display => $"{Name}  —  {Summary}";
}

public abstract record IpSettings;

public sealed record DhcpSettings : IpSettings;

public sealed record StaticSettings(IPAddress Ip, IPAddress Mask, IPAddress? Gateway, IPAddress[] Dns) : IpSettings
{
    public static StaticSettings Parse(string ip, string mask, string gateway, string dns) => new(
        ParseIp(ip, "IP address"),
        ParseMask(mask),
        string.IsNullOrWhiteSpace(gateway) ? null : ParseIp(gateway, "Gateway"),
        [.. dns.Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
               .Select(d => ParseIp(d, "DNS"))]);

    private static IPAddress ParseIp(string text, string field)
    {
        text = text.Trim();
        return text.Count(c => c == '.') == 3
               && IPAddress.TryParse(text, out var ip)
               && ip.AddressFamily == AddressFamily.InterNetwork
            ? ip
            : throw new FormatException($"{field} \"{text}\" is not a valid IPv4 address.");
    }

    // Accepts "255.255.255.0", "24", "/24" or empty (→ /24).
    private static IPAddress ParseMask(string text)
    {
        text = text.Trim().TrimStart('/');
        if (text is "") text = "24";
        if (!int.TryParse(text, out var prefix)) return ParseIp(text, "Subnet mask");
        if (prefix is < 0 or > 32) throw new FormatException("Prefix must be between 0 and 32.");

        var bytes = new byte[4];
        BinaryPrimitives.WriteUInt32BigEndian(bytes, prefix == 0 ? 0 : uint.MaxValue << (32 - prefix));
        return new IPAddress(bytes);
    }
}

public static class NetConfig
{
    public static List<Adapter> GetAdapters() =>
    [
        .. NetworkInterface.GetAllNetworkInterfaces()
            .Where(n => n.NetworkInterfaceType is not (NetworkInterfaceType.Loopback or NetworkInterfaceType.Tunnel)
                        && n.Supports(NetworkInterfaceComponent.IPv4))
            .Select(ToAdapter)
            .OfType<Adapter>()
            .OrderByDescending(a => a.IsUp)
            .ThenBy(a => a.Name)
    ];

    private static Adapter? ToAdapter(NetworkInterface n)
    {
        try
        {
            var props = n.GetIPProperties();
            var v4 = props.GetIPv4Properties();
            var addr = props.UnicastAddresses.FirstOrDefault(u => u.Address.AddressFamily == AddressFamily.InterNetwork);
            var gateway = props.GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
            var dns = props.DnsAddresses.Where(d => d.AddressFamily == AddressFamily.InterNetwork);

            return new Adapter(
                Index: v4.Index,
                Name: n.Name,
                IsUp: n.OperationalStatus == OperationalStatus.Up,
                Dhcp: v4.IsDhcpEnabled,
                Ip: addr?.Address.ToString() ?? "",
                Mask: addr?.IPv4Mask.ToString() ?? "",
                Gateway: gateway?.Address.ToString() ?? "",
                Dns: string.Join(", ", dns));
        }
        catch (NetworkInformationException)
        {
            return null; // adapter vanished or has no IPv4 stack
        }
    }

    public static async Task ApplyAsync(Adapter a, IpSettings settings)
    {
        // netsh accepts the interface index as "name" — no quoting issues with spaces.
        var name = $"name={a.Index}";

        switch (settings)
        {
            case DhcpSettings:
                if (!a.Dhcp) await Netsh("interface", "ipv4", "set", "address", name, "source=dhcp");
                await Netsh("interface", "ipv4", "set", "dnsservers", name, "source=dhcp");
                break;

            case StaticSettings s:
                var gateway = s.Gateway?.ToString() ?? "";
                var unchanged = !a.Dhcp && a.Ip == s.Ip.ToString() && a.Mask == s.Mask.ToString() && a.Gateway == gateway;
                if (!unchanged)
                {
                    await Netsh("interface", "ipv4", "set", "address", name, "source=static",
                        $"address={s.Ip}", $"mask={s.Mask}", $"gateway={(gateway is "" ? "none" : gateway)}");
                }

                if (s.Dns is [])
                {
                    await Netsh("interface", "ipv4", "set", "dnsservers", name, "source=static", "address=none");
                    break;
                }
                await Netsh("interface", "ipv4", "set", "dnsservers", name, "source=static",
                    $"address={s.Dns[0]}", "register=primary", "validate=no");
                foreach (var (dns, i) in s.Dns.Skip(1).Select((d, i) => (d, i + 2)))
                    await Netsh("interface", "ipv4", "add", "dnsservers", name, $"address={dns}", $"index={i}", "validate=no");
                break;
        }
    }

    private static async Task Netsh(params string[] args)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        var oem = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);

        var psi = new ProcessStartInfo("netsh")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = oem,
            StandardErrorEncoding = oem,
        };
        foreach (var arg in args) psi.ArgumentList.Add(arg);

        using var process = Process.Start(psi) ?? throw new InvalidOperationException("Could not start netsh.");
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        if (process.ExitCode != 0)
        {
            var message = $"{output}\n{error}".Trim();
            throw new InvalidOperationException(message is "" ? $"netsh failed (exit code {process.ExitCode})." : message);
        }
    }
}
