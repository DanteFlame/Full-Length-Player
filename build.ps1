$ErrorActionPreference = "Stop"

$project = Join-Path $PSScriptRoot "src\FullLengthPlayer\FullLengthPlayer.csproj"
$out = Join-Path $PSScriptRoot "publish\FullLengthPlayer-win-x64"

if (Test-Path $out) {
    Remove-Item $out -Recurse -Force
}

New-Item -ItemType Directory -Force -Path $out | Out-Null

dotnet restore $project
dotnet publish $project -c Release -r win-x64 --self-contained true -o $out

Write-Host ""
Write-Host "Build complete: $out"
Write-Host "Run FullLengthPlayer.exe from that folder. Keep the native DLLs beside it."
