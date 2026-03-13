param(
    [string]$Configuration = "Release",
    [string]$Runtime = "win-x64",
    [string]$OutputDir = "",
    [switch]$SkipPackage
)

$ErrorActionPreference = "Stop"
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12

$RootDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$ProjectDir = Join-Path $RootDir "InvisibleGorilla-TUN"
$ProjectFile = Join-Path $ProjectDir "InvisibleGorilla-TUN.csproj"
$WrapperDir = Join-Path $RootDir "TUN-Wrapper"
$DistDir = Join-Path $RootDir "dist"
$DefaultOutputDir = Join-Path $RootDir "publish"

if ([string]::IsNullOrWhiteSpace($OutputDir)) {
    $OutputDir = $DefaultOutputDir
}

$OutputDir = [System.IO.Path]::GetFullPath($OutputDir)
$DistDir = [System.IO.Path]::GetFullPath($DistDir)
$TunDllPath = Join-Path $ProjectDir "tun.dll"
$Tun2SocksPath = Join-Path $ProjectDir "tun2socks.exe"
$WintunPath = Join-Path $ProjectDir "wintun.dll"
$LegacyBootstrapReleaseApiUrl = "https://api.github.com/repos/InvisibleManVPN/InvisibleMan-TUN/releases/latest"
$script:BootstrapTempDir = $null

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkGray
    Write-Host "  $Text" -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor DarkGray
}

function Write-Step {
    param([string]$Text)
    Write-Host "[..] $Text" -ForegroundColor Yellow
}

function Write-Ok {
    param([string]$Text)
    Write-Host "[OK] $Text" -ForegroundColor Green
}

function Write-Err {
    param([string]$Text)
    Write-Host "[!!] $Text" -ForegroundColor Red
}

function Require-Command {
    param([string]$Name)
    if (-not (Get-Command $Name -ErrorAction SilentlyContinue)) {
        throw "Команда '$Name' не найдена в PATH."
    }
}

function Invoke-DownloadFile {
    param(
        [Parameter(Mandatory)][string]$Url,
        [Parameter(Mandatory)][string]$Destination
    )

    if (Test-Path $Destination) {
        Remove-Item $Destination -Force
    }

    Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing
}

function Get-ProjectVersion {
    [xml]$project = Get-Content $ProjectFile
    $version = $project.Project.PropertyGroup.Version | Select-Object -First 1
    if ([string]::IsNullOrWhiteSpace($version)) {
        throw "Не удалось определить версию из $ProjectFile."
    }

    return $version.Trim()
}

function Get-BootstrapExtractDir {
    if ($script:BootstrapTempDir -and (Test-Path $script:BootstrapTempDir)) {
        return (Join-Path $script:BootstrapTempDir "extract")
    }

    Write-Step "Скачивание bootstrap-архива из InvisibleMan-TUN release..."

    $release = Invoke-RestMethod -Uri $LegacyBootstrapReleaseApiUrl -UseBasicParsing
    $asset = $release.assets |
        Where-Object { $_.name -match ".*(x64|amd64).*\.zip$" } |
        Select-Object -First 1

    if (-not $asset) {
        throw "Не удалось найти x64 zip asset в upstream release InvisibleMan-TUN."
    }

    $script:BootstrapTempDir = Join-Path ([System.IO.Path]::GetTempPath()) ("ig-tun-bootstrap-" + [guid]::NewGuid())
    $tempArchive = Join-Path $script:BootstrapTempDir $asset.name
    $extractDir = Join-Path $script:BootstrapTempDir "extract"

    New-Item -ItemType Directory -Path $script:BootstrapTempDir | Out-Null
    Invoke-DownloadFile -Url $asset.browser_download_url -Destination $tempArchive
    Expand-Archive -Path $tempArchive -DestinationPath $extractDir -Force

    return $extractDir
}

function Copy-DependencyFromBootstrap {
    param(
        [Parameter(Mandatory)][string]$FileName,
        [Parameter(Mandatory)][string]$Destination
    )

    $extractDir = Get-BootstrapExtractDir
    $file = Get-ChildItem -Path $extractDir -Recurse -File |
        Where-Object { $_.Name -ieq $FileName } |
        Select-Object -First 1

    if (-not $file) {
        throw "Не удалось найти $FileName внутри bootstrap-архива."
    }

    Copy-Item $file.FullName $Destination -Force
}

function Ensure-Tun2Socks {
    if (Test-Path $Tun2SocksPath) {
        Write-Ok "tun2socks.exe уже существует"
        return
    }

    Write-Step "Подготовка tun2socks.exe..."
    Copy-DependencyFromBootstrap -FileName "tun2socks.exe" -Destination $Tun2SocksPath
    Write-Ok "tun2socks.exe -> $Tun2SocksPath"
}

function Ensure-Wintun {
    if (Test-Path $WintunPath) {
        Write-Ok "wintun.dll уже существует"
        return
    }

    Write-Step "Подготовка wintun.dll..."
    Copy-DependencyFromBootstrap -FileName "wintun.dll" -Destination $WintunPath
    Write-Ok "wintun.dll -> $WintunPath"
}

function Build-TunWrapper {
    Write-Step "Сборка tun.dll..."
    Push-Location $WrapperDir
    try {
        if (Test-Path "tun.dll") {
            Remove-Item "tun.dll" -Force
        }

        if (Test-Path "tun.h") {
            Remove-Item "tun.h" -Force
        }

        & go build --buildmode=c-shared -trimpath -ldflags "-s -w -buildid=" -o "tun.dll" "."
        if ($LASTEXITCODE -ne 0) {
            throw "go build завершился с ошибкой."
        }

        Copy-Item "tun.dll" $TunDllPath -Force
        Write-Ok "tun.dll -> $TunDllPath"
    }
    finally {
        foreach ($file in @("tun.dll", "tun.h")) {
            if (Test-Path $file) {
                Remove-Item $file -Force
            }
        }

        Pop-Location
    }
}

function Publish-Service {
    Write-Step "Публикация InvisibleGorilla-TUN..."

    if (Test-Path $OutputDir) {
        Remove-Item $OutputDir -Recurse -Force
    }

    New-Item -ItemType Directory -Path $OutputDir | Out-Null

    & dotnet publish $ProjectFile -c $Configuration -r $Runtime --self-contained true -o $OutputDir
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish завершился с ошибкой."
    }

    $exePath = Join-Path $OutputDir "InvisibleGorilla-TUN.exe"
    if (-not (Test-Path $exePath)) {
        throw "После публикации не найден $exePath."
    }

    Write-Ok "Публикация готова: $OutputDir"
}

function Package-Service {
    param([Parameter(Mandatory)][string]$Version)

    if ($SkipPackage) {
        return
    }

    Write-Step "Упаковка release-архива..."

    if (-not (Test-Path $DistDir)) {
        New-Item -ItemType Directory -Path $DistDir | Out-Null
    }

    $zipName = "InvisibleGorilla-TUN-$Runtime-v$Version.zip"
    $zipPath = Join-Path $DistDir $zipName

    if (Test-Path $zipPath) {
        Remove-Item $zipPath -Force
    }

    Compress-Archive -Path (Join-Path $OutputDir "*") -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Ok "Архив создан: $zipPath"
}

Write-Section "Invisible Gorilla TUN :: Build Script"

try {
    Require-Command "go"
    Require-Command "dotnet"

    $version = Get-ProjectVersion
    Write-Host "Version: $version" -ForegroundColor Gray

    Ensure-Tun2Socks
    Ensure-Wintun
    Build-TunWrapper
    Publish-Service
    Package-Service -Version $version

    Write-Host ""
    Write-Ok "Готово"
}
finally {
    if ($script:BootstrapTempDir -and (Test-Path $script:BootstrapTempDir)) {
        Remove-Item $script:BootstrapTempDir -Recurse -Force -ErrorAction SilentlyContinue
    }
}
