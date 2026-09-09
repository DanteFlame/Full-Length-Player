# Authoritative product specification

Gonz's explicit decisions in the September 2026 conversation override the former
README and code. This document preserves that agreement; future work must not
mistake the roadmap for features already delivered.

## Platform and media

- Windows executable first. iOS/iPadOS are not current targets.
- MPV/libmpv is the required playback backend, replacing LibVLC and browser video.
- Reaction is player A, the timeline master. Source movie/show is player B.
- B normally loads a high-quality local file. A accepts reaction files, unlisted
  YouTube links and Patreon/HLS streams as later milestones land.
- Both audio tracks play simultaneously, with independent volume and mute controls.
- Preserve MPV's codec capabilities; validate AV1, HEVC, embedded audio/subtitle
  selection and external subtitles against actual sample media.
- Patreon playback must supply a configurable HTTP Referer, defaulting to
  `https://www.patreon.com`, and support required headers. YouTube needs URL
  resolution into streams MPV can consume. Do not log signed URLs or credentials.

## Synchronization

- Master play/pause, timeline seeking, relative jumps and speed changes affect both.
- Independent per-player transport stays available for initial/manual alignment.
- Preserve `B time = A time + offset`; the offset may be negative.
- Numeric offset and ±0.10-second nudges, with ongoing drift correction.
- Retain master ±10-second buttons from the original concept and Left/Right ±5-second
  keyboard jumps. Space toggles playback, comma/period nudge offset, F11 toggles fullscreen.
- Fine details such as out-of-range targets, buffering, independent edits while sync
  is locked, and drift thresholds must be resolved and tested at the sync milestone.
  Old 150/750 ms thresholds are historical ideas, not fixed product requirements.
- Automatic alignment should eventually match source audio against the muffled,
  room-recorded source audio mixed with reactor voices. Manual sync is acceptable
  for the MVP. Auto-sync must be evaluated against real recordings.

## Composition

- Reaction is a crop/mask/repositionable background layer. Typically trim wasted
  space from its top and move the useful reactor area upward, leaving black below.
- Source is a foreground overlay, always horizontally centered and pinned to the
  top or bottom canvas edge via a toggle.
- Corner resizing preserves source aspect ratio, horizontal centering and the
  selected edge. Arbitrary dragging is superseded by these constraints.
- Enlarge the source until it covers unimportant legs/table space while preserving
  reactors' faces and upper bodies around it.
- Support useful compositions in both 16:9 and 4:3 canvases.

## Retained conveniences (later)

- Separate audio/subtitle track selection for each player; external SRT/ASS/SSA/VTT.
- Drag-and-drop files; file and URL inputs. Folder-first-video loading is optional.
- Remember sources, offset, volumes, mute states, speed and layout between sessions.
- Do not import old settings implicitly into the window foundation.

## “What Did They Say?” (later; no implementation now)

Button and keyboard shortcut (key assignment deferred):

1. Capture reaction trigger time, shared speed, both volumes and both mute states.
2. Rewind both videos ten seconds, preserving their offset.
3. Set both to 1.0×, mute source, set reaction volume to 100% and unmute reaction.
4. Play until A reaches the original trigger time.
5. Restore the actual captured speed, volumes and mute states exactly; continue forward.

Re-triggering, manual seeks during replay, paused activation, buffering and proximity
to the start/end require explicit behavior and tests at that milestone. They must not
silently overwrite the original saved state or leave the temporary mix active.

## Delivery gates

Each milestone needs a downloadable Windows build and evidence for its behavior.
Do not proceed past a failed or unconfirmed foundation.

| Milestone | Scope | Required verification |
| --- | --- | --- |
| 0 (current) | Plain black Windows window | Packaged EXE launches, responds, resizes, closes, relaunches; Gonz confirms locally |
| 1 | One embedded MPV surface, local media | AV1/HEVC, audio and subtitles on Windows |
| 2 | Two independent MPV instances | Both videos visible and both audio tracks audible |
| 3 | Shared transport | Master play/pause and seeking affect both |
| 4 | Offset and drift correction | Offset maintained through seeking; ±0.1s nudging |
| 5 | Shared speed, independent audio/subtitles | Speed changes preserve alignment; independent track/volume controls |
| 6 | Composition | Reaction crop/pan and centered top/bottom source resizing |
| 7 | Patreon/HLS and headers | Authorized real stream playback with correct Referer |
| 8 | YouTube resolution | Unlisted reaction URL playback |
| 9 | Audio-assisted automatic alignment | Confidence and accuracy against real reaction recordings |
| 10 | Reaction conveniences | What Did They Say restores exact state at trigger time; persistence and remaining conveniences |

## Foundation decisions

Use a small WinForms shell with a bundled .NET runtime. WinForms is only the native
window/UI layer; it does not choose the media backend. No native player dependency
is loaded at milestone 0. Introduce libmpv in a separate component at milestone 1,
keeping startup, playback, synchronization, composition and persistence separate.
Retain the old commit history for reference; do not resurrect the monolithic form.
