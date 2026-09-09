# Full-Length Player

A Windows desktop app for watching a full-length reaction alongside your high-quality
local movie or episode, with both audio tracks audible and the videos synchronized.

## Current build: milestone 4 — fixed offset and drift correction

Gonz confirmed milestone 3's shared controls. He observed drift building up across
master seeks, which this milestone addresses with a fixed **B = A + offset** model.

1. Load both local videos and pause both.
2. Align them using the individual timelines/controls.
3. Click **Lock current alignment**. The stored offset is Source B time minus Reaction A time.
4. Use shared play/pause, jumps and the master timeline. Every locked seek derives
   B's target from that stored value, never from the most recent drifted position.

Use **−0.1 s / +0.1 s** (or **comma / period**) to fine-tune B against A. Positive
nudges move B later in its file; negative nudges move it earlier. You can also type
seconds in **Offset B−A** and click **Apply offset**, which enables the lock.
Positive and negative offsets work. Non-overlapping offsets are rejected.

The status shows the fixed offset, measured drift and correction activity. Every
two seconds, when neither player is seeking/buffering, drift above **80 ms** triggers
an exact corrective seek of **B only**. A two-second settling period follows shared
seeks/pauses/offset changes; tiny errors are ignored to avoid repeated micro-seeks.
This first correction approach can produce a small audible/video skip in B when it
corrects; assess it with real reaction audio. It does not alter volumes or speed.

Shared seeks stay inside the common playable range; at its end both players pause.
Unlock explicitly for free playback. **Independent seeking, pausing or replacing a
file automatically unlocks** so correction cannot undo manual changes. Volume and
track changes leave the lock intact. After realigning, click Lock current alignment
again. The offset stays fixed across this session's shared controls; saving sessions
across app restarts remains a later milestone.

- **Space**: play/pause both. **Left/Right**: shared ±5 seconds; buttons: ±10 seconds.
- **F1/F2**: select A/B for independent shortcuts (blue heading).
- **Shift+Space / Shift+Left / Shift+Right**: selected player only, releasing sync lock.
- **Ctrl+O**: open in the selected player. Track-menu navigation and numeric-field
  editing keep their normal keys.
- Each pane retains its independent playback, volume and named audio/subtitle controls.

Side-by-side is still the temporary test layout. Confirm this milestone before
adding shared playback speed and the later composition/online-source features.

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
4. Load Source B, pause both, align the files independently, then click **Lock current alignment**.
5. Repeatedly jump forward/back and seek across the shared timeline. The stored
   offset should remain unchanged and transient drift should settle after a few seconds.
6. Test ±0.1-second nudges and a negative offset; verify the direction matches expectations.
7. Play for several minutes and listen for correction skips, persistent echo or growing drift.
8. Confirm independent seeking/pausing unlocks; relock and repeat. Replace a file and
   verify it unlocks. Volume/track changes should keep the lock enabled.

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
negative-offset bounds, invalid-offset rejection and unlock on manual edits/reload. The media fixture uses a Unicode filename.
Application artifacts upload only after both tests pass; JSON/frame/error evidence
is uploaded separately.

CI uses a null audio output because hosted runners have no speakers. It proves audio
decoding, not audible output. Its synthetic MPEG-4 fixture does not establish AV1/HEVC,
hardware-decoding or subtitle correctness on every PC; those are the manual tests above.
Caught startup errors are logged to `%LOCALAPPDATA%/FullLengthPlayer/logs/startup-error.log`.
