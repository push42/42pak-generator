<#
.SYNOPSIS
    42pak-generator publish script — builds portable and installer-ready distributions.

.DESCRIPTION
    Produces three distribution flavors:
      1. CLI Portable    — Single self-contained exe (42pak-cli.exe), no dependencies
      2. GUI Portable    — Self-contained folder (FortyTwoPak.exe + wwwroot + deps), zip-ready
      3. Installer       — Inno Setup compilation of the GUI portable into a setup.exe

.PARAMETER Target
    Which target to build: CLI, GUI, Installer, All (default: All)

.PARAMETER Configuration
    Build configuration: Release (default) or Debug

.PARAMETER Runtime
    Target runtime identifier (default: win-x64)

.PARAMETER SkipClean
    Skip cleaning previous publish output

.EXAMPLE
    .\publish.ps1                           # Build everything
    .\publish.ps1 -Target CLI               # CLI single-exe only
    .\publish.ps1 -Target GUI               # GUI portable folder only
    .\publish.ps1 -Target Installer         # GUI installer (builds GUI first if needed)
    .\publish.ps1 -Target All -Runtime win-arm64  # All targets for ARM64
#>

param(
    [ValidateSet("CLI", "GUI", "Installer", "All")]
    [string]$Target = "All",

    [ValidateSet("Release", "Debug")]
    [string]$Configuration = "Release",

    [string]$Runtime = "win-x64",

    [switch]$SkipClean
)

$ErrorActionPreference = "Stop"
$Root = $PSScriptRoot
$Src = Join-Path $Root "src"
$PublishDir = Join-Path $Root "publish"
$ReleaseDir = Join-Path $Root "release"

# Resolved output paths
$CliPublishDir = Join-Path $PublishDir "cli-portable"
$GuiPublishDir = Join-Path $PublishDir "gui-portable"

# --- Helpers ---
function Write-Step([string]$msg) {
    Write-Host "`n[$((Get-Date).ToString('HH:mm:ss'))] $msg" -ForegroundColor Cyan
}

function Write-Done([string]$msg) {
    Write-Host "  -> $msg" -ForegroundColor Green
}

function Invoke-DotNet([string]$args_str) {
    $cmd = "dotnet $args_str"
    Write-Host "  > $cmd" -ForegroundColor DarkGray
    Invoke-Expression $cmd
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet command failed with exit code $LASTEXITCODE"
    }
}

# --- Clean ---
if (-not $SkipClean) {
    Write-Step "Cleaning previous publish output..."
    @($CliPublishDir, $GuiPublishDir) | ForEach-Object {
        if (Test-Path $_) { Remove-Item $_ -Recurse -Force }
    }
    Write-Done "Clean complete"
}

# --- CLI Portable (single exe) ---
function Publish-CLI {
    Write-Step "Publishing CLI portable (single exe)..."

    $proj = Join-Path $Src "FortyTwoPak.CLI\FortyTwoPak.CLI.csproj"
    Invoke-DotNet "publish `"$proj`" -c $Configuration -r $Runtime --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -o `"$CliPublishDir`""

    $exe = Join-Path $CliPublishDir "42pak-cli.exe"
    if (Test-Path $exe) {
        $size = [math]::Round((Get-Item $exe).Length / 1MB, 1)
        Write-Done "42pak-cli.exe ($size MB) -> $CliPublishDir"
    }

    # Create zip in release/
    if (-not (Test-Path $ReleaseDir)) { New-Item $ReleaseDir -ItemType Directory -Force | Out-Null }
    $zipName = "42pak-cli-portable-$Runtime.zip"
    $zipPath = Join-Path $ReleaseDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$CliPublishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Done "Release archive: $zipPath"
}

# --- GUI Portable (folder) ---
function Publish-GUI {
    Write-Step "Publishing GUI portable (self-contained folder)..."

    $proj = Join-Path $Src "FortyTwoPak.UI\FortyTwoPak.UI.csproj"
    Invoke-DotNet "publish `"$proj`" -c $Configuration -r $Runtime --self-contained -p:PublishTrimmed=false -p:DebugType=none -p:DebugSymbols=false -o `"$GuiPublishDir`""

    $exe = Join-Path $GuiPublishDir "FortyTwoPak.exe"
    if (Test-Path $exe) {
        $totalSize = [math]::Round((Get-ChildItem $GuiPublishDir -Recurse -File | Measure-Object -Property Length -Sum).Sum / 1MB, 1)
        Write-Done "FortyTwoPak.exe + deps ($totalSize MB total) -> $GuiPublishDir"
    }

    # Also copy CLI single-exe into GUI portable so users get both
    $cliExe = Join-Path $CliPublishDir "42pak-cli.exe"
    if (Test-Path $cliExe) {
        Copy-Item $cliExe -Destination $GuiPublishDir -Force
        Write-Done "Bundled 42pak-cli.exe into GUI portable folder"
    }

    # Create zip in release/
    if (-not (Test-Path $ReleaseDir)) { New-Item $ReleaseDir -ItemType Directory -Force | Out-Null }
    $zipName = "42pak-generator-portable-$Runtime.zip"
    $zipPath = Join-Path $ReleaseDir $zipName
    if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
    Compress-Archive -Path "$GuiPublishDir\*" -DestinationPath $zipPath -CompressionLevel Optimal
    Write-Done "Release archive: $zipPath"
}

# --- Installer (Inno Setup) ---
function Publish-Installer {
    # Ensure GUI is built first
    if (-not (Test-Path (Join-Path $GuiPublishDir "FortyTwoPak.exe"))) {
        Write-Step "GUI not yet published, building GUI first..."
        Publish-GUI
    }

    Write-Step "Building installer (Inno Setup)..."

    $issPath = Join-Path $Root "installer.iss"
    if (-not (Test-Path $issPath)) {
        Write-Host "  ! installer.iss not found — skipping installer build." -ForegroundColor Yellow
        Write-Host "  ! Run this script with -Target GUI first, then compile installer.iss manually." -ForegroundColor Yellow
        return
    }

    # Find Inno Setup compiler
    $iscc = @(
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe"
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe"
    ) | Where-Object { Test-Path $_ } | Select-Object -First 1

    if (-not $iscc) {
        Write-Host "  ! Inno Setup 6 not found. Install from https://jrsoftware.org/isinfo.php" -ForegroundColor Yellow
        Write-Host "  ! You can compile installer.iss manually after installing." -ForegroundColor Yellow
        return
    }

    & $iscc /Q "/O$ReleaseDir" $issPath
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup compilation failed" }

    Write-Done "Installer created in $ReleaseDir"
}

# --- Execute ---
Write-Host "============================================" -ForegroundColor White
Write-Host "  42pak-generator Publish Script" -ForegroundColor White
Write-Host "  Target: $Target  |  Config: $Configuration  |  RID: $Runtime" -ForegroundColor DarkGray
Write-Host "============================================" -ForegroundColor White

switch ($Target) {
    "CLI"       { Publish-CLI }
    "GUI"       { Publish-CLI; Publish-GUI }
    "Installer" { Publish-CLI; Publish-GUI; Publish-Installer }
    "All"       { Publish-CLI; Publish-GUI; Publish-Installer }
}

Write-Host "`n============================================" -ForegroundColor White
Write-Host "  Publish complete!" -ForegroundColor Green
Write-Host "  Output:    $PublishDir" -ForegroundColor DarkGray
Write-Host "  Releases:  $ReleaseDir" -ForegroundColor DarkGray
Write-Host "============================================" -ForegroundColor White
