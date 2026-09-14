param([switch]$SkipSmokeTest)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'Build and launch verification require Windows.' }

$project = Join-Path $PSScriptRoot 'src/FullLengthPlayer/FullLengthPlayer.csproj'
$out = Join-Path $PSScriptRoot 'publish/FullLengthPlayer-win-x64'
if (Test-Path $out) { Remove-Item $out -Recurse -Force }
& (Join-Path $PSScriptRoot 'scripts/build-icons.ps1')
dotnet publish $project -c Release -r win-x64 --self-contained true -o $out
if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed: $LASTEXITCODE" }
if (!(Test-Path (Join-Path $out 'FullLengthPlayer.exe'))) { throw 'Executable missing.' }
& (Join-Path $PSScriptRoot 'scripts/fetch-mpv.ps1') -Destination $out
& (Join-Path $PSScriptRoot 'scripts/fetch-youtube.ps1') -Destination $out
if (!$SkipSmokeTest) {
    & (Join-Path $PSScriptRoot 'scripts/smoke-window.ps1') -Executable (Join-Path $out 'FullLengthPlayer.exe')
}
Write-Host "Build available at $out"
