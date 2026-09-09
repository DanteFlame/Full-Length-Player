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
# This libmpv build imports the Vulkan loader even when using D3D11.
# Ship the official redistributable beside the app; do not install machine-wide.
$vulkanZip = Join-Path $env:TEMP 'flp-vulkan-1.4.357.0.zip'
Invoke-WebRequest 'https://sdk.lunarg.com/sdk/download/1.4.357.0/windows/vulkan-runtime-components.zip' -OutFile $vulkanZip
if ((Get-FileHash $vulkanZip).Hash -ne 'A14672EFED15AAFC7F5A16572D35CD3A3416EADF670AEEE3CDF50EE32D5FBF83') { throw 'Vulkan runtime checksum mismatch.' }
$vulkanDir = Join-Path $Destination 'vulkan-distribution'
Expand-Archive $vulkanZip -DestinationPath $vulkanDir -Force
$loaders = @(Get-ChildItem $vulkanDir -Recurse -Filter vulkan-1.dll | Where-Object FullName -Match '[\\/]x64[\\/]')
if ($loaders.Count -ne 1) { Get-ChildItem $vulkanDir -Recurse | Select-Object FullName; throw 'Expected one x64 Vulkan loader.' }
$signature = Get-AuthenticodeSignature $loaders[0].FullName
if ($signature.Status -ne 'Valid') { throw "Vulkan loader signature invalid: $($signature.Status)" }
Copy-Item -LiteralPath $loaders[0].FullName -Destination (Join-Path $Destination 'vulkan-1.dll')
Get-FileHash $vulkanZip | Format-List
# Keep upstream headers, license and other distribution documentation together.
Copy-Item (Join-Path $PSScriptRoot '../docs/THIRD_PARTY.md') $Destination
