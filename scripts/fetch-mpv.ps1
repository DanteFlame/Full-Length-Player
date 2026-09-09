param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$archive = Join-Path $env:TEMP 'flp-mpv-20260901.7z'
$url = 'https://github.com/shinchiro/mpv-winbuild-cmake/releases/download/20260901/mpv-dev-x86_64-20260901-git-02a595ddc1.7z'
$hash = '680feac97f2da3721e331d6b10d2d0e3e02f1113be068fac06aff7f833a165d4'
if (!(Test-Path $archive) -or (Get-FileHash $archive).Hash -ne $hash) { Invoke-WebRequest $url -OutFile $archive }
if ((Get-FileHash $archive).Hash -ne $hash) { throw 'libmpv archive checksum mismatch.' }
$sevenZip = (Get-Command 7z -ErrorAction SilentlyContinue).Source
if (!$sevenZip) { $sevenZip = "$env:ProgramFiles/7-Zip/7z.exe" }
if (!(Test-Path $sevenZip)) { throw 'Install 7-Zip to unpack the pinned libmpv build.' }
$native = Join-Path $Destination 'mpv-distribution'
& $sevenZip x $archive "-o$native" -y | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Cannot unpack libmpv.' }
$dll = @(Get-ChildItem $native -Recurse -Filter libmpv-2.dll)
if ($dll.Count -ne 1) { throw 'Expected exactly one libmpv-2.dll.' }
Copy-Item -LiteralPath $dll[0].FullName -Destination (Join-Path $Destination 'libmpv-2.dll')
Get-Item (Join-Path $Destination 'libmpv-2.dll') | Select-Object Name, Length | Format-Table
$dumpbin = Get-ChildItem 'C:/Program Files/Microsoft Visual Studio/2022' -Recurse -Filter dumpbin.exe -ErrorAction SilentlyContinue | Where-Object FullName -Match 'Hostx64.x64' | Select-Object -First 1
if ($dumpbin) { & $dumpbin.FullName /DEPENDENTS (Join-Path $Destination 'libmpv-2.dll') }
# Keep upstream headers, license and other distribution documentation together.
Copy-Item (Join-Path $PSScriptRoot '../docs/THIRD_PARTY.md') $Destination
