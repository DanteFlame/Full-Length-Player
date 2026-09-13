param(
    [Parameter(Mandatory)][string]$Executable,
    [string]$ResultsDirectory = (Join-Path $PSScriptRoot '../test-results')
)
$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
if ($env:OS -ne 'Windows_NT') { throw 'This test requires a Windows desktop session.' }
$Executable = (Resolve-Path $Executable).Path
New-Item -ItemType Directory -Force $ResultsDirectory | Out-Null

if (-not ('WindowProbe' -as [type])) {
    Add-Type @'
using System;
using System.Runtime.InteropServices;
public static class WindowProbe {
    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }
    [DllImport("user32.dll")]
    public static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern IntPtr SendMessageTimeout(IntPtr window, uint message,
        UIntPtr wparam, IntPtr lparam, uint flags, uint timeout, out UIntPtr result);
    [DllImport("user32.dll", SetLastError=true)]
    public static extern bool MoveWindow(IntPtr window, int x, int y, int width, int height, bool repaint);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr window, out Rect rect);
}
'@
}

function Assert-Responsive([IntPtr]$Window) {
    [UIntPtr]$result = [UIntPtr]::Zero
    # WM_NULL with SMTO_ABORTIFHUNG: the real UI thread must answer within two seconds.
    if ([WindowProbe]::SendMessageTimeout($Window, 0, [UIntPtr]::Zero, [IntPtr]::Zero, 2, 2000, [ref]$result) -eq [IntPtr]::Zero) {
        throw 'Window message loop did not respond.'
    }
}

$results = @()
# Run the ordinary executable twice; no test-only application launch mode.
foreach ($attempt in 1..2) {
    $process = $null
    try {
        # Deliberately launch from a directory different from the executable's directory.
        $process = Start-Process -FilePath $Executable -WorkingDirectory $env:TEMP -PassThru
        if (!$process.WaitForInputIdle(30000)) { throw 'Application did not finish initializing its message loop.' }
        $deadline = [DateTime]::UtcNow.AddSeconds(30)
        do {
            $process.Refresh()
            if ($process.HasExited) { throw "Application exited before showing its window: $($process.ExitCode)" }
            if ($process.MainWindowHandle -ne [IntPtr]::Zero) { break }
            Start-Sleep -Milliseconds 100
        } while ([DateTime]::UtcNow -lt $deadline)
        $window = $process.MainWindowHandle
        if ($window -eq [IntPtr]::Zero) { throw 'No main window appeared within 30 seconds.' }
        if ($process.MainWindowTitle -ne 'Full-Length Player') { throw "Unexpected window: $($process.MainWindowTitle)" }
        if (![WindowProbe]::IsWindowVisible($window)) { throw 'Main window is hidden.' }
        Assert-Responsive $window
        foreach ($size in @(@(480, 320), @(640, 480))) {
            if (![WindowProbe]::MoveWindow($window, 40, 40, $size[0], $size[1], $true)) { throw 'Resize failed.' }
            Assert-Responsive $window
            $rect = New-Object WindowProbe+Rect
            if (![WindowProbe]::GetWindowRect($window, [ref]$rect)) { throw 'Cannot read window bounds.' }
            if (($rect.Right - $rect.Left) -ne $size[0] -or ($rect.Bottom - $rect.Top) -ne $size[1]) {
                throw "Window did not adopt requested $($size[0])x$($size[1]); actual $($rect.Right - $rect.Left)x$($rect.Bottom - $rect.Top)."
            }
        }
        # Catch delayed startup failures and confirm the event loop stays responsive.
        Start-Sleep -Seconds 2
        Assert-Responsive $window
        if (!$process.CloseMainWindow()) { throw 'Could not request normal window close.' }
        if (!$process.WaitForExit(10000)) { throw 'Application did not exit after closing the window.' }
        if ($process.ExitCode -ne 0) { throw "Application exited with code $($process.ExitCode)." }
        $results += [ordered]@{ attempt = $attempt; visible = $true; responsive = $true; resized = $true; exitCode = $process.ExitCode }
    }
    finally {
        if ($null -ne $process) {
            if (!$process.HasExited) { Stop-Process -Id $process.Id -Force }
            $process.Dispose()
        }
    }
}
[ordered]@{
    executable = $Executable
    sha256 = (Get-FileHash $Executable -Algorithm SHA256).Hash
    verifiedUtc = [DateTime]::UtcNow.ToString('o')
    runs = $results
} | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $ResultsDirectory 'window-smoke.json')
Write-Host 'PASS: published EXE launched, displayed, responded, resized and closed twice.'
