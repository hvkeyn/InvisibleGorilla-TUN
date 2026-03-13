# Invisible Gorilla TUN

> Windows TUN service for `InvisibleGorilla-XRayClient`

`Invisible Gorilla TUN` is the Windows tunneling companion service used by the client to create a virtual interface, route traffic through `tun2socks`, and manage system routes automatically.

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
```

Then:

1. Download [tun2socks](https://github.com/xjasonlyu/tun2socks/releases/latest) for your OS and extract it to `InvisibleGorilla-TUN/` and `TUN-Wrapper/`.
   Rename the executable to `tun2socks.exe`.
2. Download [wintun](https://www.wintun.net), extract it, and copy the correct `wintun.dll` to `InvisibleGorilla-TUN/` and `TUN-Wrapper/`.
3. Build `tun.dll` and copy it to the project directory:

```powershell
cd TUN-Wrapper
go build --buildmode=c-shared -o tun.dll -trimpath -ldflags "-s -w -buildid=" .
copy tun.dll ..\InvisibleGorilla-TUN
```

4. Run the service as administrator:

```powershell
dotnet run --project .\InvisibleGorilla-TUN\InvisibleGorilla-TUN.csproj -- -port={port}
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