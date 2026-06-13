param(
    [string]$Configuration = "Release",
    [switch]$SelfContained
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent $MyInvocation.MyCommand.Path
$dotnetRoot = Join-Path $root ".dotnet"
$dotnetExe = Join-Path $dotnetRoot "dotnet.exe"
$installer = Join-Path $root "tools\dotnet-install.ps1"

if (!(Test-Path $dotnetExe)) {
    New-Item -ItemType Directory -Force -Path (Split-Path -Parent $installer) | Out-Null
    Invoke-WebRequest -Uri "https://dot.net/v1/dotnet-install.ps1" -OutFile $installer
    & $installer -Channel 8.0 -InstallDir $dotnetRoot -Architecture x64
}

$project = Join-Path $root "src\QuickTabLauncher\QuickTabLauncher.csproj"
$finalPublishDir = if ($SelfContained) {
    Join-Path $root "publish-portable"
} else {
    Join-Path $root "publish"
}
$publishDirName = if ($SelfContained) { "portable" } else { "framework" }
$intermediatePublishDir = Join-Path $root ".build\publish-$publishDirName"
$selfContainedValue = if ($SelfContained) { "true" } else { "false" }

$resolvedBuildDir = [System.IO.Path]::GetFullPath((Join-Path $root ".build"))
$resolvedIntermediateDir = [System.IO.Path]::GetFullPath($intermediatePublishDir)
if ($resolvedIntermediateDir.StartsWith($resolvedBuildDir, [System.StringComparison]::OrdinalIgnoreCase) -and (Test-Path $resolvedIntermediateDir)) {
    Remove-Item -Path $resolvedIntermediateDir -Recurse -Force
}

& $dotnetExe publish $project `
    -c $Configuration `
    -r win-x64 `
    --self-contained $selfContainedValue `
    -p:PublishSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -o $intermediatePublishDir

if ($LASTEXITCODE -ne 0) {
    throw "dotnet publish failed with exit code $LASTEXITCODE"
}

$publishConfig = Join-Path $finalPublishDir "config"
$publishIcons = Join-Path $finalPublishDir "Icons"
$publishShortcuts = Join-Path $finalPublishDir "Shortcuts"
New-Item -ItemType Directory -Force -Path $finalPublishDir | Out-Null
New-Item -ItemType Directory -Force -Path $publishConfig | Out-Null
New-Item -ItemType Directory -Force -Path $publishIcons | Out-Null
New-Item -ItemType Directory -Force -Path $publishShortcuts | Out-Null

Copy-Item -Path (Join-Path $intermediatePublishDir "QuickTabLauncher.exe") -Destination (Join-Path $finalPublishDir "QuickTabLauncher.exe") -Force

$sourceAppsJson = Join-Path $root "config\apps.json"
$targetAppsJson = Join-Path $publishConfig "apps.json"
if (!(Test-Path $targetAppsJson)) {
    Copy-Item -Path $sourceAppsJson -Destination $targetAppsJson
}

$targetShortcutReadme = Join-Path $publishShortcuts "README.txt"
if (!(Test-Path $targetShortcutReadme)) {
    Copy-Item -Path (Join-Path $root "Shortcuts\README.txt") -Destination $targetShortcutReadme
}

Remove-Item -Path (Join-Path $finalPublishDir "*.pdb") -Force -ErrorAction SilentlyContinue
Remove-Item -Path (Join-Path $finalPublishDir "apps.json") -Force -ErrorAction SilentlyContinue

Write-Host "Published to $finalPublishDir"
