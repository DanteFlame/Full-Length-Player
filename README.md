# Full-Length Player

A Windows desktop app for watching a full-length reaction alongside your high-quality
local movie or episode, with both audio tracks audible and the videos synchronized.

## Current build: milestone 6 — composition and fullscreen

Milestone 5 was confirmed on Gonz's PC: speed controls work and seek synchronization
is substantially improved. This build adds the intended overlaid viewing layout.

Reaction A is the background. **Crop top/bottom %** trims unwanted room space;
**Reaction zoom** and **Pan X/Y** move the image inside its clipped area. The cropped
reaction sits at the top of a black **16:9 or 4:3 canvas**.

Source B sits in front, horizontally centered. Choose **Bottom** or **Top**, then
resize with **Source %** or drag a blue corner handle. Its display aspect ratio and
selected edge are retained, with size limited to the canvas. Track and volume
controls for both players remain below the composition.

**F or F11** enters a clean fullscreen composition; **Esc**, F or F11 returns to the
previous window. Playback shortcuts remain available in fullscreen. Layout settings
are session-only for now. Cropping can hide parts of reaction subtitles; source
subtitles stay inside the foreground video.

### Playback speed and keys

Shared speed runs from **0.25× to 4×**, adjustable through the toolbar or keyboard.
Both streams use the same rate; MPV pitch correction remains enabled. Locked speed
changes preserve the stored offset. Each pane shows its own current speed.

| Key | Action |
| --- | --- |
| A | Toggle 1× and the previous speed |
| S / D | Decrease/increase by 0.25× |
| G | Toggle favorite speed and the previous speed |
| J / K / L | Back 5 s / play-pause / forward 5 s, both players |
| Left / Space / Right | Existing aliases for the same shared actions |
| Comma / period | Offset −0.1 s / +0.1 s |
| Shift + J/K/L or arrows/Space | Apply to the player under the pointer |
| Shift + A/S/D/G | Still change the shared speed for both players |
| F / F11; Esc | Toggle fullscreen; exit fullscreen |
| F1 / F2 | Choose the fallback player when the pointer is outside both panes |
| Ctrl+O | Open media in the explicitly selected player |

Ordinary keys always target both players. Independent pause/seek
changes unlock sync. Speed always affects both, even with Shift held.
Track menus and the offset numeric field retain normal keyboard editing/navigation.

**Favorite settings** changes the favorite (default 2×), saved to
`%LOCALAPPDATA%/FullLengthPlayer/preferences.json`. Session media/offset/layout saving
is still future work. **H** is reserved for “What Did They Say?” and is not active.


A/G remember the actual speed on entering that toggle. Repeating the same key
restores it. S/D or choosing a speed ends the temporary toggle. Switching from A
to G (or vice versa) starts a new toggle from the speed currently playing; already
being at the destination with no active toggle is a no-op. Speed toggle memory is shared. Loading media or changing the favorite resets it.


### Seek/resume behavior

All shared seeks pause both players, seek, and wait for **both** positions/decoders
to settle before restoring their previous play/pause intent together. Rapid jumps
build on the pending target, not an intermediate decoder position. Pressing play/pause
while seeking changes the eventual resume intent. A 15-second timeout leaves both
paused with a retry message. An independent action cancels pending automatic resume.

The fixed `B = A + offset` lock, signed offset/nudges and two-second correction cadence
remain. Automatic corrections now briefly hold both while correcting B, so A cannot
run away during B's corrective seek. Native pause mismatches while locked pause both
and allow alignment recovery; they no longer disable correction indefinitely.
These are separate native players, so sample-perfect audio is not guaranteed; real
media testing still matters. The 80 ms correction threshold is unchanged.

“What Did They Say?”, Patreon/HLS and YouTube handling remain later stages.

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
4. Load Source B, align while paused, then **Lock current alignment**.
5. Crop the reaction's empty top space, then adjust zoom/pan to frame the reactors.
6. Drag a blue source corner; confirm it stays centered and attached to the chosen edge.
7. Switch Top/Bottom and 16:9/4:3, resize the window, and try F/F11 and Esc.
8. Check that both videos, audio and subtitles keep playing through layout changes.
9. Check Shift+S/D still changes both speeds without unlocking; Shift+J/K/L targets
   the hovered video and unlocks for manual alignment.

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
mixed pause states, shared jumps/timeline, unchanged volumes and start/end clamping.
Milestone 4 injects drift during paused and playing states, verifies automatic
recovery and a stable stored offset across repeated seeks, both nudge directions,
negative-offset bounds, invalid-offset rejection and unlock on manual edits/reload.
Milestone 5 tests native speed values, A/G restoration/interleaving, speed limits,
J/K/L dispatch, hover/fallback targeting, favorite persistence, coordinated resume,
rapid queued skips and pausing during an in-progress seek. Milestone 6 checks canvas
aspect, source centering/anchors/bounds, reaction crop/pan, foreground hover,
fullscreen restoration, native handle retention and Shift speed sharing. It also
captures the actual composed window for inspection. The media fixture uses a Unicode filename.
Application artifacts upload only after both tests pass; JSON/frame/error evidence
is uploaded separately.

CI uses a null audio output because hosted runners have no speakers. It proves audio
decoding, not audible output. Its synthetic MPEG-4 fixture does not establish AV1/HEVC,
hardware-decoding or subtitle correctness on every PC; those are the manual tests above.
Caught startup errors are logged to `%LOCALAPPDATA%/FullLengthPlayer/logs/startup-error.log`.
