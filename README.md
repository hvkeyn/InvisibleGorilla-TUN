# Invisible Gorilla TUN

> Windows-only TUN service for `InvisibleGorilla-XRayClient`

`Invisible Gorilla TUN` is the Windows tunneling companion service used by the client to create a virtual interface, route traffic through `tun2socks`, and manage system routes automatically.

## Platform scope

This repository is intentionally **Windows-only**.

- Windows desktop: this repository provides the companion TUN service used by `InvisibleGorilla-XRayClient`.
- Android: mobile support is being implemented in the client repository through an Android head and `VpnService` groundwork.
- This repository should not be treated as the Android VPN runtime, because it depends on Windows-specific APIs, Wintun, and desktop process management.

## Quick start

### Download release

Download the latest binary from [Releases](https://github.com/hvkeyn/InvisibleGorilla-TUN/releases/latest).

Run it as administrator:

```powershell
InvisibleGorilla-TUN -port={port}
```

### Build from source

```powershell
git clone "https://github.com/hvkeyn/InvisibleGorilla-TUN.git"
cd InvisibleGorilla-TUN
.\build.ps1
```

The script will automatically:

- download `tun2socks.exe`
- download `wintun.dll`
- build `tun.dll` from `TUN-Wrapper`
- publish the Windows service to `publish/`
- create a release archive in `dist/`

If you want a custom publish folder:

```powershell
.\build.ps1 -OutputDir .\artifacts\publish
```

Run the service as administrator:

```powershell
.\publish\InvisibleGorilla-TUN.exe -port={port}
```

## Service protocol

The client communicates with the service over a local TCP socket on the selected port.

Supported commands:

- `enable`

```text
-command=enable -device={device} -proxy={ip}:{port} -address={address} -server={server} -dns={dns}
```

- `disable`

```text
-command=disable
```

## Requirements

- [Go](https://go.dev/dl/)
- [.NET SDK](https://dotnet.microsoft.com/download)

## Related project

- [InvisibleGorilla-XRayClient](https://github.com/hvkeyn/InvisibleGorilla-XRayClient)