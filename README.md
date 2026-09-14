# Full-Length Player

A Windows desktop app for watching a full-length reaction alongside your high-quality
local movie or episode, with both audio tracks audible and the videos synchronized.

## Current build: milestone 13 — session setup and app icons

Milestone 12 is confirmed with a remote reaction and local source pairing and merged
into `main`. This build adds:

- **New session**: clear both videos and reset layout, alignment, speed and audio to
  defaults without restarting. The previous complete pairing stays available through
  Resume last session. The canvas again chooses the closest display aspect ratio.
  Favorite speed and your icon preference remain personal preferences.
- **Paused loading**: local files, direct streams and resolved YouTube videos load
  paused, ready for setup. Press master play when the pairing is aligned.
- **Appearance…**: choose among six supplied Solid/Glass icons in Slate/Red,
  Teal/Gold and Teal/Orange. The window and running taskbar icon change immediately
  and the preference survives restart. The EXE and newly created default shortcuts use Teal/Orange
  Glass; Windows may retain a pinned shortcut's own icon.

All six original JPEGs are preserved in `assets/icons`. The Windows build packages
16, 24, 32, 48, 64, 128 and 256 pixel icon frames from each original. This first pass
retains the supplied white background and artwork. Small-size art simplification
and transparent-background masters remain possible appearance refinements.

**PC check:** load each source type and confirm it stays paused; use New session
while aligned or during H replay and confirm a blank default setup; try all six
icons in Appearance and restart to confirm your selection is remembered.

The requested default is now **Teal / Orange · Glass** (translucent crystal).
Previously saved icon choices remain respected, and all six choices remain in the
picker. The user supplied `FLP icons.zip` for the pending transparent-source rebuild;
those originals must be inspected before replacing the current JPEG-based assets.
The requested asset refinement is to trim excess alpha margins, preserve transparency
at every size, sharpen downsampling, and inspect icons on light and dark backgrounds.

## Saved settings and sessions (milestone 12)

Milestone 11 audio matching and dialog fixes are confirmed on Gonz's PC and merged
into `main`. Audio matching stays unchanged in this milestone.

- Closing the app remembers crop, pan, zoom, source size/edge, volumes, mute states
  and shared playback speed. Startup still chooses the canvas closest to the display.
- **Resume last session** reopens the last complete video pairing saved on exit.
- **Save session… / Open session…** keep named pairings, including positions,
  the locked offset, layout, canvas, audio/subtitle selections, volumes and speed.
- Restoring waits for both media files and leaves both players **paused**.
  A named session or resume restores its saved canvas, overriding the startup choice.
- YouTube sessions retain the original video link and resolve fresh playback URLs.
  Expired Patreon/CDN links need replacing using Open URL. Missing local files need
  returning to their saved paths or reopening manually.
- External subtitle files are not restored yet; embedded track selections are.
  Re-add external subtitles after resuming.

Settings live in `%LOCALAPPDATA%/FullLengthPlayer/view-settings.json`; the last
pairing lives in `last-session.flpsession` there. Sessions can contain signed URLs
and HTTP headers, so the entire session is encrypted using Windows DPAPI. Named
`.flpsession` files and the last session are intended for the same Windows account
on the same PC, not portable sharing. Ordinary view settings contain no media URLs.
Opening the app does not automatically load streams: press Resume when ready.
Saving during “What Did They Say?” first restores your normal viewing settings.

### Milestone 12 check on your PC

1. Load a pair, lock alignment, choose tracks, and adjust layout, volumes and speed.
2. Save a named session, seek elsewhere, then open it; check both resume paused at
   the saved alignment with the saved settings.
3. Close and reopen the app. Check remembered settings and automatic display canvas,
   then press Resume last session and check the pairing and positions.
4. Try a YouTube or Patreon/local pairing. Expired links may need refreshing.

### Fullscreen viewing and complete playback

Milestones 0–12 are confirmed on Gonz's PC and merged into `main`; new development
uses small feature branches. The old LibVLC implementation remains in Git history.

- At startup, choose the closest fixed canvas (16:9, 4:3 or 16:10) to the display
  containing the app. This uses full display bounds, not taskbar-reduced work area.
  The canvas selector remains available for manual changes.
- Fullscreen mouse movement, pause/play and J/L show a seekable master timeline
  for 2.5 seconds. Cursor and timeline hide when idle, even paused; dragging keeps
  them visible. Leaving fullscreen or switching apps restores the cursor.
- J/L, arrows and master skip buttons use five seconds. H still replays ten seconds.
- Locked timeline covers both videos in full, including source-only preamble or
  credits. Each video waits at its first frame before its start and holds its last
  frame afterward. The timeline begins at whichever video starts first. Offset
  remains B−A; clock labels show elapsed time across the whole combined span.
- Audio sync defaults to multiple distinct 20-second samples across up to the next
  ten minutes, searching ±60 seconds around the estimated alignment for each.
  Agreeing strong matches within 0.10 seconds produce a median on the 0.05
  grid. A lone strong match is labelled as an unconfirmed candidate. Conflicting
  strong matches are reported, never averaged. It stops early on three agreeing
  samples or after an approximately ten-second budget. Slow network
  cancellation/cleanup can take longer. Uncheck multiple samples for the original
  single-sample check; short remaining clips may not provide enough samples.

### Confirmed milestone 10 controls

Gonz confirmed audio alignment against matching anime intros and a real reaction,
within 0.05 seconds of his manual alignment. The search radius is now ±60 seconds;
it still compares a 20-second A sample and rejects weak or ambiguous matches.
A wider radius searches more possible offsets; it does not automatically choose a
cleaner reaction scene. Move A to clearer shared audio if commentary overwhelms it.

- **H / What Did They Say?**: with alignment locked, rewind A by up to ten seconds
  and position B using the stored offset. Play both at 1×, mute B, unmute A at 100%.
  At A's original timestamp, restore both prior volumes, mute states and speeds and
  continue playing. Near A's beginning the rewind stops at zero. Press H again to
  end replay early. Manual transport, speed, offset or volume edits, and replacing
  media, restore the saved state first. Fullscreen/layout changes do not cancel it.
- **Shift + mouse wheel over video**: adjust only that video's volume, five
  percentage points per notch, clamped to 0–100%. Works in fullscreen. Overlapping
  regions target the visible foreground source. Volume sliders remain synchronized.
- **Opposite reaction edge**: source at the top anchors the cropped reaction to the
  bottom; source at the bottom anchors it to the top. Cropping keeps that anchor.
  Existing manual pan remains available as an adjustment within the mask.

Milestone 12 adds session persistence; reusable presets and UI polish remain later work.

### Audio-assisted alignment

Milestone 8 is confirmed on Gonz's PC: YouTube now loads, cropping starts at zero,
and B waits at its first frame during the reaction preamble. YouTube buffering
performance is a later improvement. End-of-source discussion passed automated
Windows testing; a real-media confirmation remains useful.

### Find audio sync

Position both players near the same shared scene, within roughly 60 seconds of
alignment. Choose **Find audio sync… → Analyze audio**. The app reads 20 seconds
of Reaction A and searches nearby Source B audio using the selected audio tracks.
Clear shared music/dialogue works best; heavy commentary, different edits, dubbing,
repeated music or little audible source audio may prevent a match.

A clear result offers **Apply and lock**, rounding to the existing 0.05-second grid.
Listen afterward and retain your previous offset if you want to restore it manually.
The match score is a similarity measure, not a probability or guarantee. Ambiguous
results change nothing. Cancel leaves playback and alignment untouched.

Analysis uses additional audio-only libmpv instances and temporary mono PCM samples
on this PC, removed when the operation finishes or is canceled. It does not use a
cloud analysis service. Network media requires additional stream requests with the
same HTTP settings; expired URLs may need reopening. Each sample read times out
after two minutes. Playback speed and volume do not affect the analysis. This first
version refines a nearby alignment; whole-episode search is not implemented.

The underlying PCM output options are documented in the [MPV manual](https://mpv.io/manual/master/#audio-output-drivers).


Gonz confirmed Patreon HLS playback with his actual stream: immediate loading,
seeking, synchronization with local media and speed controls all work. He also
confirmed the fixed 16:10 canvas on a MacBook Air through Moonlight.

### Open YouTube

Use **Open URL** on either player and paste a public or unlisted YouTube video link.
Watch, youtu.be, Shorts, embed and individual /live links are recognized. Only that
video is opened; playlist and timestamp parameters are stripped. Playback starts
at the beginning so you can establish the reaction/source alignment yourself.
The Patreon/header fields are disabled for recognized YouTube links.

The bundled **yt-dlp + Deno** resolver retrieves the streams, then MPV plays the
selected video and audio, including separately served high-quality tracks. Existing
playback continues during lookup. **Cancel YouTube**, opening another source or
closing the app cancels it; a stale lookup cannot replace newer media. Lookups time
out after 90 seconds. URLs and signed stream metadata are not saved to application logs; failures save
a redacted diagnostic summary to `%LOCALAPPDATA%\FullLengthPlayer\logs\youtube-error.log`; browser cookies and personal extractor configs are not used.

This milestone targets public/unlisted on-demand videos viewable without signing in.
Private, sign-in/age-gated videos and live broadcasts are not supported here. YouTube
can reject automated requests or change its extraction requirements. On a failure,
retry the original link; a later build may need updated resolver dependencies. No
silent self-updates or extra installation are required.

### Offset precision

**Lock current alignment** rounds B−A to the nearest **0.05 seconds** and applies the
rounded alignment. Typed offsets also snap to that grid. Buttons, comma/period and
numeric arrows all adjust by **0.05 s**. Stored offsets display two decimal places,
e.g. 18.15 → 18.20 → 18.25. Exact halfway values round away from zero. Drift remains
a separate measured value; rounding does not promise sample-perfect audio.

### Open a stream

Choose **Open URL** on either player and paste a direct HTTP/HTTPS media or `.m3u8`
URL. For Reaction A, **Patreon Referer preset** starts enabled and supplies
`https://www.patreon.com`. You can edit the Referer or clear it for other streams.
Optional HTTP headers use one `Name: value` per line. Commas and backslashes in
values are preserved. Headers apply to the stream and its playlist/segment requests;
only use headers intended for the servers serving that stream.

Patreon requires a direct media URL, not a post page. The Patreon path does not
extract links or sign in. YouTube video links use the separate resolver described above. Signed media URLs may expire;
use **Open URL** again with a fresh authorized URL when needed. 

URLs and headers are protected inside saved sessions and are not written to application logs. They are cleared
on each replacement load and do not transfer to the other player. Network failures
show a retry message without the URL or header values. HTTPS certificate checking
stays enabled. Existing file open controls remain available.

For seeking and locked sync, use an on-demand stream with a known duration and
seek support. Live/unknown-duration streams are not the shared-timeline target of
this milestone. Network buffering can delay playback; the existing sync controller
waits for settling and corrects alignment afterward.

### Composition

Reaction A is the background. **Crop top/bottom %** trims unwanted room space;
**Reaction zoom** and **Pan X/Y** move the image inside its clipped area. The cropped
reaction sits at the top of a black **16:9, 4:3 or 16:10 canvas**.

Source B sits in front, horizontally centered. Choose **Bottom** or **Top**, then
resize with **Source %** or drag a blue corner handle. Its display aspect ratio and
selected edge are retained, with size limited to the canvas. Track and volume
controls for both players remain below the composition.

**F or F11** enters a clean fullscreen composition; **Esc**, F or F11 returns to the
previous window. Playback shortcuts remain available in fullscreen. Layout settings
are remembered by milestone 12 settings and sessions. Cropping can hide parts of reaction subtitles; source
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
| Comma / period | Offset −0.05 s / +0.05 s |
| Shift + J/K/L or arrows/Space | Apply to the player under the pointer |
| Shift + A/S/D/G | Still change the shared speed for both players |
| F / F11; Esc | Toggle fullscreen; exit fullscreen |
| F1 / F2 | Choose the fallback player when the pointer is outside both panes |
| Ctrl+O | Open media in the explicitly selected player |

Transport and speed keys target both players by default. Independent pause/seek
changes unlock sync. Speed always affects both, even with Shift held.
Track menus and the offset numeric field retain normal keyboard editing/navigation.

**Favorite settings** changes the favorite (default 2×), saved to
`%LOCALAPPDATA%/FullLengthPlayer/preferences.json`. Session media/offset/layout saving
is still future work. **H** now triggers “What Did They Say?” (see above).


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

“What Did They Say?” and audio-assisted automatic sync remain later stages.

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
5. Open a public or unlisted YouTube video in A. Wait for video **and audio** to start.
6. Load B locally, align and lock; confirm the stored offset is a multiple of 0.05 s.
7. Test comma/period and the ±0.05 buttons, shared seeking, speed and fullscreen.
8. Try replacing YouTube with Patreon or a local file, and cancelling a pending lookup.

Please report stutter, black video, missing sound/subtitles, or one player's controls
unexpectedly affecting the other. Keep all downloaded files together. The app still
has its temporary standard Windows icon and title bar.

## Planned full player

- Reaction = Player A/master; local source = Player B, following `B = A + offset`.
- Shared play/pause, seeking and speed, independent alignment controls, ±0.05 s nudges,
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
is uploaded separately. Milestone 7 adds a loopback HTTP server requiring the Patreon
Referer and an exact custom header on a redirected HLS master, variant playlist and
segments. It verifies missing-header rejection, concurrent direct HTTP playback,
shared HLS seeking/speed, header isolation/reset and recovery after HTTP 403. This
proves the plumbing; Gonz's current authorized Patreon URL is the real-service test.

CI uses a null audio output because hosted runners have no speakers. It proves audio
decoding, not audible output. Its synthetic MPEG-4 fixture does not establish AV1/HEVC,
hardware-decoding or subtitle correctness on every PC; those are the manual tests above.
Caught startup errors are logged to `%LOCALAPPDATA%/FullLengthPlayer/logs/startup-error.log`.

## UI polish after functionality

The project does not end with functional milestones. A dedicated polish stage will
make the visible controls attractive and more compact while preserving the fixed
aspect preview, setup-before-viewing workflow and clean fullscreen composition.
Gonz's main display is currently a 4:3 iPad Pro streaming Windows through Moonlight.

Milestone 8 verification exercises the packaged yt-dlp executable against a local
HTTP media endpoint, YouTube URL normalization, native split video/audio playback,
shared seeking/speed, cancellation/replacement, external-audio reset and offset
rounding/nudges on positive and negative values. These deterministic checks do not
establish live YouTube availability; Gonz's public/unlisted link test is the acceptance gate.


## Milestone 8 repairs (confirmed)

Gonz confirmed non-Patreon CDN playback, all 0.05-second controls, and successful
YouTube loading in the repair build. If a resolver failure occurs, it writes a redacted
`%LOCALAPPDATA%/FullLengthPlayer/logs/youtube-error.log` including the exit/error stage
and diagnostic text. URLs, video IDs and credential-related lines are removed. The
previous build discarded stderr; it has no detailed retrospective YouTube log.

Locked transport now spans the full reaction timeline. Before B's mapped start,
B stays paused at zero; after B ends, it stays on its last frame while A continues.
Crossing or seeking back into the shared portion rejoins B at the stored offset.
Source visibility stays unchanged for now. Both crop defaults are zero.

Windows checks include automatic boundary crossing, seeking into/out of preamble
and discussion, pause/speed behavior while B is held, and diagnostic redaction.
A separate live public YouTube extraction probe records service behavior from CI;
CI may face restrictions different from the user's PC. Gonz has now confirmed
YouTube loading; milestone 9 audio-assisted alignment is confirmed.


Milestone 9 checks noisy/echoed audio matching, positive and negative offsets,
silence/unrelated/repeated-audio rejection, cancellation and exact-start native PCM
extraction. Network tests also extract audio from protected HLS and separately
served YouTube-style audio. These fixtures do not establish reliability for every
real reaction recording; the first release remains experimental.

Verified downloads normally use Actions artifacts (seven-day retention). If storage
is full, a push to the rebuild branch can attach the tested ZIP to a **draft release**
instead. Existing artifacts are not removed automatically. Draft downloads require
repository access and do not publish a public release.

Milestone 10 tests exact replay state restoration, beginning clamping, cancellation,
replacement, opposite-edge cropping and independent/fullscreen wheel volume.

Milestone 11 tests both added union boundaries, source-to-reaction rejoining,
source-tail seeking/pause/speed, fullscreen timeline visibility, five-second keys,
canvas selection, consensus agreement/conflict and cancellation.
