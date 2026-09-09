# Full-Length Player

A Windows desktop app for watching a full-length reaction alongside your high-quality
local movie or episode, with both audio tracks audible and the videos synchronized.

## Current build: milestone 1 — one embedded MPV player

Milestone 0 passed Windows CI and Gonz confirmed the downloaded EXE opens, resizes,
closes and reopens on his PC. This next build adds **one local-video player**:

- Open a local video with **Open video**, **Ctrl+O**, drag-and-drop, or a command-line path.
- Play/pause with the button or **Space**; seek with the timeline or **Left/Right** (5 seconds).
- Set volume from 0–100%; cycle embedded audio/subtitle tracks or load external subtitles.
- Resize/maximize the window while the video stays embedded and preserves its aspect ratio.

This is an incremental test build. Two players, synchronization, URL loading and the
final composition layout are still future milestones. Confirm local playback on
Gonz's PC before implementing the second player.

## Technology

**C# / .NET 8 WinForms**, Windows 10/11 x64, with a small direct P/Invoke wrapper
around the **libmpv C API**. MPV handles video/audio decoding and subtitle rendering;
WinForms provides the window and controls. The native player renders into a child
window using D3D11. Hardware decoding uses `auto-safe`, with software fallback.
There is no browser playback layer or LibVLC dependency.

The download includes the .NET runtime and `libmpv-2.dll`. Build tooling pins the
standard x86_64 shinchiro MPV build dated 20260901 and checks its SHA-256 before
unpacking; it does not require an x86_64-v3 CPU. See [third-party details](docs/THIRD_PARTY.md).
The native wrapper is isolated in `MpvPlayer.cs`, UI in `MainForm.cs`, and CI playback
checks in `PlaybackVerification.cs`. Personal mpv configuration/scripts are disabled.

## Download and test

1. Download **FullLengthPlayer-win-x64** from this branch's successful **Build Windows** run.
2. Extract the artifact and the inner ZIP into a fresh folder. Keep all files together.
3. Launch **FullLengthPlayer.exe**, choose **Open video**, and select a local MKV/MP4.
4. Check moving video and audible sound. Try an AV1 and/or HEVC file if available.
5. Pause/resume, seek forward/back, adjust volume, and select subtitles/audio tracks.
6. Resize/maximize while playing, open another file, then close and reopen the app.

Please report any black video, missing sound/subtitles, playback stutter or errors,
along with the codec/file type. The status line shows current audio/subtitle track IDs.
The window still has its temporary standard Windows icon and title bar.

## Planned full player

- Reaction = Player A/master; local source = Player B, following `B = A + offset`.
- Shared play/pause, seeking and speed, independent alignment controls, ±0.10 s nudges,
  drift correction, and separate volumes/tracks/subtitles. Both audio tracks play.
- Crop and reposition the reaction inside its mask. Overlay the source horizontally
  centered, resizable, and anchored to the top or bottom of the whole canvas.
- Direct HLS/Patreon streams with configurable headers and Patreon Referer support;
  unlisted YouTube URL resolution in a separate later milestone.
- Persist settings; attempt audio-fingerprint automatic alignment after manual sync works.
- **What Did They Say?**: save position/speed/volumes/mutes, rewind both 10 seconds,
  play at 1× with source muted and reaction at 100%, then restore the exact prior
  settings when A reaches the original trigger time. Button and keyboard shortcut.

The complete authoritative requirements and milestone sequence are in
[docs/SPECIFICATION.md](docs/SPECIFICATION.md). Explicit user decisions supersede old
README requirements. The old LibVLC application remains in Git history at
`0bb6eef83658b3792be28edae9a4c0cb11deb8b5`; the rebuild continues in PR #10.

## Build and verification

On Windows, install .NET 8 SDK and 7-Zip, then run `./build.ps1` in PowerShell.
Output: `publish/FullLengthPlayer-win-x64/FullLengthPlayer.exe`.
The script downloads the pinned native dependency and runs window verification.
`-SkipSmokeTest` only packages; it does not establish verification.

GitHub Actions tests the extracted ZIP from a path containing spaces and an unrelated
working directory. It verifies visible/responsive windows, resize and clean shutdown
twice, then uses a generated local video with audio and subtitles to check native
initialization, decoding, pause, seek, volume, subtitle selection, frame capture,
resume and reopening media. It tests a Unicode filename too. Application artifacts
are uploaded only after both tests pass; JSON/frame/error evidence is uploaded separately.

CI uses a null audio output because hosted runners have no speakers. It proves audio
decoding, not audible output. Its synthetic MPEG-4 fixture does not establish AV1/HEVC,
hardware-decoding or subtitle correctness on every PC; those are the manual test above.
Caught startup errors are logged to `%LOCALAPPDATA%/FullLengthPlayer/logs/startup-error.log`.
