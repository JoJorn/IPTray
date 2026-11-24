# IP Switcher

A tiny Windows tray app to quickly change the IP settings of a network adapter.

Left-click the tray icon → pick an adapter → **Automatic (DHCP)** or **Manual** → **Apply**.
Right-click for Open / Exit. Press Esc or click elsewhere to close the flyout.

## Features

- Switch any adapter between DHCP and a static IP in two clicks
- Set IP address, subnet mask, gateway and DNS servers
- Mask field accepts `255.255.255.0`, `24` or `/24` (empty = /24)
- Multiple DNS servers, comma separated
- Adapter list refreshes automatically when your network changes
- Tray tooltip shows the current IP
- Modern Windows 11 look (Fluent theme, follows light/dark mode)

## Requirements

- Windows 10/11
- [.NET 11 SDK](https://dotnet.microsoft.com/download) to build (`winget install Microsoft.DotNet.SDK.11`)
- Administrator rights (needed to change IP settings)

## Build

Run:

```
dotnet publish -c Release -o publish
```

Then run `publish\IpTray.exe`.

## Start with Windows (no UAC prompt)

Because the app needs admin rights, a normal startup shortcut would show a UAC prompt every time.
A scheduled task with *highest privileges* starts it elevated without a prompt.

Run the `install-startup.ps1` script:

```powershell
Start-Process powershell -Verb RunAs -ArgumentList "-ExecutionPolicy Bypass -File `"$PWD\install-startup.ps1`""
```

The settings line keeps Windows from stopping the app after 3 days or on battery.

| Action | Command |
|---|---|
| Start now (no prompt) | `Start-ScheduledTask IpTray` |
| Remove from startup | `Unregister-ScheduledTask IpTray` |

Tip: exit the app from the tray menu before rebuilding, otherwise the build can't overwrite the .exe.

## How it works

| File | What |
|---|---|
| `App.xaml(.cs)` | Tray icon, menu, single instance |
| `MainWindow.xaml(.cs)` | The flyout UI |
| `NetConfig.cs` | Reads adapters, validates input, applies settings with `netsh` |

No NuGet packages, just .NET and the built-in `netsh` command.