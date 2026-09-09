# Full-Length Player

A Windows desktop app for watching a full-length reaction alongside your high-quality
local movie or episode, with both audio tracks audible and the videos synchronized.

## Current build: milestone 2 — two independent MPV players

Gonz confirmed milestone 1 on his Windows PC with both HEVC and AV1 anime:
video, audio, subtitles, seeking, volume and replacing media all work.

This build displays **Reaction A** on the left and **Source B** on the right.
Each owns an independent MPV instance and has its own:

- Open video / drag-and-drop, play/pause, timeline, ±5-second seeking and volume.
- **Audio** and **Subtitles** dropdown menus with track titles, language, codec,
  a checkmark for the current selection, and an Off entry. Missing metadata falls
  back to the track number/type. External subtitles appear in the subtitle menu.
- External subtitle loading and replacement-media loading.

Both videos and audio streams can play simultaneously. Controls only affect their
own player. Click a pane's heading/controls, or use **F1** for A / **F2** for B,
to select the player for **Space**, **Left/Right** and **Ctrl+O**. The active heading
is blue and says “Keyboard controls.” Track-menu arrow navigation remains native.
A command-line first path opens A; an optional second path opens B.

Side-by-side is a temporary verification layout. Shared transport, synchronization,
URL loading and the final cropped/anchored overlay composition are later milestones.
Confirm two simultaneous videos and audible audio before proceeding to milestone 3.

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
4. Load a different local video into Source B. Confirm both pictures move and both
   audio tracks are audible. HEVC + AV1 together is a useful real-world test.
5. Pause, seek and change volume on A; confirm B keeps playing unchanged. Reverse roles.
6. Use each Audio/Subtitles menu to pick a named track; confirm it affects only that pane.
7. Replace one video while the other plays. Resize/maximize, close and reopen.

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
resume and replacement on both sides. The media fixture uses a Unicode filename.
Application artifacts upload only after both tests pass; JSON/frame/error evidence
is uploaded separately.

CI uses a null audio output because hosted runners have no speakers. It proves audio
decoding, not audible output. Its synthetic MPEG-4 fixture does not establish AV1/HEVC,
hardware-decoding or subtitle correctness on every PC; those are the manual tests above.
Caught startup errors are logged to `%LOCALAPPDATA%/FullLengthPlayer/logs/startup-error.log`.
