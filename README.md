# Full-Length Player

A Windows desktop app for watching a full-length reaction alongside your high-quality
local movie or episode, with both audio tracks audible and the videos synchronized.

## Current build: milestone 3 — shared transport

Gonz confirmed milestone 2: two different codecs play together, with working
independent playback, seeking, volumes, and named audio/subtitle menus.

The new **BOTH PLAYERS** bar adds play/pause, ±10-second jumps, and a shared
timeline referenced to Reaction A. Shared controls become available when both
videos have loaded. If either video is playing, master play/pause pauses both;
if both are paused, it starts both.

Shared seeks move both videos by the same number of seconds from their current
positions. For example, A at 8 seconds and B at 12 seconds become A at 18 and B
at 22 after +10. Dragging the shared timeline to A=5 moves B to 9. At either
file's start/end, the movement of both is limited equally to preserve alignment.
Seeking retains each player's play/pause state.

- **Space**: play/pause both. **Left/Right**: move both ±5 seconds.
- **F1/F2**: select A/B for independent shortcuts (blue heading).
- **Shift+Space / Shift+Left / Shift+Right**: selected player only.
- **Ctrl+O**: open a file in the selected player.
- Each pane's buttons, timeline, volume and named track menus remain independent.

This is shared command delivery, not a continuous synchronization system. The
current separation is respected for each seek; there is no saved offset or drift
correction until milestone 4. Side-by-side is still the temporary test layout.

## Technology

**C# / .NET 8 WinForms**, Windows 10/11 x64, with a small direct P/Invoke wrapper
around the **libmpv C API**. MPV handles video/audio decoding and subtitle rendering;
WinForms provides the window and controls. The native player renders into a child
window using D3D11. Hardware decoding uses `auto-safe`, with software fallback.
There is no browser playback layer or LibVLC dependency.

The download includes the .NET runtime, `libmpv-2.dll`, and the official Vulkan loader
required by this MPV build (even though playback uses D3D11). Build tooling pins the
standard x86_64 shinchiro MPV build dated 20260901 and checks its SHA-256 before
unpacking; it does not require an x86_64-v3 CPU. See [third-party details](docs/THIRD_PARTY.md).
The native wrapper is isolated in `MpvPlayer.cs`, each player and its controls in
`PlayerPane.cs`, window orchestration in `MainForm.cs`, and CI playback
checks in `PlaybackVerification.cs`. Personal mpv configuration/scripts are disabled.

## Download and test

1. Download **FullLengthPlayer-win-x64** from this branch's successful **Build Windows** run.
2. Extract the artifact and the inner ZIP into a fresh folder. Keep all files together.
3. Launch **FullLengthPlayer.exe**, choose **Open video**, and select a local MKV/MP4.
4. Load Source B. Use **Play / Pause both**, then the ±10-second buttons.
5. Pause both, position A and B a few seconds apart using their own timelines, then
   use the shared timeline. Their separation should remain approximately the same.
6. Test **Space / Left / Right** for both, then **F1/F2 + Shift shortcuts** independently.
7. Check each pane's controls still affect only that video, then close and reopen.

Please report stutter, black video, missing sound/subtitles, or one player's controls
unexpectedly affecting the other. Keep all downloaded files together. The app still
has its temporary standard Windows icon and title bar.

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
twice, then loads two distinct generated local videos with audio. It checks concurrent
decoding, independent pause/seek/volume, named track selection and checkmarks, external
subtitle selection/off, track isolation, decoded frame capture from both players,
resume and replacement on both sides. Milestone 3 also checks shared play/pause,
mixed pause states, shared jumps/timeline, unchanged volumes and start/end clamping. The media fixture uses a Unicode filename.
Application artifacts upload only after both tests pass; JSON/frame/error evidence
is uploaded separately.

CI uses a null audio output because hosted runners have no speakers. It proves audio
decoding, not audible output. Its synthetic MPEG-4 fixture does not establish AV1/HEVC,
hardware-decoding or subtitle correctness on every PC; those are the manual tests above.
Caught startup errors are logged to `%LOCALAPPDATA%/FullLengthPlayer/logs/startup-error.log`.
