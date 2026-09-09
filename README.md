# Full-Length Player

A Windows desktop player being rebuilt around **MPV/libmpv** for synchronized
full-length reactions and local movies/shows.

## Current milestone: 0 — window foundation

This build only opens a black, resizable window titled **Full-Length Player**.
Closing it exits the application. Playback and MPV integration are deliberately
not implemented yet. The old LibVLC implementation and dependencies have been removed
on this rebuild branch; their history remains available at commit
`0bb6eef83658b3792be28edae9a4c0cb11deb8b5`.

The authoritative product decisions and later milestones are in
[docs/SPECIFICATION.md](docs/SPECIFICATION.md). Do not start milestone 1 until the
Windows launch check passes and Gonz confirms the downloaded build opens on his PC.

## Download and run

1. Open this branch's successful **Build Windows** run in GitHub Actions.
2. Download the **FullLengthPlayer-win-x64** artifact and extract it. If it contains
   another ZIP, extract that too.
3. Run **FullLengthPlayer.exe** from the extracted folder. Keep the entire folder
   together; the .NET runtime is included, so no separate runtime install is required.
4. Confirm the black window appears, resize/maximize it, close it, and launch it again.

Target: Windows 10/11 x64. The build is unsigned. A Windows CI pass verifies the
runner's desktop, not every Windows machine; local confirmation remains the gate.

## Build on Windows

Install the .NET 8 SDK and run from PowerShell:

```powershell
.\build.ps1
```

Output: `publish\FullLengthPlayer-win-x64\FullLengthPlayer.exe`.
The build script also runs the launch test. `-SkipSmokeTest` only publishes;
it does not establish launch verification.

## Verification

The Windows workflow publishes a self-contained build, ZIPs and extracts it, and
launches that extracted executable with an unrelated working directory. An external
Win32 probe checks the exact window title, visibility, message-loop responsiveness,
two resize operations, and normal close with exit code 0. The sequence runs twice.
The application artifact is uploaded only after all checks pass. The separate
`window-launch-evidence` artifact contains the EXE hash and results.

This verifies only the window foundation, not decoding, audio, subtitles or sync.
No media engine, saved settings or network access participates in startup.
Caught errors display a dialog and exit nonzero; when writable, details are saved
to `%LOCALAPPDATA%\FullLengthPlayer\logs\startup-error.log`.
