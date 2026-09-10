param([Parameter(Mandatory)][string]$Destination)
$ErrorActionPreference = 'Stop'
$target = Join-Path $Destination 'youtube'
New-Item -ItemType Directory -Force $target | Out-Null
$exe = Join-Path $target 'yt-dlp.exe'
Invoke-WebRequest 'https://github.com/yt-dlp/yt-dlp/releases/download/2026.08.19/yt-dlp.exe' -OutFile $exe
if ((Get-FileHash $exe).Hash -ne '66674953fe251b89f4d08c5f0e35e0728679bd67ab3d7d05c0562af101dd3e7a') { throw 'yt-dlp checksum mismatch.' }
$zip = Join-Path $env:TEMP 'flp-deno-2.9.6.zip'
Invoke-WebRequest 'https://github.com/denoland/deno/releases/download/v2.9.6/deno-x86_64-pc-windows-msvc.zip' -OutFile $zip
if ((Get-FileHash $zip).Hash -ne '15e5300b0ba3c3695a7621d90160a746ec9e710228cee639afa9d580f6e3cd11') { throw 'Deno checksum mismatch.' }
Expand-Archive $zip -DestinationPath $target -Force
Invoke-WebRequest 'https://raw.githubusercontent.com/yt-dlp/yt-dlp/2026.08.19/LICENSE' -OutFile (Join-Path $target 'yt-dlp-LICENSE.txt')
Invoke-WebRequest 'https://raw.githubusercontent.com/yt-dlp/yt-dlp/2026.08.19/THIRD_PARTY_LICENSES.txt' -OutFile (Join-Path $target 'yt-dlp-THIRD_PARTY_LICENSES.txt')
Invoke-WebRequest 'https://raw.githubusercontent.com/denoland/deno/v2.9.6/LICENSE.md' -OutFile (Join-Path $target 'deno-LICENSE.md')
& $exe --version
if ($LASTEXITCODE -ne 0) { throw 'Bundled yt-dlp failed to launch.' }
& (Join-Path $target 'deno.exe') --version
if ($LASTEXITCODE -ne 0) { throw 'Bundled Deno failed to launch.' }
