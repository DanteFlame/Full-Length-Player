param([Parameter(Mandatory)][string]$Executable)
$ErrorActionPreference = 'Stop'
# Live-service probe is separate from deterministic playback tests: CI IPs may be blocked.
$bin = Join-Path (Split-Path (Resolve-Path $Executable)) 'youtube'
$info = [Diagnostics.ProcessStartInfo]::new((Join-Path $bin 'yt-dlp.exe'))
$info.UseShellExecute = $false; $info.CreateNoWindow = $true
$info.RedirectStandardOutput = $true; $info.RedirectStandardError = $true
foreach ($arg in @('--ignore-config','--no-plugin-dirs','--no-cache-dir','--no-playlist','--skip-download','--dump-single-json','--no-progress','--socket-timeout','15','--retries','1','--extractor-retries','1','--js-runtimes',"deno:$(Join-Path $bin 'deno.exe')",'--format','bestvideo+bestaudio/best','--','https://www.youtube.com/watch?v=aqz-KE-bpKQ')) { $info.ArgumentList.Add($arg) }
$p = [Diagnostics.Process]::new(); $p.StartInfo = $info
try {
    $null = $p.Start(); $outTask = $p.StandardOutput.ReadToEndAsync(); $errTask = $p.StandardError.ReadToEndAsync()
    if (!$p.WaitForExit(95000)) { $p.Kill($true); $p.WaitForExit(); $result = 'timeout' }
    else { $result = if ($p.ExitCode -eq 0) { 'extraction succeeded' } else { 'extraction failed' } }
    $detail = $errTask.GetAwaiter().GetResult() -replace 'https?://\S+', '[URL]' -replace 'aqz-KE-bpKQ', '[video ID]' -replace '[A-Za-z0-9_\-+/=]{32,}', '[opaque value]'
    $detail = $detail -replace '(?im)^.*(?:cookie|authorization|password|bearer).*$', '[credential-related line omitted]'
    $summary = "Live public YouTube probe: $result`n$detail"
    New-Item -ItemType Directory -Force test-results | Out-Null
    $summary | Set-Content test-results/youtube-probe.txt
    Write-Host $summary
} finally { $p.Dispose() }
